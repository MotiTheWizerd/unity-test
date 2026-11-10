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
        /// The skeleton every clip's motion is read against: a Mixamo-rigged character downloaded from Mixamo in its
        /// T-pose (a character download with Pose "T-pose", no animation; VikingM1 today). Only the T-pose's bone
        /// orientations matter, and Mixamo poses every rig it makes the same way. A clip file without skin has no rest
        /// pose of its own, so left alone Unity takes its first frame for one (a run mid-stride); and read against a
        /// character's own import instead, every other character inherits that character's stance, since a muscle's
        /// value is relative to the reference's T-pose (Unity's auto T-pose of the hero's A-posed rest kept his feet
        /// turned out, and splayed the Vikings' legs). This file stands in the T-pose itself, so its own avatar serves.
        /// </summary>
        public const string ReferenceRig = AnimationsFolder + "/" + HumanoidMoves.ReferenceFolder + "/TPose.fbx";

        /// <summary>Until the reference is in place: the hero's rig, which only he plays right.</summary>
        const string FallbackRig = CharacterImport.CharactersFolder + "/VikingHero/VikingHero.fbx";

        bool InFolder => assetPath.StartsWith(AnimationsFolder + "/", System.StringComparison.Ordinal);

        bool IsReference => assetPath == ReferenceRig;

        // Bumped when how clips import changes, so every clip reimports with the new rule (2: the T-pose reference,
        // imported before the clips that read it; 3: the hands' curves from the move's .json).
        public override uint GetVersion() => 3;

        void OnPreprocessModel()
        {
            if (!InFolder)
                return;

            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            if (IsReference)
            {
                // Its own first frame is the T-pose; it is a skeleton, not a move.
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.importAnimation = false;
                return;
            }

            importer.importAnimation = true;
            var rig = RigAt(ReferenceRig);
            // No reference file at all: the hero's rig, which only he plays right. (A file not yet imported in this
            // batch is no cause for alarm: the clip imports again once it is.)
            var missing = !File.Exists(ReferenceRig);
            if (rig == null && missing && (rig = RigAt(FallbackRig)) != null)
                Debug.LogWarning($"[Forge] No T-pose at {ReferenceRig}; {assetPath} reads its motion against {FallbackRig}, which only that character " +
                                 "plays right. Download a Mixamo-rigged character from Mixamo with Pose \"T-pose\" to that path.");
            if (rig != null)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                importer.sourceAvatar = rig;
            }
            else
            {
                if (missing)
                    Debug.LogWarning($"[Forge] No humanoid rig at {ReferenceRig} nor {FallbackRig}; {assetPath} reads its motion against its own first frame.");
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }
        }

        // The humanoid avatar of the rig at `path`, which this import then follows: a change there reimports the clip,
        // and in a batch (a fresh clone's first import) a clip imported before the rig imports again once the rig's
        // avatar is there. Null when there is none, or none yet.
        Avatar RigAt(string path)
        {
            context.DependsOnArtifact(path);
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault(a => a.isHuman && a.isValid);
        }

        void OnPreprocessAnimation()
        {
            if (!InFolder || IsReference)
                return;

            var importer = (ModelImporter)assetImporter;
            var name = Path.GetFileNameWithoutExtension(assetPath);
            var role = HumanoidMoves.RoleOf(InSet(assetPath));
            var loops = role.HasValue && HumanoidMoves.Loops(role.Value, name);
            var lifts = role.HasValue && HumanoidMoves.Lifts(role.Value, name);
            // How the move holds (its .json beside it, there or not: a change to it reimports the clip), as curves the
            // animator blends into its parameters; every clip carries them, so a move that says nothing says zero.
            context.DependsOnSourceAsset(MoveSettings.PathFor(assetPath));
            var curves = MoveSettings.Read(assetPath)
                .Select(c => new ClipAnimationInfoCurve { name = c.Name, curve = AnimationCurve.Constant(0f, 1f, c.Value) })
                .ToArray();
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
                clip.curves = curves;
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
