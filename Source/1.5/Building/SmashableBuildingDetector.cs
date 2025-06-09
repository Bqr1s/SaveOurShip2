using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace SaveOurShip2
{
	[StaticConstructorOnStartup]
	class SmashableBuildingsDetector
	{
		static readonly string defNames = "AncientPipelineSection,AncientShoppingCart,AncientPostbox,AncientVendingMachine,AncientAirConditioner,AncientKitchenSink," +
			"AncientStove,AncientOven,AncientATM,AncientWashingMachine,AncientHydrant,AncientMicrowave,AncientToilet,AncientRefrigerator,AncientSmallCrate," +
			"AncientLongCrate,AncientLargeCrate,AncientWheel,AncientGiantWheel,AncientRustedCar,AncientRustedTruck,AncientRustedJeep,AncientRustedCarFrame," +
			"AncientWarwalkerTorso,AncientRustedDropship,AncientWarwalkerClaw,AncientWarwalkerLeg,AncientMiniWarwalkerRemains,AncientWarspiderRemains," +
			"AncientPipelineSection,AncientWarwalkerFoot,AncientTank,AncientAPC,AncientWarwalkerShell,AncientJetEngine,AncientDropshipEngine,AncientPodCar," +
			"AncientTankTrap,AncientLargeRustedEngineBlock,AncientFence,AncientRazorWire,AncientPipes,AncientMegaCannonTripod,AncientMegaCannonBarrel,AncientContainer," +
			"AncientMechDropBeacon,AncientSecurityTurret,AncientRustedEngineBlock,AncientShipBeacon,AncientBed,AncientSystemRack,AncientEquipmentBlocks,AncientMachine," +
			"AncientStorageCylinder,AncientDisplayBank,AncientLockerBank,AncientCrate,AncientMilitaryCrate,AncientSpacerCrate,AncientBarrel,AncientGenerator,AncientOperatingTable";
		static readonly HashSet<string> smashableDefs;
		static SmashableBuildingsDetector()
		{
			smashableDefs = new HashSet<string>();
			IEnumerable<string> names = defNames.Split(',').ToList();
			foreach(string name in names)
			{
				smashableDefs.Add(name);
			}
		}
		public static bool IsSmashable(string defName)
		{
			return smashableDefs.Contains(defName);
		}

	}
}
