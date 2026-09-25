#if NETCODE
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SoapSlide
{
    /// <summary>
    /// Server-spawned Netcode object: syncs SoapSlide phase / toasts / game over to clients (lobby + EdgeGap dedicated).
    /// </summary>
    public sealed class SoapSlideGameStateNetwork : NetworkBehaviour
    {
        public static SoapSlideGameStateNetwork Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
                Instance = this;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this)
                Instance = null;
            base.OnNetworkDespawn();
        }

        public void ServerSyncPhase(SoapSlidePhase phase, float phaseStartUnscaled, float phaseDuration, int round)
        {
            if (!IsServer) return;
            SyncPhaseClientRpc((byte)phase, phaseStartUnscaled, phaseDuration, round);
        }

        [ClientRpc]
        void SyncPhaseClientRpc(byte phase, float phaseStartUnscaled, float phaseDuration, int round)
        {
            var ctx = SoapSlideOnlineHudContext.Instance;
            if (ctx != null)
                ctx.ApplyPhase((SoapSlidePhase)phase, phaseStartUnscaled, phaseDuration, round);
            else
                SoapSlideOnlineHudContext.QueuePhaseIfNoInstance((SoapSlidePhase)phase, phaseStartUnscaled,
                    phaseDuration, round);
        }

        public void ServerShowToast(string msg)
        {
            if (!IsServer) return;
            ShowToastClientRpc(new FixedString128Bytes(msg));
        }

        [ClientRpc]
        void ShowToastClientRpc(FixedString128Bytes msg)
        {
            var hud = UnityEngine.Object.FindFirstObjectByType<SoapSlideHUD>();
            hud?.ShowToast(msg.ToString());
        }

        public void ServerShowGameOverFfa(int winnerSlot)
        {
            if (!IsServer) return;
            ShowGameOverFfaClientRpc(winnerSlot);
        }

        [ClientRpc]
        void ShowGameOverFfaClientRpc(int winnerSlot)
        {
            var hud = UnityEngine.Object.FindFirstObjectByType<SoapSlideHUD>();
            if (hud == null) return;
            int mySlot = GetLocalOwnedSlot();
            bool won = mySlot >= 0 && mySlot == winnerSlot;
            hud.ShowGameOver(won);
        }

        /// <param name="winTeam">0 or 1 when a team wins; -1 when draw (both eliminated).</param>
        public void ServerShowGameOverTeams(int winTeam, bool draw)
        {
            if (!IsServer) return;
            ShowGameOverTeamsClientRpc(winTeam, draw);
        }

        [ClientRpc]
        void ShowGameOverTeamsClientRpc(int winTeam, bool draw)
        {
            var hud = UnityEngine.Object.FindFirstObjectByType<SoapSlideHUD>();
            if (hud == null) return;
            int myTeam = GetLocalOwnedTeamIndex();
            bool won = !draw && myTeam >= 0 && myTeam == winTeam;
            hud.ShowGameOver(won, true, draw);
        }

        static int GetLocalOwnedSlot()
        {
            foreach (var net in UnityEngine.Object.FindObjectsByType<SoapSlidePlayerNet>(FindObjectsSortMode.None))
            {
                if (net.IsOwner && !net.IsBotPlayer.Value)
                    return net.SlotIndex.Value;
            }

            return -1;
        }

        static int GetLocalOwnedTeamIndex()
        {
            foreach (var net in UnityEngine.Object.FindObjectsByType<SoapSlidePlayerNet>(FindObjectsSortMode.None))
            {
                if (net.IsOwner && !net.IsBotPlayer.Value)
                    return net.TeamIndexNet.Value;
            }

            return -1;
        }
    }
}
#endif
