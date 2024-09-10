#ifndef VEGETATION_INSTANCED_TRANSFORMS_INCLUDED
#define VEGETATION_INSTANCED_TRANSFORMS_INCLUDED

#if UNITY_ANY_INSTANCING_ENABLED

struct InstanceTransform
{
	float4x4 transform;
	float4x4 inverseTransform;
};
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_D3D11) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PSSL) || defined(SHADER_API_XBOXONE)
uniform StructuredBuffer<InstanceTransform> _InstancesTransforms;
#endif

#endif

void setupInstancesIndirect()
{
#if UNITY_ANY_INSTANCING_ENABLED
	#ifndef DOTS_INSTANCING_ON

#ifdef unity_ObjectToWorld
#undef unity_ObjectToWorld
#endif

#ifdef unity_WorldToObject
#undef unity_WorldToObject
#endif

	unity_ObjectToWorld = _InstancesTransforms[unity_InstanceID].transform;
	unity_WorldToObject = _InstancesTransforms[unity_InstanceID].inverseTransform;
	#endif
#endif
}

void InstancedSetup_float(float3 A, out float3 Out) 
{
	Out = A;
}

#endif