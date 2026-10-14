using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FallGuyClone
{
    /// <summary>HUD (timer, progress, messages) and menus, all built in code with uGUI.</summary>
    public class GameUI : MonoBehaviour
    {
        GameManager gm;
        Font font;

        Text timerText;
        Text checkpointText;
        RectTransform progressFill;
        Text bigText;
        Text toastText;
        GameObject hud;
        GameObject menuPanel;
        GameObject pausePanel;
        GameObject resultPanel;
        GameObject catOverlay;
        Text skinText;
        Text bestText;
        Text resultTitle;
        Text resultBody;

        float bigTimer;
        float toastTimer;
        float catTimer;
        bool catPersistent;

        public static GameUI Create()
        {
            var go = new GameObject("Game UI");
            var ui = go.AddComponent<GameUI>();
            ui.Build();
            return ui;
        }

        public void Bind(GameManager manager) => gm = manager;

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }

            // ----- HUD -----
            hud = Panel("HUD", transform, Color.clear).gameObject;
            var timerBg = Panel("TimerBg", hud.transform, new Color(0.15f, 0.1f, 0.3f, 0.75f));
            Place(timerBg, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(260f, 90f));
            timerText = Label(timerBg, "2:30", 64, Color.white);
            Stretch(timerText.rectTransform);

            var barBg = Panel("ProgressBg", hud.transform, new Color(0.15f, 0.1f, 0.3f, 0.75f));
            Place(barBg, new Vector2(0.5f, 1f), new Vector2(0f, -122f), new Vector2(620f, 22f));
            progressFill = Panel("ProgressFill", barBg, Art.Mint);
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.offsetMin = new Vector2(3f, 3f);
            progressFill.offsetMax = new Vector2(-3f, -3f);
            var startLbl = Label(barBg, "START", 20, Color.white, TextAnchor.MiddleRight);
            Place(startLbl.rectTransform, new Vector2(0f, 0.5f), new Vector2(-50f, 0f), new Vector2(90f, 30f));
            var finishLbl = Label(barBg, "FINISH", 20, Color.white, TextAnchor.MiddleLeft);
            Place(finishLbl.rectTransform, new Vector2(1f, 0.5f), new Vector2(55f, 0f), new Vector2(90f, 30f));

            checkpointText = Label(hud.transform, "", 28, Color.white, TextAnchor.UpperRight);
            Place(checkpointText.rectTransform, new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(420f, 40f), new Vector2(1f, 1f));

            var hint = Label(hud.transform,
                "WASD Move   Mouse Look   Space Jump   LMB / Ctrl Dive   R Respawn   Esc Pause", 22,
                new Color(1f, 1f, 1f, 0.85f), TextAnchor.LowerLeft);
            Place(hint.rectTransform, new Vector2(0f, 0f), new Vector2(30f, 24f), new Vector2(1200f, 34f), new Vector2(0f, 0f));

            bigText = Label(transform, "", 160, Art.Yellow);
            Place(bigText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 80f), new Vector2(1200f, 220f));
            toastText = Label(transform, "", 54, Art.Cyan);
            Place(toastText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -140f), new Vector2(1200f, 90f));

            catOverlay = new GameObject("Laughing Cat", typeof(RectTransform));
            catOverlay.transform.SetParent(transform, false);
            Place((RectTransform)catOverlay.transform, new Vector2(0f, 1f),
                new Vector2(140f, -140f), new Vector2(250f, 260f), new Vector2(0f, 1f));
            var catImageObject = new GameObject("Cat Illustration", typeof(RectTransform), typeof(Image));
            catImageObject.transform.SetParent(catOverlay.transform, false);
            var catImage = catImageObject.GetComponent<Image>();
            catImage.sprite = GameFeedback.CatSprite;
            catImage.preserveAspect = true;
            catImage.raycastTarget = false;
            Place(catImage.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 15f), new Vector2(220f, 220f));
            var catCaption = Label(catOverlay.transform, "HAHA!", 52, Art.Yellow);
            Place(catCaption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -105f), new Vector2(230f, 60f));
            catOverlay.SetActive(false);

            // ----- Main menu -----
            menuPanel = Panel("Menu", transform, new Color(0.25f, 0.12f, 0.45f, 0.55f)).gameObject;
            var title = Label(menuPanel.transform, "FALL GUY CLONE", 150, Art.Yellow);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(1400f, 200f));
            var sub = Label(menuPanel.transform, "Dodge the obstacles and reach the FINISH before time runs out!", 36, Color.white);
            Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(1400f, 60f));

            Button(menuPanel.transform, "<", new Vector2(-300f, 60f), new Vector2(90f, 80f), Art.Purple, () => gm.ChangeSkin(-1));
            Button(menuPanel.transform, ">", new Vector2(300f, 60f), new Vector2(90f, 80f), Art.Purple, () => gm.ChangeSkin(1));
            skinText = Label(menuPanel.transform, "", 40, Color.white);
            Place(skinText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(480f, 80f));

            Button(menuPanel.transform, "PLAY", new Vector2(0f, -70f), new Vector2(420f, 110f), Art.Mint, () => gm.StartCountdown());
            Button(menuPanel.transform, "QUIT", new Vector2(0f, -200f), new Vector2(300f, 80f), Art.Pink, () => gm.Quit());
            bestText = Label(menuPanel.transform, "", 30, Color.white);
            Place(bestText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(900f, 50f));
            var controls = Label(menuPanel.transform,
                "Enter: Play    Q / E: Change character    In game: WASD, Mouse, Space, LMB/Ctrl, Esc", 26,
                new Color(1f, 1f, 1f, 0.8f));
            Place(controls.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(1600f, 40f));

            // ----- Pause -----
            pausePanel = Panel("Pause", transform, new Color(0.1f, 0.05f, 0.2f, 0.7f)).gameObject;
            var pt = Label(pausePanel.transform, "PAUSED", 120, Color.white);
            Place(pt.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1000f, 160f));
            Button(pausePanel.transform, "RESUME", new Vector2(0f, 40f), new Vector2(380f, 100f), Art.Mint, () => gm.TogglePause());
            Button(pausePanel.transform, "RESTART", new Vector2(0f, -80f), new Vector2(380f, 100f), Art.Yellow, () => gm.Retry());
            Button(pausePanel.transform, "MAIN MENU", new Vector2(0f, -200f), new Vector2(380f, 100f), Art.Pink, () => gm.BackToMenu());

            // ----- Result -----
            resultPanel = Panel("Result", transform, new Color(0.1f, 0.05f, 0.2f, 0.6f)).gameObject;
            resultTitle = Label(resultPanel.transform, "", 150, Art.Yellow);
            Place(resultTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 220f), new Vector2(1600f, 200f));
            resultBody = Label(resultPanel.transform, "", 40, Color.white);
            Place(resultBody.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 50f), new Vector2(1200f, 160f));
            Button(resultPanel.transform, "PLAY AGAIN", new Vector2(0f, -110f), new Vector2(420f, 100f), Art.Mint, () => gm.Retry());
            Button(resultPanel.transform, "MAIN MENU", new Vector2(0f, -230f), new Vector2(420f, 100f), Art.Pink, () => gm.BackToMenu());
        }

        // ---------- Public API ----------

        public void ShowState(GameState s)
        {
            if (s == GameState.Menu || s == GameState.Countdown || s == GameState.Qualified)
                HideCat();
            hud.SetActive(s == GameState.Countdown || s == GameState.Playing || s == GameState.Paused);
            menuPanel.SetActive(s == GameState.Menu);
            pausePanel.SetActive(s == GameState.Paused);
            resultPanel.SetActive(s == GameState.Qualified || s == GameState.Eliminated);
            if (s == GameState.Menu || s == GameState.Qualified || s == GameState.Eliminated)
            {
                bigTimer = 0f;
                toastTimer = 0f;
            }
            Refresh();
        }

        public void Refresh()
        {
            if (gm == null) return;
            string skin = PlayerAvatar.Skins[gm.SkinIndex].Replace("character-", "").ToUpperInvariant();
            skinText.text = "Character: " + skin;
            bestText.text = gm.BestTime > 0f ? "Best time: " + FormatTime(gm.BestTime) : "No best time yet";
        }

        public void SetResult(bool qualified, bool newBest)
        {
            resultTitle.text = qualified ? "QUALIFIED!" : "ELIMINATED!";
            resultTitle.color = qualified ? Art.Mint : Art.Pink;
            if (qualified)
            {
                resultBody.text = $"Finish time: {FormatTime(gm.Elapsed)}   Falls: {gm.Falls}\n" +
                                  (newBest ? "NEW BEST TIME!" : "Best: " + FormatTime(gm.BestTime));
            }
            else
            {
                resultBody.text = $"Time's up! You reached {Mathf.RoundToInt(gm.Progress * 100f)}% of the course.\n" +
                                  "Press Enter or R to try again.";
            }
        }

        public void BigMessage(string text, Color color)
        {
            bigText.text = text;
            bigText.color = color;
            bigTimer = 1f;
        }

        public void ShowCat(float duration = 0f, bool persistent = false)
        {
            catPersistent = persistent;
            catTimer = duration;
            catOverlay.SetActive(true);
            catOverlay.transform.SetAsLastSibling();
        }

        void HideCat()
        {
            catPersistent = false;
            catTimer = 0f;
            if (catOverlay != null) catOverlay.SetActive(false);
        }

        public void Toast(string text, Color color)
        {
            toastText.text = text;
            toastText.color = color;
            toastTimer = 1.6f;
        }

        public static string FormatTime(float seconds)
        {
            seconds = Mathf.Max(0f, seconds);
            int m = (int)(seconds / 60f);
            float s = seconds - m * 60f;
            return $"{m}:{s:00.00}";
        }

        // ---------- Update ----------

        void Update()
        {
            if (gm == null) return;
            float dt = Time.unscaledDeltaTime;

            if (hud.activeSelf)
            {
                int secs = Mathf.CeilToInt(gm.TimeLeft);
                timerText.text = $"{secs / 60}:{secs % 60:00}";
                bool low = gm.TimeLeft < 20f && gm.State == GameState.Playing;
                timerText.color = low ? Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), Mathf.PingPong(Time.time * 3f, 1f)) : Color.white;
                progressFill.anchorMax = new Vector2(gm.Progress, 1f);
                checkpointText.text = $"Checkpoint {gm.CheckpointIndex}/{gm.CheckpointCount}";
            }

            bigTimer -= dt;
            bigText.gameObject.SetActive(bigTimer > 0f);
            if (bigTimer > 0f)
            {
                float t = 1f - bigTimer;
                float scale = t < 0.15f ? Mathf.Lerp(1.6f, 1f, t / 0.15f) : 1f;
                bigText.rectTransform.localScale = Vector3.one * scale;
                var c = bigText.color;
                c.a = Mathf.Clamp01(bigTimer * 3f);
                bigText.color = c;
            }

            toastTimer -= dt;
            toastText.gameObject.SetActive(toastTimer > 0f);
            if (toastTimer > 0f)
            {
                var c = toastText.color;
                c.a = Mathf.Clamp01(toastTimer * 2f);
                toastText.color = c;
            }

            if (!catPersistent && catOverlay.activeSelf)
            {
                catTimer -= dt;
                if (catTimer <= 0f) HideCat();
            }
        }

        // ---------- Widget helpers ----------

        RectTransform Panel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = color.a > 0.01f;
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            return rt;
        }

        Text Label(Transform parent, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.15f, 0.05f, 0.3f, 0.9f);
            outline.effectDistance = new Vector2(3f, -3f);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
            shadow.effectDistance = new Vector2(0f, -6f);
            return t;
        }

        void Button(Transform parent, string label, Vector2 pos, Vector2 size, Color color, UnityAction onClick)
        {
            var go = new GameObject(label + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Place(rt, new Vector2(0.5f, 0.5f), pos, size);
            var img = go.GetComponent<Image>();
            img.color = color;
            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            btn.colors = colors;
            btn.onClick.AddListener(onClick);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.15f, 0.05f, 0.3f, 0.8f);
            shadow.effectDistance = new Vector2(0f, -8f);
            var t = Label(go.transform, label, Mathf.RoundToInt(size.y * 0.45f), Color.white);
            Stretch(t.rectTransform);
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, anchor.y >= 1f ? 1f : 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }
    }
}
