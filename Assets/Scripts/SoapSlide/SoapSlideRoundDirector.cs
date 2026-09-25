using System.Collections.Generic;
using UnityEngine;

namespace SoapSlide
{
    [DefaultExecutionOrder(-20)]
    public sealed class SoapSlideRoundDirector : MonoBehaviour, ISoapSlideMatchDirector
    {
        [Header("Round timing")]
        [SerializeField] float _planningSeconds = 8f;
        [SerializeField] float _maxActionSeconds = 50f;
        [SerializeField] float _settleSpeedThreshold = 0.55f;
        [SerializeField] float _settleHoldSeconds = 0.28f;
        [SerializeField] float _betweenRoundPause = 1f;

        [Header("Physics")]
        [SerializeField] float _maxImpulse = 14f;
        [Tooltip("Ignore 'everyone calm' until this many seconds into Action — avoids ending the round before first physics step.")]
        [SerializeField] float _minActionSecondsBeforeCalmEnd = 0.25f;

        [Header("Arena")]
        [SerializeField] float _spawnRingRadius = 5.5f;
        [SerializeField] float _arenaHalfExtents = 12f;

        public float PlanningSeconds => _planningSeconds;
        public float ArenaHalfExtents => _arenaHalfExtents;
        public SoapSlidePhase Phase { get; private set; } = SoapSlidePhase.Planning;
        public bool MatchIsActive => true;
        public int CurrentRound { get; private set; } = 1;
        public int CurrentRoundSeed { get; private set; }

        readonly List<SlideParticipant> _alive = new List<SlideParticipant>();
        SoapSlideHUD _hud;
        SlideParticipant _human;
        SoapSlideGameType _gameType = SoapSlideGameType.EveryoneAlone;

        float _phaseStartTime;
        float _settleTimer;
        int _aliveCountWhenActionBegan;

        readonly HashSet<ulong> _expectedOnlineClientIds = new HashSet<ulong>();
        readonly HashSet<ulong> _readyOnlineClientIds = new HashSet<ulong>();
        bool _onlineStartGateActive;

        public IReadOnlyList<SlideParticipant> AliveParticipants => _alive;
        public float PlanningTimeRemaining => Phase == SoapSlidePhase.Planning ? Mathf.Max(0f, _planningSeconds - (Time.unscaledTime - _phaseStartTime)) : 0f;
        public float ActionTimeRemaining => Phase == SoapSlidePhase.Action ? Mathf.Max(0f, _maxActionSeconds - (Time.unscaledTime - _phaseStartTime)) : 0f;

        public int LastWinnerSlot { get; private set; } = -1;
        public SlideParticipant LocalHuman => _human;

        public bool IsLocalPlayerSpectating =>
            _human != null && _human.IsEliminated && Phase != SoapSlidePhase.GameOver;

        public SlideParticipant GetSpectateCameraTarget()
        {
            foreach (var p in _alive)
            {
                if (p != null && !p.IsEliminated)
                    return p;
            }

            return null;
        }

        public void Configure(SlideParticipant human, List<SlideParticipant> everyone, SoapSlideHUD hud,
            SoapSlideGameType gameType = SoapSlideGameType.EveryoneAlone)
        {
            _human = human;
            _alive.Clear();
            _alive.AddRange(everyone);
            _hud = hud;
            _gameType = gameType;
        }


        public void SetArenaFromBootstrap(float arenaHalfExtents, float spawnRingRadius)
        {
            _arenaHalfExtents = arenaHalfExtents;
            _spawnRingRadius = spawnRingRadius;
        }

        public void NotifyFell(SlideParticipant p)
        {
            if (Phase != SoapSlidePhase.Action || p.IsEliminated) return;
#if NETCODE
            p.GetComponent<SoapSlidePlayerNet>()?.ReplicateEliminatedVisualsToClients();
#endif
            p.MarkEliminated();
            _alive.Remove(p);
            if (p == _human)
            {
                _hud?.SetMainMenuVisible(true);
            }

            string msg = p == _human ? "You fell!" : "A rival fell.";
            _hud?.ShowToast(msg);
#if NETCODE
            SoapSlideGameStateNetwork.Instance?.ServerShowToast(msg);
#endif
        }

        public void StartMatch()
        {
            CurrentRoundSeed = Random.Range(1, int.MaxValue);
            BeginPlanningPhase();
        }

        /// <summary>Server online: wait until each human client signals ready before planning countdown.</summary>
        public void ArmOnlineReadyGate(ulong[] sortedHumanClientIds)
        {
            _expectedOnlineClientIds.Clear();
            _readyOnlineClientIds.Clear();
            if (sortedHumanClientIds != null)
            {
                foreach (var id in sortedHumanClientIds)
                    _expectedOnlineClientIds.Add(id);
            }

            _onlineStartGateActive = _expectedOnlineClientIds.Count > 0;
            Phase = SoapSlidePhase.WaitingToStart;
            _phaseStartTime = Time.unscaledTime;
            foreach (var p in _alive)
                p.SetPlanningFrozen(true);
#if NETCODE
            PushNetPhase(0f);
#endif
        }

        public void RegisterOnlineHumanReady(ulong clientId)
        {
            if (!SoapSlideNetUtils.ShouldRunGameSimulation) return;
            if (!_onlineStartGateActive || Phase != SoapSlidePhase.WaitingToStart) return;
            if (!_expectedOnlineClientIds.Contains(clientId)) return;
            _readyOnlineClientIds.Add(clientId);
            if (_readyOnlineClientIds.Count < _expectedOnlineClientIds.Count)
                return;
            _onlineStartGateActive = false;
            _expectedOnlineClientIds.Clear();
            _readyOnlineClientIds.Clear();
            CurrentRoundSeed = Random.Range(1, int.MaxValue);
            BeginPlanningPhase();
        }

        void FixedUpdate()
        {
            if (!SoapSlideNetUtils.ShouldRunGameSimulation) return;
            if (Phase == SoapSlidePhase.GameOver)
                return;
            if (Phase == SoapSlidePhase.WaitingToStart)
                return;

            float elapsed = Time.unscaledTime - _phaseStartTime;

            if (Phase == SoapSlidePhase.Planning)
            {
                foreach (var p in _alive)
                    p.TickPlanning(_planningSeconds);

                var watch = (_human != null && _human.IsEliminated) ? GetSpectateCameraTarget() : _human;
                _hud?.SetPlanning(_planningSeconds - elapsed, _human, watch);
                
                if (elapsed >= _planningSeconds)
                    CommitAndRunAction();
            }
            else if (Phase == SoapSlidePhase.Action)
            {
                if (_human != null && _human.IsEliminated)
                {
                    var w = GetSpectateCameraTarget();
                    _hud?.SetSpectatorAction(w != null ? w.DisplayName : "");
                }

                if (TryEarlyEndAction())
                    return;
                                if (elapsed >= _maxActionSeconds)
                    EndRound();
            }
            else if (Phase == SoapSlidePhase.BetweenRounds)
            {
                                if (elapsed >= _betweenRoundPause)
                    BeginPlanningPhaseAfterBetween();
            }
        }

        bool TryEarlyEndAction()
        {
            if (_gameType == SoapSlideGameType.Teams4v4)
            {
                int t0 = GetAliveOnTeam(0), t1 = GetAliveOnTeam(1);
                // One team fully eliminated while the other still has survivors (not "both zero" from FFA team indices).
                if ((t0 == 0 && t1 > 0) || (t1 == 0 && t0 > 0))
                {
                    EndRound();
                    return true;
                }
            }
            else if (GetAliveCount() <= 1 && _aliveCountWhenActionBegan > 1)
            {
                EndRound();
                return true;
            }

            if (Time.unscaledTime - _phaseStartTime < _minActionSecondsBeforeCalmEnd)
                return false;

            if (AllParticipantsCalm())
            {
                _settleTimer += Time.fixedUnscaledDeltaTime;
                if (_settleTimer >= _settleHoldSeconds)
                {
                    EndRound();
                    return true;
                }
            }
            else
                _settleTimer = 0f;

            return false;
        }

        bool AllParticipantsCalm()
        {
            float threshSq = _settleSpeedThreshold * _settleSpeedThreshold;
            foreach (var p in _alive)
            {
                if (p.IsEliminated || p.Body == null) continue;
                var rb = p.Body;
                if (rb.IsSleeping())
                    continue;
                if (rb.linearVelocity.sqrMagnitude > threshSq)
                    return false;
            }

            return _alive.Count > 0;
        }

        int GetAliveCount()
        {
            int n = 0;
            foreach (var p in _alive)
            {
                if (!p.IsEliminated) n++;
            }

            return n;
        }

        int GetAliveOnTeam(int team)
        {
            int n = 0;
            foreach (var p in _alive)
            {
                if (p.IsEliminated || p.TeamIndex != team) continue;
                n++;
            }

            return n;
        }

        void CommitAndRunAction()
        {
            Phase = SoapSlidePhase.Action;
            _phaseStartTime = Time.unscaledTime;
            _aliveCountWhenActionBegan = GetAliveCount();
                        _hud?.SetPhaseLabel("Slide!");
            if (_human != null && _human.IsEliminated)
            {
                var w = GetSpectateCameraTarget();
                _hud?.SetSpectatorAction(w != null ? w.DisplayName : "");
            }
            else
                _hud?.SetActionHud();

            foreach (var p in _alive)
            {
                p.SetPlanningFrozen(false);
                p.ApplyCommittedImpulse(_maxImpulse);
            }

            _settleTimer = 0f;
#if NETCODE
            PushNetPhase(_maxActionSeconds);
#endif
        }

        void EndRound()
        {
            LastWinnerSlot = -1;

            if (_gameType == SoapSlideGameType.Teams4v4)
            {
                int a = GetAliveOnTeam(0), b = GetAliveOnTeam(1);
                if (a == 0 && b == 0)
                {
                    Phase = SoapSlidePhase.GameOver;
                    _hud?.ShowGameOver(false, true, true);
#if NETCODE
                    PushNetPhaseGameOver();
                    SoapSlideGameStateNetwork.Instance?.ServerShowGameOverTeams(-1, true);
#endif
                    return;
                }

                if (a == 0 || b == 0)
                {
                    int winTeam = a > 0 ? 0 : 1;
                    foreach (var p in _alive)
                    {
                        if (!p.IsEliminated && p.TeamIndex == winTeam)
                        {
                            LastWinnerSlot = p.SlotIndex;
                            break;
                        }
                    }

                    bool won = _human != null && _human.TeamIndex == winTeam;
                    Phase = SoapSlidePhase.GameOver;
                    _hud?.ShowGameOver(won, true, false);
#if NETCODE
                    PushNetPhaseGameOver();
                    SoapSlideGameStateNetwork.Instance?.ServerShowGameOverTeams(winTeam, false);
#endif
                    return;
                }

                StartBetweenRounds();
                return;
            }

            if (GetAliveCount() <= 1)
            {
                Phase = SoapSlidePhase.GameOver;
                SlideParticipant sole = null;
                foreach (var p in _alive)
                {
                    if (p != null && !p.IsEliminated)
                    {
                        sole = p;
                        break;
                    }
                }

                LastWinnerSlot = sole != null ? sole.SlotIndex : -1;
                bool won = sole != null && _human != null && sole == _human && !_human.IsEliminated;
                _hud?.ShowGameOver(won);
#if NETCODE
                PushNetPhaseGameOver();
                SoapSlideGameStateNetwork.Instance?.ServerShowGameOverFfa(LastWinnerSlot);
#endif
                return;
            }

            StartBetweenRounds();
        }

        void StartBetweenRounds()
        {
            Phase = SoapSlidePhase.BetweenRounds;
            CurrentRound++;
            CurrentRoundSeed = Random.Range(1, int.MaxValue);
            _phaseStartTime = Time.unscaledTime;
            _hud?.SetPhaseLabel($"Round {CurrentRound}");
#if NETCODE
            PushNetPhase(_betweenRoundPause);
#endif
        }

        void BeginPlanningPhaseAfterBetween()
        {
            BeginPlanningPhase();
        }

        void BeginPlanningPhase()
        {
            Phase = SoapSlidePhase.Planning;
            _phaseStartTime = Time.unscaledTime;
            foreach (var p in _alive)
            {
                p.SetPlanningFrozen(true);
                p.BeginPlanningRound(CurrentRoundSeed, _alive);
            }

            _hud?.SetPhaseLabel("Plan your slide");
#if NETCODE
            PushNetPhase(_planningSeconds);
#endif
        }

#if NETCODE
        void PushNetPhase(float duration)
        {
            if (!SoapSlideNetUtils.ShouldRunGameSimulation) return;
            SoapSlideGameStateNetwork.Instance?.ServerSyncPhase(Phase, _phaseStartTime, duration, CurrentRound);
        }

        void PushNetPhaseGameOver()
        {
            if (!SoapSlideNetUtils.ShouldRunGameSimulation) return;
            SoapSlideGameStateNetwork.Instance?.ServerSyncPhase(SoapSlidePhase.GameOver, Time.unscaledTime, 0f,
                CurrentRound);
        }
#endif
    }
}
