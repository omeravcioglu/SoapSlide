using SoapSlide;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SoapSlide.Lobby
{
    /// <summary>Menu: display name, mode, then load the gameplay scene (local bots only).</summary>
    public sealed class MenuSceneBootstrap : MonoBehaviour
    {
        const int MatchSize = 8;

        [SerializeField] string _gameSceneName = "SampleScene";

        Text _status;
        InputField _nameField;
        SoapSlideGameType _pendingGameType = SoapSlideGameType.EveryoneAlone;

        void Awake()
        {
            if (LobbySession.Instance == null)
            {
                var ls = new GameObject("LobbySession");
                ls.AddComponent<LobbySession>();
            }

            BuildUi();
        }

        void BuildUi()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("MenuCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            Font font = Font.CreateDynamicFontFromOSFont("Arial", 18);

            _status = AddText(canvas.transform, "Status", "SoapSlide — choose local play", 22,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -80), new Vector2(900, 48));

            _nameField = AddInputField(canvas.transform, "PlayerName", "Display name", new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(400, 40), font);
            if (LobbySession.Instance != null)
                _nameField.text = LobbySession.Instance.PlayerName;

            float y = -280f;
            var topStretch = new Vector2(0.5f, 1f);
            AddButton(canvas.transform, "BtnFFA", "Play (everyone alone)", font, topStretch, topStretch, new Vector2(0, y),
                () => StartLocalMatch(SoapSlideGameType.EveryoneAlone));
            y -= 56f;
            AddButton(canvas.transform, "Btn4v4", "Play (teams 4v4)", font, topStretch, topStretch, new Vector2(0, y),
                () => StartLocalMatch(SoapSlideGameType.Teams4v4));
        }

        static Text AddText(Transform parent, string name, string msg, int size, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var t = go.AddComponent<Text>();
            t.font = Font.CreateDynamicFontFromOSFont("Arial", size);
            t.fontSize = size;
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.text = msg;
            return t;
        }

        static Button AddButton(Transform parent, string name, string label, Font font, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(280, 44);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.18f, 0.28f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);
            var tgo = new GameObject("Text");
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tx = tgo.AddComponent<Text>();
            tx.font = font;
            tx.fontSize = 20;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.color = Color.white;
            tx.text = label;
            return btn;
        }

        static InputField AddInputField(Transform parent, string name, string placeholder, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 pos, Vector2 sizeDelta, Font font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.12f, 0.18f, 0.95f);
            var field = go.AddComponent<InputField>();
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8, 4);
            trt.offsetMax = new Vector2(-8, -4);
            var tx = textGo.AddComponent<Text>();
            tx.font = font;
            tx.fontSize = 18;
            tx.color = Color.white;
            tx.supportRichText = false;
            field.textComponent = tx;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var prt = phGo.AddComponent<RectTransform>();
            prt.anchorMin = Vector2.zero;
            prt.anchorMax = Vector2.one;
            prt.offsetMin = new Vector2(8, 4);
            prt.offsetMax = new Vector2(-8, -4);
            var ph = phGo.AddComponent<Text>();
            ph.font = font;
            ph.fontSize = 18;
            ph.color = new Color(1f, 1f, 1f, 0.35f);
            ph.text = placeholder;
            field.placeholder = ph;

            return field;
        }

        void StartLocalMatch(SoapSlideGameType gameType)
        {
            _pendingGameType = gameType;
            var lobby = LobbySession.Instance;
            if (lobby == null) return;

            if (_nameField != null && !string.IsNullOrWhiteSpace(_nameField.text))
                lobby.PlayerName = _nameField.text.Trim();

            lobby.GameType = _pendingGameType;

            string baseName = string.IsNullOrWhiteSpace(lobby.PlayerName) ? "Player" : lobby.PlayerName.Trim();
            var roster = new string[MatchSize];
            for (int i = 0; i < MatchSize; i++)
                roster[i] = i == 0 ? baseName : $"Bot {i}";
            lobby.MatchRoster = roster;

            SetStatus("Loading…");
            SceneManager.LoadScene(_gameSceneName, LoadSceneMode.Single);
        }

        void SetStatus(string msg)
        {
            if (_status != null)
                _status.text = msg;
        }
    }
}
