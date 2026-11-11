using UnityEditor;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Where a humanoid character's forearm rolls. Mecanim spreads a lower arm's roll between the elbow and the wrist
    /// (<see cref="UnityEngine.HumanDescription.lowerArmTwist"/>), half to each by default; our rigs (Mixamo's, from
    /// Tripo models) have no forearm twist bones, so the wrist's half wrings the mesh at the wrist, worst at a swing's
    /// end. A real forearm rolls along its length and the wrist only bends, so the whole roll goes to the forearm.
    /// Build Characters sets it on every character (<see cref="CharacterBuilder"/>).
    /// </summary>
    static class HumanoidTwist
    {
        /// <summary>How much of the lower arm's roll the forearm takes: all of it.</summary>
        public const float LowerArm = 1f;

        /// <summary>
        /// Sets the forearm's share of its roll on the humanoid import at <paramref name="modelPath"/> and reimports it;
        /// false, with no reimport, when it isn't a humanoid import or already has it.
        /// </summary>
        public static bool Apply(string modelPath)
        {
            if (!(AssetImporter.GetAtPath(modelPath) is ModelImporter importer) || importer.animationType != ModelImporterAnimationType.Human)
                return false;
            var description = importer.humanDescription;
            if (description.lowerArmTwist == LowerArm)
                return false;
            description.lowerArmTwist = LowerArm;
            importer.humanDescription = description;
            importer.SaveAndReimport();
            return true;
        }
    }
}
