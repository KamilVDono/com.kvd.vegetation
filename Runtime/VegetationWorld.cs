using System.Collections.Generic;
using UnityEngine;

namespace KVD.Vegetation
{
	[ExecuteAlways]
	public class VegetationWorld : MonoBehaviour
	{
		[SerializeField] private List<VegetationItem> _items = new();
		[SerializeField] private Bounds _bounds;
		[SerializeField, Range(10, 500), Delayed] private int _cellSize = 50;
		[SerializeField, Range(0.01f, 4f), Delayed] private float _density = 1;
		
		private List<VegetationCell> _cells = new();
		
		public float DensityModifier => _density;
		public IReadOnlyList<VegetationItem> SourceItems => _items;
		public IReadOnlyList<VegetationCell> Cells => _cells;

		private void OnEnable()
		{
			Bootstrap();
		}

		private void OnDisable()
		{
			TearDown();
		}

		private void Update()
		{
			VegetationManager.Instance.Cull(_cells);
		}
		
		public void Bootstrap()
		{
			_items.Sort(static (left, right) => right.OccupiedSpace.CompareTo(left.OccupiedSpace));
			
			var cellsCountX = Mathf.CeilToInt(_bounds.size.x/_cellSize);
			var cellsCountZ = Mathf.CeilToInt(_bounds.size.z/_cellSize);
			
			var startX = _bounds.min.x + (_cellSize/2f);
			var startZ = _bounds.min.z + (_cellSize/2f);
			
			for (var x = 0; x < cellsCountX; x++)
			{
				var posX = startX + x * _cellSize;
				for (var z = 0; z < cellsCountZ; z++)
				{
					var posZ     = startZ + z * _cellSize;
					var seed     = (uint)((x+1) + (z+1)*cellsCountX);
					var position = new Vector3(posX, 0, posZ);
					var cell     = new VegetationCell(_cellSize, position);
					cell.Spawn(_items, _density, seed);
					_cells.Add(cell);
				}
			}
		}
		
		public void TearDown()
		{
			foreach (var cell in _cells)
			{
				cell.Dispose();
			}
			_cells.Clear();
		}

		private void OnDrawGizmos()
		{
			Gizmos.color = Color.yellow;
			Gizmos.DrawWireCube(_bounds.center, _bounds.size);
			
			Gizmos.color = Color.blue;
			foreach (var cell in _cells)
			{
				Gizmos.DrawWireCube(cell.Bounds.center, cell.Bounds.size);
			}
		}
	}
}
