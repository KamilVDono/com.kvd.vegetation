using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;

namespace KVD.Vegetation
{
	[ExecuteAlways]
	public class VegetationItemTestRenderer : MonoBehaviour
	{
		private static readonly int InstanceColorsId = Shader.PropertyToID("_InstanceColors");
#nullable disable
		[SerializeField] private VegetationItem _vegetationItem;
		[SerializeField, Range(10, 200_000),]
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
			var instancesTransforms =
				new NativeArray<InstanceTransform>((int)_count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			var instancesColors = new NativeArray<InstanceColor>((int)_count, Allocator.Temp);
			for (var i = 0; i < _count; i++)
			{
				var position = new float3(Random.Range(-10f, 10f), Random.Range(-10f, 10f), Random.Range(-10f, 10f));
				var rotation = quaternion.identity;
				var scale    = new float3(1, 1, 1);

				var datum = new InstanceTransform
				{
					transform = float4x4.TRS(position, rotation, scale),
				};
				instancesTransforms[i] = datum;
				
				instancesColors[i] = new()
				{
					//color = Color.Lerp(Color.red, Color.blue, Random.value)
					color = Color.white,
				};
			}

			_runtimeVegetationItem.BindTransformsInPlace(instancesTransforms);
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
