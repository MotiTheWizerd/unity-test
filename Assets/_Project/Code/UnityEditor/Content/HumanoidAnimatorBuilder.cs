using System.Collections.Generic;
using System.IO;
using System.Linq;
using Forge.Play;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Builds an animator controller per move set. A set is a folder Content/Animations/{Set} of <see cref="HumanoidMoves"/>
    /// clips ({Move}.fbx); <see cref="BaseSet"/> (Humanoid) is every character's default, and any other set (a guard's,
    /// an elder's) holds only the moves it changes, the rest coming from the base. Each set's {Set}.controller is built
    /// from its moves, so a clip dropped in joins at the next build:
    /// <list type="bullet">
    /// <item>Ground: the ground moves blended by speed. Each clip sits at the speed it walks (its root motion), so the feet
    /// keep pace; past the fastest clip it plays faster (<see cref="HumanoidAnimation.Pace"/>).</item>
    /// <item>Air: a jump when the body leaves the ground going up, a fall when it drops, a landing when it touches down.
    /// Each is optional; without it the body goes straight on to the next.</item>
    /// </list>
    /// A set is a whole controller rather than an override of the base's, because its clips walk at their own speeds and
    /// the blend has to sit at them. Parameters are <see cref="HumanoidAnimation"/>'s, which feeds them. Controllers are
    /// rebuilt in place, so their GUIDs, and the characters wearing them, survive.
    /// </summary>
    static class HumanoidAnimatorBuilder
    {
        /// <summary>The move set every character plays unless it names another, and the one other sets fall back to.</summary>
        public const string BaseSet = "Humanoid";

        // Seconds one move crossfades into the next.
        const float Crossfade = 0.15f;

        // Meters per second down past which leaving the ground is a fall, not a step off a kerb or a bump.
        const float FallSpeed = 3f;

        // Meters per second up past which leaving the ground is a jump.
        const float JumpSpeed = 0.5f;

        // Speed (in the clips' size) past which a landing gives way to moving on.
        const float MovingOn = 0.5f;

        /// <summary>Every set's controller by set name; a set with no ground moves to build from (its own or the base's) is left out.</summary>
        public static IReadOnlyDictionary<string, AnimatorController> BuildAll()
        {
            var built = new Dictionary<string, AnimatorController>(System.StringComparer.Ordinal);
            if (!AssetDatabase.IsValidFolder(AnimationImport.AnimationsFolder))
                return built;
            foreach (var folder in AssetDatabase.GetSubFolders(AnimationImport.AnimationsFolder).OrderBy(f => f, System.StringComparer.Ordinal))
            {
                var set = Path.GetFileName(folder);
                var controller = Build(set);
                if (controller != null)
                    built[set] = controller;
            }
            return built;
        }

        /// <summary>The set's controller, built from the clips present; null when there are no ground moves to build from.</summary>
        public static AnimatorController Build(string set)
        {
            var ground = HumanoidMoves.Ground.Select(move => (move, clip: Clip(set, move))).Where(m => m.clip != null).ToArray();
            if (ground.Length == 0)
            {
                Debug.LogWarning($"[Forge] Move set {set} has no ground moves ({string.Join(", ", HumanoidMoves.Ground)}), nor has {BaseSet}; no animator built.");
                return null;
            }

            var path = ControllerPath(set);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path)
                ?? AnimatorController.CreateAnimatorControllerAtPath(path);
            Clear(controller, path);

            var tree = GroundTree(controller, "Ground", ground, out var topSpeed);

            controller.AddParameter(HumanoidAnimation.Speed, AnimatorControllerParameterType.Float);
            controller.AddParameter(new AnimatorControllerParameter { name = HumanoidAnimation.Pace, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            controller.AddParameter(new AnimatorControllerParameter { name = HumanoidAnimation.TopSpeed, type = AnimatorControllerParameterType.Float, defaultFloat = topSpeed });
            controller.AddParameter(new AnimatorControllerParameter { name = HumanoidAnimation.Grounded, type = AnimatorControllerParameterType.Bool, defaultBool = true });
            controller.AddParameter(HumanoidAnimation.VerticalSpeed, AnimatorControllerParameterType.Float);

            var machine = controller.layers[0].stateMachine;
            var moving = GroundState(machine, "Ground", tree);
            machine.defaultState = moving;
            var grounds = new List<AnimatorState> { moving };

            // Weapons drawn: the drawn ground moves, each falling back to its plain one, when the set has any of them.
            var drawn = HumanoidMoves.Ground.Select((move, i) => (move, clip: Clip(set, HumanoidMoves.DrawnGround[i]) ?? Clip(set, move)))
                .Where(m => m.clip != null).ToArray();
            if (HumanoidMoves.DrawnGround.Any(move => Clip(set, move) != null))
            {
                controller.AddParameter(HumanoidAnimation.Drawn, AnimatorControllerParameterType.Bool);
                var drawnMoving = GroundState(machine, "Drawn Ground", GroundTree(controller, "Drawn Ground", drawn, out _));
                When(moving, drawnMoving, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = HumanoidAnimation.Drawn });
                When(drawnMoving, moving, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = HumanoidAnimation.Drawn });
                grounds.Add(drawnMoving);
            }

            BuildAir(set, machine, moving, grounds);
            BuildArms(set, controller);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // The ground moves blended by speed, each clip at the speed it walks (an idle at rest).
        static BlendTree GroundTree(AnimatorController controller, string name, IEnumerable<(string move, AnimationClip clip)> moves, out float topSpeed)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.Simple1D,
                blendParameter = HumanoidAnimation.Speed,
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            topSpeed = 0f;
            foreach (var (move, clip) in moves)
            {
                var speed = move == HumanoidMoves.Idle ? 0f : ClipSpeed(clip);
                tree.AddChild(clip, speed);
                topSpeed = Mathf.Max(topSpeed, speed);
            }

            return tree;
        }

        static AnimatorState GroundState(AnimatorStateMachine machine, string name, BlendTree tree)
        {
            var state = machine.AddState(name);
            state.motion = tree;
            state.speedParameter = HumanoidAnimation.Pace;
            state.speedParameterActive = true;
            return state;
        }

        // Drawing and sheathing on the arms only (a layer masked to them, which HumanoidAnimation weighs in while they
        // play), so the legs walk or run on underneath; left out when the set has neither move.
        static void BuildArms(string set, AnimatorController controller)
        {
            var draw = Clip(set, HumanoidMoves.Draw);
            var sheathe = Clip(set, HumanoidMoves.Sheathe);
            if (draw == null && sheathe == null)
                return;

            controller.AddParameter(HumanoidAnimation.DrawTrigger, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(HumanoidAnimation.SheatheTrigger, AnimatorControllerParameterType.Trigger);
            controller.AddLayer(HumanoidAnimation.ArmsLayer);
            var layers = controller.layers;
            var arms = layers[layers.Length - 1];
            arms.defaultWeight = 0f;
            arms.avatarMask = ArmsMask(set, controller);
            controller.layers = layers;

            var machine = arms.stateMachine;
            var rest = machine.AddState("Rest");
            rest.writeDefaultValues = false;
            machine.defaultState = rest;
            foreach (var (clip, trigger) in new[] { (draw, HumanoidAnimation.DrawTrigger), (sheathe, HumanoidAnimation.SheatheTrigger) })
            {
                if (clip == null)
                    continue;
                var state = machine.AddState(clip.name);
                state.motion = clip;
                var start = machine.AddAnyStateTransition(state);
                start.duration = Crossfade;
                start.hasFixedDuration = true;
                start.canTransitionToSelf = false;
                start.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = trigger } };
                AtEnd(state, rest);
            }
        }

        // The set's mask of the arms and fingers, made once and kept (rebuilt in place) beside its controller.
        static AvatarMask ArmsMask(string set, AnimatorController controller)
        {
            var path = $"{SetFolder(set)}/{set} Arms.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }

            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
            {
                var arm = part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                    part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, arm);
            }

            EditorUtility.SetDirty(mask);
            return mask;
        }

        static string SetFolder(string set) => $"{AnimationImport.AnimationsFolder}/{set}";

        static string ControllerPath(string set) => $"{SetFolder(set)}/{set}.controller";

        // From any ground state into the air; back down onto the plain ground, which goes on to the drawn one if drawn.
        static void BuildAir(string set, AnimatorStateMachine machine, AnimatorState moving, IReadOnlyList<AnimatorState> grounds)
        {
            var jump = State(set, machine, HumanoidMoves.Jump);
            var fall = State(set, machine, HumanoidMoves.Fall);
            var land = State(set, machine, HumanoidMoves.Land);
            // Touching down goes through the landing when there is one.
            var touchDown = land ?? moving;

            if (jump != null)
            {
                foreach (var ground in grounds)
                    When(ground, jump, Airborne(), Rising());
                if (fall != null)
                    AtEnd(jump, fall);
                When(jump, touchDown, OnGround());
            }

            if (fall != null)
            {
                foreach (var ground in grounds)
                    When(ground, fall, Airborne(), Dropping());
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

        static AnimatorState State(string set, AnimatorStateMachine machine, string move)
        {
            var clip = Clip(set, move);
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

        // The set's own clip for the move, else the base set's.
        static AnimationClip Clip(string set, string move) =>
            OwnClip(set, move) ?? (set == BaseSet ? null : OwnClip(BaseSet, move));

        static AnimationClip OwnClip(string set, string move) =>
            AssetDatabase.LoadAllAssetsAtPath($"{SetFolder(set)}/{move}.fbx").OfType<AnimationClip>().FirstOrDefault(c => c.name == move);

        // Empties the controller for a rebuild: its parameters, its layers past the first, its states and the blend trees they played.
        static void Clear(AnimatorController controller, string path)
        {
            foreach (var parameter in controller.parameters)
                controller.RemoveParameter(parameter);
            while (controller.layers.Length > 1)
                controller.RemoveLayer(controller.layers.Length - 1);
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states)
                machine.RemoveState(child.state);
            foreach (var tree in AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>())
                Object.DestroyImmediate(tree, true);
        }
    }
}
