namespace Forge.World
{
    /// <summary>The single bridge between engine-free System.Numerics math and Unity math.</summary>
    public static class NumericsConversions
    {
        public static UnityEngine.Vector3 ToUnity(this System.Numerics.Vector3 v) =>
            new UnityEngine.Vector3(v.X, v.Y, v.Z);

        public static UnityEngine.Quaternion ToUnity(this System.Numerics.Quaternion q) =>
            new UnityEngine.Quaternion(q.X, q.Y, q.Z, q.W);

        public static System.Numerics.Vector3 ToNumerics(this UnityEngine.Vector3 v) =>
            new System.Numerics.Vector3(v.x, v.y, v.z);

        public static System.Numerics.Quaternion ToNumerics(this UnityEngine.Quaternion q) =>
            new System.Numerics.Quaternion(q.x, q.y, q.z, q.w);
    }
}
