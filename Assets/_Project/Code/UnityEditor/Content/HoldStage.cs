using System;
using System.IO;
using System.Linq;
using Forge.Play;
using Forge.World.Assets;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Object = UnityEngine.Object;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// A stage in the editor, outside play, to tune where a move holds by eye: a character stands in the Scene view, at the origin,
    /// posed in a move (its drawn idle), a thing in its hand as the game holds it (<see cref="CharacterModel"/>: the same
    /// grip, the same aim from the move's .json), and the thing is turned with the Scene view's rotate gizmo until it
    /// looks right; one button writes where it now points into the move's .json beside its clip
    /// (<see cref="MoveSettings"/>), which reimports the clip, and the stage poses again with the hand carrying it there.
    /// She is shown in the window itself (<see cref="StagePreview"/>), whichever tab the editor has in front. The stage is
    /// never saved with the scene and goes when the window closes. Forge → Characters → Hold Stage.
    /// </summary>
    sealed class HoldStage : EditorWindow
    {
        const string ProfilePath = "Assets/_Project/Settings/Player/PlayerProfile.asset";
        const string CatalogPath = "Assets/_Project/Content/PrefabCatalog.asset";
        const string DefaultMove = HumanoidAnimation.DrawnIdleMove;
        static readonly string[] Hands = { "RightHand", "LeftHand" };
        // What each hand holds by default: the player's starting sword and shield.
        static readonly string[] DefaultHeld = { "models/viking_sword", "models/round_shield_painted" };

        int _look;
        int _move = -1;
        int _hand;
        readonly string[] _heldKeys = (string[])DefaultHeld.Clone();

        GameObject _stage;
        GameObject _standing;
        CharacterModel _character;
        // Per hand (Hands' order): the thing it holds, the prefab it came from, and where the hand carries it (its local pose).
        readonly GameObject[] _held = new GameObject[Hands.Length];
        readonly GameObject[] _heldPrefab = new GameObject[Hands.Length];
        readonly UnityEngine.Pose[] _heldAsPlaced = new UnityEngine.Pose[Hands.Length];
        float _leanForward;
        float _leanRight;
        // Per hand: where the move turns it (the move's .json, as the sliders change it; zero: as the clip has it), and
        // where the sliders' hand pointed when they left zero, which they lean from.
        readonly Vector3[] _aims = new Vector3[Hands.Length];
        Vector3 _leanFrom;
        AnimationClip _clip;
        PlayableGraph _graph;
        // She is seen here, in the window, whichever tab the editor shows.
        StagePreview _preview;
        // What the last save came to, read back from the files.
        string _saved;
        MessageType _savedType;

        // Reloaded from the files, a save must put it back this close to where it was left, else it says so.
        const float MostSavedMiss = 0.001f;
        const float MostSavedTurn = 0.2f;

        [MenuItem("Forge/Characters/Hold Stage")]
        static void Open() => GetWindow<HoldStage>("Hold Stage");

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            _preview = new StagePreview();
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Clear();
            _preview?.Dispose();
            _preview = null;
        }

        // The stage is not saved, so it doesn't survive into play; it is cleared before, and stood again by hand after.
        void OnPlayModeChanged(PlayModeStateChange change) => Clear();

        // Moved or turned with the Scene view's gizmo, it is shown here as it goes.
        void OnInspectorUpdate()
        {
            if (_stage != null)
                Repaint();
        }

        void OnGUI()
        {
            var profile = AssetDatabase.LoadAssetAtPath<PlayerProfile>(ProfilePath);
            var looks = profile != null ? profile.Looks.Where(l => l != null).ToArray() : Array.Empty<GameObject>();
            if (looks.Length == 0)
            {
                EditorGUILayout.HelpBox($"No characters to stand: {ProfilePath} lists none (Build Characters fills it).", MessageType.Info);
                return;
            }

            EditorGUI.BeginChangeCheck();
            _look = Mathf.Clamp(_look, 0, looks.Length - 1);
            _look = EditorGUILayout.Popup("Character", _look, looks.Select(l => l.name).ToArray());
            var moves = MovesOf(looks[_look]);
            if (_move < 0)
                _move = Math.Max(0, Array.FindIndex(moves, m => m.name == DefaultMove));
            _move = Mathf.Clamp(_move, 0, Math.Max(0, moves.Length - 1));
            _move = EditorGUILayout.Popup("Move", _move, moves.Select(m => m.name).ToArray());
            _heldKeys[0] = EditorGUILayout.TextField("Right hand holds", _heldKeys[0]);
            _heldKeys[1] = EditorGUILayout.TextField("Left hand holds", _heldKeys[1]);
            var changed = EditorGUI.EndChangeCheck();

            // Which hand the sliders turn; switching keeps what was done to either (Save keeps both).
            EditorGUI.BeginChangeCheck();
            _hand = EditorGUILayout.Popup("Sliders turn", _hand, new[] { "Right hand", "Left hand" });
            if (EditorGUI.EndChangeCheck())
                _leanForward = _leanRight = 0f;

            EditorGUILayout.Space();
            if (_stage == null)
            {
                // Outside play only: in play the game's world and camera are in the way, and leaving it clears the stage.
                var playing = EditorApplication.isPlayingOrWillChangePlaymode;
                if (playing)
                    EditorGUILayout.HelpBox("Stop Play first: the stage stands her in the editor's Scene view, outside the game.", MessageType.Info);
                using (new EditorGUI.DisabledScope(playing))
                {
                    if (GUILayout.Button("Stand her on the stage"))
                        Stand(looks[_look], moves.Length > 0 ? moves[_move] : null);
                }

                return;
            }

            if (changed)
            {
                Clear();
                Stand(looks[_look], moves.Length > 0 ? moves[_move] : null);
            }

            var jsonPath = _clip != null ? MoveSettings.PathFor(AssetDatabase.GetAssetPath(_clip)) : null;
            EditorGUILayout.HelpBox("Move (W) or turn (E) the sword or the shield in the Scene view: that is where it sits in the hand, " +
                "in every move. Turn a hand with the sliders: that is this move's, and whatever the hand holds follows it. Save keeps " +
                "everything as you see it, both hands; she is then stood again from the saved files and measured against what you left.",
                MessageType.None);
            // Where the held thing itself points (its own grip's +Y), which the sliders and the gizmo turn.
            if (_held[_hand] == null || !_character.TryGetHeld(Hands[_hand], out _, out var grip))
            {
                EditorGUILayout.HelpBox($"Nothing in the {Hands[_hand]}: no prefab '{_heldKeys[_hand]}' in {CatalogPath}, or the character lacks the hand.", MessageType.Warning);
            }
            else if (GUILayout.Button("Select it in the Scene view"))
            {
                Selection.activeGameObject = _held[_hand];
                Tools.current = Tool.Move;
                SceneView.lastActiveSceneView?.Frame(new Bounds(grip.position, Vector3.one * 1.2f), false);
            }

            // The sliders turn the hand in this move, whatever it holds, which every thing it may hold follows.
            EditorGUI.BeginChangeCheck();
            var wasStill = _leanForward == 0f && _leanRight == 0f;
            _leanForward = EditorGUILayout.Slider("Hand forward (+) / back (−)", _leanForward, -180f, 180f);
            _leanRight = EditorGUILayout.Slider("Hand right (+) / left (−)", _leanRight, -180f, 180f);
            if (EditorGUI.EndChangeCheck())
            {
                // A lean starts from wherever the hand points now.
                if (wasStill)
                    _leanFrom = HandPoints(_hand);
                Lean();
            }

            var points = HandPoints(_hand);
            EditorGUILayout.LabelField("Hand points", $"[{points.x:0.00}, {points.y:0.00}, {points.z:0.00}]  (x right, y up, z forward)");

            EditorGUILayout.LabelField("Saves to", string.Join(", ", new[] { jsonPath }.Concat(_heldPrefab.Select(ItemJson))
                .Where(path => path != null).Select(Path.GetFileName)));
            using (new EditorGUI.DisabledScope(jsonPath == null))
            {
                if (GUILayout.Button("Save"))
                    Save(jsonPath);
            }

            if (GUILayout.Button("Put both back as saved"))
                Pose();
            if (_saved != null)
                EditorGUILayout.HelpBox(_saved, _savedType);

            EditorGUILayout.Space();
            if (GUILayout.Button("Take her off the stage"))
            {
                Clear();
                return;
            }

            // The rest of the window shows her; drag to turn round her, scroll to come closer.
            var area = GUILayoutUtility.GetRect(10f, 10f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (_preview.Draw(area, _stage.transform.position + Vector3.up))
                Repaint();
        }

        // Every clip the character's controller plays, by name.
        static AnimationClip[] MovesOf(GameObject look)
        {
            var animator = look.GetComponentInChildren<Animator>(true);
            var controller = animator != null ? animator.runtimeAnimatorController : null;
            return controller != null ? controller.animationClips.OrderBy(c => c.name, StringComparer.Ordinal).ToArray() : Array.Empty<AnimationClip>();
        }

        // The character on a stage at the origin, posed in the move, a thing in each hand, and the Scene view (opened when
        // none is) framed on her. Not at the view's pivot: that drifts anywhere (once 166 km out, where she stood unseen
        // and floats lose their millimetres), while outside play the scene holds nothing there (the world is the game's).
        void Stand(GameObject look, AnimationClip clip, bool frame = true)
        {
            Clear();
            var view = SceneView.lastActiveSceneView != null ? SceneView.lastActiveSceneView : GetWindow<SceneView>();
            _stage = new GameObject("Hold Stage") { hideFlags = HideFlags.DontSave };
            _standing = look;
            _character = new CharacterModel(_stage.transform, look);
            _clip = clip;
            var animator = _character.Animators.FirstOrDefault(a => a.isHuman);
            if (animator != null && clip != null)
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                _graph = PlayableGraph.Create("Hold stage");
                _graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var playable = AnimationClipPlayable.Create(_graph, clip);
                playable.SetApplyFootIK(false);
                AnimationPlayableOutput.Create(_graph, "Pose", animator).SetSourcePlayable(playable);
            }

            Pose();
            var catalog = AssetDatabase.LoadAssetAtPath<PrefabCatalog>(CatalogPath);
            for (var hand = 0; hand < Hands.Length; hand++)
            {
                if (catalog == null || string.IsNullOrWhiteSpace(_heldKeys[hand]) || !catalog.TryGet(_heldKeys[hand], out var prefab))
                    continue;
                var held = Object.Instantiate(prefab);
                held.name = prefab.name;
                if (!_character.Attach(held, ModelGrip.Of(held), Hands[hand], false, held.GetComponent<HeldLook>()))
                    continue;
                _held[hand] = held;
                _heldPrefab[hand] = prefab;
                _heldAsPlaced[hand] = new UnityEngine.Pose(held.transform.localPosition, held.transform.localRotation);
            }

            Pose();
            // Nothing of the stage is saved with the scene.
            foreach (var transform in _stage.GetComponentsInChildren<Transform>(true))
                transform.gameObject.hideFlags = HideFlags.DontSave;
            if (frame)
                view.Frame(new Bounds(_stage.transform.position + Vector3.up, Vector3.one * 2.2f), false);
        }

        // As the game shows it: each held thing back where its hand carries it (as saved), then the move's first frame
        // with the hands turned as the move's .json says, by the game's own code, so what stands here is what plays.
        void Pose()
        {
            for (var hand = 0; hand < Hands.Length; hand++)
            {
                if (_held[hand] != null)
                    _held[hand].transform.SetLocalPositionAndRotation(_heldAsPlaced[hand].position, _heldAsPlaced[hand].rotation);
            }

            var settings = _clip != null ? MoveSettings.Read(AssetDatabase.GetAssetPath(_clip)) : MoveSettings.None;
            _aims[0] = settings.RightHand;
            _aims[1] = settings.LeftHand;
            PoseHands();
            _leanForward = _leanRight = 0f;
        }

        // The move's first frame with each hand turned as the window has it (the move's .json, as the sliders change it),
        // by the game's own code; what the hands hold stays where it sits in them.
        void PoseHands()
        {
            if (_graph.IsValid())
            {
                _graph.GetRootPlayable(0).SetTime(0);
                _graph.Evaluate();
            }

            _character.AimHand(true, _aims[0]);
            _character.AimHand(false, _aims[1]);
            SceneView.RepaintAll();
        }

        // Where `hand` points now (its hold point's +Y), in the body's frame.
        Vector3 HandPoints(int hand) =>
            _character.TryGetHoldPoint(Hands[hand], out var point)
                ? _character.Root.transform.InverseTransformDirection(point.up).normalized
                : Vector3.zero;

        // The hand turned from where it pointed when the sliders left zero, in the body's frame: forward about the body's
        // right axis, right about its forward axis (degrees); the move's aim for it is then that way, and she is posed so.
        void Lean()
        {
            if (_leanFrom == Vector3.zero)
                return;
            _aims[_hand] = Quaternion.Euler(_leanForward, 0f, -_leanRight) * _leanFrom;
            PoseHands();
        }

        // Keeps everything as it is seen, both hands, like a picture, split as the game holds it: each hand's turn goes
        // to the move (the window's aim for it, in this move only, whatever it holds), and where each thing sits in its
        // hand to the thing itself (its seat on the grip, in every move). The hands are posed as the game poses them, and
        // each seat is what puts its grip where it was left on that hand; then she is stood again from the files and
        // each is measured against where it was left. A hand holding what isn't a model (no json of its own to seat it
        // in) is left as the files have it, and said so.
        void Save(string jsonPath)
        {
            var count = Hands.Length;
            for (var hand = 0; hand < count; hand++)
                FoldInnerEdits(hand);
            var left = new UnityEngine.Pose?[count];
            var notes = new System.Collections.Generic.List<string>();
            for (var hand = 0; hand < count; hand++)
            {
                if (_held[hand] == null || !_character.TryGetHeld(Hands[hand], out _, out var grip))
                    continue;
                if (ItemJson(_heldPrefab[hand]) == null)
                {
                    notes.Add($"{_held[hand].name} isn't a model with a json of its own: not saved.");
                    continue;
                }

                left[hand] = new UnityEngine.Pose(grip.position, grip.rotation);
            }

            // The hands as the game will pose them, which what they hold is then seated on.
            PoseHands();
            for (var hand = 0; hand < count; hand++)
            {
                if (!left[hand].HasValue || !_character.TrySeatFor(Hands[hand], left[hand].Value, out var seat))
                    continue;
                var prefabPath = AssetDatabase.GetAssetPath(_heldPrefab[hand]);
                var folder = Path.GetDirectoryName(prefabPath).Replace('\\', '/');
                var name = Path.GetFileNameWithoutExtension(prefabPath);
                ModelBuilder.WriteHold(folder, name, seat.position, Degrees(seat.rotation.eulerAngles));
                ModelBuilder.BuildModel(folder, name);
            }

            // The rest of what the move's .json says is kept as it is.
            var existing = MoveSettings.Parse(File.Exists(jsonPath) ? File.ReadAllText(jsonPath) : null, jsonPath);
            File.WriteAllText(jsonPath, existing.WithHands(_aims[0], _aims[1]).Write());
            AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceUpdate);

            // Stood again where the view already looks, so it doesn't jump away from the hand being tuned, with the same
            // thing selected as before, so the gizmo stays on it.
            var names = _held.Select(held => held != null ? held.name : null).ToArray();
            var selected = Array.FindIndex(_held, held => held != null && Selection.activeTransform != null && Selection.activeTransform.IsChildOf(held.transform));
            Stand(_standing, _clip, frame: false);
            if (selected >= 0 && _held[selected] != null)
                Selection.activeGameObject = _held[selected];
            var within = true;
            for (var hand = 0; hand < count; hand++)
            {
                if (!left[hand].HasValue)
                    continue;
                if (!_character.TryGetHeld(Hands[hand], out _, out var again))
                {
                    within = false;
                    notes.Add($"{names[hand]}: not in the hand once stood again.");
                    continue;
                }

                var miss = Vector3.Distance(again.position, left[hand].Value.position);
                var turn = Quaternion.Angle(again.rotation, left[hand].Value.rotation);
                var kept = miss <= MostSavedMiss && turn <= MostSavedTurn;
                within &= kept;
                notes.Add(kept
                    ? $"{names[hand]}: where you left it ({miss * 1000f:0.0} mm, {turn:0.00}°)."
                    : $"{names[hand]}: {miss * 1000f:0.0} mm and {turn:0.00}° from where you left it. Tell Brokkr.");
            }

            _saved = (within ? "Saved, and stood again from the files. " : "Saved, but not all came back as left. ") + string.Join(" ", notes);
            _savedType = within ? MessageType.Info : MessageType.Error;
            Debug.Log($"[Forge] Hold Stage: {Path.GetFileName(jsonPath)} and the held things' json saved. {_saved}");
        }

        // A part of the held thing moved on its own (a mesh picked in the Hierarchy and dragged) is taken as the whole
        // thing moved so, as a picture keeps what is seen: the whole is put where that part shows it, and the part back
        // in its place within it. Only one part can be taken so; any other moved part goes back as the files have it.
        void FoldInnerEdits(int hand)
        {
            var held = _held[hand];
            var prefab = _heldPrefab[hand];
            if (held == null || prefab == null)
                return;
            foreach (var part in held.GetComponentsInChildren<Transform>(true))
            {
                if (part == held.transform)
                    continue;
                var made = prefab.transform.Find(PathFrom(held.transform, part));
                if (made == null || (Vector3.Distance(part.localPosition, made.localPosition) < 1e-5f &&
                                     Quaternion.Angle(part.localRotation, made.localRotation) < 1e-3f))
                    continue;
                var seen = Matrix4x4.TRS(part.position, part.rotation, Vector3.one);
                part.SetLocalPositionAndRotation(made.localPosition, made.localRotation);
                var moved = seen * Matrix4x4.TRS(part.position, part.rotation, Vector3.one).inverse;
                held.transform.SetPositionAndRotation(moved.MultiplyPoint3x4(held.transform.position), moved.rotation * held.transform.rotation);
                return;
            }
        }

        // The path to `part` below `root`, by names, as Transform.Find takes it.
        static string PathFrom(Transform root, Transform part)
        {
            var names = new System.Collections.Generic.List<string>();
            for (var at = part; at != null && at != root; at = at.parent)
                names.Insert(0, at.name);
            return string.Join("/", names);
        }

        // Degrees between -180 and 180, as they read in an Inspector.
        static Vector3 Degrees(Vector3 euler) =>
            new Vector3(Mathf.DeltaAngle(0f, euler.x), Mathf.DeltaAngle(0f, euler.y), Mathf.DeltaAngle(0f, euler.z));

        // The json of the model a held prefab was built from, where its seat in the hand is saved; null for what isn't a model.
        static string ItemJson(GameObject prefab)
        {
            if (prefab == null)
                return null;
            var path = AssetDatabase.GetAssetPath(prefab);
            var fbx = Path.ChangeExtension(path, ".fbx");
            return File.Exists(fbx) ? Path.ChangeExtension(path, ".json") : null;
        }

        void Clear()
        {
            // The Inspector would keep showing what is about to go, and fail on it once it has.
            if (_stage != null && Selection.activeTransform != null && Selection.activeTransform.IsChildOf(_stage.transform))
            {
                Selection.activeGameObject = null;
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }
            if (_graph.IsValid())
                _graph.Destroy();
            _character?.Dispose();
            _character = null;
            Array.Clear(_held, 0, _held.Length);
            Array.Clear(_heldPrefab, 0, _heldPrefab.Length);
            _clip = null;
            _saved = null;
            if (_stage != null)
                DestroyImmediate(_stage);
            _stage = null;
        }
    }
}
