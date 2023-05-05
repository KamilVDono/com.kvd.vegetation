using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace KVD.Vegetation
{
	[BurstCompile]
	public struct PoissonDiscSampler : IJob
	{
		private const float MinScale = 0.8f;
		private const float MaxScale = 1.2f;
		private const float BoundsRadiusScaler = 0.4f;
		private static readonly float FullCircle = math.PI*2;
		
		public NativeList<float3> points;
		public NativeList<half2> rotationAndScale;
		public NativeList<int> itemIndices;
		
		private readonly float3 _boundsMin;
		private readonly float3 _boundsMax;
		private readonly float3 _maxItemDiameter;
		private readonly float _cellSize;
		private readonly int _attempts;
		private int _desiredCells;
		private int _initialShots;
		private readonly int _gridDimension;

		private Unity.Mathematics.Random _random;

		// NativeList cannot be DeallocateOnJobCompletion so we wee to make it public and deallocate it manually
		public NativeList<int> active;
		[DeallocateOnJobCompletion] private NativeArray<int> _grid;
		[DeallocateOnJobCompletion, ReadOnly] private NativeArray<float3> _itemsDiameters;
		[DeallocateOnJobCompletion] private NativeArray<int> _lefCells;
		[DeallocateOnJobCompletion, ReadOnly] private NativeArray<float> _areas;
		[DeallocateOnJobCompletion] private NativeArray<float> _itemsPriority;
		[DeallocateOnJobCompletion] private NativeArray<int> _leftItemIndices;
		
		private readonly ItemsByPrioritySort _sorter;

		public PoissonDiscSampler(List<VegetationItem> items, Bounds bounds, float density, int attempts, uint seed)
		{
			var boundsArea = bounds.size.x*bounds.size.z;
			
			_lefCells        = new(items.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			_areas           = new(items.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			_leftItemIndices = new(items.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			_itemsPriority   = new(items.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			_sorter          = new(_itemsPriority);

			_desiredCells = 0;
			for (var i = 0; i < items.Count; i++)
			{
				_areas[i]           =  items[i].Area();
				_lefCells[i]        =  items[i].DesiredInstances(boundsArea, density);
				_desiredCells       += _lefCells[i];
				_leftItemIndices[i] =  i;
			}
			
			_itemsDiameters = new(items.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			
			var minGroundDiameter = float.MaxValue;
			_maxItemDiameter = float3.zero;
			for (var i = 0; i < _itemsDiameters.Length; i++)
			{
				var diameter = items[i].OccupiedSpace;
				_itemsDiameters[i] = new(diameter, items[i].BushSpace, items[i].TreeSpace);
				if (diameter < minGroundDiameter)
				{
					minGroundDiameter = diameter;
				}
				for (var j = 0; j < 3; j++)
				{
					if (_itemsDiameters[i][j] > _maxItemDiameter[j])
					{
						_maxItemDiameter[j] = _itemsDiameters[i][j];
					}
				}
			}
			minGroundDiameter   *= MinScale;
			_maxItemDiameter[0] *= MaxScale;
			_maxItemDiameter[1] *= MaxScale;
			_maxItemDiameter[2] *= MaxScale;

			_attempts      = attempts;
			_boundsMin     = bounds.min;
			_boundsMax     = bounds.max;
			_cellSize      = minGroundDiameter/math.sqrt(2);
			_gridDimension = (int)math.ceil(bounds.size.x/_cellSize);
			
			_grid        = new(_gridDimension*_gridDimension, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			active      = new(_gridDimension, Allocator.TempJob);
			points      = new(_desiredCells, Allocator.TempJob);
			rotationAndScale = new(_desiredCells, Allocator.TempJob);
			itemIndices = new(_desiredCells, Allocator.TempJob);

			_initialShots = math.ceillog2(_desiredCells)+4;
			_random      = new(seed);
			
			for (var i = 0; i < _grid.Length; i++)
			{
				_grid[i] = -1;
			}
		}

		public void Execute()
		{
			var canSampleNext = false;
			do
			{
				var itemsCount = 0;
				
				for (var i = 0; i < _lefCells.Length; i++)
				{
					if (_lefCells[i] > 0)
					{
						itemsCount++;
						_itemsPriority[i] = _lefCells[i]*_areas[i];
					}
					else
					{
						_itemsPriority[i] = -1;
					}
				}
				if (itemsCount == 0)
				{
					break;
				}
				
				_leftItemIndices.Sort(_sorter);
				
				var result = Next(_leftItemIndices.Slice(0, itemsCount));
				canSampleNext = result.Item1;
				if (canSampleNext)
				{
					_lefCells[result.Item2] -= 1;
				}
			}
			while (canSampleNext);
		}

		public (bool, int) Next(NativeSlice<int> itemIndicesToChoose)
		{
			if (_desiredCells < 1)
			{
				return (false, -1);
			}
			if (TryFindInitialPoint(itemIndicesToChoose, out var chosenItem))
			{
				return (true, chosenItem);
			}
			return TryInsertNextPosition(itemIndicesToChoose);
		}

		private bool TryFindInitialPoint(NativeSlice<int> itemIndicesToChoose, out int chosenItem)
		{
			if (_initialShots < 1)
			{
				chosenItem = -1;
				return false;
			}
			
			var usedIndex = 0;
			var found           = false;
			var initialAttempts = _attempts*2;
			while (_initialShots > 0 && !found)
			{
				for (var i = 0; !found && i < initialAttempts; i++)
				{
					var point = GetRandomPoint();
					for (usedIndex = 0; !found && usedIndex < itemIndicesToChoose.Length; usedIndex++)
					{
						var itemIndex      = itemIndicesToChoose[usedIndex];
						var scale          = new half(_random.NextFloat(MinScale, MaxScale));
						var targetDiameter = _itemsDiameters[itemIndex]*scale;
						if (IsValidPoint(point, targetDiameter))
						{
							found = true;
							var rotation = new half(_random.NextFloat(0, FullCircle));
							AddPoint(point, itemIndex, rotation, scale);
						}
					}
				}
				_initialShots--;
			}
			chosenItem = itemIndicesToChoose[--usedIndex];
			return found;
		}

		private (bool, int) TryInsertNextPosition(NativeSlice<int> itemIndicesToChoose)
		{
			var found     = false;
			var usedIndex = 0;

			while (active.Length > 0 && !found)
			{
				var index           = _random.NextInt(0, active.Length);
				var subjectIndex    = active[index];
				var subject         = points[subjectIndex];
				var subjectDiameter = _itemsDiameters[itemIndices[subjectIndex]];

				for (var i = 0; !found && i < _attempts; i++)
				{
					for (usedIndex = 0; !found && usedIndex < itemIndicesToChoose.Length; usedIndex++)
					{
						var itemIndex      = itemIndicesToChoose[usedIndex];
						var scale          = new half(_random.NextFloat(MinScale, MaxScale));
						var targetDiameter = _itemsDiameters[itemIndex]*scale;
						var point          = GetRandomPointAround(subject, subjectDiameter[0], targetDiameter[0]);
						if (IsValidPoint(point, targetDiameter))
						{
							found = true;
							var rotation = new half(_random.NextFloat(0, FullCircle));
							AddPoint(point, itemIndex, rotation, scale);
						}
					}
				}

				if (!found)
				{
					active.RemoveAtSwapBack(index);
				}
			}

			var chosenItem = itemIndicesToChoose[--usedIndex];
			return (found, chosenItem);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float3 GetRandomPointAround(float3 subject, float subjectDiameter, float targetDiameter)
		{
			var r1          = _random.NextFloat();
			var r2          = _random.NextFloat();
			var minDistance = (subjectDiameter+ targetDiameter)*0.5f;
			var radius      = minDistance*(r1 + 1);
			var angle       = FullCircle*r2;
			var x           = subject.x + radius*math.cos(angle);
			var z           = subject.z + radius*math.sin(angle);
			return new(x, subject.y, z);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private float3 GetRandomPoint()
		{
			var x = _random.NextFloat(_boundsMin.x, _boundsMax.x);
			var z = _random.NextFloat(_boundsMin.z, _boundsMax.z);
			return new(x, _boundsMin.y, z);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddPoint(float3 point, int itemIndex, half rotation, half scale)
		{
			point.y = 1000;
			var insertionIndex = points.Length;
			points.Add(point);
			rotationAndScale.Add(new(rotation, scale));
			itemIndices.Add(itemIndex);
			active.Add(insertionIndex);
			_grid[GridIndex(point)] = insertionIndex;
			--_desiredCells;
		}

		private bool IsValidPoint(float3 point, float3 insertDiameter)
		{
			var boundsRadius = insertDiameter*BoundsRadiusScaler;
			if (!IsPointInBounds(point, boundsRadius[0]) ||
			    !IsPointInBounds(point, boundsRadius[1]) ||
			    !IsPointInBounds(point, boundsRadius[2]))
			{
				return false;
			}
			
			var insertIndex = GridIndex2D(point);

			return IsValidPointAtLevel(point, insertDiameter, insertIndex, 0) &&
			       IsValidPointAtLevel(point, insertDiameter, insertIndex, 1) &&
			       IsValidPointAtLevel(point, insertDiameter, insertIndex, 2);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsPointInBounds(float3 point, float radius)
		{
			return point.x >= _boundsMin.x + radius &&
			       point.z >= _boundsMin.z + radius &&
			       point.x <= _boundsMax.x - radius &&
			       point.z <= _boundsMax.z - radius;
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsValidPointAtLevel(float3 point, float3 insertDiameters, int2 insertIndex, int level)
		{
			var insertDiameter = insertDiameters[level];
			if (insertDiameter < 0)
			{
				// Item is not reaching this level
				return true;
			}
			var maxAvgDistance = (_maxItemDiameter[level]+insertDiameter)/2;
			var samplingRadius = (int)math.ceil(maxAvgDistance/_cellSize);

			var startX = math.max(0, insertIndex.x-samplingRadius);
			var endX   = math.min(_gridDimension-1, insertIndex.x+samplingRadius);
			var startZ = math.max(0, insertIndex.y-samplingRadius);
			var endZ   = math.min(_gridDimension-1, insertIndex.y+samplingRadius);

			for (var z = startZ; z <= endZ; z++)
			{
				for (var x = startX; x <= endX; x++)
				{
					var index      = z*_gridDimension+x;
					var pointIndex = _grid[index];
					if (pointIndex == -1)
					{
						continue;
					}
					var otherDiameters = _itemsDiameters[itemIndices[pointIndex]];
					var otherDiameter  = otherDiameters[level]*rotationAndScale[pointIndex].y;
					if (otherDiameter < 0)
					{
						// Other is not reaching this level
						continue;
					}
					var fullDistance = (otherDiameter+insertDiameter)/2;
					var otherPoint   = points[pointIndex].xz;
					if (math.distancesq(otherPoint, point.xz) < fullDistance*fullDistance)
					{
						return false;
					}
				}
			}

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int2 GridIndex2D(float3 point)
		{
			var x = (int)math.floor((point.x - _boundsMin.x)/_cellSize);
			var z = (int)math.floor((point.z - _boundsMin.z)/_cellSize);
			return new(x, z);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private int GridIndex(float3 point)
		{
			var index2D = GridIndex2D(point);
			return index2D.y*_gridDimension + index2D.x;
		}
		
		struct ItemsByPrioritySort : IComparer<int>
		{
			[ReadOnly, NativeDisableContainerSafetyRestriction]
			private NativeArray<float> _priorities;
			
			public ItemsByPrioritySort(NativeArray<float> priorities)
			{
				_priorities = priorities;
			}
			
			public int Compare(int x, int y)
			{
				return _priorities[y].CompareTo(_priorities[x]);
			}
		}
	}
}
