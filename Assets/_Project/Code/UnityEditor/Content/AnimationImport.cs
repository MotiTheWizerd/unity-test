using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// How animations import, decided once for the folder: every file under Content/Animations is one humanoid clip
    /// (Mixamo "without skin"), named after its file, so it plays on any humanoid character (<see cref="CharacterImport"/>).
    /// The clip never turns the body it plays on (its turn is baked into the pose); its travel stays root motion, which
    /// the player ignores (its body moves itself) but which tells how fast the clip walks. Its rise and fall stay in the
    /// pose (a stride's bob), except a jump's, which the body's own rise already shows. Whether a clip loops or lifts is
    /// its move's, by the folder it sits in (<see cref="HumanoidMoves.RoleOf"/>).
    /// </summary>
    sealed class AnimationImport : AssetPostprocessor
    {
        public const string AnimationsFolder = "Assets/_Project/Content/Animations";

        /// <summary>
        /// The skeleton every clip's motion is read against: a Mixamo-rigged character in its T-pose. A clip file
        /// without skin has no rest pose of its own, so left alone Unity takes its first frame for one (a run mid-stride)
        /// and plays it back with the feet turned out. Any Mixamo-rigged character's bones serve; the hero's do today.
        /// </summary>
        public const string ReferenceRig = CharacterImport.CharactersFolder + "/VikingHero/VikingHero.fbx";

        bool InFolder => assetPath.StartsWith(AnimationsFolder + "/", System.StringComparison.Ordinal);

        void OnPreprocessModel()
        {
            if (!InFolder)
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            context.DependsOnSourceAsset(ReferenceRig);
            var rig = AssetDatabase.LoadAllAssetsAtPath(ReferenceRig).OfType<Avatar>().FirstOrDefault(a => a.isHuman && a.isValid);
            if (rig != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = rig;
            }
            else
            {
                Debug.LogWarning($"[Forge] No humanoid rig at {ReferenceRig}; {assetPath} reads its motion against its own first frame.");
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = true;
        }

        void OnPreprocessAnimation()
        {
            if (!InFolder)
                return;

            var importer = (ModelImporter)assetImporter;
            var name = Path.GetFileNameWithoutExtension(assetPath);
            var role = HumanoidMoves.RoleOf(InSet(assetPath));
            var loops = role.HasValue && HumanoidMoves.Loops(role.Value, name);
            var lifts = role.HasValue && HumanoidMoves.Lifts(role.Value, name);
            importer.clipAnimations = importer.defaultClipAnimations.Take(1).Select(clip =>
            {
                clip.name = name;
                clip.loopTime = loops;
                clip.loopPose = loops;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = !lifts;
                clip.keepOriginalPositionY = true;
                clip.lockRootPositionXZ = false;
                return clip;
            }).ToArray();
        }

        /// <summary>Where <paramref name="assetPath"/> sits in its move set ("Styles/Archery/BowShot.fbx"); null outside the animations.</summary>
        public static string InSet(string assetPath)
        {
            if (assetPath == null || !assetPath.StartsWith(AnimationsFolder + "/", System.StringComparison.Ordinal))
                return null;
            var inSets = assetPath.Substring(AnimationsFolder.Length + 1);
            var slash = inSets.IndexOf('/');
            return slash < 0 ? null : inSets.Substring(slash + 1);
        }
    }
}
