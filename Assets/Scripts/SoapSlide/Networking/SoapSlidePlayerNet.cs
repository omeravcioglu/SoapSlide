#if NETCODE
using Ignitives.MultiplayerEngine;
using SoapSlide.Lobby;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SoapSlide
{
    /// <summary>
    /// Netcode layer for a SoapSlide capsule — server-authoritative loop:
    /// clients send planning input (<see cref="SubmitPlanServerRpc"/>); only the server runs <see cref="Rigidbody"/> simulation;
    /// <see cref="Unity.Netcode.Components.NetworkTransform"/> + <see cref="Unity.Netcode.Components.NetworkRigidbody"/>
    /// replicate motion to clients with interpolation (see player prefab).
    /// Character id comes from lobby via <see cref="RuntimeSessionData"/> on the owning client.
    /// </summary>
    [RequireComponent(typeof(SlideParticipant))]
    public sealed class SoapSlidePlayerNet : NetworkBehaviour
    {
        SlideParticipant _slide;

        public NetworkVariable<int> SlotIndex { get; } =
            new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsBotPlayer { get; } =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<FixedString64Bytes> CharacterId { get; } =
            new NetworkVariable<FixedString64Bytes>(default,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<FixedString64Bytes> DisplayName { get; } =
            new NetworkVariable<FixedString64Bytes>(default,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public NetworkVariable<int> TeamIndexNet { get; } =
            new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public SlideParticipant Slide => _slide;

        Vector2 _lastSentXZ;
        int _lastSentForce = -1;
        float _nextPlanSendFixedTime;

        /// <summary>Call on server immediately after NetworkObject.Spawn.</summary>
        public void ServerInitMetadata(int slot, bool isBot, string displayName, string characterId, int teamIndex)
        {
            if (!IsServer) return;
            SlotIndex.Value = slot;
            IsBotPlayer.Value = isBot;
            DisplayName.Value = new FixedString64Bytes(displayName ?? "Player");
            CharacterId.Value = new FixedString64Bytes(characterId ?? "");
            TeamIndexNet.Value = teamIndex;
            if (isBot)
                _slide?.ApplyCharacterFromIdServer(characterId ?? "bot");
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _slide = GetComponent<SlideParticipant>();
            if (_slide == null) return;

            if (!IsServer)
                _slide.InitReplicaFromNetwork(this);

            if (IsOwner && !IsBotPlayer.Value)
                _slide.ApplyNetworkLocalHuman(true);

            if (IsOwner && !IsServer && !IsBotPlayer.Value)
            {
                var cid = RuntimeSessionData.Instance != null ? RuntimeSessionData.Instance.SelectedCharacterId : "";
                if (string.IsNullOrEmpty(cid) && GameContentLibrary.Instance != null &&
                    GameContentLibrary.Instance.CharacterData is { Count: > 0 } list)
                    cid = list[0].CharacterId;
                SubmitProfileServerRpc(new FixedString64Bytes(cid ?? ""),
                    new FixedString64Bytes(GetLocalPlayerName()));
            }
        }

        static string GetLocalPlayerName()
        {
            var lobby = LobbySession.Instance;
            if (lobby != null && !string.IsNullOrWhiteSpace(lobby.PlayerName))
                return lobby.PlayerName.Trim();
            return "Player";
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void SubmitProfileServerRpc(FixedString64Bytes characterId, FixedString64Bytes displayName)
        {
            CharacterId.Value = characterId;
            DisplayName.Value = displayName;
            _slide?.ApplyCharacterFromIdServer(characterId.ToString());
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void SubmitPlanServerRpc(float dirX, float dirZ, int forceLevel)
        {
            _slide?.SetServerPlanXZ(dirX, dirZ, forceLevel);
        }

        /// <summary>After local input runs in Update, push the latest plan on fixed steps so it lines up with server physics.</summary>
        void FixedUpdate()
        {
            if (!IsOwner || IsServer || IsBotPlayer.Value || _slide == null) return;
            if (SoapSlideOnlineHudContext.Instance != null &&
                SoapSlideOnlineHudContext.Instance.CurrentPhase != SoapSlidePhase.Planning)
                return;

            var dir = _slide.PlanDirectionWorld;
            int force = _slide.PlanForceLevel;
            var xz = new Vector2(dir.x, dir.z);
            if (Time.fixedUnscaledTime < _nextPlanSendFixedTime) return;
            if (Mathf.Approximately(xz.x, _lastSentXZ.x) && Mathf.Approximately(xz.y, _lastSentXZ.y) && force == _lastSentForce)
                return;
            _lastSentXZ = xz;
            _lastSentForce = force;
            _nextPlanSendFixedTime = Time.fixedUnscaledTime + 0.05f;
            SubmitPlanServerRpc(dir.x, dir.z, force);
        }

        /// <summary>Notify remote machines that this participant was eliminated (server already ran <see cref="SlideParticipant.MarkEliminated"/>).</summary>
        public void ReplicateEliminatedVisualsToClients()
        {
            if (!IsServer) return;
            FellVisualClientRpc();
        }

        [ClientRpc]
        void FellVisualClientRpc()
        {
            if (IsServer) return;
            _slide?.ApplyEliminatedVisualOnly();
        }

        /// <summary>Owner: signal ready so the server can begin planning once everyone has tapped (host calls server path directly).</summary>
        public void ClientRequestMatchStartReady()
        {
            if (!IsOwner || IsBotPlayer.Value) return;
            if (IsServer)
            {
                var dir = UnityEngine.Object.FindFirstObjectByType<SoapSlideRoundDirector>();
                dir?.RegisterOnlineHumanReady(NetworkManager.Singleton.LocalClientId);
                return;
            }

            MatchStartReadyServerRpc();
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        void MatchStartReadyServerRpc()
        {
            if (!IsServer) return;
            var dir = UnityEngine.Object.FindFirstObjectByType<SoapSlideRoundDirector>();
            dir?.RegisterOnlineHumanReady(OwnerClientId);
        }
    }
}
#endif
