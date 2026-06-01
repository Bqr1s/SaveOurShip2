using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Vehicles;

namespace SaveOurShip2
{
	public partial class ShipInteriorMod2
	{
		private sealed class MoveShipContext
		{
			public Building core;
			public Map targetMap;
			public IntVec3 adjustment;
			public Faction fac;
			public byte rotNum;
			public bool includeRock;
			public bool clearArea;

			public bool devMode;
			public TimeHelper watch = new TimeHelper();
			public int rotb;
			public Map sourceMap;
			public bool sourceMapIsSpace;
			public ShipMapComp sourceMapComp;
			public bool playerMove;
			public int shipIndex;
			public HashSet<int> shipIndexes;
			public SpaceShipCache shipCache;
			public HashSet<IntVec3> sourceArea;
			public bool targetMapIsSpace;
			public float weBeCrashing;
			public ShipMapComp targetMapComp;
			public HashSet<IntVec3> targetArea = new HashSet<IntVec3>();

			public HashSet<Thing> toMoveShipParts = new HashSet<Thing>();
			public HashSet<Thing> toMoveBuildings = new HashSet<Thing>();
			public HashSet<Thing> toMoveThings = new HashSet<Thing>();
			public List<Thing> toDestroy = new List<Thing>();
			public List<Zone> zonesToCopy = new List<Zone>();
			public List<Room> roomsToTemp = new List<Room>();
			public List<IntVec3> fogToCopy = new List<IntVec3>();
			public List<Tuple<IntVec3, float>> posTemp = new List<Tuple<IntVec3, float>>();
			public List<IntVec3> sealedLocations = new List<IntVec3>();
			public List<Tuple<IntVec3, TerrainDef, ColorDef>> terrainToCopy = new List<Tuple<IntVec3, TerrainDef, ColorDef>>();
			public List<Tuple<IntVec3, RoofDef>> roofToCopy = new List<Tuple<IntVec3, RoofDef>>();
			public List<IntVec3> fireExplosions = new List<IntVec3>();
			public List<Pawn> pawns = new List<Pawn>();
			public List<Plant> plants = new List<Plant>();
			public List<Building> toUninstall = new List<Building>();
			public List<MinifiedThing> toInstallAfterMove = new List<MinifiedThing>();
			public IEnumerable<CompEngineTrail> engines;
			public bool fail;
			public StringBuilder reason = new StringBuilder();

			public MoveShipContext(Building core, Map targetMap, IntVec3 adjustment, Faction fac, byte rotNum, bool includeRock, bool clearArea)
			{
				this.core = core;
				this.targetMap = targetMap;
				this.adjustment = adjustment;
				this.fac = fac;
				this.rotNum = rotNum;
				this.includeRock = includeRock;
				this.clearArea = clearArea;
				rotb = 4 - rotNum;
				devMode = Prefs.DevMode;
			}
		}

		//td change or make new for call on ship direct
		public static void MoveShip(Building core, Map targetMap, IntVec3 adjustment, Faction fac = null, byte rotNum = 0, bool includeRock = false, bool clearArea = false)
		{
			if (core.DestroyedOrNull())
			{
				Log.Warning("SOS2: ".Colorize(Color.cyan) + "Ship move to map: ".Colorize(Color.red) + targetMap + " failed, target destroyed!".Colorize(Color.red));
				return;
			}
			var ctx = new MoveShipContext(core, targetMap, adjustment, fac, rotNum, includeRock, clearArea);
			if (!MoveShipPrepare(ctx))
				return;
			MoveShipUninstallConnectedBuildings(ctx);
			MoveShipScanSourceArea(ctx);
			MoveShipSetupTakeoffFires(ctx);
			MoveShipPrepareTargetArea(ctx);
			if (!MoveShipCheckAdjacentShips(ctx))
				return;
			MoveShipClearTargetObstacles(ctx);
			if (!MoveShipDespawnForMove(ctx))
				return;
			MoveShipRespawnOnTarget(ctx);
			MoveShipUpdateShipCache(ctx);
			MoveShipConsumeMoveFuel(ctx);
			MoveShipMergeAdjacentShips(ctx);
			MoveShipTransferZones(ctx);
			MoveShipTransferTerrainAndPlants(ctx);
			MoveShipTransferFogAndRoofs(ctx);
			MoveShipRestoreRoomClimate(ctx);
			MoveShipFinalizeMoveEffects(ctx);
		}

		private static bool MoveShipPrepare(MoveShipContext ctx)
		{
					if (ctx.devMode)
				ctx.watch.Record("prepare");

			MoveShipFlag = true;
					shipOriginMap = null;
					ctx.sourceMap = ctx.core.Map;
					ctx.sourceMapIsSpace = sourceMap.IsSpace();
					ctx.sourceMapComp = sourceMap.GetComponent<ShipMapComp>();
					ctx.playerMove = ctx.core.Faction == Faction.OfPlayer && sourceMapComp.ShipMapState != ShipMapState.inCombat;
					ctx.shipIndex = sourceMapComp.ShipIndexOnVec(ctx.core.Position);
					if (ctx.shipIndex == -1)
					{
						Log.Error("SOS2: ".Colorize(Color.cyan) + ctx.sourceMap + " Ship move to map: ".Colorize(Color.red) + ctx.targetMap + " failed, no valid ship index found! Save, reload and report!".Colorize(Color.red));
						return;
					}
					else if (ctx.devMode)
					{
						Log.Message("SOS2: ".Colorize(Color.cyan) + ctx.sourceMap + " Ship ".Colorize(Color.green) + ctx.shipIndex + " Moving ship to ".Colorize(Color.green) + ctx.targetMap + " with: ".Colorize(Color.green) + ctx.core);
					}
					ctx.shipIndexes = new HashSet<int> { ctx.shipIndex };
					ctx.shipCache = sourceMapComp.ShipsOnMap[ctx.shipIndex];
					ctx.sourceArea = new HashSet<IntVec3>(ctx.shipCache.Area);
			
					if (ctx.targetMap == null)
				ctx.targetMap = ctx.core.Map;
			
					ctx.targetMapIsSpace = targetMap.IsSpace();
					ctx.weBeCrashing = 0;
					ctx.targetMapComp = targetMap.GetComponent<ShipMapComp>();
					ctx.targetArea = new HashSet<IntVec3>();
			
					if (sourceMapComp.ShipMapState == ShipMapState.inCombat && sourceMapComp.Docked.Any()) //undock all in combat
					{
						sourceMapComp.UndockAllFrom(ctx.shipIndex);
					}
			return true;
		}

		private static void MoveShipUninstallConnectedBuildings(MoveShipContext ctx)
		{
					foreach (IntVec3 pos in ctx.sourceArea)
					{
						// Uninstall connected buildings which can't be "just moved"
						foreach (Thing t in pos.GetThingList(ctx.sourceMap))
						{
							t.beingTransportedOnGravship = true;
							if (t is Building b)
							{
								if (b.def.defName == "CashRegister_CashRegister")
								{
									toUninstall.Add(b);
								}
								// Life support system moved to separate mod from questionable ethics. Uninstalling it does not work straight up,
								// but unpowering disables well enough for ctx.shipCache move.
								if (b.def.defName == "QE_LifeSupportSystem")
								{
									CompFlickable flickComp = b.TryGetComp<CompFlickable>();
									if (flickComp != null && flickComp.SwitchIsOn)
									{
										flickComp.DoFlick();
									}
								}
							}
						}
					}
					foreach (Building item in ctx.toUninstall)
					{
						IntVec3 oldPosition = item.Position;
						try
						{
							MinifiedThing uninstalled = item.Uninstall();
							// Force uninstalled to original posion in order to install it back in that position too
							uninstalled.Position = oldPosition;
							toInstallAfterMove.Add(uninstalled);
						}
						catch (Exception e)
						{
							if (item != null)
							{
								Log.Warning("Error uninstaling during ship move: " + item.def.defName + " at " + item.Position);
							}
							throw e;
						}
					}
		}

		private static void MoveShipScanSourceArea(MoveShipContext ctx)
		{
					foreach (IntVec3 pos in ctx.sourceArea)
					{
						IntVec3 adjustedPos = TransformForShipMove(ctx.targetMap, pos, ctx.adjustment, ctx.rotb);
						//ctx.shipCache cache: move ShipCells
						targetMapComp.MapShipCells.Add(adjustedPos, new Tuple<int, int>(sourceMapComp.MapShipCells[pos].Item1, sourceMapComp.MapShipCells[pos].Item2));
						sourceMapComp.MapShipCells.Remove(pos);
						//store room temps
						Room room = pos.GetRoom(ctx.sourceMap);
						if (room != null && !roomsToTemp.Contains(room) && !ExposedToOutside(room))
						{
							roomsToTemp.Add(room);
							float temp = room.Temperature;
							posTemp.Add(new Tuple<IntVec3, float>(adjustedPos, temp));
							if (!ExposedToOutside(room))
							{
								sealedLocations.Add(adjustedPos);
							}
						}
						//add to target area and ctx.shipCache
						targetArea.Add(adjustedPos);
						//zonesToDestroy.Add(targetMap.zoneManager.ZoneAt(adjustedPos));
						//clear LZ
						foreach (Thing t in adjustedPos.GetThingList(ctx.targetMap))
						{
							if (!toDestroy.Contains(t) && !(t is Building_SteamGeyser))
								toDestroy.Add(t);
						}
						if (!ctx.targetMapIsSpace)
							targetMap.snowGrid.SetDepth(adjustedPos, 0f);
						//add all things from area
						foreach (Thing t in pos.GetThingList(ctx.sourceMap))
						{
							if (t is Building b)
							{
								if (b is Building_SteamGeyser)
									continue;
								if (b.def.building.supportsWallAttachments) //add external wall lights
								{
									for (int i = 0; i < 4; i++)
									{
										IntVec3 c = t.Position + GenAdj.CardinalDirections[i];
										if (ctx.shipCache.Area.Contains(c))
											continue;
										foreach (Thing l in c.GetThingList(ctx.sourceMap))
										{
											ThingDef l2 = GenConstruct.BuiltDefOf(l.def) as ThingDef;
											if ((l2?.building) != null && l2.building.isAttachment && GenMath.PositiveMod(l.Rotation.AsInt - 2, 4) == i)
											{
												toMoveBuildings.Add(l);
											}
										}
									}
								}
								else if (b is Building_ShipAirlock a && a.docked)
								{
									a.DeSpawnDock();
								}
								else
								{
									b.TryGetComp<CompEngineTrail>()?.Off();
									var transportComp = b.TryGetComp<CompTransporter>();
									if (transportComp != null)
									{
										toMoveThings.AddRange(transportComp.innerContainer.ToList());
										transportComp.CancelLoad();
									}
									else if (b is Building_NutrientPasteDispenser n)
									{
										n.cachedAdjCellsCardinal = null;
									}
								}
			
								var cacheComp = t.TryGetComp<CompShipCachePart>();
								if (cacheComp != null && cacheComp.Props.AnyPart)
									toMoveShipParts.Add(t);
								else
									toMoveBuildings.Add(t);
							}
							else
							{
								if (t is Pawn p)
								{
									pawns.Add(p);
									/*if (p.Faction == Faction.OfPlayer && p.holdingOwner is Building) //ctx.pawns in containers, abort
									{
										Log.Message("Pawn holding thing: " + p.holdingOwner);
										Messages.Message(TranslatorFormattedStringExtensions.Translate("SoS.MoveFailPawns", p.holdingOwner.Owner.ToString()), null, MessageTypeDefOf.NegativeEvent);
										return;
									}*/
								}
								else if (t is Plant plant)
									plants.Add(plant);
								// Explosion is a thing too, but it's better not move. Otherwise, will cause erors when moving to another map
								if (!(t is Explosion))
									toMoveThings.Add(t);
							}
						}
						foreach (Pawn p in ctx.pawns) //drop carried things, add to move list
						{
							if (p.IsCarrying() && p.carryTracker.TryDropCarriedThing(p.Position, ThingPlaceMode.Direct, out Thing carriedt))
							{
								toMoveThings.Add(carriedt);
							}
							//p.CurJob.Clear();
						}
						foreach(Plant plant in ctx.plants)
			                {
							try
							{
								if(plant.Spawned)
									plant.DeSpawn();
							}
							catch (Exception e)
							{
								Log.Error("[SoS2] Error despawning plant: " + e);
							}
						}
			
						if (sourceMap.zoneManager.ZoneAt(pos) != null && !zonesToCopy.Contains(sourceMap.zoneManager.ZoneAt(pos)))
						{
							zonesToCopy.Add(sourceMap.zoneManager.ZoneAt(pos));
						}
						//store terrain
						var sourceTerrain = sourceMap.terrainGrid.TerrainAt(pos);
						ColorDef sourceColor = sourceMap.terrainGrid.ColorAt(pos);
						if (sourceTerrain.layerable)
						{
							terrainToCopy.Add(new Tuple<IntVec3, TerrainDef, ColorDef>(adjustedPos, sourceTerrain, sourceColor));
			
							sourceMap.terrainGrid.RemoveTopLayer(pos, false);
						}
						else if (ctx.includeRock && IsRock(sourceTerrain))
						{
							terrainToCopy.Add(new Tuple<IntVec3, TerrainDef, ColorDef>(adjustedPos, sourceTerrain, sourceColor));
							sourceMap.terrainGrid.SetTerrain(pos, ResourceBank.TerrainDefOf.EmptySpace);
						}
						if (pos.Fogged(ctx.sourceMap))
						{
							fogToCopy.Add(adjustedPos);
						}
						RoofDef sourceRoof = sourceMap.roofGrid.RoofAt(pos);
						if (IsRoofDefAirtight(sourceRoof))
						{
							roofToCopy.Add(new Tuple<IntVec3, RoofDef>(adjustedPos, sourceRoof));
						}
						sourceMap.roofGrid.SetRoof(pos, null);
						if (ctx.core is Building_ShipBridge && core.Faction == Faction.OfPlayer) //home zone ships
						{
							sourceMap.areaManager.Home[pos] = false;
							targetMap.areaManager.Home[adjustedPos] = true;
						}
					}
					/*foreach (Zone z in zonesToDestroy)
					{
						z.Delete();
					}*/
		}

		private static void MoveShipSetupTakeoffFires(MoveShipContext ctx)
		{
					ctx.engines = ship.Engines.Where(e => e.flickComp.SwitchIsOn && !e.Props.energy && !e.Props.reactionless && e.refuelComp.Fuel > 0 && (!ctx.targetMapIsSpace || e.Props.takeOff));
					foreach (CompEngineTrail engine in ctx.engines)
					{
						if (ctx.targetMapIsSpace && !ctx.sourceMapIsSpace)
						{
							if (engine.parent.Rotation.AsByte == 0)
								fireExplosions.Add(engine.parent.Position + new IntVec3(0, 0, -3));
							else if (engine.parent.Rotation.AsByte == 1)
								fireExplosions.Add(engine.parent.Position + new IntVec3(-3, 0, 0));
							else if (engine.parent.Rotation.AsByte == 2)
								fireExplosions.Add(engine.parent.Position + new IntVec3(0, 0, 3));
							else
								fireExplosions.Add(engine.parent.Position + new IntVec3(3, 0, 0));
						}
					}
					if (!ctx.targetMapIsSpace)
		}

		private static void MoveShipPrepareTargetArea(MoveShipContext ctx)
		{
					{
						foreach (IntVec3 pos in ctx.targetArea) //check since placeworker ignores this
						{
							if (pos.Fogged(ctx.targetMap))
							{
								Log.Message("Tried to land ship in fogged area - oops");
								targetMap.fogGrid.FloodUnfogAdjacent(pos);
							}
						}
						foreach (IntVec3 pos in ctx.targetArea)
						{
							targetMap.terrainGrid.RemoveTopLayer(pos, false);
						}
						if (ctx.clearArea) //clear ground map of all floors (would be beter to store it and load it on takeoff)
						{
							foreach (IntVec3 pos in ctx.targetArea)
							{
								foreach (Thing t in pos.GetThingList(ctx.targetMap))
								{
									if (!toDestroy.Contains(t))
										toDestroy.Add(t);
								}
							}
						}
					}
					if (targetMap.IsSpace() && ctx.adjustment != IntVec3.Zero) //find adjacent ships
		}

		private static bool MoveShipCheckAdjacentShips(MoveShipContext ctx)
		{
					{
						foreach (IntVec3 pos in ctx.targetArea)
						{
							foreach (IntVec3 vec in GenAdj.CellsAdjacentCardinal(pos, Rot4.North, new IntVec2(1, 1)).Where(v => !targetArea.Contains(v) && targetMapComp.MapShipCells.ContainsKey(v)))
							{
								var adjShip = targetMapComp.ShipsOnMap[targetMapComp.ShipIndexOnVec(vec)];
								//if non ctx.fac ctx.shipCache near, abort
								if (adjShip.Faction != ship.Faction)
								{
									Messages.Message(TranslatorFormattedStringExtensions.Translate("SoS.MoveFailFaction"), null, MessageTypeDefOf.NegativeEvent);
									return;
								}
								shipIndexes.Add(targetMapComp.MapShipCells[vec].Item1);
							}
						}
					}
					if (ctx.devMode)
						watch.Record("processSourceArea");
			
			return true;
		}

		private static void MoveShipClearTargetObstacles(MoveShipContext ctx)
		{
					//move live ctx.pawns out of target area, destroy non buildings
					foreach (Thing thing in ctx.toDestroy)
					{
						if (thing is Pawn pawn && (!pawn.Dead || !pawn.Downed))
						{
							thing.Position = CellFinder.RandomClosewalkCellNear(thing.Position, ctx.targetMap, 50, (IntVec3 x) => !targetArea.Contains(x));
							pawn.Notify_Teleported();
						}
						else if (!thing.Destroyed)
						{
							try
							{
								thing.Destroy();
							}
							catch (Exception e)
							{
								if (thing != null)
								{
									Log.Warning("Error destroying during ship move: " + thing.def.defName + " at " + thing.Position);
								}
								throw e;
							}
						}
					}
					if (ctx.devMode)
						watch.Record("destroySource");
			
		}

		private static bool MoveShipDespawnForMove(MoveShipContext ctx)
		{
					//move things - new
					//despawn, error check if playermove
					ctx.fail = false;
					ctx.reason.Clear();
					foreach (Thing spawnThing in toMoveThings.Where(t => !t.Destroyed))
					{
						try
						{
							if (spawnThing.Spawned)
							{
								spawnThing.DeSpawn();
							}
						}
						catch (Exception e)
						{
							reason.AppendLine(e.Message);
							var sb = new StringBuilder();
							sb.AppendFormat("Error spawning {0}: {1}\n", spawnThing.def.label, e.Message);
							if (ctx.devMode)
								sb.AppendLine(e.StackTrace);
							Log.Warning(sb.ToString());
							if (ctx.playerMove)
							{
						ctx.fail = true;
								break;
							}
						}
					}
					if (!ctx.fail)
					{
						foreach (Thing spawnThing in toMoveBuildings.Where(t => !t.Destroyed))
						{
							try
							{
								if (spawnThing.Spawned)
								{
									// If it is a dining table in Gastronomy, it was reported that when actullly set to allow dining, broke ctx.shipCache launch
									// With the use of that option on modded table.
									ThingComp diningComp = (spawnThing as ThingWithComps)?.AllComps?.FirstOrDefault((ThingComp t) => t.GetType().Name == "CompCanDineAt");
									if (diningComp != null)
									{
										MethodInfo tryRemoveDiningSpots = diningComp.GetType().GetMethod("TryRemoveDiningSpots", BindingFlags.NonPublic | BindingFlags.Instance);
										if (tryRemoveDiningSpots != null)
										{
											tryRemoveDiningSpots.Invoke(diningComp, new object[] { });
										}
									}
									spawnThing.DeSpawn();
								}
							}
							catch (Exception e)
							{
								reason.AppendLine(e.Message);
								var sb = new StringBuilder();
								sb.AppendFormat("Error spawning {0}: {1}\n", spawnThing.def.label, e.Message);
								if (ctx.devMode)
									sb.AppendLine(e.StackTrace);
								Log.Warning(sb.ToString());
								if (ctx.playerMove)
								{
							ctx.fail = true;
									break;
								}
							}
						}
					}
					if (!ctx.fail)
					{
						foreach (Thing spawnThing in toMoveShipParts.Where(t => !t.Destroyed))
						{
							try
							{
								if (spawnThing.Spawned)
									spawnThing.DeSpawn();
							}
							catch (Exception e)
							{
								reason.AppendLine(e.Message);
								var sb = new StringBuilder();
								sb.AppendFormat("Error spawning {0}: {1}\n", spawnThing.def.label, e.Message);
								if (ctx.devMode)
									sb.AppendLine(e.StackTrace);
								Log.Warning(sb.ToString());
								if (ctx.playerMove)
								{
							ctx.fail = true;
									break;
								}
							}
						}
					}
					if (ctx.fail)
					{
						foreach (Thing spawnThing in toMoveShipParts.Where(t => !t.Destroyed && !t.Spawned))
						{
							spawnThing.SpawnSetup(ctx.sourceMap, true);
							spawnThing.beingTransportedOnGravship = false;
						}
						foreach (Thing spawnThing in toMoveBuildings.Where(t => !t.Destroyed && !t.Spawned))
						{
							spawnThing.SpawnSetup(ctx.sourceMap, true);
							spawnThing.beingTransportedOnGravship = false;
						}
						foreach (Thing spawnThing in toMoveThings.Where(t => !t.Destroyed && !t.Spawned))
						{
							spawnThing.SpawnSetup(ctx.sourceMap, true);
							spawnThing.beingTransportedOnGravship = false;
						}
						Find.LetterStack.ReceiveLetter("SoS.MoveFail".Translate(), "SoS.MoveFailDesc".Translate(ctx.reason), LetterDefOf.NegativeEvent);
						MoveShipFlag = false;
						return;
					}
			return !ctx.fail;
		}

		private static void MoveShipRespawnOnTarget(MoveShipContext ctx)
		{
					foreach (Thing spawnThing in ctx.toMoveShipParts)
					{
						ReSpawnThingOnMap(spawnThing, ctx.targetMap, ctx.adjustment, ctx.rotb, ctx.fac);
						spawnThing.beingTransportedOnGravship = false;
					}
					foreach (Thing spawnThing in ctx.toMoveBuildings)
					{
						try
						{
							ReSpawnThingOnMap(spawnThing, ctx.targetMap, ctx.adjustment, ctx.rotb, ctx.fac);
							spawnThing.beingTransportedOnGravship = false;
						}
						catch (Exception e)
						{
							if (spawnThing != null)
							{
								Log.Warning("Error respawning during ship move: " + spawnThing.def.defName + " at " + spawnThing.Position);
							}
							throw e;
						}
					}
					foreach (Thing spawnThing in ctx.toMoveThings)
					{
						if (!(spawnThing is Plant))
						{
							ReSpawnThingOnMap(spawnThing, ctx.targetMap, ctx.adjustment, ctx.rotb, ctx.fac);
							spawnThing.beingTransportedOnGravship = false;
						}
					}
					foreach (MinifiedThing minified in ctx.toInstallAfterMove)
					{
						Thing toInstall = minified.InnerThing;
						GenSpawn.Spawn(toInstall, minified.Position, ctx.targetMap, toInstall.Rotation);
						minified.InnerThing = null;
						minified.Destroy();
						toInstall.beingTransportedOnGravship = false;
					}
					if (ctx.devMode)
						watch.Record("moveThings");
			
					//adjust cache
		}

		private static void MoveShipUpdateShipCache(MoveShipContext ctx)
		{
					//adjust cache
					if (ctx.targetMap != ctx.sourceMap) //ctx.shipCache cache: if moving to different map, move cache
					{
						targetMapComp.ShipsOnMap.Add(ctx.shipIndex, sourceMapComp.ShipsOnMap[ctx.shipIndex]);
			ctx.shipCache = targetMapComp.ShipsOnMap[ctx.shipIndex];
						ship.Map = ctx.targetMap;
						if (ctx.adjustment != IntVec3.Zero && ship.BuildingsDestroyed.Any()) //cache: adjust destroyed
						{
							HashSet<Tuple<ThingDef, IntVec3, Rot4>> buildingsDestroyed = new HashSet<Tuple<ThingDef, IntVec3, Rot4>>(ship.BuildingsDestroyed);
							ship.BuildingsDestroyed.Clear();
							foreach (var sh in buildingsDestroyed)
							{
								IntVec3 transformedBuildingPos = TransformForShipMove(ctx.targetMap, sh.Item2, ctx.adjustment, ctx.rotb);
								ship.BuildingsDestroyed.Add(new Tuple<ThingDef, IntVec3, Rot4>(sh.Item1, transformedBuildingPos, sh.Item3));
							}
							buildingsDestroyed.Clear();
						}
						sourceMapComp.RemoveShipFromCache(ctx.shipIndex);
					}
					if (ctx.adjustment != IntVec3.Zero) //ctx.shipCache cache: adjust area
					{
						ctx.shipCache.Area.Clear();
						foreach (IntVec3 pos in ctx.sourceArea)
						{
							ctx.shipCache.Area.Add(TransformForShipMove(ctx.targetMap, pos, ctx.adjustment, ctx.rotb));
						}
					}
					MoveShipFlag = false;
			
		}

		private static void MoveShipConsumeMoveFuel(MoveShipContext ctx)
		{
					//draw fuel, exhaust area actions
					if (ctx.core is Building_ShipBridge && ctx.playerMove)
					{
						float fuelNeeded = ship.MassActual;
						float fuelStored = 0f;
						foreach (CompEngineTrail engine in ctx.engines)
						{
							fuelStored += engine.refuelComp.Fuel;
							if (engine.PodFueled)
							{
								fuelStored += engine.refuelComp.Fuel;
								/*if (ModsConfig.BiotechActive && !ctx.sourceMapIsSpace)
								{
									foreach (IntVec3 v in engine.ExhaustArea)
									{
										if (Rand.Chance(0.8f))
											v.Pollute(ctx.sourceMap, true);
									}
								}*/
							}
						}
						if (ctx.sourceMapIsSpace)
						{
							if (ctx.targetMapIsSpace) //space map
							{
								if (ctx.targetMap == ctx.sourceMap)
									fuelNeeded *= pctFuelLocal;
								else
									fuelNeeded *= pctFuelMap;
							}
							else //to ground
							{
								fuelNeeded *= pctFuelLand;
								if (fuelNeeded > fuelStored)
							ctx.weBeCrashing = fuelStored / fuelNeeded;
								else if (!ship.CanMove())
							ctx.weBeCrashing = 1f;
							}
						}
						else //to space
						{
							fuelNeeded = ship.MassTakeoff * (pctFuelTakeoff - pctFuelTakeoffPerOptimizer * ship.EffectiveFuelOptimizerCount);
						}
						foreach(CompEngineTrail engine in ctx.engines)
							engine.refuelComp.ConsumeFuel(fuelNeeded * engine.refuelComp.Fuel / fuelStored);
						if (ctx.devMode)
							watch.Record("takeoffEffects");
					}
					if (shipIndexes.Count > 1) //ctx.shipCache cache: adjacent ships found, merge in order: largest ctx.shipCache, ctx.shipCache, wreck
		}

		private static void MoveShipMergeAdjacentShips(MoveShipContext ctx)
		{
					if (shipIndexes.Count > 1) //ctx.shipCache cache: adjacent ships found, merge in order: largest ctx.shipCache, ctx.shipCache, wreck
					{
						Log.Message("SOS2: ".Colorize(Color.cyan) + " ship move found adjacent ships in area, merging!");
						targetMapComp.CheckAndMerge(ctx.shipIndexes);
					}
					//move zones
		}

		private static void MoveShipTransferZones(MoveShipContext ctx)
		{
					//move zones
					if (zonesToCopy.Any())
					{
						foreach (Zone zone in ctx.zonesToCopy) //only move fully contained zones
						{
							bool allOn = true;
							foreach (IntVec3 v in zone.Cells)
							{
								if (!sourceArea.Contains(v))
								{
									allOn = false;
									break;
								}
							}
							if (allOn)
							{
								sourceMap.zoneManager.DeregisterZone(zone);
								zone.zoneManager = targetMap.zoneManager;
								List<IntVec3> newCells = new List<IntVec3>();
								foreach (IntVec3 cell in zone.cells)
								{
									newCells.Add(TransformForShipMove(ctx.targetMap, cell, ctx.adjustment, ctx.rotb));
								}
								zone.cells = newCells;
								targetMap.zoneManager.RegisterZone(zone);
							}
						}
						targetMap.zoneManager.RebuildZoneGrid();
						sourceMap.zoneManager.RebuildZoneGrid();
					}
					if (ctx.devMode)
						watch.Record("moveZones");
			
		}

		private static void MoveShipTransferTerrainAndPlants(MoveShipContext ctx)
		{
					//move terrain
					try
					{
						foreach (Tuple<IntVec3, TerrainDef, ColorDef> tup in ctx.terrainToCopy)
						{
							var targetTile = targetMap.terrainGrid.TerrainAt(tup.Item1);
							if (!targetTile.layerable || IsHull(targetTile))
							{
								targetMap.terrainGrid.SetTerrain(tup.Item1, tup.Item2);
								targetMap.terrainGrid.SetTerrainColor(tup.Item1, tup.Item3);
							}
						}
						if (ctx.includeRock)
						{
							foreach (IntVec3 pos in ctx.sourceArea)
							{
								sourceMap.terrainGrid.SetTerrain(pos, ResourceBank.TerrainDefOf.EmptySpace);
							}
						}
					}
					catch (Exception e)
					{
						Log.Warning("" + e);
					}
					foreach(Plant plant in ctx.plants)
			            {
						ReSpawnThingOnMap(plant, ctx.targetMap, ctx.adjustment, ctx.rotb, ctx.fac);
					}
					if (ctx.devMode)
						watch.Record("moveTerrain");
					//move fog
		}

		private static void MoveShipTransferFogAndRoofs(MoveShipContext ctx)
		{
					//move fog
					foreach (IntVec3 pos in ctx.fogToCopy)
					{
						targetMap.fogGrid.fogGrid.Set(targetMap.cellIndices.CellToIndex(pos), value:true);
						targetMap.mapDrawer.MapMeshDirty(pos, (ulong)MapMeshFlagDefOf.FogOfWar);// | (ulong)MapMeshFlagDefOf.Things);
					}
			
					//move roofs
					foreach (Tuple<IntVec3, RoofDef> tup in ctx.roofToCopy)
					{
						targetMap.roofGrid.SetRoof(tup.Item1, tup.Item2);
					}
					if (ctx.devMode)
						watch.Record("moveRoof");
					
		}

		private static void MoveShipRestoreRoomClimate(MoveShipContext ctx)
		{
					
					//restore temp in ctx.shipCache
					foreach (Tuple<IntVec3, float> t in ctx.posTemp)
					{
						Room room = t.Item1.GetRoom(ctx.targetMap);
						room.Temperature = t.Item2;
					}
			
					if (targetMap.Biome.inVacuum)
			            {
						foreach(IntVec3 vec in ctx.sealedLocations)
						{
							Room room = vec.GetRoom(ctx.targetMap);
							room.Vacuum = 0f;
			                }
			            }
			
					//landing - remove space map if no ctx.pawns or cores
		}

		private static void MoveShipFinalizeMoveEffects(MoveShipContext ctx)
		{
					if (!ctx.targetMapIsSpace && !sourceMap.spawnedThings.Any((Thing t) => (t is Pawn || (t is Building_ShipBridge b && b.mannableComp == null)) && !t.Destroyed))
					{
						WorldObject oldParent = sourceMap.Parent;
						Current.Game.DeinitAndRemoveMap(ctx.sourceMap, false);
						Find.World.worldObjects.Remove(oldParent);
					}
			
					//takeoff - explosions
					if (!ctx.sourceMapIsSpace)
					{
						foreach (IntVec3 pos in ctx.fireExplosions)
						{
							GenExplosion.DoExplosion(pos, ctx.sourceMap, 3.9f, DamageDefOf.Flame, null);
						}
					}
			
					//post spawn effects
					if (ctx.sourceMap != ctx.targetMap)
					{
						//ctx.pawns to lord
						foreach (Pawn p in ctx.pawns)
							AddPawnToLord(ctx.targetMap, p);
			
						//power
						ship.ForceRePower = 2;
						sourceMap.powerNetManager.UpdatePowerNetsAndConnections_First();
					}
					else
					{
						ship.ForceRePower = 1;
					}
					targetMap.powerNetManager.UpdatePowerNetsAndConnections_First();
			
					//crash damage
					if (!ctx.targetMapIsSpace)
						ship.WeBeCrashing = ctx.weBeCrashing;
			
					//heat
					targetMap.GetComponent<ShipMapComp>().heatGridDirty = true;
			
					// The source map might be destroyed before
					if (!sourceMap.Disposed)
					{
						//regen affected map layers
						List<Section> sourceSec = new List<Section>();
						foreach (IntVec3 pos in ctx.sourceArea)
						{
							Section sec = sourceMap.mapDrawer.SectionAt(pos);
							if (!sourceSec.Contains(sec))
								sourceSec.Add(sec);
						}
						foreach (Section sec in sourceSec)
						{
							// In transit map is not expected to have all layers set up, neither needs them
							if (sourceMapComp.ShipMapState != ShipMapState.inTransit)
							{
								sec.RegenerateAllLayers(); //RegenerateDirtyLayers - some layers are not set dirty properly (zones), slower
							}
						}
					}
					List<Section> targetSec = new List<Section>();
					foreach (IntVec3 pos in ctx.targetArea)
					{
						Section sec = targetMap.mapDrawer.SectionAt(pos);
						if (!targetSec.Contains(sec))
							targetSec.Add(sec);
					}
					foreach (Section sec in targetSec)
					{
						sec.RegenerateAllLayers(); //RegenerateDirtyLayers - some layers are not set dirty properly (zones), slower
					}
					if (ctx.devMode)
					{
						watch.Record("finalize");
						Log.Message("SOS2: ".Colorize(Color.cyan) + ctx.sourceMap + " Ship move complete in ".Colorize(Color.green) + watch.MakeReport());
					}
				}
		}

	}
}
