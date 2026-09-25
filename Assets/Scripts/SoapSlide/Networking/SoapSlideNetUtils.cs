#if NETCODE
using Unity.Netcode;
#endif
using UnityEngine;

namespace SoapSlide
{
    /// <summary>
    /// Detects Netcode dedicated / client vs offline SoapSlide.
    /// </summary>
    public static class SoapSlideNetUtils
    {
        public static bool IsNetcodeActive =>
#if NETCODE
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
#else
            false;
#endif

        public static bool IsServer =>
#if NETCODE
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
#else
            true;
#endif

        /// <summary>Physics, falls, and round director FixedUpdate run only here when online.</summary>
        public static bool ShouldRunGameSimulation => !IsNetcodeActive || IsServer;
    }
}
