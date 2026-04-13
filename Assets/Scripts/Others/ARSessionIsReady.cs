using UnityEngine;
#if !TRAINAR_XR_HMD
using UnityEngine.XR.ARFoundation;
#endif
using System.Collections;

namespace Others
{
    /// <summary>
    /// Detects if the ARSession is loaded and deactivates the loading screen.
    /// On head-mounted XR builds (TRAINAR_XR_HMD) there is no ARFoundation
    /// session — the loading screen is dismissed as soon as the scene loads.
    /// </summary>
    public class ARSessionIsReady : MonoBehaviour
    {
        /// <summary>
        /// Reference to the loadingScreen.
        /// </summary>
        /// <value>Disabled when ARSession is ready.</value>
        [SerializeField]
        private GameObject loadingScreen;

        /// <summary>
        /// Checks if the ARSession is ready to disable the loading screen.
        /// </summary>
        void Update()
        {
#if TRAINAR_XR_HMD
            if (loadingScreen != null)
                loadingScreen.SetActive(false);
#else
            if (ARSession.state == ARSessionState.SessionTracking)
                loadingScreen.SetActive(false);
#endif
        }
    }
}
