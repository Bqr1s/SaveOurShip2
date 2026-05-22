using System;
using Verse;
using RimWorld;

namespace SaveOurShip2
{
	public class PlaceWorker_ShipPlating : PlaceWorker
	{
		//not on ship hull, not under any building that blocks path
		public override AcceptanceReport AllowsPlacing(BuildableDef def, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			CellRect occupiedRect = GenAdj.OccupiedRect(loc, rot, def.Size);
			foreach (IntVec3 vec in occupiedRect)
			{
				if (vec.Fogged(map) || map.roofGrid.RoofAt(loc) == RoofDefOf.RoofRockThick)
				{
					return false;
				}
				bool isEmptyOdysseySpace = false;
				TerrainDef terrain = map.terrainGrid.TerrainAt(loc);
				if (ModsConfig.OdysseyActive)
				{
					isEmptyOdysseySpace = map.terrainGrid.TerrainAt(loc) == TerrainDefOf.Space;
					TerrainDef foundation = map.terrainGrid.FoundationAt(loc);
					if (foundation?.IsSubstructure ?? false)
					{
						// Not allowed to mix spaceship and gravship hull, that can cause issues
						isEmptyOdysseySpace = false;
					}
				}
				if (isEmptyOdysseySpace)
                {
					return true;
                }
				// Hull plating assumes heavy terrain need, but doesn't have that in XML in order to skip affordance check for empty Ody space
				TerrainAffordanceDef requiredTerrain = TerrainAffordanceDefOf.Heavy;
				// Heavy terrain not required when extending plating off an existing player ship hull (anchored start, free growth)
				if (!loc.GetAffordances(map).Contains(requiredTerrain) && !isEmptyOdysseySpace && !AdjacentToPlayerShipPart(loc, map))
				{
					// Vanilla string key
					return new AcceptanceReport(TranslatorFormattedStringExtensions.Translate("TerrainCannotSupport_TerrainAffordance", def, requiredTerrain).CapitalizeFirst());
				}
				foreach (Thing t in vec.GetThingList(map))
				{
					if (t is Building b)
					{
						if (b.def.passability == Traversability.Impassable || b.def.building.shipPart || b is Building_Door || b.Faction != Faction.OfPlayer || (b.TryGetComp<CompForbiddable>()?.Forbidden ?? false))
							return false;
					}
					else if (t is Blueprint_Build) //td no idea why this cant be checked for def.shipPart, etc.
					{
						return false;
					}
				}
			}
			return true;
		}

		// True if a cardinally adjacent cell holds (or is queued to hold) a player ship part, so plating may extend off an existing hull
		private static bool AdjacentToPlayerShipPart(IntVec3 loc, Map map)
		{
			for (int i = 0; i < 4; i++)
			{
				IntVec3 vec = loc + GenAdj.CardinalDirections[i];
				if (!vec.InBounds(map))
					continue;
				foreach (Thing t in vec.GetThingList(map))
				{
					if (t.Faction != Faction.OfPlayer)
						continue;
					if (t is Building b && b.def.building.shipPart)
						return true;
					if ((t is Blueprint || t is Frame) && t.def.entityDefToBuild is ThingDef td && (td.building?.shipPart ?? false))
						return true;
				}
			}
			return false;
		}
	}
}