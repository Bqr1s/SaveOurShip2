using RimWorld;
using UnityEngine;
using Verse;

namespace SaveOurShip2
{
	public class GlobalUnlockDef : Def
	{
		// This is a "mechanical" def utilizing engene's capability to read objects and their properties into def database to give
		// C# code handy acess to those propertires. In-game objects of that def are not supposed to exist.
		// Global unlock of all ships as starting options is not considered a part of core SOS 2 gameplay, but people keep asking things like that
		// so that unlock is intended to be shipped as a submod by Cruel Moose
		public bool AllShipsUnlockedFlag = false;

		public static bool AllShipsUnlocked()
		{
			GlobalUnlockDef unlock = DefDatabase<GlobalUnlockDef>.GetNamed("GlobalUnlocks");
			return unlock?.AllShipsUnlockedFlag ?? false;
		}
	}
}

