using UnityEngine;

namespace KVD.Vegetation
{
	[RequireComponent(typeof(Camera))]
	public class VegetationCamera : MonoBehaviour
	{
#nullable disable
		private Camera _camera;
#nullable restore

		private void Awake()
		{
			_camera = GetComponent<Camera>();
		}

		private void OnEnable()
		{
			VegetationManager.Instance.RegisterCamera(_camera);
		}

		private void OnDisable()
		{
			VegetationManager.Instance.UnregisterCamera(_camera);
		}
	}
}
