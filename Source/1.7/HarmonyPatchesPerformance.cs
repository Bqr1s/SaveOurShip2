using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;
using HarmonyLib;

namespace SaveOurShip2
{
    // Building_door.OpenPct is quite some math, can be optimized a lot if ticksSinceOpen is 0
    [HarmonyPatch(typeof(Building_Door), "get_OpenPct")]
    public static class FastDoorOpenPct
    {
        public static bool Prefix(Building_Door __instance, ref float __result)
        {
            if (__instance.ticksSinceOpen == 0)
            {
                __result = 0f;
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(GenTemperature), "ComfortableTemperatureRange", new Type[] { typeof(Pawn) })]
    public class FasterComfortableTemperature
    {
        private static Dictionary<Pawn, FloatRange> cachedTemperatureRanges = new Dictionary<Pawn, FloatRange>();

        public static void PurgeCache()
        {
            cachedTemperatureRanges.Clear();
        }

        public static bool Prefix(Pawn p, ref FloatRange __result, out bool __state)
        {
			if (ModIntegration.IsMissileGirlActive())
			{
                // Do not optimize comofrable temperature getter, as it is again duplicates with Missile Girl and could cause 
                // too long update delays with double throttling.
                __state = false;
				return true;
			}
			// Occasionally completely clear cache to remove no longer existing enemy pawns, pawns from closed maps, etc
			if (Find.TickManager.TicksGame % GenDate.TicksPerDay == 0)
            {
                cachedTemperatureRanges.Clear();
            }
            if (cachedTemperatureRanges.ContainsKey(p))
            {
                __result = cachedTemperatureRanges[p];
                __state = false;
                // If some pawn is exposed to space and got dressed into EVA, there will be a delay that shouldn't be too long
                // but having delay is better than serius performance impact
                if (p.IsHashIntervalTick(GenTicks.TicksPerRealSecond * 6))
                {
                    cachedTemperatureRanges.Remove(p);
                }
                return false;
            }
            else
            {
                __state = true;
                return true;
            }
        }

        public static void Postfix(Pawn p, ref FloatRange __result, bool __state)
        {
            // __state is need add to cache
            if (__state)
            {
                cachedTemperatureRanges.Add(p, __result);
            }
        }
    }

    //  DoLeavingsFor(Thing diedThing, Map map, DestroyMode mode, CellRect leavingsRect, Predicate<IntVec3> nearPlaceValidator = null, List<Thing> listOfLeavingsOut = null)
    [HarmonyPatch(typeof(GenLeaving), "DoLeavingsFor", new Type[] { typeof(Thing), typeof(Map), typeof(DestroyMode), typeof(CellRect), typeof(Predicate<IntVec3>),
        typeof(List<Thing>) })]
    public static class FasterLeavings
    {
        public static bool Prefix(Thing diedThing, Map map, DestroyMode mode, CellRect leavingsRect, Predicate<IntVec3> nearPlaceValidator = null,
            List<Thing> listOfLeavingsOut = null)
        {
            if (mode != DestroyMode.KillFinalize)
            {
                return true;
            }
            if(ResourceBank.IsNonModdedPlating(diedThing.def))
            {
                // normal hull tile drops nothing
                return false;
            }
            if (ResourceBank.IsNonModdedHull(diedThing.def))
            {
                Thing slag = ThingMaker.MakeThing(ThingDefOf.ChunkSlagSteel);
                if (GenDrop.TryDropSpawn(slag, leavingsRect.CenterCell, map, ThingPlaceMode.Near, out var lastResultingThing, null, nearPlaceValidator, playDropSound: false))
                {
                    listOfLeavingsOut?.Add(slag);
                }
                return false;
            }
            return true;
        }
    }
}
