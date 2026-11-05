using UnityEditor;
using UnityEditor.SceneManagement;

namespace Forge.UnityEditor
{
    /// <summary>Dev tool: opens the UI gallery scene (every Forge component in every state) and plays it.</summary>
    static class UiGalleryOpener
    {
        const string Scene = "Assets/_Project/Scenes/UiGallery.unity";

        [MenuItem("Forge/UI Gallery")]
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
