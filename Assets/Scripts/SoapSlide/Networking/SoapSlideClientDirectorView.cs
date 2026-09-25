using UnityEngine;

namespace SoapSlide
{
    /// <summary>
    /// Client-only stand-in for <see cref="ISoapSlideMatchDirector"/> (camera / spectator) while the real director runs on the server.
    /// </summary>
    public sealed class SoapSlideClientDirectorView : MonoBehaviour, ISoapSlideMatchDirector
    {
        [SerializeField] float _arenaHalfExtents = 12f;
        SlideParticipant _human;

        public void Configure(SlideParticipant localHuman, float arenaHalfExtents)
        {
            _human = localHuman;
            _arenaHalfExtents = arenaHalfExtents;
        }

        public SoapSlidePhase Phase =>
            SoapSlideOnlineHudContext.Instance != null
                ? SoapSlideOnlineHudContext.Instance.CurrentPhase
                : SoapSlidePhase.Planning;

        public bool MatchIsActive =>
            Phase != SoapSlidePhase.GameOver && Phase != SoapSlidePhase.WaitingToStart;

        public float ArenaHalfExtents => _arenaHalfExtents;

        public SlideParticipant LocalHuman => _human;

        public bool IsLocalPlayerSpectating =>
            _human != null && _human.IsEliminated && Phase != SoapSlidePhase.GameOver;

        public SlideParticipant GetSpectateCameraTarget()
        {
#if NETCODE
            foreach (var net in Object.FindObjectsByType<SoapSlidePlayerNet>(FindObjectsSortMode.None))
            {
                var s = net.Slide;
                if (s != null && !s.IsEliminated)
                    return s;
            }
#endif
            return null;
        }

        public void NotifyFell(SlideParticipant p)
        {
        }
    }
}
