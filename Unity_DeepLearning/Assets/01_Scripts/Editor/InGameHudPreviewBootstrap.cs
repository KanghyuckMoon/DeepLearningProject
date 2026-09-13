#if UNITY_EDITOR
using DeepLearning.GameClient;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeepLearning.EditorTools
{
    /// <summary>
    /// Ensures that an already-open GameScene receives the editable HUD preview
    /// immediately after scripts reload, without requiring Play Mode.
    /// </summary>
    [InitializeOnLoad]
    internal static class InGameHudPreviewBootstrap
    {
        static InGameHudPreviewBootstrap()
        {
            EditorApplication.delayCall += EnsurePreview;
        }

        private static void EnsurePreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            GameClientPresenterMono[] presenters = Object.FindObjectsByType<GameClientPresenterMono>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (GameClientPresenterMono presenter in presenters)
            {
                if (!presenter.gameObject.scene.IsValid() || presenter.gameObject.scene.name != "GameScene")
                {
                    continue;
                }

                InGameHudViewMono hud = presenter.GetComponent<InGameHudViewMono>();
                if (hud == null)
                {
                    hud = Undo.AddComponent<InGameHudViewMono>(presenter.gameObject);
                }

                presenter.InitializeHudPreview(hud);
                EditorSceneManager.MarkSceneDirty(presenter.gameObject.scene);
            }
        }
    }
}
#endif
