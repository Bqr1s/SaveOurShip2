using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace SaveOurShip2
{
	public class DodgeChanceSubmodScaleDef : Def
	{
		// See DodgeChanceSubmodScaleDef XML comments for details
		public List<float> multiplierList = new List<float>();

		static bool loggedAdjustment = false;

		public static float GetEffectiveMultiplier()
		{
			DodgeChanceSubmodScaleDef multipliers = DefDatabase<DodgeChanceSubmodScaleDef>.GetNamed("DodgeChanceSubmodScale");
			float result = 1f;
			if (multipliers != null && !multipliers.multiplierList.NullOrEmpty())
			{
				result = multipliers.multiplierList.Min();
			}
			if(ModLister.GetActiveModWithIdentifier(ModIntegration.SpinalEnginesModID, true) != null)
            {
				float spinalEnginesMultiplier = 0.4f;
				result = Mathf.Min(result, spinalEnginesMultiplier);
				if (!loggedAdjustment)
				{
					loggedAdjustment = true;
					Log.Message("Adjusted TWR Dodge multipler for Spinal Engines on SOS 2 side");
				}
			}
			return result;
		}
	}
}

