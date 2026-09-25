using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SoapSlide
{
    public sealed class SoapSlideHUD : MonoBehaviour
    {
        Text _phase;
        Text _timer;
        Text _plan;
        Text _toast;
        Button _mainMenuButton;
        GameObject _matchStartRoot;
        Button _matchStartButton;
        Text _matchStartLabel;

        float _toastUntil;

        public static SoapSlideHUD Build(Canvas root)
        {
            var go = new GameObject("SoapSlideHUD");
            go.transform.SetParent(root.transform, false);
            var hud = go.AddComponent<SoapSlideHUD>();
            hud.CreateTexts(root);
            return hud;
        }

        void CreateTexts(Canvas root)
        {
            Font font = Font.CreateDynamicFontFromOSFont("Arial", 16);

            _phase = CreateLine("Phase", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -40), 28, font);
            _timer = CreateLine("Timer", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -85), 36, font);
            _plan = CreateLine("Plan", root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -140), 22, font);
            _toast = CreateLine("Toast", root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 26, font);

            var toastColor = _toast.color;
            toastColor.a = 0f;
            _toast.color = toastColor;

            _mainMenuButton = CreateMainMenuButton(root, font);
        }

        static Button CreateMainMenuButton(Canvas root, Font font)
        {
            var go = new GameObject("MainMenuButton");
            go.transform.SetParent(root.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0, 72);
            rt.sizeDelta = new Vector2(320, 48);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.14f, 0.2f, 0.94f);
            var btn = go.AddComponent<Button>();
            var cb = btn.colors;
            cb.highlightedColor = new Color(0.2f, 0.24f, 0.34f, 1f);
            cb.pressedColor = new Color(0.08f, 0.1f, 0.14f, 1f);
            btn.colors = cb;
            go.SetActive(false);

            var tgo = new GameObject("Text");
            tgo.transform.SetParent(go.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8, 6);
            trt.offsetMax = new Vector2(-8, -6);
            var tx = tgo.AddComponent<Text>();
            tx.font = font;
            tx.fontSize = 22;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.color = Color.white;
            tx.text = "Main menu";

            return btn;
        }

        public void BindMainMenu(SoapSlideMenuReturn menuReturn)
        {
            if (_mainMenuButton == null) return;
            _mainMenuButton.onClick.RemoveAllListeners();
            if (menuReturn != null)
                _mainMenuButton.onClick.AddListener(menuReturn.GoToMainMenu);
            SetMainMenuVisible(false);
        }

        public void SetMainMenuVisible(bool visible)
        {
            if (_mainMenuButton != null)
                _mainMenuButton.gameObject.SetActive(visible);
        }

        static Text CreateLine(string name, Canvas parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, int size, Font font)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(900, 48);

            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = "";
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0, 0, 0, 0.75f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        void Update()
        {
            if (_toast == null) return;
            if (Time.unscaledTime < _toastUntil)
            {
                var c = _toast.color;
                c.a = 1f;
                _toast.color = c;
            }
            else
            {
                var c = _toast.color;
                c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 3f);
                _toast.color = c;
            }
        }

        public void SetPhaseLabel(string s)
        {
            if (_phase != null) _phase.text = s;
        }

        public void SetLobbyWaiting(string title, string subtitle)
        {
            if (_phase != null) _phase.text = title;
            if (_timer != null) _timer.text = subtitle;
            if (_plan != null) _plan.text = "";
        }

        public void SetMatchStartGate(bool visible, UnityAction onStartPressed = null)
        {
            EnsureMatchStartGate();
            if (_matchStartRoot == null) return;
            _matchStartRoot.SetActive(visible);
            if (_matchStartButton != null)
            {
                _matchStartButton.onClick.RemoveAllListeners();
                if (visible && onStartPressed != null)
                    _matchStartButton.onClick.AddListener(onStartPressed);
            }
        }

        void EnsureMatchStartGate()
        {
            if (_matchStartRoot != null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            Font font = Font.CreateDynamicFontFromOSFont("Arial", 22);

            _matchStartRoot = new GameObject("MatchStartGate");
            _matchStartRoot.transform.SetParent(canvas.transform, false);
            var rootRt = _matchStartRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var dim = new GameObject("Dim");
            dim.transform.SetParent(_matchStartRoot.transform, false);
            var dimRt = dim.AddComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.55f);
            dimImg.raycastTarget = true;

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_matchStartRoot.transform, false);
            var prt = panel.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.5f, 0.5f);
            prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = Vector2.zero;
            prt.sizeDelta = new Vector2(520, 220);
            var pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.1f, 0.12f, 0.18f, 0.97f);

            var hintGo = new GameObject("StartHint");
            hintGo.transform.SetParent(panel.transform, false);
            var lrt = hintGo.AddComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0.5f, 0.55f);
            lrt.anchorMax = new Vector2(0.5f, 0.55f);
            lrt.pivot = new Vector2(0.5f, 0.5f);
            lrt.anchoredPosition = Vector2.zero;
            lrt.sizeDelta = new Vector2(480, 80);
            _matchStartLabel = hintGo.AddComponent<Text>();
            _matchStartLabel.font = font;
            _matchStartLabel.fontSize = 20;
            _matchStartLabel.alignment = TextAnchor.MiddleCenter;
            _matchStartLabel.color = Color.white;
            _matchStartLabel.text = "When everyone taps Start, the planning round begins together.";

            var btnGo = new GameObject("StartButton");
            btnGo.transform.SetParent(panel.transform, false);
            var brt = btnGo.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 0.22f);
            brt.anchorMax = new Vector2(0.5f, 0.22f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(280, 52);
            var bImg = btnGo.AddComponent<Image>();
            bImg.color = new Color(0.18f, 0.55f, 0.32f, 0.98f);
            _matchStartButton = btnGo.AddComponent<Button>();
            var cb = _matchStartButton.colors;
            cb.highlightedColor = new Color(0.22f, 0.65f, 0.38f, 1f);
            cb.pressedColor = new Color(0.12f, 0.35f, 0.22f, 1f);
            _matchStartButton.colors = cb;

            var tgo = new GameObject("Text");
            tgo.transform.SetParent(btnGo.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8, 6);
            trt.offsetMax = new Vector2(-8, -6);
            var tx = tgo.AddComponent<Text>();
            tx.font = font;
            tx.fontSize = 24;
            tx.alignment = TextAnchor.MiddleCenter;
            tx.color = Color.white;
            tx.text = "Start";

            _matchStartRoot.SetActive(false);
        }

        public void SetPlanning(float timeLeft, SlideParticipant localHuman, SlideParticipant watchSubject)
        {
            if (_timer != null)
                _timer.text = $"{Mathf.Max(0f, timeLeft):0.0}s";

            if (_plan == null || localHuman == null) return;

            if (localHuman.IsEliminated)
            {
                string nm = watchSubject != null ? watchSubject.DisplayName : "…";
                _plan.text = $"Spectating — {nm}  ·  Next round in {Mathf.Max(0f, timeLeft):0.0}s";
                return;
            }

            _plan.text =
                $"{localHuman.DisplayName}  —  Aim: mouse on platform   |   Force: {localHuman.PlanForceLevel}/10   (1–0, wheel, +/-)";
        }

        public void SetActionHud()
        {
            if (_timer != null) _timer.text = "";
            if (_plan != null) _plan.text = "";
        }

        public void SetSpectatorAction(string watchingName)
        {
            if (_timer != null) _timer.text = "";
            if (_plan != null)
                _plan.text = string.IsNullOrEmpty(watchingName) ? "Spectating" : $"Spectating — {watchingName}";
        }

        public void ShowToast(string msg)
        {
            if (_toast == null) return;
            _toast.text = msg;
            var c = _toast.color;
            c.a = 1f;
            _toast.color = c;
            _toastUntil = Time.unscaledTime + 2f;
        }

        public void ShowGameOver(bool won, bool teamMode = false, bool draw = false)
        {
            if (draw)
            {
                SetPhaseLabel("Draw!");
                if (_timer != null) _timer.text = "Both teams wiped out";
                if (_plan != null) _plan.text = "";
                ShowToast("What a slide.");
                SetMainMenuVisible(true);
                return;
            }

            if (teamMode)
            {
                SetPhaseLabel(won ? "Your team wins!" : "Your team lost");
                if (_timer != null) _timer.text = won ? "Last team on the soap!" : "You can return to the menu anytime.";
                if (_plan != null) _plan.text = "";
                ShowToast(won ? "Team victory." : "The other squad slid better.");
                SetMainMenuVisible(true);
                return;
            }

            SetPhaseLabel(won ? "You win!" : "Game over");
            if (_timer != null) _timer.text = won ? "Last on the soap!" : "You can return to the menu anytime.";
            if (_plan != null) _plan.text = "";
            ShowToast(won ? "Nice sliding." : "The soap claims another victim.");
            SetMainMenuVisible(true);
        }
    }
}
