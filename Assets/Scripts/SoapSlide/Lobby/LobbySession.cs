using System.Collections.Generic;
using SoapSlide;
using UnityEngine;

namespace SoapSlide.Lobby
{
    /// <summary>Survives Menu → game load; holds profile and the 8-player roster for the match.</summary>
    public sealed class LobbySession : MonoBehaviour
    {
        public static LobbySession Instance { get; private set; }

        public string PlayerName { get; set; } = "Player";
        public Color PlayerColor { get; set; } = new Color(0.2f, 0.55f, 1f);

        public readonly List<string> FriendNames = new List<string>();

        /// <summary>Exactly 8 display names: [0] = you, rest = opponents (bots locally).</summary>
        public string[] MatchRoster { get; set; }

        public SoapSlideGameType GameType { get; set; } = SoapSlideGameType.EveryoneAlone;


        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
