using UnityEngine;

namespace SoapSlide
{
    /// <summary>
    /// Client-side phase/timer context driven by <see cref="SoapSlideGameStateNetwork"/> RPCs.
    /// </summary>
    public sealed class SoapSlideOnlineHudContext : MonoBehaviour
    {
        public static SoapSlideOnlineHudContext Instance { get; private set; }

        static SoapSlidePhase s_pendingPhase = SoapSlidePhase.Planning;
        static float s_pendingPhaseStart;
        static float s_pendingPhaseDuration = 1f;
        static int s_pendingRound = 1;
        static bool s_hasPendingPhase;

        SoapSlideHUD _hud;
        SlideParticipant _localSlide;

        public SoapSlidePhase CurrentPhase { get; private set; } = SoapSlidePhase.Planning;
        float _phaseStartUnscaled;
        float _phaseDuration = 1f;
        int _round = 1;

        public static void QueuePhaseIfNoInstance(SoapSlidePhase phase, float phaseStartUnscaled, float phaseDuration,
            int round)
        {
            if (Instance != null) return;
            s_pendingPhase = phase;
            s_pendingPhaseStart = phaseStartUnscaled;
            s_pendingPhaseDuration = phaseDuration;
            s_pendingRound = round;
            s_hasPendingPhase = true;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (s_hasPendingPhase)
            {
                s_hasPendingPhase = false;
                ApplyPhase(s_pendingPhase, s_pendingPhaseStart, s_pendingPhaseDuration, s_pendingRound);
            }
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Bind(SoapSlideHUD hud, SlideParticipant localSlide)
        {
            _hud = hud;
            _localSlide = localSlide;
            RefreshMatchStartGateFromPhase();
            SyncLocalPlanningVisualsFromPhase(CurrentPhase);
        }

        public void SetLocalSlide(SlideParticipant localSlide) => _localSlide = localSlide;

        public void ApplyPhase(SoapSlidePhase phase, float phaseStartUnscaled, float phaseDuration, int round)
        {
            CurrentPhase = phase;
            _phaseStartUnscaled = phaseStartUnscaled;
            _phaseDuration = Mathf.Max(0.01f, phaseDuration);
            _round = round;
            RefreshMatchStartGateFromPhase();
            SyncLocalPlanningVisualsFromPhase(phase);
        }

        /// <summary>
        /// Server calls <see cref="SlideParticipant.SetPlanningFrozen"/> on simulating peers; thin clients must mirror arrow visibility from phase RPCs.
        /// </summary>
        void SyncLocalPlanningVisualsFromPhase(SoapSlidePhase phase)
        {
            if (_localSlide == null) return;
            if (!SoapSlideNetUtils.IsNetcodeActive || SoapSlideNetUtils.ShouldRunGameSimulation)
                return;

            if (phase == SoapSlidePhase.Planning)
                _localSlide.SetPlanningFrozen(true);
            else
                _localSlide.SetPlanningFrozen(false);
        }

        void RefreshMatchStartGateFromPhase()
        {
            if (_hud == null) return;
#if NETCODE
            if (CurrentPhase == SoapSlidePhase.WaitingToStart && _localSlide != null)
            {
                var net = _localSlide.GetComponent<SoapSlidePlayerNet>();
                if (net != null && net.IsOwner && !net.IsBotPlayer.Value)
                {
                    _hud.SetMatchStartGate(true, () => net.ClientRequestMatchStartReady());
                    return;
                }
            }
#endif
            _hud.SetMatchStartGate(false);
        }

        void Update()
        {
            if (_hud == null) return;

            float elapsed = Time.unscaledTime - _phaseStartUnscaled;
            if (CurrentPhase == SoapSlidePhase.Planning)
            {
                var watch = _localSlide != null && !_localSlide.IsEliminated ? _localSlide : null;
                _hud.SetPlanning(_phaseDuration - elapsed, _localSlide, watch);
            }
            else if (CurrentPhase == SoapSlidePhase.Action)
            {
                if (_localSlide != null && _localSlide.IsEliminated)
                    _hud.SetSpectatorAction("Spectating");
                else
                    _hud.SetActionHud();
            }
            else if (CurrentPhase == SoapSlidePhase.BetweenRounds)
                _hud.SetPhaseLabel($"Round {_round}");
            else if (CurrentPhase == SoapSlidePhase.WaitingToStart)
            {
#if NETCODE
                var net = _localSlide != null ? _localSlide.GetComponent<SoapSlidePlayerNet>() : null;
                if (net != null && net.IsOwner && !net.IsBotPlayer.Value)
                    _hud.SetLobbyWaiting("SoapSlide", "Tap Start when you are ready (everyone must tap).");
                else
                    _hud.SetLobbyWaiting("SoapSlide", "Waiting for other players…");
#else
                _hud.SetLobbyWaiting("SoapSlide", "Waiting…");
#endif
            }
        }
    }
}
