using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;

namespace KVD.Vegetation
{
	[Serializable]
	public class VegetationCell : IDisposable
	{
		private static readonly ProfilerMarker SamplerCreationMarker = new("Sampler Creation");
		private static readonly ProfilerMarker SamplerExecuteMarker = new("Sampler Execute");
		private static readonly ProfilerMarker RaycastCreationMarker = new("Raycast Creation");
		private static readonly ProfilerMarker RaycastExecuteMarker = new("Raycast Execute");
		private static readonly ProfilerMarker RaycastWriteBackMarker = new("Raycast Write Back");
		private static readonly ProfilerMarker TransformsJobsMarker = new("Transforms Jobs");
		private static readonly ProfilerMarker RuntimeItemCreationMarker = new("Runtime Item Creation");

		private bool _culled = true;

		public List<RuntimeVegetationItem> items = new();

		public Bounds Bounds{ get; private set; }

		public bool Culled
		{
			get => _culled;
			set
			{
				if (_culled == value)
				{
					return;
				}
				_culled = value;
				if (_culled)
				{
					for (var i = 0; i < items.Count; i++)
					{
						items[i].StopRendering();
					}
				}
				else
				{
					for (var i = 0; i < items.Count; i++)
					{
						items[i].StartRendering();
					}
				}
			}
		}

		public VegetationCell(float size, Vector3 position)
		{
			Bounds = new(position, Vector3.one*size);
		}

		public void Spawn(List<VegetationItem> source, float density, uint seed)
		{
			items.Capacity = Mathf.Max(items.Capacity, source.Count);

			SamplerCreationMarker.Begin();
			var sampler                 = new PoissonDiscSampler(source, Bounds, density, 30, seed);
			var samplerPoints           = sampler.points;
			var samplerItemIndices      = sampler.itemIndices;
			var samplerRotationAndScale = sampler.rotationAndScale;
			var samplerActiveList       = sampler.active;
			SamplerCreationMarker.End();

			SamplerExecuteMarker.Begin();
			sampler.Run();
			samplerActiveList.Dispose();
			SamplerExecuteMarker.End();

			RaycastCreationMarker.Begin();
			var queryParameter = new QueryParameters
			{
				hitBackfaces = false, hitMultipleFaces = false, hitTriggers = QueryTriggerInteraction.UseGlobal,
				layerMask    = -1,
			};
			var points      = samplerPoints.AsArray();
			var pointsCount = points.Length;
			var results     = new NativeArray<RaycastHit>(pointsCount, Allocator.TempJob);
			var commands    = new NativeArray<RaycastCommand>(pointsCount, Allocator.TempJob);
			for (var i = 0; i < pointsCount; i++)
			{
				commands[i] = new(points[i], Vector3.down, queryParameter, 2000);
			}
			RaycastCreationMarker.End();

			RaycastExecuteMarker.Begin();
			RaycastCommand.ScheduleBatch(commands, results, 32, 1).Complete();
			RaycastExecuteMarker.End();

			RaycastWriteBackMarker.Begin();
			for (var i = 0; i < pointsCount; i++)
			{
				var point = points[i];
				point.y   = results[i].point.y;
				points[i] = point;
			}
			RaycastWriteBackMarker.End();

			commands.Dispose();
			results.Dispose();

			var handles    = new NativeArray<JobHandle>(source.Count, Allocator.Temp);
			var transforms = new NativeArray<NativeList<InstanceTransform>>(source.Count, Allocator.Temp);
			for (var i = 0; i < source.Count; i++)
			{
				var transform = new NativeList<InstanceTransform>(points.Length, Allocator.TempJob);
				var job = new InstanceTransformExtract
				{
					points           = points,
					rotationAndScale = samplerRotationAndScale.AsArray(),
					indices          = samplerItemIndices.AsArray(),
					itemIndex        = i,
					transforms       = transform,
				};

				handles[i]    = job.Schedule();
				transforms[i] = transform;
			}

			TransformsJobsMarker.Begin();
			JobHandle.CompleteAll(handles);
			TransformsJobsMarker.End();
			
			handles.Dispose();
			samplerPoints.Dispose();
			samplerItemIndices.Dispose();
			samplerRotationAndScale.Dispose();

			RuntimeItemCreationMarker.Begin();
			var futures = new NativeArray<RuntimeVegetationItem.BindInstanceTransformsFuture>(source.Count, Allocator.Temp);
			var startingIndex = items.Count;
			for (var i = 0; i < source.Count; i++)
			{
				var transform             = transforms[i];
				var runtimeVegetationItem = source[i].ToRuntimeVegetationItem((uint)transform.Length);
				items.Add(runtimeVegetationItem);
				var future = runtimeVegetationItem.BindTransforms(transform.AsArray());
				futures[i] = future;
			}
			for (var i = 0; i < source.Count; i++)
			{
				items[i+startingIndex].BindTransforms(futures[i]);
				transforms[i].Dispose();
			}
			RuntimeItemCreationMarker.End();
			
			futures.Dispose();
			transforms.Dispose();
		}

		public void Dispose()
		{
			foreach (var item in items)
			{
				item.Dispose();
			}
			items.Clear();
		}
	}
}