using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yibi
{
    // M0 only: proves that the saved scene reached the player loop.
    public sealed class EnvironmentProbe : MonoBehaviour
    {
        public bool Started { get; private set; }

        private IEnumerator Start()
        {
            Started = true;
            Debug.Log("YIBI_M0_STARTED scene=" + SceneManager.GetActiveScene().name);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--m0-smoke") < 0)
                yield break;
            for (int i = 0; i < 30; i++) yield return null;
            Debug.Log("YIBI_M0_PLAYER_LOOP_OK frames=30");
            Application.Quit(0);
        }
    }
}
