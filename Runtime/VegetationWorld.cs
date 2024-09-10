using System.Collections.Generic;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace KVD.Vegetation
{
	[ExecuteAlways]
	public class VegetationWorld : MonoBehaviour
	{
		[SerializeField] private List<VegetationItem> _items = new();
		[SerializeField] private Bounds _bounds;
		[SerializeField, Range(10, 500), Delayed]
		private int _cellSize = 50;
		[SerializeField, Range(1, 50), Delayed]
		private int _attempts = 20;
		[SerializeField, Range(1, 10), Delayed]
		private int _spawnConcurrent = 4;
		[SerializeField, Range(0.01f, 4f), Delayed]
		private float _density = 1;
		
		private List<VegetationCell> _cells = new();
		private List<VegetationCell> _toSpawn = new();
		private List<VegetationCell> _spawning = new();
		
		public float DensityModifier => _density;
		public IReadOnlyList<VegetationItem> SourceItems => _items;
		public IReadOnlyList<VegetationCell> Cells => _cells;

		private void OnEnable()
		{
			Bootstrap();
			VegetationUpdateProvider.Instance.Register(this);
		}

		private void OnDisable()
		{
			VegetationUpdateProvider.Instance.Unregister(this);
			TearDown();
		}

		public void UnityUpdate()
		{
			VegetationManager.Instance.Cull(_cells);
			
			_spawning.Sort(static (a, b) => a.PredictSpawningProgress().CompareTo(b.PredictSpawningProgress()));
			
			var startIndex         = _spawning.Count-1;
			var forceCompleteIndex = -1;
				
			for (var i = startIndex; forceCompleteIndex == -1 && i >= 0; i--)
			{
				if (_spawning[i].PredictSpawningProgress() == VegetationCell.SpawningProgress.Waiting)
				{
					forceCompleteIndex = i;
				}
			}

			for (var i = startIndex; i >= 0; i--)
			{
				var cell = _spawning[i];
				if (cell.UpdateSpawning(i == forceCompleteIndex) == VegetationCell.SpawningProgress.Done)
				{
					_spawning.RemoveAt(i);
					_cells.Add(cell);
				}
			}
			if (_toSpawn.Count > 0)
			{
				TryFillSpawning();
			}
		}
		
		public void Bootstrap()
		{
			_items.Sort(static (left, right) => right.OccupiedSpace.CompareTo(left.OccupiedSpace));

			var halfSize = _cellSize*0.5f;

			var startX = _bounds.min.x + halfSize;
			var startZ = _bounds.min.z + halfSize;
			startX = math.ceil(math.abs(startX)/_cellSize)*_cellSize*math.sign(startX);
			startZ = math.ceil(math.abs(startZ)/_cellSize)*_cellSize*math.sign(startZ);
			
			var xOvershoot  = _bounds.min.x - (startX-halfSize);
			var zOvershoot  = _bounds.min.z - (startZ-halfSize);
			var cellsCountX = Mathf.CeilToInt((_bounds.size.x+xOvershoot)/_cellSize);
			var cellsCountZ = Mathf.CeilToInt((_bounds.size.z+zOvershoot)/_cellSize);

			for (var x = 0; x < cellsCountX; x++)
			{
				var posX = startX + x * _cellSize;
				for (var z = 0; z < cellsCountZ; z++)
				{
					var posZ     = startZ + z * _cellSize;
					var position = new Vector3(posX, 0, posZ);
					var cell     = new VegetationCell(_cellSize, position, IndexFromCenter(position));
					_toSpawn.Add(cell);
				}
			}
		}
		
		public void TearDown()
		{
			_toSpawn.Clear();
			foreach (var cell in _spawning)
			{
				cell.Dispose();
			}
			_spawning.Clear();
			foreach (var cell in _cells)
			{
				cell.Dispose();
			}
			_cells.Clear();
		}

		public int ContentHash()
		{
			var hash = _bounds.GetHashCode();
			unchecked
			{
				foreach (var item in _items)
				{
					hash = (hash * 397) ^ item.GetHashCode();
				}
				hash = (hash * 397) ^ _cellSize;
				hash = (hash * 397) ^ _attempts;
				hash = (hash * 397) ^ _density.GetHashCode();
			}
			return hash;
		}

		private void TryFillSpawning()
		{
			var left         = _spawnConcurrent - _spawning.Count;
			var toSpawnCount = Mathf.Min(left, _toSpawn.Count);
			for (var i = 0; i < toSpawnCount; i++)
			{
				var toSpawn = _toSpawn[^1];
				_toSpawn.RemoveAt(_toSpawn.Count-1);
				toSpawn.StartSpawning(_items, _attempts, _density);
				_spawning.Add(toSpawn);
			}
			JobHandle.ScheduleBatchedJobs();
		}

		private int2 IndexFromCenter(Vector3 center)
		{
			var x = (int)math.round(center.x);
			var z = (int)math.round(center.z);
			return new(x, z);
		}

		private static GUIStyle _labelStyle;
		private void OnDrawGizmos()
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireCube(_bounds.center, _bounds.size);

			Gizmos.color = Color.blue;
			foreach (var cell in _cells)
			{
				var center = cell.Bounds.center;
				Gizmos.DrawWireCube(center, cell.Bounds.size);
#if UNITY_EDITOR
				_labelStyle                   ??= new(GUI.skin.label);
				_labelStyle.alignment         =   TextAnchor.MiddleCenter;
				_labelStyle.normal.background =   Texture2D.whiteTexture;
				_labelStyle.normal.textColor  =   Color.blue;
				UnityEditor.Handles.Label(center, $"({cell.Index.x}, {cell.Index.y})", _labelStyle);
#endif
			}
		}

		class PotentialCell
		{
			public int2 Id{ get; }
			public Bounds Bounds{ get; }
			public IReadOnlyList<VegetationItem> PrototypeItems{ get; }
		}

		class LoadedCell
		{
			public int2 Id{ get; }
			public int Instances{ get; }
		}
	}
}
