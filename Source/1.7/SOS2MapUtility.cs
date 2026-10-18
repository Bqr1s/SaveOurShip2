using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Vehicles;
using SmashTools;

namespace SaveOurShip2
{
	public static class SOS2MapUtility
	{
		public static bool AnyVehiclePreventsMapRemoval(Map map)
		{
			if(MapHelper.AnyVehicleSkyfallersBlockingMap(map) ||
				MapHelper.AnyAerialVehiclesInRecon(map))
			{
				return true;
			}
			foreach (VehiclePawn vehicle in map.GetDetachedMapComponent<VehiclePositionManager>().AllClaimants)
			{
				if (vehicle.MovementPermissions.HasFlag(VehiclePermissions.Autonomous))
				{
					return true;
				}

				foreach (Pawn passenger in vehicle.AllPawnsAboard)
				{
					if (MapPawns.IsValidColonyPawn(passenger))
					{
						return true;
					}
				}
			}
			return false;
		}

		public static void FixWorldObjectFaction(PlanetTile tile)
		{
			// The issue is it is possible to scan or to arrive to landed ship whithout it's world object faction set to player.
			// Which needs to be fixed to make it abandonable.
			WorldObject worldObject = Find.WorldObjects.ObjectsAt(tile).FirstOrDefault(t => true);
			if (worldObject != null && worldObject.Faction != Faction.OfPlayer && worldObject.def.defName == "EscapeShip")
			{
				worldObject.SetFaction(Faction.OfPlayer);
			}
		}
	}
}

