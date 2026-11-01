using System.Collections.Generic;
using UnityEngine;

namespace Forge.World.Assets
{
    /// <summary>
    /// Built-in catalog mapping "primitive/*" keys to Unity primitives, so the
    /// prototype works without any art. Unknown keys become a magenta cube so
    /// broken references are obvious in the world.
    /// </summary>
    public sealed class PrimitiveAssetCatalog : IAssetCatalog
    {
        public const string Prefix = "primitive/";

        static readonly Dictionary<string, PrimitiveType> Primitives = new Dictionary<string, PrimitiveType>
        {
            { Prefix + "cube", PrimitiveType.Cube },
            { Prefix + "sphere", PrimitiveType.Sphere },
            { Prefix + "cylinder", PrimitiveType.Cylinder },
            { Prefix + "capsule", PrimitiveType.Capsule },
        };

        Material _missingMaterial;

        public GameObject Instantiate(string assetKey, Transform parent)
        {
            GameObject instance;
            if (assetKey != null && Primitives.TryGetValue(assetKey, out var type))
            {
                instance = GameObject.CreatePrimitive(type);
            }
            else
            {
                Debug.LogWarning($"[Forge] Unknown asset key '{assetKey}'; showing placeholder.");
                instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
                instance.GetComponent<Renderer>().sharedMaterial = MissingMaterial();
            }

            instance.transform.SetParent(parent, false);
            return instance;
        }

        Material MissingMaterial()
        {
            if (_missingMaterial == null)
            {
                var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _missingMaterial = new Material(source.GetComponent<Renderer>().sharedMaterial) { color = Color.magenta };
                Object.Destroy(source);
            }

            return _missingMaterial;
        }
    }
}
