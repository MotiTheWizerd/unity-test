using UnityEditor;
using UnityEditor.SceneManagement;

namespace Forge.UnityEditor
{
    /// <summary>Dev tool: opens the face editor's scene (the screen alone, on a character of its own) and plays it.</summary>
    static class FaceEditorOpener
    {
        const string Scene = "Assets/_Project/Scenes/FaceEditor.unity";

        [MenuItem("Forge/Characters/Face Editor")]
        static void Open()
        {
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(Scene);
            EditorApplication.EnterPlaymode();
        }
    }
}
