using System;
using Verse;
using RimWorld;

namespace SaveOurShip2
{
	public class PlaceWorker_OnShipHull : PlaceWorker
	{
		public override AcceptanceReport AllowsPlacing(BuildableDef def, IntVec3 loc, Rot4 rot, Map map, Thing thingToIgnore = null, Thing thing = null)
		{
			//standalone-capable parts (e.g. small turrets) may be placed off ship hull, on any building-supporting terrain
			bool canWorkStandalone = (def as ThingDef)?.GetCompProperties<CompProps_ShipHeat>()?.canWorkStandalone ?? false;
			CellRect occupiedRect = GenAdj.OccupiedRect(loc, rot, def.Size);
			foreach (IntVec3 vec in occupiedRect)
			{
				bool hasPlating = false;
				bool hasRestrictedBay = false;
				HasPlatingAndRestrictedBayFor(def, loc, map, out hasPlating, out hasRestrictedBay);
				if (hasRestrictedBay)
					return false;
				if (!hasPlating && !canWorkStandalone)
					return new AcceptanceReport(TranslatorFormattedStringExtensions.Translate("SoS.PlaceOnShipHull"));
			}
			return true;
		}

		public static void HasPlatingAndRestrictedBayFor(BuildableDef def, IntVec3 location, Map map, out bool hasPlating, out bool hasRestrictedBay)
        {
			hasPlating = false;
			hasRestrictedBay = false;
			foreach (Thing t in location.GetThingList(map))
			{
				if (t is Building b && b.Faction == Faction.OfPlayer)
				{
					var shipPart = b.TryGetComp<CompShipCachePart>();
					if (shipPart != null && (shipPart.Props.isPlating || (shipPart.Props.isHardpoint && def.defName.Contains("Turret"))))
					{
						hasPlating = true;
						if (hasPlating && hasRestrictedBay)
                        {
							return;
                        }
					}
					if (b.TryGetComp<CompShipBaySalvage>() != null)
						hasRestrictedBay = true;
				}
			}
		}
	}
}