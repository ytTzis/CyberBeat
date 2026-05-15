using UnityEngine;
using UnityEngine.SceneManagement;

public class FirstSceneButton : MonoBehaviour
{
    [SerializeField] private string targetSceneName = "BackGroundScene";

    private void Awake()
    {
        FirstSceneHyperatePanelController.EnsureExists();
    }

    public void StartGame()
    {
        EnsureHeartRateMonitoringStarted();
        SceneManager.LoadScene(targetSceneName);
    }

    public void QuitGame()
    {
        if (string.Equals(gameObject.name, "Key", System.StringComparison.OrdinalIgnoreCase))
        {
            FirstSceneHyperatePanelController.ShowPanel();
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void EnsureHeartRateMonitoringStarted()
    {
        if (FindFirstObjectByType<hyperateSocket>() == null)
        {
            GameObject socketObject = new GameObject("hyperateSocket");
            hyperateSocket socket = socketObject.AddComponent<hyperateSocket>();

            // Kick off the websocket immediately after the player presses Start.
            socket.Connect();
        }

        if (HeartRateSimulator.Instance == null)
        {
            GameObject simulatorObject = new GameObject("HeartRateSimulator");
            simulatorObject.AddComponent<HeartRateSimulator>();
        }
    }
}
