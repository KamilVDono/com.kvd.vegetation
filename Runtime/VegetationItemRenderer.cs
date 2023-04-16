using System.Runtime.InteropServices;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KVD.Vegetation
{
	[ExecuteAlways]
	public class VegetationItemRenderer : MonoBehaviour
	{
		private static readonly int InstanceColorsId = Shader.PropertyToID("_InstanceColors");
#nullable disable
		[SerializeField] private VegetationItem _vegetationItem;
		[SerializeField, Range(10, 1_000_000),]
		private uint _count = 100;
#nullable restore

		private RuntimeVegetationItem? _runtimeVegetationItem;

		private void OnEnable()
		{
			if (_vegetationItem == null)
			{
				return;
			}
			_runtimeVegetationItem = _vegetationItem.ToRuntimeVegetationItem(_count);

			// Initialize buffer with the given population.
			var instancesTransforms = new InstanceTransform[_count];
			var instancesColors     = new InstanceColor[_count];
			for (var i = 0; i < _count; i++)
			{
				var datum    = new InstanceTransform();
				var position = new Vector3(Random.Range(-10, 10), Random.Range(-10, 10), Random.Range(-10, 10));
				var rotation = Quaternion.identity;
				var scale    = Vector3.one;

				datum.transform = Matrix4x4.TRS(position, rotation, scale);
				datum.inverseTransform = datum.transform.inverse;

				instancesTransforms[i] = datum;
				
				instancesColors[i] = new()
				{
					color = Color.Lerp(Color.red, Color.blue, Random.value)
				};
			}

			_runtimeVegetationItem.BindTransforms(instancesTransforms);
			_runtimeVegetationItem.BindData(instancesColors, InstanceColor.Stride(), InstanceColorsId);
			_runtimeVegetationItem.StartRendering();
		}

		private void OnDisable()
		{
			_runtimeVegetationItem?.Dispose();
			_runtimeVegetationItem = null;
		}

		private void OnDrawGizmosSelected()
		{
			_runtimeVegetationItem?.DrawGizmosBounds();
		}

		private struct InstanceColor
		{
			public Color color;

			public static int Stride()
			{
				return Marshal.SizeOf<InstanceColor>();
			}
		}
	}
}
