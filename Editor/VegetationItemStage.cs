using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KVD.Vegetation.Editor.Editor
{
	public class VegetationItemStage : PreviewSceneStage
	{
		private VegetationItem _target;
		private GameObject _prefabInstance;

		public override string assetPath => AssetDatabase.GetAssetPath(_target);

		protected override GUIContent CreateHeaderContent()
		{
			var path  = AssetDatabase.GetAssetPath(_target);
			var label = Path.GetFileNameWithoutExtension(path);
			var icon  = AssetDatabase.GetCachedIcon(path);
			return new(label, icon);
		}

		public override ulong GetCombinedSceneCullingMaskForCamera()
		{
			return 0;
		}

		protected override bool OnOpenStage()
		{
			base.OnOpenStage();
			_prefabInstance = Instantiate(_target.Prefab);
			StageUtility.PlaceGameObjectInCurrentStage(_prefabInstance);
			_prefabInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
			SceneView.duringSceneGui -= SceneGUI;
			SceneView.duringSceneGui += SceneGUI;
			return true;
		}

		protected override void OnCloseStage()
		{
			SceneView.duringSceneGui -= SceneGUI;
			base.OnCloseStage();
		}

		private void SceneGUI(SceneView sceneView)
		{
			EditorGUI.BeginChangeCheck();
			var scale = _target.OccupiedSpace;
			scale = Handles.RadiusHandle(Quaternion.identity, _target.Offset, scale/2)*2;
			if (EditorGUI.EndChangeCheck())
			{
				_target.OccupiedSpace = scale;
				EditorUtility.SetDirty(_target);
			}
			
			EditorGUI.BeginChangeCheck();
			scale = _target.BushSpace;
			scale = Handles.RadiusHandle(Quaternion.identity, _target.Offset+Vector3.up*2, scale/2)*2;
			if (EditorGUI.EndChangeCheck())
			{
				_target.BushSpace = scale;
				EditorUtility.SetDirty(_target);
			}
			
			EditorGUI.BeginChangeCheck();
			scale = _target.TreeSpace;
			scale = Handles.RadiusHandle(Quaternion.identity, _target.Offset+Vector3.up*5, scale/2)*2;
			if (EditorGUI.EndChangeCheck())
			{
				_target.TreeSpace = scale;
				EditorUtility.SetDirty(_target);
			}

			EditorGUI.BeginChangeCheck();
			var offset = _target.Offset;
			offset = Handles.PositionHandle(offset, Quaternion.identity);
			if (EditorGUI.EndChangeCheck())
			{
				offset.y = 0;
				_target.Offset = offset;
				EditorUtility.SetDirty(_target);
				var editor = (VegetationItemEditor)UnityEditor.Editor.CreateEditor(_target);
				editor.BakeItemData();
			}
			
		}

		// === Open
		static void Open(VegetationItem target)
		{
			var stage = CreateInstance<VegetationItemStage>();
			stage._target = target;
			StageUtility.GoToStage(stage, true);
		}
		
		[OnOpenAsset]
		public static bool OnOpenAsset(int instanceID, int line)
		{
			var target = EditorUtility.InstanceIDToObject(instanceID);
 
			if (target is VegetationItem vegetationItem)
			{
				Open(vegetationItem);
				return true;
			}
 
			return false;
		}
	}
}
