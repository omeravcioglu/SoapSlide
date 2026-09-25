using System.Collections;
using System.Collections.Generic;
#if NETCODE
using System.Linq;
using Ignitives.MultiplayerEngine;
using Unity.Netcode;
#endif
using SoapSlide.Lobby;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SoapSlide
{
    [DefaultExecutionOrder(-200)]
    public sealed class SoapSlideGameBootstrap : MonoBehaviour
    {
        const int MatchSize = 8;

        [SerializeField] float _platformHalfExtents = 12f;
        [SerializeField] float _spawnRingRadius = 5.2f;
        [SerializeField] string _menuSceneName = "Menu";
        [SerializeField] GameObject _localPlayerIndicatorPrefab;

#if NETCODE
        [Tooltip("Optional; if null, loads Resources/SoapSlide/SoapSlideNetworkPlayer")]
        [SerializeField]
        GameObject _networkPlayerPrefab;

        [Tooltip("Optional; if null, loads Resources/SoapSlide/SoapSlideGameStateRoot")]
        [SerializeField]
        GameObject _gameStateNetPrefab;

        const string ResNetPlayer = "SoapSlide/SoapSlideNetworkPlayer";
        const string ResGameState = "SoapSlide/SoapSlideGameStateRoot";

        [Tooltip("Dedicated server: max seconds to wait for scene sync / players before starting. Unfilled arena slots become AI.")]
        [SerializeField]
        float _serverHumanConnectTimeoutSeconds = 60f;

        [Tooltip("After Netcode reports all players loaded, wait until at least this many clients are connected before spawning SoapSlide (0 = skip).")]
        [SerializeField]
        int _minimumHumanClientsBeforeArenaSpawn = 1;

        [Tooltip("Server: real seconds to wait after spawning network players before arming the Start gate — lets ownership/spawn replicate to clients.")]
        [SerializeField]
        float _serverSpawnSettleSeconds = 1.5f;

        bool _serverSoapSlideAllPlayersLoadedGate;
#endif

        void Awake()
        {
#if NETCODE
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                SessionManagerBase.OnAllPlayersLoaded += OnServerSoapSlideAllPlayersLoaded;
#endif
            StartCoroutine(BootstrapRoutine());
        }

#if NETCODE
        void OnDestroy()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
                SessionManagerBase.OnAllPlayersLoaded -= OnServerSoapSlideAllPlayersLoaded;
        }

        void OnServerSoapSlideAllPlayersLoaded()
        {
            _serverSoapSlideAllPlayersLoadedGate = true;
        }
#endif

        IEnumerator BootstrapRoutine()
        {
#if NETCODE
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsServer)
                {
                    yield return ServerOnlineRoutine();
                    yield break;
                }

                if (NetworkManager.Singleton.IsClient)
                {
                    yield return ClientOnlineRoutine();
                    yield break;
                }
            }
#endif
            yield return OfflineRoutine();
        }

#if NETCODE
        IEnumerator ServerOnlineRoutine()
        {
            var t = 0f;
            var limit = Mathf.Max(5f, _serverHumanConnectTimeoutSeconds);
            while (!_serverSoapSlideAllPlayersLoadedGate && t < limit)
            {
                t += Time.deltaTime;
                yield return null;
            }

            if (!_serverSoapSlideAllPlayersLoadedGate)
                Debug.LogWarning(
                    $"[SoapSlideGameBootstrap] After {limit:0.#}s, OnAllPlayersLoaded did not fire — starting match with current connections + AI fill.");

            yield return WaitForMinimumHumanClientsBeforeSpawn();

            HidePersistentMenuUiFromPreviousScenes();

            var playerPrefab = _networkPlayerPrefab != null
                ? _networkPlayerPrefab
                : Resources.Load<GameObject>(ResNetPlayer);
            var statePrefab = _gameStateNetPrefab != null
                ? _gameStateNetPrefab
                : Resources.Load<GameObject>(ResGameState);
            if (playerPrefab == null || statePrefab == null)
            {
                Debug.LogError(
                    "[SoapSlideGameBootstrap] Missing SoapSlide network prefabs. Add Resources/SoapSlide/SoapSlideNetworkPlayer and SoapSlideGameStateRoot, or assign serialized prefabs.");
                yield break;
            }

            var slipIce = new PhysicsMaterial("SoapSlip")
            {
                dynamicFriction = 0.26f,
                staticFriction = 0.3f,
                bounciness = 0.08f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average
            };

            var bodyMat = new PhysicsMaterial("SoapBody")
            {
                dynamicFriction = 0.26f,
                staticFriction = 0.3f,
                bounciness = 0.62f,
                frictionCombine = PhysicsMaterialCombine.Multiply,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            BuildPlatform(_platformHalfExtents, slipIce);
            BuildFallZone();

            var stateGo = Instantiate(statePrefab);
            stateGo.GetComponent<NetworkObject>().Spawn(false);

            var directorGo = new GameObject("SoapSlideDirector");
            var localDirector = directorGo.AddComponent<SoapSlideRoundDirector>();
            localDirector.SetArenaFromBootstrap(_platformHalfExtents, _spawnRingRadius);

            // Re-sample after wait: up to 8 humans; any remaining slots are AI (bots).
            var sortedClients = NetworkManager.Singleton.ConnectedClientsIds.OrderBy(id => id).ToArray();
            int humanCount = Mathf.Min(MatchSize, sortedClients.Length);
            if (humanCount < MatchSize)
                Debug.Log(
                    $"[SoapSlideGameBootstrap] {humanCount} human(s) connected — filling {MatchSize - humanCount} slot(s) with AI.");

            var nameRng = new System.Random(
                unchecked((int)(System.DateTime.UtcNow.Ticks & 0x7FFFFFFF)) ^ (humanCount * 13 + 17));

            var lobby = LobbySession.Instance;
            var gameType = lobby != null ? lobby.GameType : SoapSlideGameType.EveryoneAlone;
            if (gameType != SoapSlideGameType.EveryoneAlone && gameType != SoapSlideGameType.Teams4v4)
                gameType = SoapSlideGameType.EveryoneAlone;

            var everyone = new List<SlideParticipant>(MatchSize);
            for (int i = 0; i < humanCount; i++)
            {
                float ang = (Mathf.PI * 2f * i) / MatchSize;
                var go = Instantiate(playerPrefab,
                    new Vector3(Mathf.Cos(ang) * _spawnRingRadius, 1.1f, Mathf.Sin(ang) * _spawnRingRadius),
                    Quaternion.identity);
                int team = gameType == SoapSlideGameType.Teams4v4 ? (i < 4 ? 0 : 1) : -1;
                Color c = PickParticipantColor(i, true, gameType, new Color(0.2f, 0.55f, 1f), 0);
                string nm = $"Player{i + 1}";
                var slide = go.GetComponent<SlideParticipant>();
                slide.Init(localDirector, false, true, c, bodyMat, nm, i, team);
                var no = go.GetComponent<NetworkObject>();
                no.SpawnAsPlayerObject(sortedClients[i]);
                var net = go.GetComponent<SoapSlidePlayerNet>();
                net.ServerInitMetadata(i, false, nm, "", team);
                everyone.Add(slide);
            }

            for (int i = humanCount; i < MatchSize; i++)
            {
                float ang = (Mathf.PI * 2f * i) / MatchSize;
                var go = Instantiate(playerPrefab,
                    new Vector3(Mathf.Cos(ang) * _spawnRingRadius, 1.1f, Mathf.Sin(ang) * _spawnRingRadius),
                    Quaternion.identity);
                int team = gameType == SoapSlideGameType.Teams4v4 ? (i < 4 ? 0 : 1) : -1;
                Color c = PickParticipantColor(i, false, gameType, new Color(0.2f, 0.55f, 1f), 0);
                string nm = BotNameGenerator.Next(nameRng);
                var slide = go.GetComponent<SlideParticipant>();
                slide.Init(localDirector, false, false, c, bodyMat, nm, i, team);
                var no = go.GetComponent<NetworkObject>();
                no.Spawn(false);
                var net = go.GetComponent<SoapSlidePlayerNet>();
                net.ServerInitMetadata(i, true, nm, "bot", team);
                everyone.Add(slide);
            }

            if (_serverSpawnSettleSeconds > 0f)
                yield return new WaitForSecondsRealtime(_serverSpawnSettleSeconds);

            var localHuman = everyone.Count > 0 ? everyone[0] : null;
            localDirector.Configure(localHuman, everyone, null, gameType);
            BindFallZone(localDirector);
            if (humanCount > 0)
            {
                var gateIds = new ulong[humanCount];
                for (int i = 0; i < humanCount; i++)
                    gateIds[i] = sortedClients[i];
                localDirector.ArmOnlineReadyGate(gateIds);
            }
            else
                localDirector.StartMatch();

            if (NetworkManager.Singleton.IsClient)
                yield return ClientSoapSlidePresentationRoutine();

            yield break;
        }

        /// <summary>
        /// Server builds the soap platform locally; it is not a networked object, so pure clients need the same mesh for visuals (and mouse aim feel).
        /// </summary>
        void TryBuildVisualArenaForThinClient()
        {
#if NETCODE
            if (NetworkManager.Singleton == null) return;
            if (!NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) return;
            if (GameObject.Find("SoapPlatform") != null) return;

            var slipIce = new PhysicsMaterial("SoapSlip")
            {
                dynamicFriction = 0.26f,
                staticFriction = 0.3f,
                bounciness = 0.08f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average
            };

            BuildPlatform(_platformHalfExtents, slipIce);
#endif
        }

        IEnumerator WaitForMinimumHumanClientsBeforeSpawn()
        {
            var min = Mathf.Max(0, _minimumHumanClientsBeforeArenaSpawn);
            if (min <= 0)
                yield break;

            float deadline = Time.unscaledTime + Mathf.Max(15f, _serverHumanConnectTimeoutSeconds);
            while (Time.unscaledTime < deadline)
            {
                if (NetworkManager.Singleton != null &&
                    NetworkManager.Singleton.IsServer &&
                    NetworkManager.Singleton.ConnectedClientsIds.Count >= min)
                    yield break;
                yield return null;
            }

            Debug.LogWarning(
                $"[SoapSlideGameBootstrap] Still fewer than {min} connected client(s) before arena spawn — continuing with whoever is connected.");
        }

        IEnumerator ClientOnlineRoutine()
        {
            yield return ClientSoapSlidePresentationRoutine();
        }

        IEnumerator ClientSoapSlidePresentationRoutine()
        {
            HidePersistentMenuUiFromPreviousScenes();
            TryBuildVisualArenaForThinClient();

            var viewGo = new GameObject("SoapSlideClientDirectorView");
            var clientView = viewGo.AddComponent<SoapSlideClientDirectorView>();
            clientView.Configure(null, _platformHalfExtents);

            var canvas = BuildCanvas();
            var hud = SoapSlideHUD.Build(canvas);
            var hudDriver = canvas.gameObject.AddComponent<SoapSlideOnlineHudContext>();
            var menuReturn = canvas.gameObject.AddComponent<SoapSlideMenuReturn>();
            menuReturn.Configure(_menuSceneName);
            hud.BindMainMenu(menuReturn);

            SoapSlidePlayerNet owned = null;
            var wait = 0f;
            var clientWait = Mathf.Max(30f, _serverHumanConnectTimeoutSeconds);
            while (wait < clientWait)
            {
                foreach (var n in Object.FindObjectsByType<SoapSlidePlayerNet>(FindObjectsSortMode.None))
                {
                    if (n.IsOwner && !n.IsBotPlayer.Value)
                    {
                        owned = n;
                        break;
                    }
                }

                if (owned != null)
                    break;
                wait += Time.deltaTime;
                yield return null;
            }

            if (owned != null && owned.Slide != null)
            {
                clientView.Configure(owned.Slide, _platformHalfExtents);
                hudDriver.Bind(hud, owned.Slide);
                SoapSlideLocalPlayerVisuals.AttachTo(owned.Slide.transform, _localPlayerIndicatorPrefab);
                var cam = Camera.main;
                if (cam != null)
                {
                    var follow = cam.gameObject.GetComponent<SoapSlideCameraFollow>();
                    if (follow == null)
                        follow = cam.gameObject.AddComponent<SoapSlideCameraFollow>();
                    follow.SetTarget(owned.Slide.transform);
                    if (cam.GetComponent<SoapSlideSpectatorCameraDriver>() == null)
                    {
                        var spec = cam.gameObject.AddComponent<SoapSlideSpectatorCameraDriver>();
                        spec.Init(clientView);
                    }
                }
            }
            else
                Debug.LogWarning("[SoapSlideGameBootstrap] No owned SoapSlide player found for this client.");

            yield break;
        }
#endif

        IEnumerator OfflineRoutine()
        {
            HidePersistentMenuUiFromPreviousScenes();

            SoapSlideLocalPlayerVisuals.SharedIndicatorPrefab = _localPlayerIndicatorPrefab;

            var slipIce = new PhysicsMaterial("SoapSlip")
            {
                dynamicFriction = 0.26f,
                staticFriction = 0.3f,
                bounciness = 0.08f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average
            };

            var bodyMat = new PhysicsMaterial("SoapBody")
            {
                dynamicFriction = 0.26f,
                staticFriction = 0.3f,
                bounciness = 0.62f,
                frictionCombine = PhysicsMaterialCombine.Multiply,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };

            BuildPlatform(_platformHalfExtents, slipIce);
            BuildFallZone();

            var directorGo = new GameObject("SoapSlideDirector");
            var localDirector = directorGo.AddComponent<SoapSlideRoundDirector>();
            localDirector.SetArenaFromBootstrap(_platformHalfExtents, _spawnRingRadius);

            var canvas = BuildCanvas();
            var hud = SoapSlideHUD.Build(canvas);
            var menuReturn = canvas.gameObject.AddComponent<SoapSlideMenuReturn>();
            menuReturn.Configure(_menuSceneName);
            hud.BindMainMenu(menuReturn);

            string[] rosterNames = null;
            Color playerColor = new Color(0.2f, 0.55f, 1f);
            var lobby = LobbySession.Instance;
            var gameType = lobby != null ? lobby.GameType : SoapSlideGameType.EveryoneAlone;

            if (lobby != null && lobby.MatchRoster != null && lobby.MatchRoster.Length == 8)
            {
                rosterNames = lobby.MatchRoster;
                playerColor = lobby.PlayerColor;
            }
            else if (lobby != null)
                playerColor = lobby.PlayerColor;

            const int realPlayerCount = 1;
            var nameRng = new System.Random(
                unchecked((int)(System.DateTime.UtcNow.Ticks & 0x7FFFFFFF)) ^ (realPlayerCount * 13 + 17));

            const int localProfileSlot = 0;

            var everyone = new List<SlideParticipant>(MatchSize);
            for (int i = 0; i < MatchSize; i++)
            {
                bool isLocalHumanSlot = i < realPlayerCount;
                bool realPlayerSlot = i < realPlayerCount;

                int team = gameType == SoapSlideGameType.Teams4v4 ? (i < 4 ? 0 : 1) : -1;
                Color c = PickParticipantColor(i, isLocalHumanSlot, gameType, playerColor, localProfileSlot);
                string nm = ResolveParticipantName(i, realPlayerCount, localProfileSlot, rosterNames, lobby, nameRng);
                var p = SpawnParticipant(nm, isLocalHumanSlot, realPlayerSlot, c, bodyMat, localDirector, MatchSize, i,
                    nm, team);
                everyone.Add(p);
            }

            SlideParticipant localHuman = everyone[localProfileSlot];

            localDirector.Configure(localHuman, everyone, hud, gameType);
            BindFallZone(localDirector);
            var cam = Camera.main;
            if (cam != null)
            {
                var follow = cam.gameObject.GetComponent<SoapSlideCameraFollow>();
                if (follow == null)
                    follow = cam.gameObject.AddComponent<SoapSlideCameraFollow>();
                follow.SetTarget(localHuman.transform);
                if (cam.GetComponent<SoapSlideSpectatorCameraDriver>() == null)
                {
                    var spec = cam.gameObject.AddComponent<SoapSlideSpectatorCameraDriver>();
                    spec.Init(localDirector);
                }
            }

            SoapSlideLocalPlayerVisuals.AttachTo(localHuman.transform, _localPlayerIndicatorPrefab);
            localDirector.StartMatch();

            yield break;
        }

        void BuildPlatform(float half, PhysicsMaterial groundMat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "SoapPlatform";
            go.transform.position = new Vector3(0f, -0.5f, 0f);
            go.transform.localScale = new Vector3(half * 2f, 1f, half * 2f);

            var r = go.GetComponent<Renderer>();
            SoapSlideVisualUtil.ApplyCompatibleLitMaterial(r, new Color(0.92f, 0.95f, 1f));

            var col = go.GetComponent<BoxCollider>();
            col.material = groundMat;
        }

        void BuildFallZone()
        {
            var go = new GameObject("FallZone");
            go.transform.position = new Vector3(0f, -12f, 0f);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(200f, 8f, 200f);
            go.AddComponent<SoapSlideFallZone>();
        }

        void BindFallZone(ISoapSlideMatchDirector director)
        {
            var fz = Object.FindFirstObjectByType<SoapSlideFallZone>();
            if (fz != null)
                fz.Bind(director);
        }

        static Color PickParticipantColor(int slot, bool isHuman, SoapSlideGameType mode, Color profileColor,
            int profileSlot)
        {
            if (mode == SoapSlideGameType.Teams4v4)
                return TeamSlotColor(slot);

            return isHuman && slot == profileSlot
                ? profileColor
                : Color.HSVToRGB((slot * 0.11f + 0.04f) % 1f, 0.72f, 0.92f);
        }

        static Color TeamSlotColor(int slot)
        {
            float t = (slot % 4) / 3f;
            if (slot < 4)
                return Color.Lerp(new Color(0.25f, 0.45f, 1f), new Color(0.5f, 0.75f, 1f), t);
            return Color.Lerp(new Color(1f, 0.35f, 0.3f), new Color(1f, 0.65f, 0.4f), t);
        }

        static string ResolveParticipantName(int slot, int realPlayerCount, int profileSlot, string[] rosterNames,
            LobbySession lobby, System.Random nameRng)
        {
            if (rosterNames != null && slot < rosterNames.Length)
                return rosterNames[slot];

            if (slot == profileSlot)
            {
                if (lobby != null && !string.IsNullOrWhiteSpace(lobby.PlayerName))
                    return lobby.PlayerName.Trim();
                return "Player";
            }

            if (slot < realPlayerCount)
                return BotNameGenerator.FakeQueueName(nameRng);

            return BotNameGenerator.Next(nameRng);
        }

        SlideParticipant SpawnParticipant(string objectName, bool localHuman, bool realPlayerSlot, Color color,
            PhysicsMaterial bodyMat,
            ISoapSlideMatchDirector director, int total, int index, string displayName, int teamIndex)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = objectName;
            float ang = (Mathf.PI * 2f * index) / Mathf.Max(1, total);
            float ring = _spawnRingRadius;
            go.transform.position = new Vector3(Mathf.Cos(ang) * ring, 1.1f, Mathf.Sin(ang) * ring);

            var primCol = go.GetComponent<CapsuleCollider>();
            if (primCol != null)
                Object.DestroyImmediate(primCol);

            var p = go.AddComponent<SlideParticipant>();
            p.Init(director, localHuman, realPlayerSlot, color, bodyMat, displayName, index, teamIndex);
            return p;
        }

        static void HidePersistentMenuUiFromPreviousScenes()
        {
            var menuCanvas = GameObject.Find("MenuCanvas");
            if (menuCanvas != null)
                menuCanvas.SetActive(false);
        }

        internal static Transform FindChildTransformIncludingInactive(Transform root, string childName)
        {
            if (root == null) return null;
            foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            {
                if (tr.name == childName)
                    return tr;
            }

            return null;
        }

        static Canvas BuildCanvas()
        {
            if (Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("SoapSlideCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            return canvas;
        }
    }
}
