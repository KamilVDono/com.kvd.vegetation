using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KVD.Vegetation.Editor.Editor
{
	[CustomEditor(typeof(VegetationWorld))]
	public class VegetationWorldEditor : UnityEditor.Editor
	{
		[SerializeField] private bool _isDebugExpanded;
		
		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();
			base.OnInspectorGUI();
			var vegetationWorld = (VegetationWorld)target;
			if (EditorGUI.EndChangeCheck() && vegetationWorld.enabled)
			{
				vegetationWorld.TearDown();
				vegetationWorld.Bootstrap();
			}

			_isDebugExpanded = EditorGUILayout.Foldout(_isDebugExpanded, "Debug");
			if (!_isDebugExpanded)
			{
				return;
			}
			
			var cells  = vegetationWorld.Cells;
			var culled = cells.Count(static c => c.Culled);
			
			EditorGUILayout.LabelField($"Cells: Count({cells.Count}); Culled({culled}); Visible({cells.Count - culled})");
			EditorGUILayout.Separator();
			
			var renderingItems  = 0f;
			var renderingMeshes = 0f;
			var renderCalls     = 0f;
			
			for (var i = 0; i < cells.Count; i++)
			{
				var cell = cells[i];
				EditorGUILayout.LabelField($"Cell {i}: Culled({cell.Culled}); Items({cell.items.Count})");
				if (cell.Culled)
				{
					EditorGUILayout.Separator();
					continue;
				}
				
				EditorGUI.indentLevel++;
				for (var j = 0; j < cell.items.Count; j++)
				{
					var item = cell.items[j];
					
					renderingItems += item.Count;
					renderingMeshes += item.Count * item.MeshesCount;
					renderCalls += item.MeshesCount;
					
					var boundsArea   = cell.Bounds.size.x*cell.Bounds.size.z;
					var desiredInstances = vegetationWorld.SourceItems[j].DesiredInstances(boundsArea, vegetationWorld.DensityModifier);
					var spawnedRatio = item.Count/(float)desiredInstances;
					
					EditorGUILayout.LabelField($"Item {item.Name} - {item.Count}/{desiredInstances}({spawnedRatio:P1}) instances with {item.MeshesCount} meshes");
				}
				EditorGUI.indentLevel--;
				EditorGUILayout.Separator();
			}
			
			EditorGUILayout.LabelField($"Rendering: Items({renderingItems}); Meshes({renderingMeshes}); RenderCalls({renderCalls})");
		}
	}
}
