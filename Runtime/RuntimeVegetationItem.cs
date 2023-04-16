using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace KVD.Vegetation
{
	public class RuntimeVegetationItem : IDisposable
	{
		const int ArgsStride = 5 * sizeof(uint);
		
		private readonly Mesh _mesh;
		private readonly Material[] _materials;
		private readonly Matrix4x4 _objectTransform;
		private readonly int _layer;
		private readonly ShadowCastingMode _shadowCastingMode;
		
		private Bounds _bounds;
		private readonly ComputeBuffer _argsBuffer;
		private readonly List<BufferData> _buffers = new();
		
		public string Name => _mesh.name;

		public RuntimeVegetationItem(VegetationItem item, uint count)
		{
			_mesh              = item.Mesh;
			_materials         = item.Materials;
			_objectTransform   = item.ObjectTransform;
			_layer             = item.Layer;
			_shadowCastingMode = item.ShadowCastingMode;
			
			// Arguments for drawing mesh.
			var args = new uint[_materials.Length*5];
			for (var i = 0; i < _materials.Length; i++)
			{
				args[i*5 + 0] = item.IndicesCounts[i];
				args[i*5 + 1] = count;
				args[i*5 + 2] = item.IndicesStarts[i];
				args[i*5 + 3] = item.BaseVertices[i];
				args[i*5 + 4] = 0;
			}
			_argsBuffer = new(_materials.Length, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
			_argsBuffer.SetData(args);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void BindTransforms(Matrix4x4[] transforms)
		{
			var instanceTransforms = new InstanceTransform[transforms.Length];
			for (var i = 0; i < transforms.Length; i++)
			{
				instanceTransforms[i] = new() { transform = transforms[i] };
			}
			BindTransforms(instanceTransforms);
		}
		
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void BindTransforms(InstanceTransform[] transforms)
		{
			for (var i = 0; i < transforms.Length; i++)
			{
				transforms[i].transform        *= _objectTransform;
				transforms[i].inverseTransform =  transforms[i].transform.inverse;
			}
			var buffer = new ComputeBuffer(transforms.Length, InstanceTransform.Stride());
			buffer.SetData(transforms);
			BindData(buffer, InstanceTransform.InstancesTransformsId);
			
			CalculateBounds(transforms);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void BindData(Array data, int stride, int property)
		{
			var computeBuffer = new ComputeBuffer(data.Length, stride);
			computeBuffer.SetData(data);
			BindData(computeBuffer, property);
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

		private void CalculateBounds(InstanceTransform[] transforms)
		{
			var localBounds = _mesh.bounds;
			var boundsPoints = new Vector3[8];
			boundsPoints[0] = localBounds.min;
			boundsPoints[1] = new(localBounds.min.x, localBounds.min.y, localBounds.max.z);
			boundsPoints[2] = new(localBounds.min.x, localBounds.max.y, localBounds.min.z);
			boundsPoints[3] = new(localBounds.min.x, localBounds.max.y, localBounds.max.z);
			boundsPoints[4] = new(localBounds.max.x, localBounds.min.y, localBounds.min.z);
			boundsPoints[5] = new(localBounds.max.x, localBounds.min.y, localBounds.max.z);
			boundsPoints[6] = new(localBounds.max.x, localBounds.max.y, localBounds.min.z);
			boundsPoints[7] = localBounds.max;
			
			var worldBounds = GeometryUtility.CalculateBounds(boundsPoints, transforms[0].transform);
			for (var i = 1; i < transforms.Length; i++)
			{
				worldBounds.Encapsulate(GeometryUtility.CalculateBounds(boundsPoints, transforms[i].transform));
			}
			_bounds = worldBounds;
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
	}
}
