using RimWorld;
using UnityEngine;
using Verse;

namespace SaveOurShip2
{
	// Vanilla-styled autodoor with an integrated vacuum barrier. While powered the barrier holds the
	// atmosphere - airtight even while the door is open, regardless of build material. Unpowered, it
	// behaves as an ordinary door of its material (a non-airtight stuff then leaks). The barrier
	// shimmer is drawn persistently UNDER the door, so the door art reveals it as it slides open.
	public class Building_GravshipAirlock : Building_Door
	{
		CompPowerTrader powerComp;
		static Graphic shimmer;

		CompPowerTrader Power => powerComp ?? (powerComp = this.TryGetComp<CompPowerTrader>());

		bool BarrierActive => Power != null && Power.PowerOn;

		// Odyssey's VacuumComponent reads this: false = airtight. Powered, the field seals regardless
		// of material; unpowered, fall back to normal door behaviour (which respects the stuff).
		public override bool ExchangeVacuum => BarrierActive ? false : base.ExchangeVacuum;

		// drawSize is large because the VacBarrier texture carries wide transparent margins - the
		// visible shimmer is only a fraction of the quad.
		static Graphic Shimmer => shimmer ?? (shimmer = GraphicDatabase.Get<Graphic_Multi>(
			"Things/Building/VacBarrier/VacBarrier_Barrier", ShaderDatabase.TransparentPostLight,
			new Vector2(2.5f, 2.5f), new Color(0.55f, 0.8f, 1f, 0.7f)));

		protected override void DrawAt(Vector3 drawLoc, bool flip = false)
		{
			if (BarrierActive) //persistent while powered; the door movers, drawn above, hide it when shut
			{
				Vector3 p = drawLoc;
				p.y = AltitudeLayer.Shadows.AltitudeFor(); //just below DoorMoveable so the door occludes it
				Shimmer.Draw(p, flip ? Rotation.Opposite : Rotation, this);
			}
			base.DrawAt(drawLoc, flip);
		}
	}
}
