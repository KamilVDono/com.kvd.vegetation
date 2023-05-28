using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace KVD.Vegetation
{
	[Serializable]
	public class VegetationCell : IDisposable
	{
		private static readonly ProfilerMarker SamplerCreationMarker = new("Sampler Creation");
		private static readonly ProfilerMarker RaycastCreationMarker = new("Raycast Creation");
		private static readonly ProfilerMarker RaycastWriteBackMarker = new("Raycast Write Back");
		private static readonly ProfilerMarker RuntimeItemCreationFuturesMarker = new("Runtime Item Creation.Futures");
		private static readonly ProfilerMarker RuntimeItemCreationBindsMarker = new("Runtime Item Creation.Binds");

		// === Lifetime/Spawning state
		private bool _disposed;
		private readonly int2 _index;
		private IReadOnlyList<VegetationItem> _sources;
		private SamplerData _samplerData;
		private TransformsPrepareData _transformsPrepareData;
		private FuturesTransformsData _futuresTransformsData;

		// === Rendering state
		private bool _culled = true;
		private List<RuntimeVegetationItem> _items = new();

		public Bounds Bounds{ get; private set; }
		public int2 Index => _index;
		public IReadOnlyList<RuntimeVegetationItem> Items => _items;

		public bool Culled
		{
			get => _culled;
			set
			{
				if (_culled == value)
				{
					return;
				}
				_culled = value;
				if (_culled)
				{
					for (var i = 0; i < _items.Count; i++)
					{
						_items[i].StopRendering();
					}
				}
				else
				{
					for (var i = 0; i < _items.Count; i++)
					{
						_items[i].StartRendering();
					}
				}
			}
		}

		public VegetationCell(float size, Vector3 position, int2 index)
		{
			Bounds = new(position, Vector3.one*size);
			_index = index;
		}

		public void StartSpawning(List<VegetationItem> source, int attempts, float density)
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(VegetationCell), "Cannot start spawning on disposed cell");
			}
			_sources       = source;
			_items.Capacity = _sources.Count;

			SamplerCreationMarker.Begin();
			// TODO: Better seed calculation
			var seed    = (uint)math.abs(-math.abs(_index.x)-1-math.abs(_index.y));
			var sampler = new PoissonDiscSampler(_sources, Bounds, density, attempts, seed);
			SamplerCreationMarker.End();
			
			var samplerHandle = sampler.Schedule();
			samplerHandle = sampler.active.Dispose(samplerHandle);
			_samplerData = new(sampler, samplerHandle);
		}
		
		public SpawningProgress PredictSpawningProgress()
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(VegetationCell), "Cannot update spawning on disposed cell");
			}
			// === Step 3. Finish spawn
			if (_futuresTransformsData != null)
			{
				if (_futuresTransformsData.WillComplete() == SpawningProgress.Waiting)
				{
					return SpawningProgress.Waiting;
				}
			}
			// === Step 2. Prepare transforms
			if (_transformsPrepareData != null)
			{
				return _transformsPrepareData.WillComplete();
			}

			// === Step 1. Generate points ongoing
			if (_samplerData != null)
			{
				return _samplerData.WillComplete();
			}
			
			return SpawningProgress.Done;
		}

		public SpawningProgress UpdateSpawning(bool force)
		{
			if (_disposed)
			{
				throw new ObjectDisposedException(nameof(VegetationCell), "Cannot update spawning on disposed cell");
			}
			// === Step 3. Finish spawn
			if (_futuresTransformsData != null)
			{
				if (_futuresTransformsData.Progress(force) == SpawningProgress.Waiting)
				{
					return SpawningProgress.Waiting;
				}

				BindFutureTransforms();
			}
			// === Step 2. Prepare transforms
			if (_transformsPrepareData != null)
			{
				var progress = _transformsPrepareData.Progress(force);
				if (progress == SpawningProgress.Waiting)
				{
					return SpawningProgress.Waiting;
				}
				
				// === Transition to step 3.
				SpawnTransitionToFutureTransforms();
				return progress;
			}

			// === Step 1. Generate points ongoing
			if (_samplerData != null)
			{
				var progress = _samplerData.Progress(force);
				if (progress == SpawningProgress.Waiting)
				{
					return SpawningProgress.Waiting;
				}

				// === Transition to step 2.
				SpawnTransitionPoissonPointsTransformation();
				return progress;
			}
			
			return SpawningProgress.Done;
		}
		
		public void Dispose()
		{
			_disposed = true;
			if (_samplerData != null)
			{
				_samplerData.handle.handle.Complete();
				_samplerData.Dispose();
				_samplerData = null;
			}
			if (_transformsPrepareData != null)
			{
				_transformsPrepareData.handle.handle.Complete();
				_transformsPrepareData.Dispose();
				_transformsPrepareData = null;
			}
			if (_futuresTransformsData != null)
			{
				_futuresTransformsData.handle.handle.Complete();
				_futuresTransformsData.Dispose();
				_futuresTransformsData = null;
			}
			foreach (var item in _items)
			{
				item.Dispose();
			}
			_items.Clear();
			_sources = null;
		}
		
		private void SpawnTransitionPoissonPointsTransformation()
		{
			RaycastCreationMarker.Begin();
			var queryParameter = new QueryParameters
			{
				hitBackfaces = false, hitMultipleFaces = false, hitTriggers = QueryTriggerInteraction.UseGlobal,
				layerMask    = -1,
			};
			var points      = _samplerData.points.AsArray();
			var pointsCount = points.Length;
			var results     = new NativeArray<RaycastHit>(pointsCount, Allocator.TempJob);
			var commands    = new NativeArray<RaycastCommand>(pointsCount, Allocator.TempJob);
			for (var i = 0; i < pointsCount; i++)
			{
				commands[i] = new(points[i], Vector3.down, queryParameter, 2000);
			}
			RaycastCreationMarker.End();

			var raycastsHandle = RaycastCommand.ScheduleBatch(commands, results, 32, 1);
			raycastsHandle = commands.Dispose(raycastsHandle);

			RaycastWriteBackMarker.Begin();
			var writeBackPointsJob = new WriteBackPoints { points = points, hits = results };
			var writeBackHandle    = writeBackPointsJob.Schedule(points.Length, 32, raycastsHandle);
			RaycastWriteBackMarker.End();

			writeBackHandle = results.Dispose(writeBackHandle);

			var handles    = new NativeArray<JobHandle>(_sources.Count, Allocator.TempJob);
			var transforms = new NativeArray<NativeList<InstanceTransform>>(_sources.Count, Allocator.TempJob);
			for (var i = 0; i < _sources.Count; i++)
			{
				var transform = new NativeList<InstanceTransform>(points.Length, Allocator.TempJob);
				var job = new InstanceTransformExtract
				{
					points           = points,
					rotationAndScale = _samplerData.rotationAndScale.AsArray(),
					indices          = _samplerData.itemIndices.AsArray(),
					itemIndex        = i,
					transforms       = transform,
				};

				handles[i]    = job.Schedule(writeBackHandle);
				transforms[i] = transform;
			}

			var instanceTransformExtractHandle = JobHandle.CombineDependencies(handles);
			handles.Dispose();

			var disposePoints  = _samplerData.points.Dispose(instanceTransformExtractHandle);
			var disposeIndices = _samplerData.itemIndices.Dispose(instanceTransformExtractHandle);
			var disposeRot     = _samplerData.rotationAndScale.Dispose(instanceTransformExtractHandle);
			var disposeHandles = JobHandle.CombineDependencies(disposePoints, disposeIndices, disposeRot);
			_transformsPrepareData = new(transforms, disposeHandles);
			_samplerData           = null;
		}
		
		private void SpawnTransitionToFutureTransforms()
		{
			RuntimeItemCreationFuturesMarker.Begin();
			var transforms = _transformsPrepareData.transforms;
			var futures =
				new NativeArray<RuntimeVegetationItem.BindInstanceTransformsFuture>(_sources.Count, Allocator.TempJob);
			for (var i = 0; i < _sources.Count; i++)
			{
				var transform             = transforms[i];
				var runtimeVegetationItem = _sources[i].ToRuntimeVegetationItem((uint)transform.Length);
				_items.Add(runtimeVegetationItem);
				var future = runtimeVegetationItem.BindTransforms(transform.AsArray());
				futures[i] = future;
			}
			RuntimeItemCreationFuturesMarker.End();
			var handles = new NativeArray<JobHandle>(futures.Length, Allocator.TempJob);
			for (var i = 0; i < futures.Length; i++)
			{
				handles[i] = futures[i].handle;
			}
			var futuresHandle = JobHandle.CombineDependencies(handles);
			transforms.Dispose();
			_futuresTransformsData = new(futures, futuresHandle);
			_transformsPrepareData = null;
		}
		
		private void BindFutureTransforms()
		{
			RuntimeItemCreationBindsMarker.Begin();
			var futures = _futuresTransformsData.futures;
			for (var i = 0; i < _sources.Count; i++)
			{
				_items[i].BindTransforms(futures[i]);
				if (_culled)
				{
					_items[i].StopRendering();
				}
				else
				{
					_items[i].StartRendering();
				}
			}
			RuntimeItemCreationBindsMarker.End();
			_futuresTransformsData.futures.Dispose();
			_futuresTransformsData = null;
		}

		public enum SpawningProgress
		{
			Waiting = 0,
			AutoProgressed = 1,
			ForceProgressed = 2,
			Done = 3,
		}
		
		struct WriteBackPoints : IJobParallelFor
		{
			[ReadOnly] public NativeSlice<RaycastHit> hits;
			public NativeSlice<float3> points;

			public void Execute(int index)
			{
				var point = points[index];
				point.y       = hits[index].point.y;
				points[index] = point;
			}
		}

		class SamplerData : IDisposable
		{
			internal NativeList<float3> points;
			internal NativeList<half2> rotationAndScale;
			internal NativeList<int> itemIndices;
			internal readonly JobHandleWithLifetime handle;

			public SamplerData(in PoissonDiscSampler sampler, JobHandle handle)
			{
				points           = sampler.points;
				rotationAndScale = sampler.rotationAndScale;
				itemIndices      = sampler.itemIndices;
				this.handle      = new() { handle = handle };
			}

			public SpawningProgress Progress(bool force) => handle.Progress(force);
			
			public SpawningProgress WillComplete() => handle.WillComplete();

			public void Dispose()
			{
				points.Dispose();
				rotationAndScale.Dispose();
				itemIndices.Dispose();
			}
		}

		class TransformsPrepareData : IDisposable
		{
			internal NativeArray<NativeList<InstanceTransform>> transforms;
			internal readonly JobHandleWithLifetime handle;

			public TransformsPrepareData(NativeArray<NativeList<InstanceTransform>> transforms, JobHandle handle)
			{
				this.transforms = transforms;
				this.handle      = new() { handle = handle };
			}
			
			public SpawningProgress Progress(bool force) => handle.Progress(force);
			
			public SpawningProgress WillComplete() => handle.WillComplete();

			public void Dispose()
			{
				transforms.Dispose();
			}
		}

		class FuturesTransformsData : IDisposable
		{
			internal NativeArray<RuntimeVegetationItem.BindInstanceTransformsFuture> futures;
			internal readonly JobHandleWithLifetime handle;
			
			public FuturesTransformsData(NativeArray<RuntimeVegetationItem.BindInstanceTransformsFuture> futures, JobHandle handle)
			{
				this.futures = futures;
				this.handle  = new() { handle = handle };
			}
			
			public SpawningProgress Progress(bool force) => handle.Progress(force);
			
			public SpawningProgress WillComplete() => handle.WillComplete();

			public void Dispose()
			{
				futures.Dispose();
			}
		}

		class JobHandleWithLifetime
		{
			internal JobHandle handle;
			internal int age;
			
			public SpawningProgress Progress(bool force)
			{
				age++;
				if (age < 3)
				{
					return SpawningProgress.Waiting;
				}
				if (handle.IsCompleted)
				{
					handle.Complete();
					return SpawningProgress.AutoProgressed;
				}
				if (age == 4 || force)
				{
					handle.Complete();
					return SpawningProgress.ForceProgressed;
				}
				return SpawningProgress.Waiting;
			}

			public SpawningProgress WillComplete()
			{
				if (age < 2)
				{
					return SpawningProgress.Waiting;
				}
				if (handle.IsCompleted)
				{
					return SpawningProgress.AutoProgressed;
				}
				if (age == 3)
				{
					handle.Complete();
					return SpawningProgress.ForceProgressed;
				}
				return SpawningProgress.Waiting;
			}
		}
	}
}