using KVD.Utils.DataStructures;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace KVD.Vegetation
{
	[CreateAssetMenu(menuName = "Vegetation/Vegetation Item")]
	public class VegetationItem : ScriptableObject
	{
#nullable disable
		[field: Header("User definitions")]
		[field: SerializeField] public GameObject Prefab{ get; private set; }
		[field: SerializeField] public Layer Layer { get; private set; }
		[field: SerializeField] public ShadowCastingMode ShadowCastingMode { get; private set; }

		[field: Header("Spawning data")]
		[field: SerializeField] public VegetationSpacingType SpacingType{ get; private set; } = VegetationSpacingType.Ground;
		[field: SerializeField] public float DesiredDensity{ get; private set; }
		[field: SerializeField] public float OccupiedSpace{ get; set; }
		[field: SerializeField] public float BushSpace{ get; set; }
		[field: SerializeField] public float TreeSpace{ get; set; }
		[field: SerializeField] public Vector3 Offset{ get; set; }
		
		[field: Header("Baked Unity data")]
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

		public int DesiredInstances(float area, float densityModifier)
		{
			return (int)math.floor(DesiredDensity*area*densityModifier);
		}
		public float Area()
		{
			var r = OccupiedSpace*0.5f;
			return Mathf.PI*r*r;
		}
	}
}
