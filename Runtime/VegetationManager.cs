using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using KVD.Utils.DataStructures;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace KVD.Vegetation
{
	public class VegetationManager
	{
		private static VegetationManager? _instance;

		public static VegetationManager Instance
		{
			get
			{
				return _instance ??= new();
			}
		}
		
		private readonly HashSet<Camera> _cameras = new();
		private readonly List<RuntimeVegetationItem> _vegetationItems = new();
#if ENABLE_PROFILER
		private readonly OnDemandDictionary<CameraItemPair, ProfilerMarker> _markers =
			new(static pair => new($"VegetationManager.Render {pair.Camera.name} {pair.Item.Name}"));
		private static readonly Plane[] Planes = new Plane[6];
#endif

		private VegetationManager()
		{
			RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
		}

		public void RegisterCamera(Camera camera)
		{
			_cameras.Add(camera);
		}

		public void UnregisterCamera(Camera camera)
		{
			_cameras.Remove(camera);
		}

		public void Add(RuntimeVegetationItem runtimeVegetationItem)
		{
			_vegetationItems.Add(runtimeVegetationItem);
		}
		
		public void Remove(RuntimeVegetationItem runtimeVegetationItem)
		{
			_vegetationItems.Remove(runtimeVegetationItem);
		}

		internal void Cull(List<VegetationCell> cells)
		{
			var visible = new NativeArray<bool>(cells.Count, Allocator.Temp);
#if UNITY_EDITOR
			if (Application.isEditor && !Application.isPlaying)
			{
				if (UnityEditor.SceneView.lastActiveSceneView == null || UnityEditor.SceneView.lastActiveSceneView.camera == null)
				{
					for (var i = 0; i < cells.Count; i++)
					{
						cells[i].Culled = true;
					}
					visible.Dispose();
					return;
				}
				var camera  = UnityEditor.SceneView.lastActiveSceneView.camera;
				Cull(cells, camera, visible);
			
				for (var i = 0; i < cells.Count; i++)
				{
					cells[i].Culled = !visible[i];
				}
				visible.Dispose();
				return;
			}
#endif
			foreach (var camera in _cameras)
			{
				Cull(cells, camera, visible);
			}
			
			for (var i = 0; i < cells.Count; i++)
			{
				cells[i].Culled = !visible[i];
			}
			visible.Dispose();
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static void Cull(List<VegetationCell> cells, Camera camera, NativeArray<bool> visible)
		{
			GeometryUtility.CalculateFrustumPlanes(camera, Planes);
			for (var i = 0; i < cells.Count; i++)
			{
				var vegetationCell = cells[i];
				visible[i] = visible[i] || GeometryUtility.TestPlanesAABB(Planes, vegetationCell.Bounds);
			}
		}

		private void OnBeginCameraRendering(ScriptableRenderContext _, Camera camera)
		{
			var isValidCamera = _cameras.Contains(camera);
#if UNITY_EDITOR
			if (camera.cameraType == CameraType.SceneView)
			{
				var currentStage = UnityEditor.SceneManagement.StageUtility.GetCurrentStage();
				if (currentStage == null || currentStage is UnityEditor.SceneManagement.MainStage)
				{
					isValidCamera = true;
				}
				else if (currentStage is UnityEditor.SceneManagement.PrefabStage prefabStage)
				{
					isValidCamera = prefabStage.mode == UnityEditor.SceneManagement.PrefabStage.Mode.InContext;
				}
				else
				{
					isValidCamera = false;
				}
			}
#endif
			if (!isValidCamera)
			{
				return;
			}

			Render(camera);
		}

		private void Render(Camera camera)
		{
			for (var i = 0; i < _vegetationItems.Count; i++)
			{
#if ENABLE_PROFILER
				var pair   = new CameraItemPair(camera, _vegetationItems[i]);
				var marker = _markers[pair];
				marker.Begin();
#endif
				_vegetationItems[i].Render(camera);
#if ENABLE_PROFILER
				marker.End();
#endif
			}
		}
		
		readonly struct CameraItemPair : IEquatable<CameraItemPair>
		{
			public readonly Camera Camera;
			public readonly RuntimeVegetationItem Item;

			public CameraItemPair(Camera camera, RuntimeVegetationItem item)
			{
				Camera = camera;
				Item   = item;
			}

			public bool Equals(CameraItemPair other)
			{
				return Camera.Equals(other.Camera) && Item.Equals(other.Item);
			}
			public override bool Equals(object? obj)
			{
				return obj is CameraItemPair other && Equals(other);
			}
			public override int GetHashCode()
			{
				unchecked
				{
					return (Camera.GetHashCode()*397) ^ Item.GetHashCode();
				}
			}
			public static bool operator ==(CameraItemPair left, CameraItemPair right)
			{
				return left.Equals(right);
			}
			public static bool operator !=(CameraItemPair left, CameraItemPair right)
			{
				return !left.Equals(right);
			}
		}
	}
}
