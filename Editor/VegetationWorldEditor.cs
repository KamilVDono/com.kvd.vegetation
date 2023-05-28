using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace KVD.Vegetation.Editor.Editor
{
	[CustomEditor(typeof(VegetationWorld))]
	public class VegetationWorldEditor : UnityEditor.Editor
	{
		[SerializeField] private bool _isDebugExpanded;
		[SerializeField] private bool[] _areCellsExpanded = Array.Empty<bool>();
		private int _previousHash;

		private void OnEnable()
		{
			_previousHash = ((VegetationWorld)target).ContentHash();
		}

		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();
			base.OnInspectorGUI();
			var vegetationWorld = (VegetationWorld)target;
			if (EditorGUI.EndChangeCheck() && vegetationWorld.enabled)
			{
				var newHash = vegetationWorld.ContentHash();
				if (_previousHash != newHash)
				{
					vegetationWorld.TearDown();
					vegetationWorld.Bootstrap();
					_previousHash = newHash;
				}
			}

			DrawDebug(vegetationWorld);

			if (GUILayout.Button("Bake"))
			{
				var vegetationDirectoryPath = Path.Combine(Application.streamingAssetsPath, "Vegetation");
				var sceneDirectoryPath =
					Path.Combine(vegetationDirectoryPath, $"{vegetationWorld.gameObject.scene.name}");
				if (!Directory.Exists(vegetationDirectoryPath))
				{
					Directory.CreateDirectory(vegetationDirectoryPath);
				}
				if (!Directory.Exists(sceneDirectoryPath))
				{
					Directory.CreateDirectory(sceneDirectoryPath);
				}
				
				var manifestPath = Path.Combine(sceneDirectoryPath, "manifest.txt");
				using var manifestFile = File.Open(manifestPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);
				using var manifestWriter = new StreamWriter(manifestFile, Encoding.UTF8);
				manifestWriter.WriteLine($"Cells: {vegetationWorld.Cells.Count}");
			}
		}
		
		private void DrawDebug(VegetationWorld vegetationWorld)
		{
			_isDebugExpanded = EditorGUILayout.Foldout(_isDebugExpanded, "Debug", true);
			if (!_isDebugExpanded)
			{
				return;
			}

			var cells  = vegetationWorld.Cells;
			var culled = cells.Count(static c => c.Culled);

			EditorGUILayout.LabelField($"Cells: Count({cells.Count}); Culled({culled}); Visible({cells.Count-culled})");
			EditorGUILayout.Separator();

			var renderingItems  = 0f;
			var renderingMeshes = 0f;
			var renderingCalls  = 0f;

			var allItems  = 0f;
			var allMeshes = 0f;
			var allCalls  = 0f;

			if (_areCellsExpanded.Length < cells.Count)
			{
				Array.Resize(ref _areCellsExpanded, cells.Count);
			}

			EditorGUI.indentLevel++;
			for (var i = 0; i < cells.Count; i++)
			{
				var cell = cells[i];
				_areCellsExpanded[i] = EditorGUILayout.Foldout(_areCellsExpanded[i],
					$"Cell {i}: Culled({cell.Culled}); Items({cell.Items.Count})", true);

				EditorGUI.indentLevel++;
				for (var j = 0; j < cell.Items.Count; j++)
				{
					var item = cell.Items[j];

					if (!cell.Culled)
					{
						renderingItems  += item.Count;
						renderingMeshes += item.Count*item.MeshesCount;
						renderingCalls  += item.MeshesCount;
					}
					allItems  += item.Count;
					allMeshes += item.Count*item.MeshesCount;
					allCalls  += item.MeshesCount;

					var boundsArea = cell.Bounds.size.x*cell.Bounds.size.z;
					var desiredInstances = vegetationWorld.SourceItems[j]
						.DesiredInstances(boundsArea, vegetationWorld.DensityModifier);
					var spawnedRatio = item.Count/(float)desiredInstances;

					if (!cell.Culled && _areCellsExpanded[i])
					{
						EditorGUILayout.LabelField(
							$"Item {item.Name} - {item.Count}/{desiredInstances}({spawnedRatio:P1}) instances with {item.MeshesCount} meshes");
					}
				}
				EditorGUI.indentLevel--;
				EditorGUILayout.Separator();
			}
			EditorGUI.indentLevel--;

			EditorGUILayout.BeginHorizontal();

			EditorGUILayout.BeginVertical(GUILayout.Width(64));
			EditorGUILayout.LabelField("All:", GUILayout.Width(64));
			EditorGUILayout.LabelField("Rendering:", GUILayout.Width(64));
			EditorGUILayout.LabelField("Culled:", GUILayout.Width(64));
			EditorGUILayout.EndVertical();

			EditorGUILayout.BeginVertical();
			EditorGUILayout.LabelField($"Items({allItems}); Meshes({allMeshes}); RenderCalls({allCalls})");
			EditorGUILayout.LabelField($"Items({renderingItems}); Meshes({renderingMeshes}); RenderCalls({renderingCalls})");
			EditorGUILayout.LabelField(
				$"Items({allItems-renderingItems}); Meshes({allMeshes-renderingMeshes}); RenderCalls({allCalls-renderingCalls})");
			EditorGUILayout.EndVertical();

			EditorGUILayout.EndHorizontal();
		}
	}
}
