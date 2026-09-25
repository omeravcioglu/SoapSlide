using UnityEngine;
using UnityEngine.SceneManagement;
#if NETCODE
using Unity.Netcode;
#endif

namespace SoapSlide
{
    /// <summary>Loads the menu scene from the gameplay HUD.</summary>
    public sealed class SoapSlideMenuReturn : MonoBehaviour
    {
        [SerializeField] string _menuSceneName = "Menu";

        public void Configure(string sceneName)
        {
            if (!string.IsNullOrWhiteSpace(sceneName))
                _menuSceneName = sceneName.Trim();
        }

        public void GoToMainMenu()
        {
#if NETCODE
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
#endif
            if (!string.IsNullOrEmpty(_menuSceneName))
                SceneManager.LoadScene(_menuSceneName, LoadSceneMode.Single);
        }
    }
}
