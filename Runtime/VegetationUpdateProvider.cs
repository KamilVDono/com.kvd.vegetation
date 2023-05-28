using System.Collections.Generic;
using UnityEngine;

namespace KVD.Vegetation
{
	[ExecuteAlways]
	public class VegetationUpdateProvider : MonoBehaviour
	{
		private static VegetationUpdateProvider _instance;
		public static VegetationUpdateProvider Instance
		{
			get
			{
				if (_instance == null)
				{
					var instanceGo = new GameObject(nameof(VegetationUpdateProvider), typeof(VegetationUpdateProvider));
					_instance = instanceGo.GetComponent<VegetationUpdateProvider>();
					if (Application.isPlaying)
					{
						DontDestroyOnLoad(instanceGo);
					}
					else
					{
#if UNITY_EDITOR
						UnityEditor.EditorApplication.update += _instance.Update;
#endif
						instanceGo.hideFlags = HideFlags.HideInHierarchy |
						                       HideFlags.DontSaveInBuild |
						                       HideFlags.DontSaveInEditor;
					}
				}
				return _instance;
			}
		}

		// Still not sure if it's valid to have more than single world but let it be for now
		private List<VegetationWorld> _vegetationWorlds = new();

		private void Update()
		{
			foreach (var world in _vegetationWorlds)
			{
				world.UnityUpdate();
			}
		}
		
		public void Register(VegetationWorld vegetationWorld)
		{
			_vegetationWorlds.Add(vegetationWorld);
		}
		
		public void Unregister(VegetationWorld vegetationWorld)
		{
			_vegetationWorlds.Remove(vegetationWorld);
		}
	}
}
