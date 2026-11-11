using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;

namespace Forge.Tests.Content
{
    // Content guard: the base move set as Build Characters made it (Humanoid/_Built/Humanoid.controller).
    public class MoveSetContentTests
    {
        const string ControllerPath = "Assets/_Project/Content/Animations/Humanoid/_Built/Humanoid.controller";

        // With weapons drawn, standing (the drawn ground, a style's stance), whatever the body does it does in the drawn
        // clips (a plain one would drop the sword and the shield): every moving child is a drawn clip, a stand-in sped
        // up where the set has no drawn clip as fast (a sprint as the drawn run, faster), a reverse played backward.
        [Test]
        public void WithWeaponsDrawn_EveryWayItMoves_IsADrawnClip()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null, "Build Characters makes the base move set's controller.");

            var drawnStates = controller.layers[0].stateMachine.states.Select(s => s.state)
                .Where(s => s.name == "Drawn Ground" || (s.name.EndsWith(" Ground") && s.name != "Ground" && !s.name.Contains("Crouch")))
                .ToArray();
            Assert.That(drawnStates.Select(s => s.name), Does.Contain("Drawn Ground"));

            foreach (var state in drawnStates)
            {
                var tree = state.motion as BlendTree;
                Assert.That(tree, Is.Not.Null, $"{state.name} blends its moves.");
                var moving = tree.children.Where(c => c.threshold != 0f || c.position != UnityEngine.Vector2.zero).ToArray();
                Assert.That(moving, Is.Not.Empty, $"{state.name} moves.");
                foreach (var child in moving)
                    Assert.That(child.motion.name, Does.StartWith("Drawn"),
                        $"{state.name} moves at {child.position} (threshold {child.threshold}) in the plain {child.motion.name}: the weapons drop.");
            }
        }
    }
}
