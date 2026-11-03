using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EncoreKit.GameHarness
{
    // A simulated daily rewards screen with the Encore gift button on it, for demos, tests and screenshots.
    // Builds its whole UI in code, and lays it out again when the screen turns between portrait and landscape.
    public sealed class GameHarness : MonoBehaviour
    {
        [Tooltip("A test key. The default is the local backend's demo key.")]
        [SerializeField] private string apiKey = "pk_test_localdemoappkeytest00000";
        [SerializeField] private string giftsPlacement = "home";
        [Tooltip("Off for a publisher's setup: the gift button already turns into the reminder after a claim.")]
        [SerializeField] private bool claimReminder = true;

        private static readonly Color Sky = Hex("7CC6EE");
        private static readonly Color Horizon = Hex("C8ECF8");
        private static readonly Color Sea = Hex("4AAFD6");
        private static readonly Color Hill = Hex("8BCB5A");
        private static readonly Color Board = Hex("E8A0C8");
        private static readonly Color BoardEdge = Hex("B5528F");
        private static readonly Color Tile = Hex("FFF3C9");
        private static readonly Color TileToday = Hex("F7D774");
        private static readonly Color TileClaimed = Hex("BFE3A7");
        private static readonly Color Grand = Hex("FFC94A");
        private static readonly Color Ink = Hex("5B2A4A");
        private static readonly Color Gold = Hex("FFD54A");

        private readonly List<string> _log = new List<string>();
        private Font _font;
        private RectTransform _canvas;
        // The sky and sea fill the screen; the game UI and the gift button stay inside Screen.safeArea.
        private RectTransform _backdrop;
        private RectTransform _safeRoot;
        private RectTransform _content;
        private Rect _safeArea;
        private RectTransform _giftsSlot;
        private EncoreGiftsButtonView _giftsView;
        private RectTransform _reminderSlot;
        private EncoreClaimReminderView _reminderView;
        private bool _giftsWatched;
        private bool _reminderWatched;
        private CanvasScaler _scaler;
        private Text _logText;
        private GameObject _logPanel;
        private bool _landscape;
        private bool _giftsHidden;
        // Brand colours and a prize, to check that the served looks read them.
        private bool _brand;
        private static readonly AppearanceConfig Brand = new AppearanceConfig
        {
            AccentColor = "#FF8800",
            BackgroundColor = "#1B2A41",
            TextColor = "#FFFFFF",
            BackgroundEndColor = "#3A1F5D",
        };
        private static readonly PublisherReward Coins = new PublisherReward { Id = "harness_coins_100", Title = "100 coins" };
        private int _day = 1;
        private bool _claimedToday;
        private DateTime _resetAt;
        private int _shownHours = -1;
        private int _mainThread;

        private void Start()
        {
            _mainThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
            Screen.autorotateToPortraitUpsideDown = false;
            _font = LoadFont();
            _resetAt = DateTime.Now.AddHours(23).AddMinutes(59);

            Encore.Outcomes += outcome => Log(outcome.Kind == PlacementOutcomeKind.StrictUnlockVerified
                ? $"outcome strict unlock verified: {outcome.TransactionId}"
                : $"outcome {outcome.PlacementId}: {Describe(outcome.Result)}");
            // Set before Configure, so Configure sends it.
            Encore.PublisherRewardPolicy = new PublisherRewardPolicy(5);
            // The fake controller answers every purchase without a store, so the paywall flow runs end to end.
            Encore.Configure(apiKey, new HarnessPurchases(Log), new EncoreOptions { LogLevel = LogLevel.Debug });
            Encore.Identify("harness-player-1");
            Encore.Placement(RewardPlacement).UseCase(UseCase.RewardUsers).Prefetch();
            // One line each in harness.log: the reward answer, and ReduceChurn, which always answers false.
            Encore.Placement(RewardPlacement).UseCase(UseCase.RewardUsers).HasOffers(has => Log($"has offers reward: {has}"));
            Encore.Placement(RewardPlacement).HasOffers(has => Log($"has offers reduce churn: {has}"));

            BuildCanvas();
            Layout();
        }

        private void Update()
        {
            if ((Screen.width > Screen.height) != _landscape || Screen.safeArea != _safeArea) Layout();
            UpdateGiftsText();
            WatchHeight(_giftsView, "gift button", ref _giftsWatched);
            WatchHeight(_reminderView, "claim reminder", ref _reminderWatched);
        }

        // Logs each height the shared trigger host reports, once the component has made its trigger.
        private void WatchHeight(EncoreTriggerViewBase view, string name, ref bool watched)
        {
            if (watched || view == null || view.Trigger == null) return;
            watched = true;
            view.Trigger.HeightChanged += height => Log($"{name} height: {height:0}");
        }

        // ---- Layout ----

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("HarnessCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            _scaler = canvasObject.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.matchWidthOrHeight = 0.5f;
            _canvas = (RectTransform)canvasObject.transform;

            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));

            _safeRoot = NewRect("SafeArea", _canvas);
            // The gift button lives outside the rebuilt content: recreating it would restart its native mount.
            _giftsSlot = NewRect("GiftsButtonSlot", _safeRoot);
            _giftsView = _giftsSlot.gameObject.AddComponent<EncoreGiftsButtonView>();
            _giftsView.Placement = giftsPlacement;
            _giftsView.Result += result => Log($"gift button: {Describe(result)}");
            // Zero height until the player claims an offer and comes back without finishing it.
            if (!claimReminder) return;
            _reminderSlot = NewRect("ClaimReminderSlot", _safeRoot);
            _reminderView = _reminderSlot.gameObject.AddComponent<EncoreClaimReminderView>();
            _reminderView.Placement = giftsPlacement;
            _reminderView.Result += result => Log($"claim reminder: {Describe(result)}");
        }

        private void Layout()
        {
            _landscape = Screen.width > Screen.height;
            _scaler.referenceResolution = _landscape ? new Vector2(1920, 1080) : new Vector2(1080, 1920);
            FitSafeArea();
            if (_backdrop != null) Destroy(_backdrop.gameObject);
            if (_content != null) Destroy(_content.gameObject);
            _backdrop = NewRect("Backdrop", _canvas);
            Stretch(_backdrop);
            _backdrop.SetAsFirstSibling();
            _content = NewRect("Content", _safeRoot);
            Stretch(_content);
            _content.SetAsFirstSibling();

            BuildBackground();
            BuildTitle();
            BuildBoard();
            BuildMascot();
            BuildToolbar();
            BuildLogPanel();

            // Landscape: a card at the bottom right, from the speech bubble's edge, so a short phone fits its text.
            // Portrait: full width above the footer.
            if (_landscape) Anchor(_giftsSlot, new Vector2(0.47f, 0.12f), new Vector2(0.97f, 0.12f), new Vector2(1, 0));
            else Anchor(_giftsSlot, new Vector2(0.05f, 0.10f), new Vector2(0.95f, 0.10f), new Vector2(0.5f, 0));
            _giftsSlot.anchoredPosition = Vector2.zero;
            // Under the board: the left half in landscape, the full width in portrait.
            if (_reminderSlot != null)
            {
                if (_landscape) Anchor(_reminderSlot, new Vector2(0.03f, 0.42f), new Vector2(0.45f, 0.42f), new Vector2(0.5f, 1));
                else Anchor(_reminderSlot, new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.40f), new Vector2(0.5f, 1));
                _reminderSlot.anchoredPosition = Vector2.zero;
            }
            Log($"layout: {(_landscape ? "landscape" : "portrait")} {Screen.width}x{Screen.height}, safe area {_safeArea}");
        }

        // On iOS the Dynamic Island and the home indicator sit inside the screen, so the UI keeps to the safe area.
        private void FitSafeArea()
        {
            _safeArea = Screen.safeArea;
            _safeRoot.anchorMin = new Vector2(_safeArea.xMin / Screen.width, _safeArea.yMin / Screen.height);
            _safeRoot.anchorMax = new Vector2(_safeArea.xMax / Screen.width, _safeArea.yMax / Screen.height);
            _safeRoot.offsetMin = _safeRoot.offsetMax = Vector2.zero;
        }

        private void BuildBackground()
        {
            var sky = Panel("Sky", _backdrop, Sky);
            Anchor(sky, new Vector2(0, 0.45f), Vector2.one, new Vector2(0.5f, 1));
            var horizon = Panel("Horizon", _backdrop, Horizon);
            Anchor(horizon, new Vector2(0, 0.38f), new Vector2(1, 0.55f), new Vector2(0.5f, 0.5f));
            foreach (var (x, size) in new[] { (0.12f, 900f), (0.55f, 1100f), (0.95f, 800f) })
            {
                var hill = Panel("Hill", _backdrop, Hill, HarnessShapes.Circle());
                Anchor(hill, new Vector2(x, 0.40f), new Vector2(x, 0.40f), new Vector2(0.5f, 0.5f));
                hill.sizeDelta = new Vector2(size, size * 0.45f);
            }
            var sea = Panel("Sea", _backdrop, Sea);
            Anchor(sea, Vector2.zero, new Vector2(1, 0.40f), new Vector2(0.5f, 0));
        }

        private void BuildTitle()
        {
            var title = Label("Title", _content, "Daily Rewards", _landscape ? 84 : 92, Gold, FontStyle.Bold);
            if (_landscape) Anchor(title, new Vector2(0, 0.86f), new Vector2(1, 0.97f), new Vector2(0.5f, 0.5f));
            else Anchor(title, new Vector2(0, 0.85f), new Vector2(1, 0.93f), new Vector2(0.5f, 0.5f));
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = BoardEdge;
            outline.effectDistance = new Vector2(4, -4);
            var subtitle = Label("Subtitle", _content, "Collect a gift every day", _landscape ? 44 : 48, Ink, FontStyle.Bold);
            Anchor(subtitle, new Vector2(0, 0.80f), new Vector2(1, 0.87f), new Vector2(0.5f, 0.5f));
        }

        private void BuildBoard()
        {
            var board = Panel("Board", _content, Board, HarnessShapes.RoundedRect(24));
            if (_landscape)
            {
                // Clear of the toolbar (190 wide, 24 in) on any width, not a fraction a narrow safe area makes too small.
                Anchor(board, new Vector2(0, 0.44f), new Vector2(1, 0.78f), new Vector2(0.5f, 0.5f));
                board.offsetMin = new Vector2(230, 0);
                board.offsetMax = new Vector2(-230, 0);
            }
            else Anchor(board, new Vector2(0.04f, 0.42f), new Vector2(0.96f, 0.78f), new Vector2(0.5f, 0.5f));
            board.gameObject.AddComponent<Outline>().effectColor = BoardEdge;

            // Landscape: one row of seven days and the grand reward. Portrait: two rows of four.
            var columns = _landscape ? 8 : 4;
            var rows = _landscape ? 1 : 2;
            for (var i = 0; i < 8; i++)
            {
                var column = i % columns;
                var row = i / columns;
                var min = new Vector2(0.02f + column * (0.96f / columns), 0.06f + (rows - 1 - row) * (0.88f / rows));
                var max = min + new Vector2(0.96f / columns - 0.012f, 0.88f / rows - 0.03f);
                if (i < 7) BuildDayTile(board, i + 1, min, max);
                else BuildGrandTile(board, min, max);
            }
        }

        private void BuildDayTile(RectTransform board, int day, Vector2 min, Vector2 max)
        {
            var isToday = day == _day;
            var claimed = day < _day || (isToday && _claimedToday);
            var tile = Panel($"Day{day}", board, claimed ? TileClaimed : isToday ? TileToday : Tile, HarnessShapes.RoundedRect(16));
            Anchor(tile, min, max, new Vector2(0.5f, 0.5f));
            var caption = day == _day ? "Today" : day == _day + 1 ? "Tomorrow" : $"Day {day}";
            var header = Label("Header", tile, caption, 30, Ink, FontStyle.Bold);
            Anchor(header, new Vector2(0, 0.80f), new Vector2(1, 0.98f), new Vector2(0.5f, 0.5f));
            BuildGiftIcon(tile, day % 2 == 0 ? Hex("5BC0EB") : Hex("E85D9A"), claimed);
            if (isToday && !_claimedToday)
            {
                var button = tile.gameObject.AddComponent<Button>();
                button.onClick.AddListener(ClaimToday);
                var hint = Label("Hint", tile, "Tap!", 28, BoardEdge, FontStyle.Bold);
                Anchor(hint, new Vector2(0, 0.02f), new Vector2(1, 0.18f), new Vector2(0.5f, 0.5f));
            }
        }

        private void BuildGiftIcon(RectTransform tile, Color colour, bool claimed)
        {
            var box = Panel("Box", tile, claimed ? Hex("9CCB86") : colour, HarnessShapes.RoundedRect(10));
            Anchor(box, new Vector2(0.22f, 0.24f), new Vector2(0.78f, 0.62f), new Vector2(0.5f, 0.5f));
            var lid = Panel("Lid", tile, claimed ? Hex("86B873") : colour * 0.85f + Color.black * 0.15f, HarnessShapes.RoundedRect(10));
            Anchor(lid, new Vector2(0.17f, 0.58f), new Vector2(0.83f, 0.72f), new Vector2(0.5f, 0.5f));
            var ribbon = Panel("Ribbon", tile, Hex("FFF7E0"));
            Anchor(ribbon, new Vector2(0.45f, 0.24f), new Vector2(0.55f, 0.72f), new Vector2(0.5f, 0.5f));
            if (!claimed) return;
            var done = Label("Done", tile, "Collected", 26, Hex("2E7D32"), FontStyle.Bold);
            Anchor(done, new Vector2(0, 0.02f), new Vector2(1, 0.20f), new Vector2(0.5f, 0.5f));
        }

        private void BuildGrandTile(RectTransform board, Vector2 min, Vector2 max)
        {
            var tile = Panel("Grand", board, Grand, HarnessShapes.RoundedRect(16));
            Anchor(tile, min, max, new Vector2(0.5f, 0.5f));
            var header = Label("Header", tile, "Grand\nReward", 30, Ink, FontStyle.Bold);
            Anchor(header, new Vector2(0, 0.68f), new Vector2(1, 0.98f), new Vector2(0.5f, 0.5f));
            for (var week = 0; week < 4; week++)
            {
                var cellMin = new Vector2(0.10f + (week % 2) * 0.41f, 0.08f + (1 - week / 2) * 0.30f);
                var cell = Panel($"Week{week + 1}", tile, Hex("FFF3C9"), HarnessShapes.RoundedRect(8));
                Anchor(cell, cellMin, cellMin + new Vector2(0.38f, 0.27f), new Vector2(0.5f, 0.5f));
                var text = Label("Text", cell, $"Week\n{week + 1}", 20, Ink, FontStyle.Bold);
                Stretch(text);
            }
        }

        private void BuildMascot()
        {
            var size = _landscape ? 220f : 240f;
            var body = Panel("Mascot", _content, Hex("FF9F43"), HarnessShapes.Circle());
            Anchor(body, new Vector2(0.06f, 0.08f), new Vector2(0.06f, 0.08f), Vector2.zero);
            if (!_landscape) Anchor(body, new Vector2(0.06f, 0.24f), new Vector2(0.06f, 0.24f), Vector2.zero);
            body.sizeDelta = new Vector2(size, size * 0.85f);
            foreach (var x in new[] { 0.35f, 0.65f })
            {
                var eye = Panel("Eye", body, Color.white, HarnessShapes.Circle());
                Anchor(eye, new Vector2(x - 0.12f, 0.52f), new Vector2(x + 0.12f, 0.80f), new Vector2(0.5f, 0.5f));
                var pupil = Panel("Pupil", eye, Hex("2D2D2D"), HarnessShapes.Circle());
                Anchor(pupil, new Vector2(0.35f, 0.25f), new Vector2(0.75f, 0.65f), new Vector2(0.5f, 0.5f));
            }
            var bubble = Panel("Bubble", _content, Color.white, HarnessShapes.RoundedRect(20));
            var bubbleMin = _landscape ? new Vector2(0.20f, 0.12f) : new Vector2(0.34f, 0.27f);
            Anchor(bubble, bubbleMin, bubbleMin + (_landscape ? new Vector2(0.24f, 0.16f) : new Vector2(0.58f, 0.09f)), new Vector2(0.5f, 0.5f));
            var line = _claimedToday ? "See you tomorrow for the next gift!" : "Come back every day for more rewards!";
            var text = Label("Text", bubble, line, 34, Ink, FontStyle.Bold);
            Stretch(text, 16);

            var footer = Label("Footer", _content, _claimedToday ? "Tap Next day to skip ahead" : "Tap today's gift to collect it", 38, Color.white, FontStyle.Bold);
            Anchor(footer, new Vector2(0, 0.01f), new Vector2(1, 0.07f), new Vector2(0.5f, 0.5f));
        }

        // ---- Harness controls ----

        private void BuildToolbar()
        {
            var bar = NewRect("Toolbar", _content);
            Anchor(bar, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
            bar.anchoredPosition = _landscape ? new Vector2(-24, -24) : new Vector2(-20, -70);
            var actions = new (string, Action)[]
            {
                ("Next day", NextDay),
                ("Reward", () => ShowReward(RewardPlacement, "Level cleared!", "Here is a thank you from us")),
                ("Paywall", ShowPaywall),
                (_giftsHidden ? "Show gift" : "Hide gift", ToggleGifts),
                ("Rotate", Rotate),
                (_brand ? "Plain" : "Brand", ToggleBrand),
                ("Log", () => _logPanel.SetActive(!_logPanel.activeSelf)),
            };
            for (var i = 0; i < actions.Length; i++)
            {
                var (title, action) = actions[i];
                var button = Panel(title, bar, new Color(0, 0, 0, 0.45f), HarnessShapes.RoundedRect(14));
                button.pivot = new Vector2(1, 1);
                button.anchorMin = button.anchorMax = new Vector2(1, 1);
                // Landscape: a column at the right edge. Portrait: one row above the title.
                button.sizeDelta = _landscape ? new Vector2(190, 64) : new Vector2(124, 60);
                button.anchoredPosition = _landscape ? new Vector2(0, -i * 74) : new Vector2(-(actions.Length - 1 - i) * 132, 0);
                button.gameObject.AddComponent<Button>().onClick.AddListener(() => action());
                Stretch(Label("Text", button, title, _landscape ? 28 : 25, Color.white, FontStyle.Bold));
            }
        }

        private void BuildLogPanel()
        {
            var wasOpen = _logPanel != null && _logPanel.activeSelf;
            var panel = Panel("Log", _content, new Color(0, 0, 0, 0.7f), HarnessShapes.RoundedRect(14));
            Anchor(panel, new Vector2(0.02f, 0.30f), new Vector2(_landscape ? 0.45f : 0.98f, 0.78f), new Vector2(0, 1));
            _logText = Label("Text", panel, "", 24, Color.white, FontStyle.Normal);
            _logText.alignment = TextAnchor.UpperLeft;
            Stretch(_logText, 16);
            _logPanel = panel.gameObject;
            _logPanel.SetActive(wasOpen);
            RenderLog();
        }

        private void ClaimToday()
        {
            _claimedToday = true;
            Log($"claimed day {_day}");
            Layout();
            // The post-action moment: the player just collected the day's in-game reward.
            ShowReward($"daily_reward_day_{_day}", $"Day {_day} reward collected!", "Here is a bonus gift from our partners");
        }

        private void NextDay()
        {
            _day = _day % 7 + 1;
            _claimedToday = false;
            _resetAt = DateTime.Now.AddHours(23).AddMinutes(59);
            _shownHours = -1;
            Log($"next day: {_day}");
            Layout();
        }

        private void ToggleGifts()
        {
            _giftsHidden = !_giftsHidden;
            _giftsView.Button?.SetVisible(!_giftsHidden);
            Layout();
        }

        private void ToggleBrand()
        {
            _brand = !_brand;
            _giftsView.Button?.SetAppearance(_brand ? Brand : null);
            _giftsView.Button?.SetPublisherReward(_brand ? Coins : null);
            Log(_brand ? "brand colours and 100 coins on" : "brand colours and prize off");
            Layout();
        }

        private void Rotate() =>
            Screen.orientation = _landscape ? ScreenOrientation.Portrait : ScreenOrientation.LandscapeLeft;

        private const string RewardPlacement = "harness_level_complete";

        private async void ShowReward(string placement, string headline, string subheadline)
        {
            var placementBuilder = Encore.Placement(placement).UseCase(UseCase.RewardUsers);
            Log($"show {placement}");
            placementBuilder.OnLoadingStateChange += loading => Log($"loading {placement}: {loading}");
            var result = await placementBuilder.Headline(headline).Subheadline(subheadline)
                .Cta("Claim my gift").Decline("Maybe later").Badge("Top pick")
                .Appearance(_brand ? Brand : null)
                .PublisherReward(_brand ? Coins : null)
                .ShowAsync();
            Log($"{placement}: {Describe(result)}");
        }

        // The paywall decline moment: the player closed the game's own store.
        private async void ShowPaywall()
        {
            const string placement = "harness_paywall_decline";
            var product = Encore.CurrentEntitlement();
            Log($"show {placement} (product {product?.ProductId ?? "none"})");
            var result = await Encore.Placement(placement).UseCase(UseCase.ReduceChurn).ShowAsync();
            Log($"{placement}: {Describe(result)}");
            var trial = await Encore.IsActiveAsync(Entitlement.FreeTrial());
            Log($"free trial active: {trial}");
        }

        // "Day 1 of 7 · 23 hr left", refreshed when the hour changes.
        private void UpdateGiftsText()
        {
            var button = _giftsView != null ? _giftsView.Button : null;
            if (button == null) return;
            // Whole hours left, as a game counts down: 23 h 59 min reads "23 hr".
            var hours = Mathf.Max(1, (int)(_resetAt - DateTime.Now).TotalHours);
            if (hours == _shownHours) return;
            _shownHours = hours;
            button.SetText("You've got a gift", $"Day {_day} of 7 · {hours} hr left");
        }

        // ---- Helpers ----

        private void Log(string line)
        {
            // Every callback logs here, so the mark shows that each one arrived on Unity's main thread.
            var thread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var mark = thread == _mainThread ? "main" : $"THREAD {thread}";
            _log.Add($"{DateTime.Now:HH:mm:ss} {line}");
            if (_log.Count > 14) _log.RemoveAt(0);
            Debug.Log($"[Harness] [{mark}] {line}");
            // Also kept in a file, so a test can read the results from the app's data folder.
            System.IO.File.AppendAllText(System.IO.Path.Combine(Application.persistentDataPath, "harness.log"), $"{DateTime.Now:HH:mm:ss.fff} [{mark}] {line}\n");
            RenderLog();
        }

        private void RenderLog()
        {
            if (_logText != null) _logText.text = string.Join("\n", _log);
        }

        private static string Describe(PresentationResult r) =>
            r.Presented
                ? $"presented, advertiser {r.Advertiser}, publisher {r.Publisher}, dismissal {r.Dismissal}{(r.Claim != null ? $", claim {r.Claim.TransactionId}" : "")}"
                : $"not presented ({r.Reason}{(r.ErrorMessage != null ? $": {r.ErrorMessage}" : "")})";

        // The built-in font since Unity 2022.2.
        private static Font LoadFont() => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static RectTransform Panel(string name, Transform parent, Color colour, Sprite sprite = null)
        {
            var rect = NewRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = colour;
            if (sprite == null) return rect;
            image.sprite = sprite;
            image.type = sprite.border == Vector4.zero ? Image.Type.Simple : Image.Type.Sliced;
            return rect;
        }

        private Text Label(string name, Transform parent, string text, int size, Color colour, FontStyle style)
        {
            var rect = NewRect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = _font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = colour;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        private static void Anchor(Component c, Vector2 min, Vector2 max, Vector2 pivot)
        {
            var rect = (RectTransform)c.transform;
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.pivot = pivot;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(Component c, float inset = 0)
        {
            var rect = (RectTransform)c.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }

        private static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.magenta;
    }
}
