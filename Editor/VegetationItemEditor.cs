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
		SerializedProperty _material;
		SerializedProperty _transform;
		SerializedProperty _indicesCount;
		SerializedProperty _indicesStart;
		SerializedProperty _baseVertex;
#nullable restore
			
		VegetationItem Target => (VegetationItem)target;

		private void OnEnable()
		{
			_mesh         = serializedObject.FindBackedProperty(nameof(VegetationItem.Mesh));
			_material     = serializedObject.FindBackedProperty(nameof(VegetationItem.Material));
			_transform    = serializedObject.FindBackedProperty(nameof(VegetationItem.ObjectTransform));
			_indicesCount = serializedObject.FindBackedProperty(nameof(VegetationItem.IndicesCount));
			_indicesStart = serializedObject.FindBackedProperty(nameof(VegetationItem.IndicesStart));
			_baseVertex   = serializedObject.FindBackedProperty(nameof(VegetationItem.BaseVertex));
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
			_mesh.objectReferenceValue     = mesh;
			_material.objectReferenceValue = meshFilter.GetComponent<MeshRenderer>().sharedMaterial;
			AssignTransform(meshFilter);
			_indicesCount.uintValue        = mesh.GetIndexCount(0);
			_indicesStart.uintValue        = mesh.GetIndexStart(0);
			_baseVertex.uintValue          = mesh.GetBaseVertex(0);
			
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
