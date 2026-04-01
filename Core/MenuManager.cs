using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _project.Scripts.Core
{
    public class MenuManager : MonoBehaviour
    {
        public void StartMainGame()
        {
            StartCoroutine(LoadScene("Main"));
        }

        private static IEnumerator LoadScene(string sceneName)
        {
            var asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (asyncLoad is { isDone: false }) yield return null;
        }
    }
}
