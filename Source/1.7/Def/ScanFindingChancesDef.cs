using RimWorld;
using UnityEngine;
using Verse;

namespace SaveOurShip2
{
	public class ScanFindingChancesDef : Def
	{
		// This is a "mechanical" def utilizing engene's capability to read objects and their properties into def database to give
		// C# code handy acess to those propertires. In-game objects of that def are not supposed to exist.
		// This XML settings allows XML mods to override the chance to find psecific scanning result, pre-mmade site.
		public const float DefaultWeight = 1f;
		public float PremadeSiteWeight = DefaultWeight;

		public static float GetPremadeSiteWeight()
		{
			ScanFindingChancesDef def = DefDatabase<ScanFindingChancesDef>.GetNamed("ScanFindingChances");
			return def?.PremadeSiteWeight ?? DefaultWeight;
		}
	}
}

