using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace KVD.Vegetation
{
	public struct InstanceTransform
	{
		public static readonly int InstancesTransformsId = Shader.PropertyToID("_InstancesTransforms");
		
		public float4x4 transform;
		public float4x4 inverseTransform;

		public static int Stride()
		{
			return Marshal.SizeOf<InstanceTransform>();
		}
	}
}
