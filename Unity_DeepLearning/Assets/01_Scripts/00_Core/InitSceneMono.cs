using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepLearning.SceneFlow
{
    /// <summary>
    /// InitScene 진입 시 Unity의 첫 프레임 초기화를 기다린 후 TitleScene으로 이동합니다.
    /// 씬에 컴포넌트를 직접 배치하지 않아도 자동으로 실행됩니다.
    /// </summary>
    public sealed class InitSceneMono : MonoBehaviour
    {
        private const string InitSceneName = "InitScene";
        private const string TitleSceneName = "TitleScene";
        private const float MinimumInitializationTime = 0.35f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWhenInitSceneLoads()
        {
            if (SceneManager.GetActiveScene().name != InitSceneName)
            {
                return;
            }

            var bootstrapObject = new GameObject(nameof(InitSceneMono));
            bootstrapObject.AddComponent<InitSceneMono>();
        }

        private IEnumerator Start()
        {
            SceneTransitionMono.SetBlackImmediately();

            // Unity 서브시스템, 씬 Awake/Start, UI 레이아웃이 안정화될 시간을 보장합니다.
            yield return null;
            yield return new WaitForEndOfFrame();

            AsyncOperation cleanup = Resources.UnloadUnusedAssets();
            float startedAt = Time.realtimeSinceStartup;
            while (!cleanup.isDone || Time.realtimeSinceStartup - startedAt < MinimumInitializationTime)
            {
                yield return null;
            }

            SceneTransitionMono.LoadScene(TitleSceneName);
        }
    }
}
