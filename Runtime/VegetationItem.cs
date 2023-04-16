using KVD.Utils.DataStructures;
using UnityEngine;
using UnityEngine.Rendering;

namespace KVD.Vegetation
{
	[CreateAssetMenu(menuName = "Vegetation/Vegetation Item")]
	public class VegetationItem : ScriptableObject
	{
#nullable disable
		[field: SerializeField] public GameObject Prefab{ get; private set; }
		[field: SerializeField] public Layer Layer { get; private set; }
		[field: SerializeField] public ShadowCastingMode ShadowCastingMode { get; private set; }
		
		[field: SerializeField] public Mesh Mesh{ get; private set; }
		[field: SerializeField] public Material[] Materials{ get; private set; }
		[field: SerializeField] public Matrix4x4 ObjectTransform{ get; private set; }
		[field: SerializeField] public uint[] IndicesCounts{ get; private set; }
		[field: SerializeField] public uint[] IndicesStarts{ get; private set; }
		[field: SerializeField] public uint[] BaseVertices{ get; private set; }
#nullable restore

		public RuntimeVegetationItem ToRuntimeVegetationItem(uint count)
		{
			return new(this, count);
		}
	}
}
