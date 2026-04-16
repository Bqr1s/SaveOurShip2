using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Concurrent;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Vehicles;
using Verse;
using Debug = System.Diagnostics.Debug;


namespace SaveOurShip2
{
	public class FastLeavingsCalculaor
	{
		// Calculator is per map.
		// Haning it paer-ship-battle will avoid any persistence issues.
		// Also players can remove mods mid-save, so recalculate after save-load will be more robust as well
		Map map;
		ShipMapComp mapComp;
		//Map targetMap;
		//ShipMapComp targetMapComp;
		private ConcurrentDictionary<string, ThingOwner<Thing>> cachedLeavings = new ConcurrentDictionary<string, ThingOwner<Thing>>();
		public FastLeavingsCalculaor(Map map)
		{
			this.map = map;
			mapComp = this.map.GetComponent<ShipMapComp>();
			if (mapComp.ShipMapState != ShipMapState.inCombat)
			{
				Log.Error("SOS 2: Fast leavings used for non-combat map");
				return;
			}
			//targetMap = originMapComp.ShipCombatTargetMap;
			//targetMapComp = targetMap.GetComponent<ShipMapComp>();
		}

		public bool ShouldUseFastLeavings(Building destroyedBuilding)
		{
			// TODO: there might be other concerns tro add.
			// Comp refuelable: different refuelable objects will drop different amout of fuel.
			CompRefuelable refuelable = destroyedBuilding.TryGetComp<CompRefuelable>();
			if (refuelable != null)
			{
				return false;
			}
			if (destroyedBuilding is Frame)
			{
				return false;
			}
			return true;
		}

		public void DoLeavingsForDestroyedBuilding(Building destroyedBuilding, Map map, CellRect leavingsRect)
		{
			Debug.Assert(map == this.map);
			// On cache miss, have to calculate leavings syncronously
			if (!cachedLeavings.ContainsKey(destroyedBuilding.def.defName))
			{
				CalculateLeavingsFor(destroyedBuilding);
			}
			PlaceLeavings(destroyedBuilding.def.defName, cachedLeavings[destroyedBuilding.def.defName], map, leavingsRect);
		}

		private void CalculateLeavingsFor(Building destroyedBuilding)
		{
			
			List<ThingDefCountClass> leavingItems = new List<ThingDefCountClass>();
			if (Rand.Chance(destroyedBuilding.def.killedLeavingsChance))
			{
				if (destroyedBuilding.def.killedLeavings != null)
				{
					leavingItems.AddRange(destroyedBuilding.def.killedLeavings);
				}
				if (destroyedBuilding.HostileTo(Faction.OfPlayer) && destroyedBuilding.def.killedLeavingsPlayerHostile != null)
				{
					leavingItems.AddRange(destroyedBuilding.def.killedLeavingsPlayerHostile);
				}
				if (destroyedBuilding.def.killedLeavingsRanges != null)
				{
					foreach (ThingDefCountRangeClass range in destroyedBuilding.def.killedLeavingsRanges)
					{
						leavingItems.Add(new ThingDefCountClass(range.thingDef, Mathf.RoundToInt(Rand.RangeInclusive(range.countRange.min,
																									                  range.countRange.max))));
					}
				}
			}
			ThingOwner<Thing> leavings = new ThingOwner<Thing>();
			if (destroyedBuilding.def.leaveResourcesWhenKilled)
			{
				foreach(ThingDefCountClass leavingItem in leavingItems)
				{
					bool needSpawn = true;
					if (leavingItem.IsChanceBased)
					{
						needSpawn = Rand.Chance(leavingItem.DropChance);
					}
					if (needSpawn && leavingItem.count > 0)
					{
						Thing item = ThingMaker.MakeThing(leavingItem.thingDef);
						item.stackCount = leavingItem.count;
						leavings.TryAdd(item);
					}
				}
			}
			cachedLeavings.TryAdd(destroyedBuilding.def.defName, leavings);
		}

		private void PlaceLeavings(string buildingDefName, ThingOwner<Thing> leavings, Map map, CellRect leavingsRect)
		{
			ThingOwner<Thing> leavingsCopy = new ThingOwner<Thing>();
			foreach(Thing thing in leavings)
			{
				Thing newThing = ThingMaker.MakeThing(thing.def);
				newThing.stackCount = thing.stackCount;
				leavingsCopy.TryAdd(newThing);
			}
			int dropCellIndex = 0;
			while (leavingsCopy.Count > 0)
			{
				Thing item = leavingsCopy[0];
				item.SetForbidden(true, warnOnFail: false);
				bool dropSuccessful = leavingsCopy.TryDrop(item, leavingsRect.ToList()[dropCellIndex], map, ThingPlaceMode.Near, out Thing resultThing, null);
				dropCellIndex++;
				if (dropCellIndex >= leavingsRect.Area)
				{
					dropCellIndex = 0;
				}
				if (!dropSuccessful)
				{
					Log.Warning("SOS 2: failed to place leavings fast, building: " + buildingDefName + "in rect: " + leavingsRect);
				}
			}
		}

	}
}

