using System;
using System.Numerics;

namespace Forge.Editing.Placement
{
    /// <summary>
    /// Fits an object to the ground under its footprint: the ground is sampled at the two ends of the
    /// footprint along the object's length (local X) and its depth (local Z), and the plane through those
    /// slopes gives the up direction to tilt to, so a fence's two ends both touch the hillside. The tilt is
    /// capped, so cliffs don't tip objects over. Engine-free: heights come from a caller-supplied sampler.
    /// </summary>
    public static class GroundFit
    {
        /// <summary>Steepest tilt applied; steeper ground tilts objects only this far.</summary>
        public const float DefaultMaxTiltDegrees = 35f;

        /// <summary>Samples are at least this far from the center, so thin footprints still read a stable slope.</summary>
        public const float MinSampleDistance = 0.25f;

        /// <param name="center">Ground position (x, z) of the object's pivot.</param>
        /// <param name="heightAt">Ground height at a ground position, or null where there is no ground.</param>
        /// <returns>False if the ground is missing under any sample (the caller keeps the object upright).</returns>
        public static bool TryFit(Vector2 center, Footprint footprint, float yawDegrees, float scale,
            Func<Vector2, float?> heightAt, float maxTiltDegrees, out GroundPlane plane)
        {
            if (heightAt == null)
                throw new ArgumentNullException(nameof(heightAt));

            plane = default;
            var axisX = PlacementMath.LocalXAxis(yawDegrees);
            var axisZ = new Vector2(-axisX.Y, axisX.X);
            var reachX = Math.Max(MinSampleDistance, footprint.SizeX * scale * 0.5f);
            var reachZ = Math.Max(MinSampleDistance, footprint.SizeZ * scale * 0.5f);

            var front = heightAt(center + axisX * reachX);
            var back = heightAt(center - axisX * reachX);
            var right = heightAt(center + axisZ * reachZ);
            var left = heightAt(center - axisZ * reachZ);
            if (!front.HasValue || !back.HasValue || !right.HasValue || !left.HasValue)
                return false;

            // The ground's run along each axis, with its rise; their cross product is the plane's up.
            var alongX = new Vector3(axisX.X * 2f * reachX, front.Value - back.Value, axisX.Y * 2f * reachX);
            var alongZ = new Vector3(axisZ.X * 2f * reachZ, right.Value - left.Value, axisZ.Y * 2f * reachZ);
            var up = ClampTilt(Vector3.Normalize(Vector3.Cross(alongZ, alongX)), maxTiltDegrees);

            // On curved ground the two axes disagree about the center height; the longer axis wins in proportion,
            // so a fence's ends (not its middle) meet a bump, while a round rock averages both.
            var height = (reachX * (front.Value + back.Value) + reachZ * (right.Value + left.Value)) / (2f * (reachX + reachZ));
            plane = new GroundPlane(up, height);
            return true;
        }

        /// <summary>
        /// Where an object's pivot goes and how it is turned so it rests on the ground under its footprint,
        /// tilted to the slope and heading <paramref name="yawDegrees"/>. Shared by placing, dropping and
        /// sculpting, so all three seat objects the same way.
        /// </summary>
        /// <returns>False if the ground is missing under any sample.</returns>
        public static bool TryPose(Vector2 center, PlacementShape shape, float yawDegrees, float scale,
            Func<Vector2, float?> heightAt, float maxTiltDegrees, out Vector3 pivot, out Quaternion rotation)
        {
            pivot = default;
            rotation = Quaternion.Identity;
            if (!TryFit(center, shape.Footprint, yawDegrees, scale, heightAt, maxTiltDegrees, out var plane))
                return false;

            rotation = Orient(plane.Up, yawDegrees);
            pivot = new Vector3(center.X, plane.Height, center.Y) + plane.Up * (shape.PivotAboveBottom * scale);
            return true;
        }

        /// <summary>Heading <paramref name="yawDegrees"/> around +Y, then tilted so the object's up becomes <paramref name="up"/>.</summary>
        public static Quaternion Orient(Vector3 up, float yawDegrees) =>
            FromTo(Vector3.UnitY, up) * Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawDegrees * (float)(Math.PI / 180.0));

        /// <summary>The shortest rotation taking unit vector <paramref name="from"/> to unit vector <paramref name="to"/>.</summary>
        public static Quaternion FromTo(Vector3 from, Vector3 to)
        {
            var axis = Vector3.Cross(from, to);
            var length = axis.Length();
            if (length < 1e-6f)
                return Quaternion.Identity; // parallel; objects never tilt past the cap, so never opposite
            var angle = (float)Math.Atan2(length, Vector3.Dot(from, to));
            return Quaternion.CreateFromAxisAngle(axis / length, angle);
        }

        static Vector3 ClampTilt(Vector3 up, float maxTiltDegrees)
        {
            var maxTilt = Math.Max(0f, maxTiltDegrees) * (float)(Math.PI / 180.0);
            var tilt = (float)Math.Acos(Math.Max(-1f, Math.Min(1f, up.Y)));
            if (tilt <= maxTilt)
                return up;

            var horizontal = new Vector2(up.X, up.Z);
            if (horizontal.LengthSquared() < 1e-12f)
                return Vector3.UnitY;
            horizontal = Vector2.Normalize(horizontal) * (float)Math.Sin(maxTilt);
            return new Vector3(horizontal.X, (float)Math.Cos(maxTilt), horizontal.Y);
        }
    }
}
