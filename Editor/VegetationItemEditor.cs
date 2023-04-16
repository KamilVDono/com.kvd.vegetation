using KVD.Utils.Editor;
using UnityEditor;
using UnityEngine;

namespace KVD.Vegetation.Editor.Editor
{
	[CustomEditor(typeof(VegetationItem))]
	public class VegetationItemEditor : UnityEditor.Editor
	{
#nullable disable
		SerializedProperty _mesh;
		SerializedProperty _materials;
		SerializedProperty _transform;
		SerializedProperty _indicesCounts;
		SerializedProperty _indicesStarts;
		SerializedProperty _baseVertices;
#nullable restore
			
		VegetationItem Target => (VegetationItem)target;

		private void OnEnable()
		{
			_mesh          = serializedObject.FindBackedProperty(nameof(VegetationItem.Mesh));
			_materials     = serializedObject.FindBackedProperty(nameof(VegetationItem.Materials));
			_transform     = serializedObject.FindBackedProperty(nameof(VegetationItem.ObjectTransform));
			_indicesCounts = serializedObject.FindBackedProperty(nameof(VegetationItem.IndicesCounts));
			_indicesStarts = serializedObject.FindBackedProperty(nameof(VegetationItem.IndicesStarts));
			_baseVertices  = serializedObject.FindBackedProperty(nameof(VegetationItem.BaseVertices));
		}

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			if (GUILayout.Button("Bake"))
			{
				BakeItemData();
			}
		}
		
		private void BakeItemData()
		{
			// Bake mesh data into asset
			var meshFilter = Target.Prefab.GetComponentInChildren<MeshFilter>();

			var mesh = meshFilter.sharedMesh;
			_mesh.objectReferenceValue = mesh;
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
	}
}
