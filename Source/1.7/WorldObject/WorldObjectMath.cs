using RimWorld.Planet;
using RimWorld;
using System;
using UnityEngine;
using Verse;

namespace SaveOurShip2
{
	// Since Space Site and orbiting ship inherit from different base game objects, they got tho have their common math in utility class, not common parent
	static class WorldObjectMath
	{
		public const int defaultRadius = 150;
		public static Vector3 vecEquator = new Vector3(0, 0, 1);
		public static Vector3 GetPos(float phi, float theta, float radius)
		{
			phi = Mathf.Clamp(phi, -Mathf.PI / 2, Mathf.PI / 2);
			float y = radius * Mathf.Sin(phi);
			float projectionRadius = radius * Mathf.Cos(phi);
			// Projection to equatorial plane
			// Theta was in interpolate vector units instead of radians. 1 interpolate vector unit = 180 degree.
			// TODO: switching to theta calculated in radians. so need to /PI here and make angle distances larger. 
			Vector3 vPlanar = Vector3.SlerpUnclamped(vecEquator * projectionRadius, vecEquator * projectionRadius * -1, theta * -1 / Mathf.PI);
			return new Vector3(vPlanar.x, y, vPlanar.z);
		}

		/// <summary>
		/// Converts Cartesian coordinates to the game's custom spherical coordinates.
		/// This is the inverse of GetPos.
		/// </summary>
		/// <param name="pos">Cartesian position vector.</param>
		/// <param name="phi">The resulting phi (latitude-like angle, in radians).</param>
		/// <param name="theta">The resulting theta (longitude-like angle parameter).</param>
		/// <param name="radius">The resulting radius.</param>
		public static void GetSphericalFromCartesian(Vector3 pos, out float phi, out float theta, out float radius)
		{
			radius = pos.magnitude;
			if (radius < 1E-5f)
			{
				radius = 0f;
				phi = 0f;
				theta = 0f;
				return;
			}
			phi = Mathf.Asin(pos.y / radius);
			theta = Mathf.Atan2(pos.x, pos.z);
		}

		// Uniform coords getting for both space site and player ship
		public static void GetSphericalCoords(MapParent obj, out float phi, out float theta, out float radius)
		{
			if (obj is WorldObjectOrbitingShip ship)
			{
				phi = ship.Phi;
				theta = ship.Theta;
				radius = ship.Radius;
			}
			else if (obj is SpaceSite site)
			{
				phi = site.phi;
				theta = site.theta;
				radius = site.radius;
			}
			else
			{
				phi = 0;
				theta = 0;
				radius = defaultRadius;
				Log.ErrorOnce("SoOS2: Failed to get coordinates for object of type:" + obj.GetType().Name, 104857937);
			}
		}

		// space site and player ship need common function to resolve theta coordinate serialization for them
		public static void SerializeTheta(ref float theta, bool forceSave = false)
		{
			// Before this fix takes effect: theta cxoordinate is saved in "interpolation units" where 1 unit = 180 degree = pi radians.
			// Saving in radians in new version, but also in old units so that can rol back to previous build 
			// in existing save
			const string oldThetaName = "theta";
			const string newThetaName = "thetaRadians";
			if (Scribe.mode == LoadSaveMode.LoadingVars)
			{
				float readTheta = Mathf.NegativeInfinity;
				Scribe_Values.Look<float>(ref readTheta, newThetaName, Mathf.NegativeInfinity);
				if (readTheta != Mathf.NegativeInfinity)
				{
					theta = readTheta;
					return;
				}
				Scribe_Values.Look<float>(ref readTheta, oldThetaName, 0);
				theta = readTheta * Mathf.PI;
			}
			else if (Scribe.mode == LoadSaveMode.Saving)
			{
				Scribe_Values.Look<float>(ref theta, newThetaName, 0, forceSave);
				float oldTheta = theta / Mathf.PI;
				Scribe_Values.Look<float>(ref oldTheta, oldThetaName, 0, forceSave);
			}
		}
	}
}
