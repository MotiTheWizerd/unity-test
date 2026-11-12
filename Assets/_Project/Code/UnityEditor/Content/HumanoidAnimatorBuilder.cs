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
    /// clips ({Move}.fbx), sorted into folders by what they are (Locomotion, Air, Styles/{Style}, ...);
    /// <see cref="BaseSet"/> (Humanoid) is every character's default, and any other set (a guard's, an elder's) holds only
    /// the moves it changes, the rest coming from the base. Each set's _Built/{Set}.controller is built from its moves,
    /// so a clip dropped in joins at the next build:
    /// <list type="bullet">
    /// <item>Ground: the ground moves blended by speed. Each clip sits at the speed it walks (its root motion), so the feet
    /// keep pace; past the fastest clip it plays faster (<see cref="HumanoidAnimation.Pace"/>).</item>
    /// <item>Air: a jump when the body leaves the ground going up (the drawn jump while drawn, when there is one), a fall
    /// when it drops, a landing when it touches down. Each is optional; without it the body goes straight on to the next.</item>
    /// <item>Drawn: the drawn ground moves while weapons are out, and a ground state per combat style's stance
    /// ({Style}Idle: the same moves standing in it), picked by <see cref="HumanoidAnimation.Stance"/>.</item>
    /// <item>Crouched: the crouched ground moves while <see cref="HumanoidAnimation.Crouched"/> (their drawn versions while
    /// drawn, when there are any), gone down into and stood up from by CrouchDown and StandUp when standing still.</item>
    /// <item>Actions: draws, sheathes and blows on masked layers, each started by a trigger of its name.</item>
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
                if (!HumanoidMoves.IsSet(set))
                    continue;
                var controller = Build(set);
                if (controller != null)
                    built[set] = controller;
            }
            return built;
        }

        /// <summary>The set's controller, built from the clips present; null when there are no ground moves to build from.</summary>
        public static AnimatorController Build(string set)
        {
            Indexes.Clear();
            var ground = HumanoidMoves.Ground.Select(move => (move, clip: Clip(set, move))).Where(m => m.clip != null)
                .Select(m => GroundMove.Of(m.move, m.clip)).ToArray();
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
            // Where each hand's held thing points, written by the clips' curves (MoveSettings) as they blend.
            foreach (var right in new[] { true, false })
            {
                for (var axis = 0; axis < 3; axis++)
                    controller.AddParameter(HumanoidAnimation.HandPoints(right, axis), AnimatorControllerParameterType.Float);
            }

            var machine = controller.layers[0].stateMachine;
            var moving = GroundState(machine, "Ground", tree);
            machine.defaultState = moving;
            var grounds = new List<AnimatorState> { moving };

            // Crouching is known before the drawn moves are built: a crouch outranks them.
            var crouches = Clip(set, HumanoidMoves.CrouchedGround[0]) != null;
            if (crouches)
                controller.AddParameter(HumanoidAnimation.Crouched, AnimatorControllerParameterType.Bool);
            BuildDrawn(set, controller, machine, moving, grounds);
            if (crouches)
                BuildCrouch(set, controller, machine, moving, grounds);
            BuildAir(set, controller, machine, moving, grounds);
            BuildActions(set, controller);
            EditorUtility.SetDirty(controller);
            return controller;
        }

        // Weapons drawn: the drawn ground moves (DrawnMoves) when the set has any of them; and each stance
        // (MoveRole.Stance), the same moves standing in it instead, entered while drawn and the Stance parameter holds
        // its key (HumanoidAnimation.StanceKey), else the set's own drawn moves (Stance 0). With a sidestep (DrawnStrafe,
        // else Strafe) the drawn states blend by the way the body goes (DirectionalTree): a fighter facing its foe
        // circles it and backs away.
        static void BuildDrawn(string set, AnimatorController controller, AnimatorStateMachine machine, AnimatorState moving,
            List<AnimatorState> grounds)
        {
            var drawn = DrawnMoves(set);
            var strafe = Clip(set, HumanoidMoves.DrawnStrafe) ?? Clip(set, HumanoidMoves.Strafe);
            var stances = Moves(set, MoveRole.Stance).Select(move => Clip(set, move)).Where(clip => clip != null).ToArray();
            var ownDrawn = HumanoidMoves.DrawnGround.Concat(new[] { HumanoidMoves.DrawnStrafe }).Any(move => Clip(set, move) != null);
            if (!ownDrawn && stances.Length == 0)
                return;

            controller.AddParameter(HumanoidAnimation.Drawn, AnimatorControllerParameterType.Bool);
            if (stances.Length > 0)
                controller.AddParameter(HumanoidAnimation.Stance, AnimatorControllerParameterType.Int);
            if (strafe != null)
            {
                controller.AddParameter(HumanoidAnimation.SideSpeed, AnimatorControllerParameterType.Float);
                controller.AddParameter(HumanoidAnimation.AheadSpeed, AnimatorControllerParameterType.Float);
            }

            if (ownDrawn)
                DrawnGround(controller, machine, moving, grounds, "Drawn Ground", drawn, 0, strafe);

            var keys = new Dictionary<int, string>();
            foreach (var stance in stances)
            {
                var key = HumanoidAnimation.StanceKey(stance.name);
                if (keys.TryGetValue(key, out var other))
                {
                    Debug.LogWarning($"[Forge] Stances {other} and {stance.name} of {set} share a key; rename one. {stance.name} is left out.");
                    continue;
                }

                keys.Add(key, stance.name);
                var moves = drawn.Select(m => m.Move == HumanoidMoves.Idle ? GroundMove.Of(m.Move, stance) : m);
                DrawnGround(controller, machine, moving, grounds, HumanoidAnimation.StanceGround(stance.name), moves, key, strafe);
            }
        }

        // The ground moves with weapons drawn, each the set's drawn clip of it (DrawnWalk); one it lacks is the plain
        // clip (an idle, a walk), unless it is faster than the fastest drawn clip: then that clip sped up to its pace (a
        // sprint shown as the drawn run, faster), as a body past its fastest clip plays it faster (StrideMatch), so the
        // weapons stay up rather than dropping into the plain run's arms.
        static GroundMove[] DrawnMoves(string set)
        {
            var own = HumanoidMoves.Ground.Select((move, i) => (move, clip: Clip(set, HumanoidMoves.DrawnGround[i]))).ToArray();
            GroundMove? fastest = null;
            foreach (var (move, clip) in own)
            {
                if (clip == null)
                    continue;
                var candidate = GroundMove.Of(move, clip);
                if (candidate.Speed > 0f && (fastest == null || candidate.Speed > fastest.Value.Speed))
                    fastest = candidate;
            }

            var moves = new List<GroundMove>();
            foreach (var (move, clip) in own)
            {
                if (clip != null)
                {
                    moves.Add(GroundMove.Of(move, clip));
                    continue;
                }

                var plain = Clip(set, move);
                if (plain == null)
                    continue;
                var standIn = GroundMove.Of(move, plain);
                moves.Add(fastest is GroundMove top && standIn.Speed > top.Speed
                    ? new GroundMove(move, top.Clip, standIn.Speed, standIn.Speed / top.Speed)
                    : standIn);
            }

            return moves.ToArray();
        }

        // One drawn ground state of `moves` (blended by the way the body goes when there is a `strafe`, else by speed),
        // entered from the plain ground while drawn in the stance keyed `key` (when the set has stances), and left back
        // to it when either changes.
        static void DrawnGround(AnimatorController controller, AnimatorStateMachine machine, AnimatorState moving, List<AnimatorState> grounds,
            string name, IEnumerable<GroundMove> moves, int key, AnimationClip strafe)
        {
            var tree = strafe != null ? DirectionalTree(controller, name, moves, strafe) : GroundTree(controller, name, moves, out _);
            var state = GroundState(machine, name, tree);
            // Entered only standing, when the set crouches: crouched, the crouch's moves play whatever is drawn.
            var enter = new List<AnimatorCondition> { new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = HumanoidAnimation.Drawn } };
            if (HasParameter(controller, HumanoidAnimation.Crouched))
                enter.Add(CrouchOff());
            if (HasParameter(controller, HumanoidAnimation.Stance))
            {
                enter.Add(new AnimatorCondition { mode = AnimatorConditionMode.Equals, parameter = HumanoidAnimation.Stance, threshold = key });
                When(state, moving, new AnimatorCondition { mode = AnimatorConditionMode.NotEqual, parameter = HumanoidAnimation.Stance, threshold = key });
            }

            When(moving, state, enter.ToArray());

            When(state, moving, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = HumanoidAnimation.Drawn });
            grounds.Add(state);
        }

        // Crouched: the crouched ground moves (MoveRole.Ground's Crouch{Move}), from any standing ground state while
        // Crouched, and back to the plain ground (which goes on to the drawn one if drawn) when not. Standing still, the
        // set's CrouchDown and StandUp play the way down and up when it has them; moving, the moves just crossfade.
        static void BuildCrouch(string set, AnimatorController controller, AnimatorStateMachine machine, AnimatorState moving,
            List<AnimatorState> grounds)
        {
            var moves = HumanoidMoves.Ground.Select((move, i) => (move, clip: Clip(set, HumanoidMoves.CrouchedGround[i])))
                .Where(m => m.clip != null).Select(m => GroundMove.Of(m.move, m.clip));
            const string name = "Crouch Ground";
            var crouched = GroundState(machine, name, GroundTree(controller, name, moves, out _));
            var down = State(set, machine, HumanoidMoves.CrouchDown);
            var up = State(set, machine, HumanoidMoves.StandUp);

            foreach (var ground in grounds)
            {
                if (down != null)
                {
                    When(ground, down, CrouchOn(), Still());
                    When(ground, crouched, CrouchOn(), Moving());
                }
                else
                    When(ground, crouched, CrouchOn());
            }

            if (down != null)
            {
                AtEnd(down, crouched);
                When(down, crouched, Moving());
                When(down, moving, CrouchOff());
                grounds.Add(down);
            }

            if (up != null)
            {
                When(crouched, up, CrouchOff(), Still());
                When(crouched, moving, CrouchOff(), Moving());
                AtEnd(up, moving);
                When(up, moving, Moving());
                When(up, crouched, CrouchOn());
                grounds.Add(up);
            }
            else
                When(crouched, moving, CrouchOff());

            grounds.Add(crouched);
            BuildDrawnCrouch(set, controller, machine, moving, crouched, up, grounds);
        }

        // Crouched with weapons drawn: the drawn crouched moves (Drawn{Crouch Move}), each falling back to its plain
        // crouched one, when the set has any of them and drawn moves to tell it by. Entered from the plain crouch while
        // drawn and left back to it when not, as the drawn ground is from the plain one; standing up leaves it as the
        // plain crouch is left.
        static void BuildDrawnCrouch(string set, AnimatorController controller, AnimatorStateMachine machine, AnimatorState moving,
            AnimatorState crouched, AnimatorState up, List<AnimatorState> grounds)
        {
            if (!HasParameter(controller, HumanoidAnimation.Drawn) || !HumanoidMoves.DrawnCrouchedGround.Any(move => Clip(set, move) != null))
                return;

            var moves = HumanoidMoves.Ground.Select((move, i) =>
                    (move, clip: Clip(set, HumanoidMoves.DrawnCrouchedGround[i]) ?? Clip(set, HumanoidMoves.CrouchedGround[i])))
                .Where(m => m.clip != null).Select(m => GroundMove.Of(m.move, m.clip));
            const string name = "Drawn Crouch Ground";
            var drawn = GroundState(machine, name, GroundTree(controller, name, moves, out _));
            When(crouched, drawn, new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = HumanoidAnimation.Drawn });
            When(drawn, crouched, new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = HumanoidAnimation.Drawn });
            if (up != null)
            {
                When(drawn, up, CrouchOff(), Still());
                When(drawn, moving, CrouchOff(), Moving());
            }
            else
                When(drawn, moving, CrouchOff());

            grounds.Add(drawn);
        }

        static bool HasParameter(AnimatorController controller, string name) => controller.parameters.Any(p => p.name == name);

        static AnimatorCondition CrouchOn() => new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = HumanoidAnimation.Crouched };

        static AnimatorCondition CrouchOff() => new AnimatorCondition { mode = AnimatorConditionMode.IfNot, parameter = HumanoidAnimation.Crouched };

        static AnimatorCondition Still() => new AnimatorCondition { mode = AnimatorConditionMode.Less, parameter = HumanoidAnimation.Speed, threshold = MovingOn };

        static AnimatorCondition Moving() => new AnimatorCondition { mode = AnimatorConditionMode.Greater, parameter = HumanoidAnimation.Speed, threshold = MovingOn };

        // A ground move as a blend tree plays it: its clip, at the speed it shows (its own, an idle's 0; a stand-in's the
        // move's), and how much faster than made the clip plays there (1: as made).
        readonly struct GroundMove
        {
            public GroundMove(string move, AnimationClip clip, float speed, float timeScale = 1f)
            {
                Move = move;
                Clip = clip;
                Speed = speed;
                TimeScale = timeScale;
            }

            public string Move { get; }
            public AnimationClip Clip { get; }
            public float Speed { get; }
            public float TimeScale { get; }

            // The move as its clip plays it: at the speed the clip walks (an idle at rest).
            public static GroundMove Of(string move, AnimationClip clip) =>
                new GroundMove(move, clip, move == HumanoidMoves.Idle ? 0f : ClipSpeed(clip));
        }

        // The ground moves blended by speed, each at the speed it shows (an idle at rest).
        static BlendTree GroundTree(AnimatorController controller, string name, IEnumerable<GroundMove> moves, out float topSpeed)
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
            var children = new List<ChildMotion>();
            foreach (var move in moves)
            {
                children.Add(new ChildMotion { motion = move.Clip, threshold = move.Speed, timeScale = move.TimeScale });
                topSpeed = Mathf.Max(topSpeed, move.Speed);
            }

            tree.children = children.ToArray();
            return tree;
        }

        // The ground moves blended by the way the body goes (SideSpeed to its right, AheadSpeed ahead, in the clips' size):
        // each forward clip ahead at the speed it walks and, played backward, as far behind; the strafe to the right at
        // its own speed and mirrored to the left, and sped up to each faster forward pace, so a sidestep keeps up with a
        // run; the idle at rest. Between them the blend is by direction, so a diagonal mixes a step ahead and aside.
        static BlendTree DirectionalTree(AnimatorController controller, string name, IEnumerable<GroundMove> moves, AnimationClip strafe)
        {
            var tree = new BlendTree
            {
                name = name,
                blendType = BlendTreeType.FreeformDirectional2D,
                blendParameter = HumanoidAnimation.SideSpeed,
                blendParameterY = HumanoidAnimation.AheadSpeed,
                hideFlags = HideFlags.HideInHierarchy,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);
            var children = new List<ChildMotion>();
            var paces = new List<float>();
            foreach (var move in moves)
            {
                children.Add(Child(move.Clip, 0f, move.Speed, timeScale: move.TimeScale));
                if (move.Speed <= 0f)
                    continue;
                children.Add(Child(move.Clip, 0f, -move.Speed, timeScale: -move.TimeScale));
                paces.Add(move.Speed);
            }

            var sidestep = ClipSpeed(strafe);
            if (sidestep > 0f)
            {
                var sideways = new List<float> { sidestep };
                sideways.AddRange(paces.Where(pace => pace > sidestep));
                foreach (var pace in sideways.Distinct())
                {
                    children.Add(Child(strafe, pace, 0f, timeScale: pace / sidestep));
                    children.Add(Child(strafe, -pace, 0f, timeScale: pace / sidestep, mirror: true));
                }
            }

            tree.children = children.ToArray();
            return tree;
        }

        static ChildMotion Child(AnimationClip clip, float side, float ahead, float timeScale = 1f, bool mirror = false) =>
            new ChildMotion { motion = clip, position = new Vector2(side, ahead), timeScale = timeScale, mirror = mirror };

        static AnimatorState GroundState(AnimatorStateMachine machine, string name, BlendTree tree)
        {
            var state = machine.AddState(name);
            state.motion = tree;
            state.speedParameter = HumanoidAnimation.Pace;
            state.speedParameterActive = true;
            return state;
        }

        // The set's actions, each on a layer of its own body parts as its settings say (MoveSettings.Plays), which
        // HumanoidAnimation weighs in while one plays: a draw or a sheathe on the arms (MoveRole.ArmsAction), so the body
        // walks or runs on unchanged; a slash or a shot on the upper body (MoveRole.UpperBodyAction), so the torso turns
        // into it while the legs still walk; a death on the whole body (MoveRole.FullBodyAction), played through and
        // stayed at its end (MoveSettings.StaysAtEnd). Each starts by a trigger named after it. A layer is left out when
        // the set has no action for it.
        static void BuildActions(string set, AnimatorController controller)
        {
            BuildActionLayer(set, controller, HumanoidAnimation.ArmsLayer, ArmsParts, Actions(set, MoveRole.ArmsAction));
            BuildActionLayer(set, controller, HumanoidAnimation.UpperBodyLayer, UpperBodyParts, Actions(set, MoveRole.UpperBodyAction));
            BuildActionLayer(set, controller, HumanoidAnimation.FullBodyLayer, null, Actions(set, MoveRole.FullBodyAction));
        }

        // The set's actions in `role`, each with whether it stays at its end once played.
        static IEnumerable<(AnimationClip Clip, bool Stays)> Actions(string set, MoveRole role) =>
            Moves(set, role).Select(move => (clip: Clip(set, move), stays: TryEntry(set, move, out var entry) && entry.StaysAtEnd))
                .Where(action => action.clip != null);

        // A layer masked to parts (none: the whole body), resting until a trigger starts one of its clips. The layer's
        // weight does the blending in and out (HumanoidAnimation), so its states change at once: a crossfade into the
        // empty rest state would draw the limbs toward the rig's neutral pose on the way out. A clip that stays keeps
        // its end once it has played (a death lies as it fell) until HumanoidAnimation rests the layer; the rest go back
        // to rest as they end.
        static void BuildActionLayer(string set, AnimatorController controller, string name, AvatarMaskBodyPart[] parts,
            IEnumerable<(AnimationClip Clip, bool Stays)> clips)
        {
            var actions = clips.ToArray();
            if (actions.Length == 0)
                return;

            controller.AddLayer(name);
            var layers = controller.layers;
            var layer = layers[layers.Length - 1];
            layer.defaultWeight = 0f;
            layer.avatarMask = parts != null ? Mask(set, name, parts) : null;
            controller.layers = layers;

            // The layer's actions play at its pace, which holds one still (a bow at full draw) at 0.
            var pace = HumanoidAnimation.ActionPace(name);
            controller.AddParameter(new AnimatorControllerParameter { name = pace, type = AnimatorControllerParameterType.Float, defaultFloat = 1f });

            var machine = layer.stateMachine;
            var rest = machine.AddState(HumanoidAnimation.RestState);
            rest.writeDefaultValues = false;
            machine.defaultState = rest;
            foreach (var (clip, stays) in actions)
            {
                var trigger = clip.name;
                controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);
                var state = machine.AddState(clip.name);
                state.motion = clip;
                state.speedParameter = pace;
                state.speedParameterActive = true;
                var start = machine.AddAnyStateTransition(state);
                start.duration = 0f;
                start.hasFixedDuration = true;
                start.canTransitionToSelf = false;
                start.conditions = new[] { new AnimatorCondition { mode = AnimatorConditionMode.If, parameter = trigger } };
                if (stays)
                    continue;
                var end = state.AddTransition(rest);
                end.hasExitTime = true;
                end.exitTime = 1f;
                end.duration = 0f;
                end.hasFixedDuration = true;
            }
        }

        static readonly AvatarMaskBodyPart[] ArmsParts =
        {
            AvatarMaskBodyPart.LeftArm, AvatarMaskBodyPart.RightArm, AvatarMaskBodyPart.LeftFingers, AvatarMaskBodyPart.RightFingers,
        };

        static readonly AvatarMaskBodyPart[] UpperBodyParts = ArmsParts.Concat(new[] { AvatarMaskBodyPart.Body, AvatarMaskBodyPart.Head }).ToArray();

        // The set's mask of a layer's parts, made once and kept (rebuilt in place) beside its controller.
        static AvatarMask Mask(string set, string layer, AvatarMaskBodyPart[] parts)
        {
            var path = $"{BuiltFolder(set)}/{set} {layer}.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(path);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, path);
            }

            for (var part = AvatarMaskBodyPart.Root; part < AvatarMaskBodyPart.LastBodyPart; part++)
                mask.SetHumanoidBodyPartActive(part, System.Array.IndexOf(parts, part) >= 0);
            EditorUtility.SetDirty(mask);
            return mask;
        }

        static string SetFolder(string set) => $"{AnimationImport.AnimationsFolder}/{set}";

        static string ControllerPath(string set) => $"{BuiltFolder(set)}/{set}.controller";

        // Where the set's built assets go (made when missing), apart from its clips.
        static string BuiltFolder(string set)
        {
            var folder = $"{SetFolder(set)}/{HumanoidMoves.BuiltFolder}";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder(SetFolder(set), HumanoidMoves.BuiltFolder);
            return folder;
        }

        // From any ground state into the air; back down onto the plain ground, which goes on to the drawn one if drawn.
        // Drawn, the jump is the drawn jump when the set has one (and drawn moves to tell it by); the fall and the
        // landing are the same either way.
        static void BuildAir(string set, AnimatorController controller, AnimatorStateMachine machine, AnimatorState moving,
            IReadOnlyList<AnimatorState> grounds)
        {
            var jump = State(set, machine, HumanoidMoves.Jump);
            var drawnJump = HasParameter(controller, HumanoidAnimation.Drawn) ? State(set, machine, HumanoidMoves.DrawnJump) : null;
            var fall = State(set, machine, HumanoidMoves.Fall);
            var land = State(set, machine, HumanoidMoves.Land);
            // Touching down goes through the landing when there is one.
            var touchDown = land ?? moving;

            foreach (var (takeOff, whenDrawn) in new[] { (jump, false), (drawnJump, true) })
            {
                if (takeOff == null)
                    continue;
                // Told apart by Drawn only when both are there: either alone jumps whatever is drawn.
                var takesOff = new List<AnimatorCondition> { Airborne(), Rising() };
                if (jump != null && drawnJump != null)
                    takesOff.Add(new AnimatorCondition { mode = whenDrawn ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot, parameter = HumanoidAnimation.Drawn });
                foreach (var ground in grounds)
                    When(ground, takeOff, takesOff.ToArray());
                if (fall != null)
                    AtEnd(takeOff, fall);
                When(takeOff, touchDown, OnGround());
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
                When(land, moving, Moving());
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

        static float ClipSpeed(AnimationClip clip) => HumanoidAnimation.ClipSpeed(clip);

        // The moves of the set in `role` (its stances, its arm actions): its own clips and the base set's, by name, each once.
        static IEnumerable<string> Moves(string set, MoveRole role)
        {
            var sets = set == BaseSet ? new[] { set } : new[] { set, BaseSet };
            return sets.SelectMany(s => Index(s).Where(m => m.Value.Role == role).Select(m => m.Key))
                .Distinct(System.StringComparer.Ordinal)
                .OrderBy(move => move, System.StringComparer.Ordinal);
        }

        // The set's own clip for the move, else the base set's.
        static AnimationClip Clip(string set, string move) =>
            TryEntry(set, move, out var entry)
                ? AssetDatabase.LoadAllAssetsAtPath(entry.Path).OfType<AnimationClip>().FirstOrDefault(c => c.name == move)
                : null;

        // The set's own move of that name, else the base set's.
        static bool TryEntry(string set, string move, out IndexedMove entry) =>
            Index(set).TryGetValue(move, out entry) || (set != BaseSet && Index(BaseSet).TryGetValue(move, out entry));

        // A move of a set: where its clip is, what it is (HumanoidMoves.RoleOf), and whether, played through, it stays at
        // its end (MoveSettings.StaysAtEnd).
        readonly struct IndexedMove
        {
            public IndexedMove(string path, MoveRole role, bool staysAtEnd)
            {
                Path = path;
                Role = role;
                StaysAtEnd = staysAtEnd;
            }

            public string Path { get; }
            public MoveRole Role { get; }
            public bool StaysAtEnd { get; }
        }

        // Each set's own moves by name, read once per build.
        static readonly Dictionary<string, Dictionary<string, IndexedMove>> Indexes =
            new Dictionary<string, Dictionary<string, IndexedMove>>(System.StringComparer.Ordinal);

        // The set's own moves: every clip in its folders, as its folder and its settings say (MoveSettings), the ones it
        // can't play left out, each said why.
        static Dictionary<string, IndexedMove> Index(string set)
        {
            if (Indexes.TryGetValue(set, out var index))
                return index;
            index = new Dictionary<string, IndexedMove>(System.StringComparer.Ordinal);
            Indexes.Add(set, index);
            var folder = SetFolder(set);
            if (!Directory.Exists(folder))
                return index;

            var files = Directory.GetFiles(folder, "*.fbx", SearchOption.AllDirectories)
                .Select(file => file.Replace('\\', '/'))
                .OrderBy(file => file, System.StringComparer.Ordinal);
            foreach (var file in files)
            {
                var move = HumanoidMoves.MoveName(file);
                MoveSettings settings;
                try
                {
                    settings = MoveSettings.Read(file);
                }
                catch (System.InvalidOperationException refused)
                {
                    Debug.LogError($"[Forge] {refused.Message} {move} is left out.");
                    continue;
                }

                var role = HumanoidMoves.RoleOf(file.Substring(folder.Length + 1), settings);
                if (role == null)
                {
                    Debug.LogWarning($"[Forge] {file} is in no move folder of {set} ({HumanoidMoves.LocomotionFolder}, " +
                        $"{HumanoidMoves.AirFolder}, {HumanoidMoves.StylesFolder}/<Style>, or one for actions); left out.");
                    continue;
                }

                if (HumanoidMoves.IgnoresPlays(role.Value, settings))
                    Debug.LogWarning($"[Forge] {MoveSettings.PathFor(file)}: \"plays\" is for actions; a move in {set}/{file.Substring(folder.Length + 1).Split('/')[0]} plays as its folder says.");

                var belongs = HumanoidMoves.Misplaced(move, role.Value);
                if (belongs != null)
                {
                    Debug.LogWarning($"[Forge] {file}: {move} belongs in {set}/{belongs}; left out.");
                    continue;
                }

                if (index.TryGetValue(move, out var first))
                {
                    Debug.LogWarning($"[Forge] {set} has two moves named {move} ({first.Path}, {file}); a move's name is its own. The second is left out.");
                    continue;
                }

                index.Add(move, new IndexedMove(file, role.Value, settings.StaysAtEnd));
            }

            return index;
        }

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
