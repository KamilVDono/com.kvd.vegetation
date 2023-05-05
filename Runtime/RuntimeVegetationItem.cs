using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace KVD.Vegetation
{
	public class RuntimeVegetationItem : IDisposable
	{
		const int ArgsStride = 5 * sizeof(uint);
		
		private readonly Mesh _mesh;
		private readonly Material[] _materials;
		private readonly float4x4 _objectTransform;
		private readonly int _layer;
		private readonly ShadowCastingMode _shadowCastingMode;
		
		private Bounds _bounds;
		private readonly ComputeBuffer _argsBuffer;
		private readonly List<BufferData> _buffers = new();

		public uint Count{ get; private set; }
		public string Name => _mesh.name;
		public int MeshesCount => _materials.Length;

		public RuntimeVegetationItem(VegetationItem item, uint count)
		{
			_mesh              = item.Mesh;
			_materials         = new Material[item.Materials.Length];
			_objectTransform   = item.ObjectTransform;
			_layer             = item.Layer;
			_shadowCastingMode = item.ShadowCastingMode;
			Count              = count;

			// Arguments for drawing mesh.
			var args = new NativeArray<uint>(_materials.Length*5, Allocator.Temp);
			for (var i = 0; i < _materials.Length; i++)
			{
				_materials[i] = new(item.Materials[i]);
				args[i*5 + 0] = item.IndicesCounts[i];
				args[i*5 + 1] = count;
				args[i*5 + 2] = item.IndicesStarts[i];
				args[i*5 + 3] = item.BaseVertices[i];
				args[i*5 + 4] = 0;
			}
			_argsBuffer = new(_materials.Length, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
			_argsBuffer.SetData(args);
			args.Dispose();
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void BindTransformsInPlace(NativeArray<InstanceTransform> transforms, bool dispose = true)
		{
			var future = BindTransforms(transforms, dispose);
			BindTransforms(future);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public BindInstanceTransformsFuture BindTransforms(NativeArray<InstanceTransform> transforms, bool dispose = true)
		{
			var future = BindInstanceTransformsFuture.Create(_objectTransform, transforms, dispose);
			return future;
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void BindTransforms(BindInstanceTransformsFuture bindTransforms)
		{
			if (bindTransforms.handle.IsCompleted)
			{
				bindTransforms.handle.Complete();
			}
			var buffer = new ComputeBuffer(bindTransforms.transforms.Length, InstanceTransform.Stride());
			buffer.SetData(bindTransforms.transforms);
			BindData(buffer, InstanceTransform.InstancesTransformsId);
			
			CalculateBounds(bindTransforms.transforms);
			bindTransforms.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void BindData(Array data, int stride, int property)
		{
			var computeBuffer = new ComputeBuffer(data.Length, stride);
			computeBuffer.SetData(data);
			BindData(computeBuffer, property);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void BindData<T>(NativeArray<T> data, int stride, int property, bool dispose = true) where T : struct
		{
			var computeBuffer = new ComputeBuffer(data.Length, stride);
			computeBuffer.SetData(data);
			BindData(computeBuffer, property);
			if (dispose) data.Dispose();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void BindData(ComputeBuffer computeBuffer, int property)
		{
			var bufferData = new BufferData
			{
				Buffer   = computeBuffer,
				Property = property,
			};
			_buffers.Add(bufferData);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void StartRendering()
		{
			VegetationManager.Instance.Add(this);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void StopRendering()
		{
			VegetationManager.Instance.Remove(this);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Render(Camera camera)
		{
			for (var i = 0; i < _materials.Length; i++)
			{
				for (var j = 0; j < _buffers.Count; j++)
				{
					_materials[i].SetBuffer(_buffers[j].Property, _buffers[j].Buffer);
				}
				Graphics.DrawMeshInstancedIndirect(_mesh, i, _materials[i], _bounds,
					_argsBuffer, argsOffset: i*ArgsStride,
					camera: camera, layer: _layer, castShadows: _shadowCastingMode);
			}
		}
		
		public void DrawGizmosBounds()
		{
			Gizmos.DrawWireCube(_bounds.center, _bounds.size);
		}

		public void Dispose()
		{
			_argsBuffer.Dispose();
			foreach (var buffer in _buffers)
			{
				buffer.Buffer.Dispose();
			}
			_buffers.Clear();
			VegetationManager.Instance.Remove(this);
		}

		private void CalculateBounds(NativeArray<InstanceTransform> transforms)
		{
			var localBounds = _mesh.bounds;
			var result      = new NativeArray<float3>(2, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
			BoundsCalculationJob.Create(transforms, result, localBounds).Run();
			_bounds = new();
			_bounds.SetMinMax(result[0], result[1]);
			result.Dispose();
		}

		private struct BufferData
		{
			private ComputeBuffer _buffer;
			private int _property;

			public ComputeBuffer Buffer
			{
				readonly get => _buffer;
				set => _buffer = value;
			}

			public int Property
			{
				readonly get => _property;
				set => _property = value;
			}
		}

		[BurstCompile]
		struct BindInstanceTransformJob : IJobParallelFor
		{
			public float4x4 objectTransform;
			public NativeArray<InstanceTransform> transforms;
			
			public void Execute(int index)
			{
				var transform = transforms[index];
				transform.transform        = math.mul(objectTransform, transform.transform);
				transform.inverseTransform = math.inverse(transform.transform);
				transforms[index]          = transform;
			}
		}
		
		public struct BindInstanceTransformsFuture : IDisposable
		{
			public JobHandle handle;
			public NativeArray<InstanceTransform> transforms;
			private bool _disposeTransforms;

			public static BindInstanceTransformsFuture Create(float4x4 objectTransform,
				NativeArray<InstanceTransform> transforms, bool dispose = true)
			{
				var instance = new BindInstanceTransformsFuture();
				instance.transforms = transforms;
				var job = new BindInstanceTransformJob()
				{
					objectTransform = objectTransform,
					transforms      = transforms,
				};
				instance.handle = job.Schedule(transforms.Length, 64);
				instance._disposeTransforms = dispose;
				return instance;
			}

			public void Dispose()
			{
				if (!handle.IsCompleted)
				{
					handle.Complete();
				}
				if (_disposeTransforms)
				{
					transforms.Dispose();
				}
			}
		}

		[BurstCompile]
		struct BoundsCalculationJob : IJob
		{
			[ReadOnly] public NativeArray<InstanceTransform> transforms;
			[ReadOnly, DeallocateOnJobCompletion]
			public NativeArray<float4> localBoundsPoints;
			public NativeArray<float3> minMaxResult;

			public static BoundsCalculationJob Create(NativeArray<InstanceTransform> transforms, NativeArray<float3> minMaxResult, Bounds localBounds)
			{
				var boundsMin    = new float4(localBounds.min, 1f);
				var boundsMax    = new float4(localBounds.max, 1f);
				var boundsPoints = new NativeArray<float4>(8, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
				boundsPoints[0] = boundsMin;
				boundsPoints[1] = new(boundsMin.x, boundsMin.y, boundsMax.z, 1f);
				boundsPoints[2] = new(boundsMin.x, boundsMax.y, boundsMin.z, 1f);
				boundsPoints[3] = new(boundsMin.x, boundsMax.y, boundsMax.z, 1f);
				boundsPoints[4] = new(boundsMax.x, boundsMin.y, boundsMin.z, 1f);
				boundsPoints[5] = new(boundsMax.x, boundsMin.y, boundsMax.z, 1f);
				boundsPoints[6] = new(boundsMax.x, boundsMax.y, boundsMin.z, 1f);
				boundsPoints[7] = boundsMax;

				var job = new BoundsCalculationJob
				{
					transforms        = transforms,
					localBoundsPoints = boundsPoints,
					minMaxResult      = minMaxResult,
				};
				return job;
			}

			public void Execute()
			{
				var point = localBoundsPoints[0];
				point = math.mul(transforms[0].transform, point);
				minMaxResult[0] = point.xyz;
				minMaxResult[1] = point.xyz;
				
				for (var i = 0; i < transforms.Length; i++)
				{
					CalculateBounds(transforms[i].transform);
				}
			}

			void CalculateBounds(float4x4 transform)
			{
				var min = minMaxResult[0];
				var max = minMaxResult[1];
				
				// Box has always 8 corners
				for (var i = 0; i < 8; i++)
				{
					var point = localBoundsPoints[i];
					point = math.mul(transform, point);
					var point3D = point.xyz;
					min = math.min(min, point3D);
					max = math.max(max, point3D);
				}
				
				minMaxResult[0] = min;
				minMaxResult[1] = max;
			}
		}
	}
}
