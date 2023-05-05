using KVD.Utils.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace KVD.Vegetation.Editor.Editor
{
	[CustomEditor(typeof(VegetationItem))]
	public class VegetationItemEditor : UnityEditor.Editor
	{
#nullable disable
		SerializedProperty _mesh;
		SerializedProperty _materials;
		SerializedProperty _transform;
		SerializedProperty _offset;
		SerializedProperty _indicesCounts;
		SerializedProperty _indicesStarts;
		SerializedProperty _baseVertices;
		
		SerializedProperty _spacingType;
		
		MeshPreview _meshPreview;
#nullable restore
			
		VegetationItem Target => (VegetationItem)target;

		private void OnEnable()
		{
			_mesh          = serializedObject.FindBackedProperty(nameof(VegetationItem.Mesh));
			_materials     = serializedObject.FindBackedProperty(nameof(VegetationItem.Materials));
			_transform     = serializedObject.FindBackedProperty(nameof(VegetationItem.ObjectTransform));
			_offset        = serializedObject.FindBackedProperty(nameof(VegetationItem.Offset));
			_indicesCounts = serializedObject.FindBackedProperty(nameof(VegetationItem.IndicesCounts));
			_indicesStarts = serializedObject.FindBackedProperty(nameof(VegetationItem.IndicesStarts));
			_baseVertices  = serializedObject.FindBackedProperty(nameof(VegetationItem.BaseVertices));
			
			_spacingType   = serializedObject.FindBackedProperty(nameof(VegetationItem.SpacingType));
			
			_meshPreview = new(_mesh.objectReferenceValue as Mesh);
		}

		private void OnDisable()
		{
			_meshPreview.Dispose();
			
			_mesh.Dispose();
			_materials.Dispose();
			_transform.Dispose();
			_offset.Dispose();
			_indicesCounts.Dispose();
			_indicesStarts.Dispose();
			_baseVertices.Dispose();
			_spacingType.Dispose();
		}

		public override void OnInspectorGUI()
		{
			EditorGUI.BeginChangeCheck();
			serializedObject.UpdateIfRequiredOrScript();
			var iterator = serializedObject.GetIterator();
			for (var enterChildren = true; iterator.NextVisible(enterChildren); enterChildren = false)
			{
				using (new EditorGUI.DisabledScope("m_Script" == iterator.propertyPath))
				{
					if (iterator.HasName("DesiredDensity"))
					{
						var maxDensity = 1f/Target.OccupiedSpace*0.5f;
						iterator.floatValue = EditorGUILayout.Slider(iterator.displayName, iterator.floatValue, 0.0001f,
							maxDensity);
					}
					else if (iterator.HasName(_offset.name))
					{
						EditorGUI.BeginChangeCheck();
						EditorGUILayout.PropertyField(iterator, true);
						if (EditorGUI.EndChangeCheck())
						{
							serializedObject.ApplyModifiedProperties();
							BakeItemData();
						}
					}
					else if (iterator.HasName("BushSpace"))
					{
						if (_spacingType.enumValueIndex >= 1)
						{
							if (iterator.floatValue < 0)
							{
								iterator.floatValue = 1f;
							}
							EditorGUILayout.PropertyField(iterator, true);
						}
						else
						{
							iterator.floatValue = -999f;
						}
					}
					else if (iterator.HasName("TreeSpace"))
					{
						if (_spacingType.enumValueIndex >= 2)
						{
							if (iterator.floatValue < 0)
							{
								iterator.floatValue = 1f;
							}
							EditorGUILayout.PropertyField(iterator, true);
						}
						else
						{
							iterator.floatValue = -999f;
						}
					}
					else
					{
						EditorGUILayout.PropertyField(iterator, true);
					}
				}
			}
			serializedObject.ApplyModifiedProperties();

			if (GUILayout.Button("Bake"))
			{
				BakeItemData();
			}
		}

		public void BakeItemData()
		{
			serializedObject.UpdateIfRequiredOrScript();
			// Bake mesh data into asset
			var meshFilter = Target.Prefab.GetComponentInChildren<MeshFilter>();

			var mesh = meshFilter.sharedMesh;
			_mesh.objectReferenceValue = mesh;
			_meshPreview.mesh = mesh;
			var materials = meshFilter.GetComponent<MeshRenderer>().sharedMaterials;
			_materials.arraySize     = materials.Length;
			_indicesCounts.arraySize = materials.Length;
			_indicesStarts.arraySize = materials.Length;
			_baseVertices.arraySize  = materials.Length;
			for (var i = 0; i < materials.Length; i++)
			{
				_materials.GetArrayElementAtIndex(i).objectReferenceValue = materials[i];
				_indicesCounts.GetArrayElementAtIndex(i).uintValue = mesh.GetIndexCount(i);
				_indicesStarts.GetArrayElementAtIndex(i).uintValue = mesh.GetIndexStart(i);
				_baseVertices.GetArrayElementAtIndex(i).uintValue  = mesh.GetBaseVertex(i);
			}
			AssignTransform(meshFilter);
			serializedObject.ApplyModifiedProperties();
		}
		
		private void AssignTransform(MeshFilter meshFilter)
		{
			var localToWorldMatrix = meshFilter.transform.localToWorldMatrix;
			localToWorldMatrix *= Matrix4x4.Translate(-_offset.vector3Value);
			                                                    
			_transform.FindPropertyRelative("e00").floatValue = localToWorldMatrix.m00;
			_transform.FindPropertyRelative("e01").floatValue = localToWorldMatrix.m01;
			_transform.FindPropertyRelative("e02").floatValue = localToWorldMatrix.m02;
			_transform.FindPropertyRelative("e03").floatValue = localToWorldMatrix.m03;
			_transform.FindPropertyRelative("e10").floatValue = localToWorldMatrix.m10;
			_transform.FindPropertyRelative("e11").floatValue = localToWorldMatrix.m11;
			_transform.FindPropertyRelative("e12").floatValue = localToWorldMatrix.m12;
			_transform.FindPropertyRelative("e13").floatValue = localToWorldMatrix.m13;
			_transform.FindPropertyRelative("e20").floatValue = localToWorldMatrix.m20;
			_transform.FindPropertyRelative("e21").floatValue = localToWorldMatrix.m21;
			_transform.FindPropertyRelative("e22").floatValue = localToWorldMatrix.m22;
			_transform.FindPropertyRelative("e23").floatValue = localToWorldMatrix.m23;
			_transform.FindPropertyRelative("e30").floatValue = localToWorldMatrix.m30;
			_transform.FindPropertyRelative("e31").floatValue = localToWorldMatrix.m31;
			_transform.FindPropertyRelative("e32").floatValue = localToWorldMatrix.m32;
			_transform.FindPropertyRelative("e33").floatValue = localToWorldMatrix.m33;
		}
		
		// === Preview ===
		public override bool HasPreviewGUI()
		{
			return true;
		}
		
		public override void OnPreviewGUI(Rect rect, GUIStyle background)
		{
			if (_meshPreview.mesh == null)
			{
				GUI.Label(rect, "No mesh to preview");
				return;
			}
			_meshPreview.OnPreviewGUI(rect, background);
		}
		
		public override void OnPreviewSettings()
		{
			if (_meshPreview.mesh == null)
			{
				return;
			}
			_meshPreview.OnPreviewSettings();
		}
		
		public override string GetInfoString()
		{
			return _meshPreview.mesh == null ? "No mesh to preview" : MeshPreview.GetInfoString(_meshPreview.mesh);
		}

		public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
		{
			return _meshPreview.mesh == null ?
				base.RenderStaticPreview(assetPath, subAssets, width, height) :
				_meshPreview.RenderStaticPreview(width, height);
		}
	}
}
