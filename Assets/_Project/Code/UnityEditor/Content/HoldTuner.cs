using System.IO;
using Forge.EditorApp;
using Forge.Play;
using UnityEditor;
using UnityEngine;

namespace Forge.UnityEditor.Content
{
    /// <summary>
    /// Tunes where a move holds, by eye: while the editor plays and the player stands in a move (its drawn idle), the
    /// thing in a hand is selected and turned with the Scene view's rotate gizmo until it looks right, and one button
    /// writes where it now points into the move's .json beside its clip (<see cref="MoveSettings"/>), which reimports
    /// the clip and turns the hand to it (<see cref="HandAim"/>); the thing is put back in the hand as it was, since the
    /// hand now carries it there. Forge → Characters → Hold Tuner.
    /// </summary>
    sealed class HoldTuner : EditorWindow
    {
        static readonly string[] Hands = { "RightHand", "LeftHand" };

        int _hand;
        GameObject _turning;
        Quaternion _wasTurned;

        [MenuItem("Forge/Characters/Hold Tuner")]
        static void Open() => GetWindow<HoldTuner>("Hold Tuner");

        void OnInspectorUpdate() => Repaint();

        void OnGUI()
        {
            var character = PlayingCharacter();
            if (character == null)
            {
                EditorGUILayout.HelpBox("Play the world from here, with the player spawned and holding something.", MessageType.Info);
                return;
            }

            _hand = GUILayout.Toolbar(_hand, new[] { "Right hand", "Left hand" });
            var bone = Hands[_hand];
            var move = character.PlayingMove;
            if (!character.TryGetHeld(bone, out var look, out var grip))
            {
                EditorGUILayout.HelpBox($"The {bone} holds nothing in hand now (draw the weapons: R).", MessageType.Info);
                return;
            }

            var movePath = move != null ? AssetDatabase.GetAssetPath(move) : null;
            var jsonPath = movePath != null ? MoveSettings.PathFor(movePath) : null;
            EditorGUILayout.LabelField("Move", move != null ? move.name : "(none)");
            EditorGUILayout.LabelField("Holds", look.name);
            var points = PointsOf(character, grip);
            EditorGUILayout.LabelField("Points now", $"[{points.x:0.00}, {points.y:0.00}, {points.z:0.00}]  (x right, y up, z forward)");
            if (jsonPath != null)
                EditorGUILayout.LabelField("Writes to", jsonPath);

            EditorGUILayout.Space();
            if (_turning != look)
            {
                if (GUILayout.Button("Select it, then turn it with the rotate gizmo (E) in the Scene view"))
                {
                    _turning = look;
                    _wasTurned = look.transform.localRotation;
                    Selection.activeGameObject = look;
                    Tools.current = Tool.Rotate;
                }
            }
            else
            {
                EditorGUILayout.HelpBox("Turn it in the Scene view until it looks right, then save. The hand will carry it there from now on.", MessageType.None);
                using (new EditorGUI.DisabledScope(jsonPath == null))
                {
                    if (GUILayout.Button($"Save to {Path.GetFileName(jsonPath ?? "the move's .json")}"))
                        Save(jsonPath, bone == Hands[0], points, look);
                }

                if (GUILayout.Button("Put it back, don't save"))
                    PutBack(look);
            }
        }

        // Where the held thing's blade points now, in the body's frame.
        static Vector3 PointsOf(CharacterModel character, Transform grip) =>
            character.Root.transform.InverseTransformDirection(grip.up).normalized;

        void Save(string jsonPath, bool right, Vector3 points, GameObject look)
        {
            var existing = File.Exists(jsonPath) ? MoveSettings.Parse(File.ReadAllText(jsonPath), jsonPath) : MoveSettings.Parse(null, jsonPath);
            var other = new Vector3(existing[right ? 3 : 0].Value, existing[right ? 4 : 1].Value, existing[right ? 5 : 2].Value);
            File.WriteAllText(jsonPath, MoveSettings.Write(right ? points : other, right ? other : points));
            PutBack(look);
            AssetDatabase.ImportAsset(jsonPath, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[Forge] {Path.GetFileName(jsonPath)}: the {(right ? "right" : "left")} hand points [{points.x:0.00}, {points.y:0.00}, {points.z:0.00}].");
        }

        void PutBack(GameObject look)
        {
            if (look != null)
                look.transform.localRotation = _wasTurned;
            _turning = null;
        }

        // The player's look while the editor plays the world; else null.
        static CharacterModel PlayingCharacter()
        {
            if (!Application.isPlaying)
                return null;
            var controller = FindFirstObjectByType<EditorController>();
            return controller != null && controller.PlayFromHere != null ? controller.PlayFromHere.Session.Character : null;
        }
    }
}
