using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace KVD.Vegetation
{
	[BurstCompile]
	public struct InstanceTransformExtractJob : IJob
	{
		[ReadOnly] public NativeSlice<float3> points;
		[ReadOnly] public NativeSlice<half2> rotationAndScale;
		[ReadOnly] public NativeSlice<int> indices;
		public int itemIndex;

		[WriteOnly] public NativeList<InstanceTransform> transforms;

		public void Execute()
		{
			for (var i = 0; i < points.Length; i++)
			{
				if (indices[i] != itemIndex)
				{
					continue;
				}
				var instanceTransform = new InstanceTransform
				{
					transform = float4x4.TRS(points[i], quaternion.RotateY(rotationAndScale[i].x), rotationAndScale[i].y),
				};
				transforms.Add(instanceTransform);
			}
		}
	}
}
