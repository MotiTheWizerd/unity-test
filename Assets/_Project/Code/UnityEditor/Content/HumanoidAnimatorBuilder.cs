using System.Linq;
using Forge.Play;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Builds the one animator controller every humanoid character plays (Content/Animations/Humanoid/Humanoid.controller)
    /// from whichever <see cref="HumanoidMoves"/> are there, so a new clip dropped in joins at the next build:
    /// <list type="bullet">
    /// <item>Ground: the ground moves blended by speed. Each clip sits at the speed it walks (its root motion), so the feet
    /// keep pace; past the fastest clip it plays faster (<see cref="HumanoidAnimation.Pace"/>).</item>
    /// <item>Air: a jump when the body leaves the ground going up, a fall when it drops, a landing when it touches down.
    /// Each is optional; without it the body goes straight on to the next.</item>
    /// </list>
    /// Parameters are <see cref="HumanoidAnimation"/>'s, which feeds them. The controller is rebuilt in place, so its GUID,
    /// and the characters wearing it, survive.
    /// </summary>
    static class HumanoidAnimatorBuilder
    {
        public const string Folder = AnimationImport.AnimationsFolder + "/Humanoid";
        const string ControllerPath = Folder + "/Humanoid.controller";

        // Seconds one move crossfades into the next.
        const float Crossfade = 0.15f;

        // Meters per second down past which leaving the ground is a fall, not a step off a kerb or a bump.
        const float FallSpeed = 3f;

        // Meters per second up past which leaving the ground is a jump.
        const float JumpSpeed = 0.5f;

        // Speed (in the clips' size) past which a landing gives way to moving on.
        const float MovingOn = 0.5f;

        /// <summary>The controller, built from the clips present; null when there are no ground moves to build from.</summary>
        public static AnimatorController Build()
        {
            var ground = HumanoidMoves.Ground.Select(move => (move, clip: Clip(move))).Where(m => m.clip != null).ToArray();
            if (ground.Length == 0)
            {
                Debug.LogWarning($"[Forge] No ground moves ({string.Join(", ", HumanoidMoves.Ground)}) under {Folder}; no animator built.");
                return null;
            }

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            Clear(controller);

            var tree = new BlendTree
            {
                name = "Ground",
                blendType = BlendTreeType.Simple1D,
                blendParameter = HumanoidAnimation.Speed,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            var topSpeed = 0f;
            foreach (var (move, clip) in ground)
            {
                var speed = move == HumanoidMoves.Idle ? 0f : ClipSpeed(clip);
                tree.AddChild(clip, speed);
                topSpeed = Mathf.Max(topSpeed, speed);
            }

            controller.AddParameter(HumanoidAnimation.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = HumanoidAnimation.Pace, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter(new AnimatorControllerParameter { name = HumanoidAnimation.TopSpeed, type = AnimatorControllerParameterType.Float, defaultFloat = topSpeed });
            controller.AddParameter(new AnimatorControllerParameter { name = HumanoidAnimation.Grounded, type = AnimatorControllerParameterType.Bool, defaultBool = true });
            controller.AddParameter(HumanoidAnimation.VerticalSpeed, AnimatorControllerParameterType.Float);

            var machine = controller.layers[0].stateMachine;
            var moving = machine.AddState("Ground");
            moving.motion = tree;
            moving.speedParameter = HumanoidAnimation.Pace;
            moving.speedParameterActive = true;
            machine.defaultState = moving;

            BuildAir(machine, moving);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static void BuildAir(AnimatorStateMachine machine, AnimatorState moving)
        {
            var jump = State(machine, HumanoidMoves.Jump);
            var fall = State(machine, HumanoidMoves.Fall);
            var land = State(machine, HumanoidMoves.Land);
            // Touching down goes through the landing when there is one.
            var touchDown = land ?? moving;

            if (jump != null)
            {
                When(moving, jump, Airborne(), Rising());
                if (fall != null)
                    AtEnd(jump, fall);
                When(jump, touchDown, OnGround());
            }

            if (fall != null)
            {
                When(moving, fall, Airborne(), Dropping());
                When(fall, touchDown, OnGround());
            }

            if (land != null)
            {
                AtEnd(land, moving);
                When(land, moving, new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = HumanoidAnimation.Speed, threshold = MovingOn });
                if (fall != null)
                    When(land, fall, Airborne(), Dropping());
            }
        }

        static AnimatorCondition Airborne() => new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = HumanoidAnimation.Grounded };

        static AnimatorCondition OnGround() => new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = HumanoidAnimation.Grounded };

        static AnimatorCondition Rising() => new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = HumanoidAnimation.VerticalSpeed, threshold = JumpSpeed };

        static AnimatorCondition Dropping() => new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = HumanoidAnimation.VerticalSpeed, threshold = -FallSpeed };

        static AnimatorState State(AnimatorStateMachine machine, string move)
        {
            var clip = Clip(move);
            if (clip == null)
                return null;
            var state = machine.AddState(move);
            state.motion = clip;
            return state;
        }

        static void When(AnimatorState from, AnimatorState to, params AnimatorCondition[] conditions)
        {
            var transition = Transition(from, to);
            transition.hasExitTime = false;
            transition.conditions = conditions;
        }

        // Once from has (nearly) played through.
        static void AtEnd(AnimatorState from, AnimatorState to)
        {
            var transition = Transition(from, to);
            transition.hasExitTime = true;
            transition.exitTime = 0.9f;
        }

        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to)
        {
            var transition = from.AddTransition(to);
            transition.duration = Crossfade;
            transition.hasFixedDuration = true;
            return transition;
        }

        // How fast the clip walks: its root motion over the ground, in the clips' own size.
        static float ClipSpeed(AnimationClip clip) => new Vector2(clip.averageSpeed.x, clip.averageSpeed.z).magnitude;

        static AnimationClip Clip(string move) =>
            AssetDatabase.LoadAllAssetsAtPath($"{Folder}/{move}.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == move);

        // Empties the controller for a rebuild: its parameters, its states and the blend trees they played.
        static void Clear(AnimatorController controller)
        {
            foreach (var parameter in controller.parameters)
                controller.RemoveParameter(parameter);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states)
                machine.RemoveState(child.state);
            foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(ControllerPath).OfType<BlendTree>())
                Object.DestroyImmediate(tree, true);
        }
    }
}
