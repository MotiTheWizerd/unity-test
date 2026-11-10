using System.Linq;
using Forge.Play;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Forge.Tests.Play
{
    // A character's eyes are read off its own head (HumanoidHead.TryFindEyeLevel), so each sees from its own height.
    public class EyeLevelTests
    {
        const string Hero = "Assets/_Project/Content/Characters/VikingHero/VikingHero.prefab";
        const string Idle = "Assets/_Project/Content/Animations/Humanoid/Locomotion/Idle.fbx";

        static AnimationClip IdleClip() =>
            AssetDatabase.LoadAllAssetsAtPath(Idle).OfType<AnimationClip>().First(clip => !clip.name.StartsWith("__preview__"));

        [Test]
        public void TheHero_SeesFromAMansEyes_HalfwayUpHisHead()
        {
            var hero = AssetDatabase.LoadAssetAtPath<GameObject>(Hero);

            Assert.That(HumanoidHead.TryFindEyeLevel(hero, IdleClip(), out var eyes), Is.True);
            // 1.84 m tall in his idle, his head joint at 1.59 m and its top at 1.84 m (from the soles, 3 cm below his root).
            Assert.That(eyes, Is.InRange(1.64f, 1.72f));
        }

        [Test]
        public void ACharacterWithoutAHead_HasNoEyeLevel()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                Assert.That(HumanoidHead.TryFindEyeLevel(cube, IdleClip(), out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(cube);
            }
        }
    }
}
