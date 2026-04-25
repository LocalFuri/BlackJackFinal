using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blackjack
{
    /// <summary>
    /// Quits the application when the right mouse button is pressed.
    /// Plays an exit sound via BlackjackGame before quitting.
    /// In the Editor, stops Play Mode instead.
    /// </summary>
    public class QuitOnRightClick : MonoBehaviour
    {
        [SerializeField] private BlackjackGame gameManager;

        private bool _isQuitting;

        private void Update()
        {
            if (_isQuitting) return;
            if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
                StartCoroutine(PlaySoundThenQuit());
        }

        private IEnumerator PlaySoundThenQuit()
        {
            _isQuitting = true;

            if (gameManager != null)
            {
                float length = gameManager.PlayExitSound();
                yield return new WaitForSeconds(length);
            }

            Quit();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(); //will only excecute if in build mode 
#endif
        }
    }
}
