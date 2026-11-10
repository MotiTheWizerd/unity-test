using Forge.World.Assets;
using UnityEngine;

namespace Forge.Play.Sound
{
    /// <summary>
    /// Which cue plays each sound key ("footsteps/walk"), as Build Sounds lists them (Content/SoundCatalog). Keys
    /// are a cue folder's path under Content/Sounds, lowercase, so a dropped-in folder is a new sound.
    /// </summary>
    [CreateAssetMenu(menuName = "Forge/Sound Catalog", fileName = "SoundCatalog")]
    public sealed class SoundCatalog : KeyedAssetCatalog<SoundCue>
    {
    }
}
