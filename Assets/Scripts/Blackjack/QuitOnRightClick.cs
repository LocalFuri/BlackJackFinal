using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blackjack
{
    /// <summary>
    /// Quits the application when the right mouse button is pressed.
    /// Plays an exit sound clip before quitting.
    /// In the Editor, stops Play Mode instead.
    /// </summary>
    public class QuitOnRightClick : MonoBehaviour
    {
        [SerializeField] private AudioClip exitSound;
        [SerializeField] private AudioSource audioSource;

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

            if (exitSound != null && audioSource != null)
            {
                audioSource.PlayOneShot(exitSound);
                yield return new WaitForSeconds(exitSound.length);
            }

            Quit();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
