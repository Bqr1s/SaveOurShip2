using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimWorld.Planet;

namespace SaveOurShip2
{
	public class AlertRamming : Alert
	{
        public AlertRamming()
			: base()
		{
			defaultLabel = "SoS.Alert.EnemyRamming".Translate();
			defaultExplanation = "SoS.Alert.EnemyRammingDesc".Translate();
		}

		public override AlertReport GetReport()
		{
			Map playerMap = ShipInteriorMod2.FindPlayerShipMap();
			Map enemyMap = ShipInteriorMod2.FindEnemyShipMap();
			Map currentMap = Find.CurrentMap;
			if (playerMap == null || enemyMap == null|| currentMap == null ||
				(currentMap != playerMap && currentMap != enemyMap))
			{
				return AlertReport.Inactive;
			}

			ShipMapComp enemyMapComp = enemyMap.GetComponent<ShipMapComp>();
			if (enemyMapComp.ShipMapState != ShipMapState.inCombat)
			{
				return AlertReport.Inactive;
			}

			if (enemyMapComp.ShipMapAI == ShipAI.ramming)
			{
				SpaceShipCache ship = ShipCollisionManager.GetCollidingShipOnMap(enemyMapComp); 
				List<Thing> culprits = ship?.GetCulpritsForAlert() ?? new List<Thing>();
				return AlertReport.CulpritsAre(culprits);
			}
			else
			{
				return AlertReport.Inactive;
			}		
		}
	}
}

