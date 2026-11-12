using System.Linq;
using Forge.Data.Records;
using Forge.Play;
using Forge.UnityEditor.Content;
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

        // Each move the core styles name plays where its settings (its .json beside its clip) put it, its name nothing:
        // a draw and a sheathe on the arms alone, a death on the whole body staying at its end (its variants too), a hit
        // laid over all the rest, a stance as a ground state of its own, the rest on the upper body.
        [Test]
        public void TheCoreStylesMoves_PlayWhereTheirSettingsPutThem()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.That(controller, Is.Not.Null, "Build Characters makes the base move set's controller.");

            foreach (var style in CoreCombatStyleSet.All())
            {
                foreach (var move in style.Moves)
                {
                    switch (move.Kind)
                    {
                        case CombatMoveKind.Draw:
                        case CombatMoveKind.Sheathe:
                            Assert.That(StateOn(controller, HumanoidAnimation.ArmsLayer, move.Animation), Is.Not.Null, $"{move.Animation} on the arms.");
                            break;
                        case CombatMoveKind.Death:
                            var deaths = new[] { move.Animation, HumanoidAnimation.Variant(move.Animation, 2) };
                            foreach (var death in deaths)
                            {
                                var state = StateOn(controller, HumanoidAnimation.FullBodyLayer, death);
                                Assert.That(state, Is.Not.Null, $"{death} on the whole body.");
                                Assert.That(state.transitions, Is.Empty, $"{death} stays at its end.");
                            }

                            break;
                        case CombatMoveKind.Hit:
                            var reactions = controller.layers.Single(l => l.name == HumanoidAnimation.ReactionsLayer);
                            Assert.That(reactions.blendingMode, Is.EqualTo(AnimatorLayerBlendingMode.Additive), "A blow taken is laid over whatever plays.");
                            Assert.That(controller.layers.Last().name, Is.EqualTo(HumanoidAnimation.ReactionsLayer), "Over all the others.");
                            Assert.That(StateOn(controller, HumanoidAnimation.ReactionsLayer, move.Animation), Is.Not.Null, $"{move.Animation} is a reaction.");
                            break;
                        case CombatMoveKind.Stance:
                            Assert.That(controller.layers[0].stateMachine.states.Select(s => s.state.name),
                                Does.Contain(HumanoidAnimation.StanceGround(move.Animation)), $"{move.Animation} is a stance.");
                            break;
                        default:
                            Assert.That(StateOn(controller, HumanoidAnimation.UpperBodyLayer, move.Animation), Is.Not.Null, $"{move.Animation} on the upper body.");
                            break;
                    }
                }
            }
        }

        static AnimatorState StateOn(AnimatorController controller, string layer, string name) =>
            controller.layers.Single(l => l.name == layer).stateMachine.states.Select(s => s.state).SingleOrDefault(s => s.name == name);
    }
}
