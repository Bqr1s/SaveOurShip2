using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Vehicles;
using Vehicles.World;
using SmashTools.Targeting;
using Verse;

namespace SaveOurShip2.Vehicles
{
    class CompHoverMode : VehicleComp
    {
        public CompProperties_HoverMode Props => props as CompProperties_HoverMode;

        public override void CompTick()
        {
            base.CompTick();
            if (Vehicle.Spawned && Vehicle.ignition.Drafted)
                LaunchProtocol.ThrowFleck(ResourceBank.FleckDefOf.SoS2Exhaust_Short, parent.DrawPos, parent.Map, 0.8f, 1, Rand.Range(0,360), 15, 0);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (Vehicle.Map.GetComponent<ShipMapComp>().ShipMapState != ShipMapState.inCombat)
            {
				const float fuelCost = 5f;
				const float fuelCostWithReserve = fuelCost + 0.1f;
				yield return new Command_ActionHighlighter
                {
                    defaultLabel = Translator.Translate("SoS.HoverJump"),
                    defaultDesc = Translator.Translate("SoS.HoverJump.Desc"),
                    icon = ContentFinder<Texture2D>.Get("UI/ShuttleLocalFlight"),
                    action = delegate ()
                    {
                        // Prevent issuing duplicate commands. If vehicle was launched already, map will be null.
                        if (LandingTargeter.Instance.IsTargeting || Vehicle.Map == null)
                        {
                            return;
                        }
                        LandingTargeter.Instance.BeginTargeting(Vehicle, Vehicle.Map, delegate (LocalTargetInfo target, Rot4 rot)
                        {
                            // CHANGE 1.6 LaunchTargeter.FlightPath = new List<FlightNode> { new FlightNode(Vehicle.Map.Tile) };
                            TargetData<GlobalTargetInfo> targetData = new TargetData<GlobalTargetInfo>();
                            targetData.targets.Add(Vehicle.Map.Parent != null ? new GlobalTargetInfo(Vehicle.Map.Parent) : new GlobalTargetInfo(Vehicle.Map.Tile));
							Vehicle.CompFueledTravel.ConsumeFuel(fuelCost);
							Vehicle.CompVehicleLauncher.Launch(targetData, new ArrivalAction_LandToCell(Vehicle, Vehicle.Map.Parent, target.Cell, rot));
                        }, (LocalTargetInfo targetInfo) => !Ext_Vehicles.IsRoofRestricted(Vehicle.VehicleDef, targetInfo.Cell, Vehicle.Map),
                        forcedTargeting: true);
                    },
                    disabled = Vehicle.CompFueledTravel.Fuel < fuelCostWithReserve || Ext_Vehicles.IsRoofRestricted(Vehicle.VehicleDef, Vehicle.Position, Vehicle.Map),
                    disabledReason = Vehicle.CompFueledTravel.Fuel < fuelCostWithReserve ? "SoS.NotEnoughFuelToLaunchShuttle".Translate() : "SoS.CannotLaunchUnderRoof".Translate()
                };
            }
		}
    }
}
