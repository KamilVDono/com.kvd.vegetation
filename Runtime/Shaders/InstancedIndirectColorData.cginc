#ifndef VEGETATION_INSTANCED_COLORS_INCLUDED
#define VEGETATION_INSTANCED_COLORS_INCLUDED

#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED

struct InstanceColor
{
    float4 color;
};
#if defined(SHADER_API_GLCORE) || defined(SHADER_API_D3D11) || defined(SHADER_API_GLES3) || defined(SHADER_API_METAL) || defined(SHADER_API_VULKAN) || defined(SHADER_API_PSSL) || defined(SHADER_API_XBOXONE)
uniform StructuredBuffer<InstanceColor> _InstanceColors;
#endif

#endif

void InstanceColor_float(out float4 Out) 
{
#ifdef UNITY_PROCEDURAL_INSTANCING_ENABLED
    Out = _InstanceColors[unity_InstanceID].color;
#else
    Out = float4(0, 0, 0, 1);
#endif
}

#endif