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
    /// A stage in the editor, outside play, to tune where a move holds by eye: a character stands in the Scene view
    /// posed in a move (its drawn idle), a thing in its hand as the game holds it (<see cref="CharacterModel"/>: the same
    /// grip, the same aim from the move's .json), and the thing is turned with the Scene view's rotate gizmo until it
    /// looks right; one button writes where it now points into the move's .json beside its clip
    /// (<see cref="MoveSettings"/>), which reimports the clip, and the stage poses again with the hand carrying it there.
    /// The stage is never saved with the scene and goes when the window closes. Forge → Characters → Hold Stage.
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
        CharacterModel _character;
        // Per hand (Hands' order): the thing it holds, and its turn as the hand carries it.
        readonly GameObject[] _held = new GameObject[Hands.Length];
        readonly Quaternion[] _heldAsPlaced = new Quaternion[Hands.Length];
        float _leanForward;
        float _leanRight;
        AnimationClip _clip;
        PlayableGraph _graph;

        [MenuItem("Forge/Characters/Hold Stage")]
        static void Open() => GetWindow<HoldStage>("Hold Stage");

        void OnEnable() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            Clear();
        }

        // The stage is not saved, so it doesn't survive into play; it is cleared before, and stood again by hand after.
        void OnPlayModeChanged(PlayModeStateChange change) => Clear();

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

            // Which hand's thing the sliders lean and Save writes; switching puts both back as the move holds them.
            EditorGUI.BeginChangeCheck();
            _hand = EditorGUILayout.Popup("Tune", _hand, new[] { "Right hand", "Left hand" });
            if (EditorGUI.EndChangeCheck() && _stage != null)
                Pose();

            EditorGUILayout.Space();
            if (_stage == null)
            {
                if (GUILayout.Button("Stand her on the stage"))
                    Stand(looks[_look], moves.Length > 0 ? moves[_move] : null);
                return;
            }

            if (changed)
            {
                Clear();
                Stand(looks[_look], moves.Length > 0 ? moves[_move] : null);
            }

            var jsonPath = _clip != null ? MoveSettings.PathFor(AssetDatabase.GetAssetPath(_clip)) : null;
            // Where the held thing itself points (its own grip's +Y), which the sliders and the gizmo turn; the hand's point stays.
            if (_held[_hand] == null || !_character.TryGetHeld(Hands[_hand], out _, out var grip))
            {
                EditorGUILayout.HelpBox($"Nothing in the {Hands[_hand]}: no prefab '{_heldKeys[_hand]}' in {CatalogPath}, or the character lacks the hand.", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.HelpBox("Lean it with the sliders (or select it and turn it with the rotate gizmo), then save: the hand will carry it there.", MessageType.None);
                EditorGUI.BeginChangeCheck();
                _leanForward = EditorGUILayout.Slider("Lean forward (+) / back (−)", _leanForward, -180f, 180f);
                _leanRight = EditorGUILayout.Slider("Lean right (+) / left (−)", _leanRight, -180f, 180f);
                if (EditorGUI.EndChangeCheck())
                    Lean();
                var points = _character.Root.transform.InverseTransformDirection(grip.up).normalized;
                EditorGUILayout.LabelField("Points now", $"[{points.x:0.00}, {points.y:0.00}, {points.z:0.00}]  (x right, y up, z forward)");
                EditorGUILayout.LabelField("Writes to", jsonPath ?? "(no clip)");
                if (GUILayout.Button("Select it in the Scene view"))
                {
                    Selection.activeGameObject = _held[_hand];
                    Tools.current = Tool.Rotate;
                    SceneView.lastActiveSceneView?.Frame(new Bounds(grip.position, Vector3.one * 1.2f), false);
                }

                using (new EditorGUI.DisabledScope(jsonPath == null))
                {
                    if (GUILayout.Button($"Save to {Path.GetFileName(jsonPath ?? "the move's .json")}"))
                        Save(jsonPath, points);
                }

                if (GUILayout.Button("Put it back as the move holds it"))
                    Pose();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Take her off the stage"))
                Clear();
        }

        // Every clip the character's controller plays, by name.
        static AnimationClip[] MovesOf(GameObject look)
        {
            var animator = look.GetComponentInChildren<Animator>(true);
            var controller = animator != null ? animator.runtimeAnimatorController : null;
            return controller != null ? controller.animationClips.OrderBy(c => c.name, StringComparer.Ordinal).ToArray() : Array.Empty<AnimationClip>();
        }

        // The character on a stage at the Scene view's pivot, posed in the move, a thing in each hand.
        void Stand(GameObject look, AnimationClip clip)
        {
            Clear();
            var view = SceneView.lastActiveSceneView;
            _stage = new GameObject("Hold Stage") { hideFlags = HideFlags.DontSave };
            _stage.transform.position = view != null ? view.pivot : Vector3.zero;
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
                _heldAsPlaced[hand] = held.transform.localRotation;
            }

            Pose();
            // Nothing of the stage is saved with the scene.
            foreach (var transform in _stage.GetComponentsInChildren<Transform>(true))
                transform.gameObject.hideFlags = HideFlags.DontSave;
            view?.Frame(new Bounds(_stage.transform.position + Vector3.up, Vector3.one * 2.2f), false);
        }

        // The move's first frame on the character, the hand turned as the move's .json says, the held thing back where the hand carries it.
        void Pose()
        {
            if (_graph.IsValid())
            {
                _graph.GetRootPlayable(0).SetTime(0);
                _graph.Evaluate();
            }

            if (_clip != null)
            {
                var curves = MoveSettings.Read(AssetDatabase.GetAssetPath(_clip));
                _character.AimHand(true, new Vector3(curves[0].Value, curves[1].Value, curves[2].Value));
                _character.AimHand(false, new Vector3(curves[3].Value, curves[4].Value, curves[5].Value));
            }

            for (var hand = 0; hand < Hands.Length; hand++)
            {
                if (_held[hand] != null)
                    _held[hand].transform.localRotation = _heldAsPlaced[hand];
            }

            _leanForward = _leanRight = 0f;
            SceneView.RepaintAll();
        }

        // The held thing leaned from where the hand carries it, in the body's frame: forward about the body's right axis,
        // right about its forward axis (degrees), as a thing held upright leans.
        void Lean()
        {
            var held = _held[_hand];
            if (held == null)
                return;
            var body = _character.Root.transform.rotation;
            var placed = held.transform.parent.rotation * _heldAsPlaced[_hand];
            held.transform.rotation = body * Quaternion.Euler(_leanForward, 0f, -_leanRight) * Quaternion.Inverse(body) * placed;
            SceneView.RepaintAll();
        }

        void Save(string jsonPath, Vector3 points)
        {
            var right = _hand == 0;
            var existing = MoveSettings.Parse(File.Exists(jsonPath) ? File.ReadAllText(jsonPath) : null, jsonPath);
            var other = new Vector3(existing[right ? 3 : 0].Value, existing[right ? 4 : 1].Value, existing[right ? 5 : 2].Value);
            File.WriteAllText(jsonPath, MoveSettings.Write(right ? points : other, right ? other : points));
            AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Forge] {Path.GetFileName(jsonPath)}: the {(right ? "right" : "left")} hand points [{points.x:0.00}, {points.y:0.00}, {points.z:0.00}].");
            Pose();
        }

        void Clear()
        {
            // The Inspector would keep showing what is about to go.
            if (_stage != null && Selection.activeTransform != null && Selection.activeTransform.IsChildOf(_stage.transform))
                Selection.activeGameObject = null;
            if (_graph.IsValid())
                _graph.Destroy();
            _character?.Dispose();
            _character = null;
            Array.Clear(_held, 0, _held.Length);
            _clip = null;
            if (_stage != null)
                DestroyImmediate(_stage);
            _stage = null;
        }
    }
}
