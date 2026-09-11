using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class TitleMono : MonoBehaviour
{
    private const string GameSceneName = "GameScene";
    private const string ServerSceneName = "ServerScene";

    public void MoveToGameScene()
    {
        SceneManager.LoadScene(GameSceneName);
    }

    public void MoveToServerScene()
    {
        SceneManager.LoadScene(ServerSceneName);
    }
}
