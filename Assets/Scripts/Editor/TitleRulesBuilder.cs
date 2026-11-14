using Hikigiwa.Title;
using Hikigiwa.UI.Option;
using Hikigiwa.UI.Rules;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.EditorTools
{
    /// <summary>
    /// タイトルシーンに「Rules」ボタンとルール説明画面を作ります。
    /// 設定画面（OptionOverlay）の暗転・パネル・見出し・戻るボタンを複製するので、見た目は設定画面とそろいます。
    /// 作ったあとは、文章・カードの絵・配置をシーン上で自由に編集できます。
    /// </summary>
    /// <remarks>
    /// もう一度実行すると、RulesButton と RulesOverlay を作り直します（シーン上で手を加えた部分は消えます）。
    /// タイトルの英字フォント（Yeseva One）は ASCII の文字しか持たないため、文章に「×」「→」などは使いません。
    /// </remarks>
    public static class TitleRulesBuilder
    {
        private const string Title = "ルール画面を作成";
        private const string ScenePath = "Assets/Scenes/Title.unity";
        private const string CardFacePath = "Assets/Art/Cards/CardFace.png";
        private const string JokerPath = "Assets/Art/Cards/Joker.png";

        // タイトル画面と同じ色
        private static readonly Color Gold = new(0.9254902f, 0.76862746f, 0.4392157f);
        private static readonly Color Cream = new(0.9647059f, 0.8980392f, 0.78039217f);
        private static readonly Color CreamDim = new(0.9647059f, 0.8980392f, 0.78039217f, 0.7f);
        private static readonly Color DividerColor = new(0.9254902f, 0.76862746f, 0.4392157f, 0.3f);

        // 文中の色（ゲーム中のHUDと同じ）
        private const string LuckyHex = "#ECC470";
        private const string UnluckyHex = "#EC6262";

        // カードの数字の色（ゲーム中のカードと同じ。CardAppearance を参照）
        private static readonly Color InkColor = new Color32(30, 26, 38, 255);
        private static readonly Color LuckyInk = new Color32(196, 140, 24, 255);
        private static readonly Color UnluckyInk = new Color32(184, 38, 46, 255);

        private const float PanelWidth = 1400f;
        private const float PanelHeight = 940f;
        private const float ScrollbarWidth = 12f;
        private const float ScrollbarGap = 24f;

        private enum Omen
        {
            Neutral,
            Lucky,
            Unlucky,
        }

        private static TMP_FontAsset font;
        private static Material fontMaterial;
        private static Texture2D cardFace;
        private static Texture2D joker;

        [MenuItem("Hikigiwa/タイトルにルール画面を作成")]
        public static void Build()
        {
            var canvas = Object.FindAnyObjectByType<TitleCanvas>(FindObjectsInactive.Include);
            if (canvas == null)
            {
                EditorUtility.DisplayDialog(Title, "開いているシーンに TitleCanvas が見つかりません。Title.unity を開いてください。", "OK");
                return;
            }

            if (!BuildInto(canvas)) return;

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("ルール画面を作成しました。シーンを保存（Ctrl+S / Cmd+S）してください。");
        }

        /// <summary>コマンドラインから実行する用：Title.unity を開いて作成し、保存します。</summary>
        public static void BuildAndSave()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = Object.FindAnyObjectByType<TitleCanvas>(FindObjectsInactive.Include);
            if (canvas == null) throw new System.InvalidOperationException($"{ScenePath} に TitleCanvas がありません。");
            if (!BuildInto(canvas)) throw new System.InvalidOperationException("ルール画面を作成できませんでした。");
            EditorSceneManager.SaveScene(scene);
            Debug.Log("ルール画面を作成し、Title.unity を保存しました。");
        }

        private static bool BuildInto(TitleCanvas canvas)
        {
            var titleView = canvas.GetComponentInChildren<TitleView>(true);
            var optionView = canvas.GetComponentInChildren<OptionView>(true);
            if (titleView == null || optionView == null || titleView.OptionButton == null)
            {
                Debug.LogError($"{Title}: TitleView・OptionView・OptionButton のどれかが見つかりません。");
                return false;
            }

            Transform optionPanel = optionView.transform.Find("Panel");
            Transform optionDim = optionView.transform.Find("Dim");
            var header = optionPanel != null ? optionPanel.Find("Header") : null;
            var headerText = header != null ? header.GetComponent<TMP_Text>() : null;
            if (optionPanel == null || optionDim == null || headerText == null || optionView.BackButton == null)
            {
                Debug.LogError($"{Title}: 設定画面の Dim・Panel・Header・BackButton のどれかが見つかりません。");
                return false;
            }

            font = headerText.font;
            fontMaterial = headerText.fontSharedMaterial;
            cardFace = AssetDatabase.LoadAssetAtPath<Texture2D>(CardFacePath);
            joker = AssetDatabase.LoadAssetAtPath<Texture2D>(JokerPath);

            Button rulesButton = CreateRulesButton(titleView);
            RulesPresenter rules = CreateRulesOverlay(canvas, optionView, optionDim, optionPanel);

            SetReference(titleView, "rulesButton", rulesButton);
            SetReference(canvas, "rules", rules);

            // Rules ボタンを含めて、タイトルのボタンを決まった順番・間隔に並べ直す
            TitleMenuLayout.Arrange(titleView);
            return true;
        }

        // ───────────── タイトルのボタン ─────────────

        /// <summary>Option ボタンを複製して Rules ボタンを作ります。位置は TitleMenuLayout が決めます。</summary>
        private static Button CreateRulesButton(TitleView titleView)
        {
            Transform parent = titleView.OptionButton.transform.parent;
            DestroyChild(parent, "RulesButton");

            var option = (RectTransform)titleView.OptionButton.transform;
            var rect = (RectTransform)Object.Instantiate(option.gameObject, parent).transform;
            rect.name = "RulesButton";
            rect.SetSiblingIndex(option.GetSiblingIndex() + 1);
            rect.anchoredPosition = option.anchoredPosition - new Vector2(0f, TitleMenuLayout.Step);

            var label = rect.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = "Rules";

            Undo.RegisterCreatedObjectUndo(rect.gameObject, Title);
            return rect.GetComponent<Button>();
        }

        // ───────────── ルール画面 ─────────────

        private static RulesPresenter CreateRulesOverlay(TitleCanvas canvas, OptionView optionView, Transform optionDim,
            Transform optionPanel)
        {
            DestroyChild(canvas.transform, "RulesOverlay");

            var overlay = NewRect("RulesOverlay", canvas.transform);
            Stretch(overlay);
            overlay.SetSiblingIndex(optionView.transform.GetSiblingIndex() + 1);
            // 一番手前に重なるので、シーン上では透明にしてタイトルや設定画面の編集を邪魔しないようにする。
            // 確認したいときは CanvasGroup の Alpha を 1 にする（プレイ時は CanvasBase が表示を切り替える）
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var view = overlay.gameObject.AddComponent<RulesView>();
            var presenter = overlay.gameObject.AddComponent<RulesPresenter>();

            // 暗転とパネルは設定画面のものを複製し、パネルの中は見出しと戻るボタンだけを残す
            Object.Instantiate(optionDim.gameObject, overlay).name = "Dim";
            var panel = (RectTransform)Object.Instantiate(optionPanel.gameObject, overlay).transform;
            panel.name = "Panel";
            for (int i = panel.childCount - 1; i >= 0; i--)
            {
                Transform child = panel.GetChild(i);
                if (child.name != "Header" && child.name != optionView.BackButton.name) Object.DestroyImmediate(child.gameObject);
            }
            panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);

            var header = (RectTransform)panel.Find("Header");
            header.anchoredPosition = new Vector2(0f, PanelHeight / 2f - 75f);
            header.GetComponent<TMP_Text>().text = "Rules";

            var back = (RectTransform)panel.Find(optionView.BackButton.name);
            back.name = "BackButton";
            back.anchoredPosition = new Vector2(0f, -PanelHeight / 2f + 75f);

            ScrollRect scroll = CreateScrollView(panel, out RectTransform content);
            BuildContent(content);

            var viewData = new SerializedObject(view);
            viewData.FindProperty("scrollRect").objectReferenceValue = scroll;
            viewData.FindProperty("backButton").objectReferenceValue = back.GetComponent<Button>();
            viewData.ApplyModifiedPropertiesWithoutUndo();

            var presenterData = new SerializedObject(presenter);
            presenterData.FindProperty("view").objectReferenceValue = view;
            presenterData.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(overlay.gameObject, Title);
            return presenter;
        }

        /// <summary>見出しと戻るボタンの間に、縦スクロールの領域を作ります。</summary>
        private static ScrollRect CreateScrollView(RectTransform panel, out RectTransform content)
        {
            var root = NewRect("ScrollView", panel);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 2f);
            root.sizeDelta = new Vector2(PanelWidth - 100f, PanelHeight - 290f);
            root.SetSiblingIndex(1);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 50f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.1f;

            var viewport = NewRect("Viewport", root);
            Stretch(viewport);
            viewport.offsetMax = new Vector2(-(ScrollbarWidth + ScrollbarGap), 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            // マウスホイールやドラッグを、文字のない所でも受け取るための透明な板
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear;

            content = NewRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 6, 40);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 細いスクロールバー（溝は薄いクリーム色、つまみは金色）
            var bar = NewRect("Scrollbar", root);
            bar.anchorMin = new Vector2(1f, 0f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(1f, 0.5f);
            bar.sizeDelta = new Vector2(ScrollbarWidth, 0f);
            var track = bar.gameObject.AddComponent<Image>();
            track.sprite = BuiltinSprite();
            track.type = Image.Type.Sliced;
            track.color = new Color(Cream.r, Cream.g, Cream.b, 0.12f);

            var area = NewRect("Sliding Area", bar);
            Stretch(area);
            var handle = NewRect("Handle", area);
            Stretch(handle);
            var handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.sprite = BuiltinSprite();
            handleImage.type = Image.Type.Sliced;
            handleImage.color = new Color(Gold.r, Gold.g, Gold.b, 0.85f);

            var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;

            scroll.viewport = viewport;
            scroll.content = content;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            return scroll;
        }

        // ───────────── 説明の中身 ─────────────

        private static string Lucky(string text) => $"<color={LuckyHex}>{text}</color>";
        private static string Unlucky(string text) => $"<color={UnluckyHex}>{text}</color>";

        private static void BuildContent(RectTransform content)
        {
            BuildSolo(content);
            BuildVersusCpu(content);
        }

        /// <summary>1人用（タイトルの Solo）のルール。</summary>
        private static void BuildSolo(RectTransform content)
        {
            PartTitle(content, "Solo", "Read the fortunes of nine customers over three nights.", first: true);

            // 1. 目的
            Heading(content, "Your Goal", first: true);
            Body(content,
                "You are a fortune teller. Each night, three customers come to you, and the game lasts three nights - " +
                "nine readings in all.\n" +
                "In each reading you draw cards to build up the customer's fortune, and every draw asks the same " +
                "question: chase more fortune, or fold and keep what you have? Draw too greedily and the reading " +
                "falls apart. Knowing when to fold is everything.\n" +
                "Your final score is the total of all nine readings. Each night also has a <b>Quota</b>: missing it " +
                "does not end the game, but meeting it earns you 2 Spirit.");

            // 2. 山札
            Heading(content, "The Deck");
            Body(content,
                "Every customer gets a freshly shuffled deck of 19 cards: the numbers 1 to 9, two of each, " +
                "plus one Joker. Cards you draw are not put back.");
            var deckRow = Row(content, "DeckCards", 10f);
            for (int number = 1; number <= 9; number++) Card(deckRow, number, Omen.Neutral, 84f, null);
            Card(deckRow, 0, Omen.Unlucky, 84f, null);

            // 3. 吉数と凶数
            Heading(content, "Lucky and Unlucky Cards");
            Body(content,
                $"Each customer has a {Lucky("Lucky Number")} and an {Unlucky("Unlucky Number")}. " +
                "Both are shown on screen when the reading begins, so you always know which cards to hope for " +
                "and which to fear.\n" +
                $"- {Lucky("Lucky Number")}: 2 cards in the deck.\n" +
                $"- {Unlucky("Unlucky Number")}: 2 cards in the deck. The Joker is always unlucky too, " +
                "so there are 3 unlucky cards in all.\n" +
                "The screen shows how many lucky and unlucky cards are still left in the deck.");
            Caption(content, "For example, a customer whose Lucky Number is 7 and Unlucky Number is 4:");
            var luckRow = Row(content, "LuckyUnluckyCards", 16f);
            Card(luckRow, 7, Omen.Lucky, 110f, Lucky("Lucky"));
            Card(luckRow, 7, Omen.Lucky, 110f, Lucky("Lucky"));
            Spacer(luckRow, 40f);
            Card(luckRow, 4, Omen.Unlucky, 110f, Unlucky("Unlucky"));
            Card(luckRow, 4, Omen.Unlucky, 110f, Unlucky("Unlucky"));
            Card(luckRow, 0, Omen.Unlucky, 110f, Unlucky("Unlucky"));

            // 4. 引く
            Heading(content, "Drawing Cards");
            Body(content,
                "Each card you draw adds its number to the customer's <b>Luck</b>.\n" +
                $"- {Lucky("Lucky card")}: your <b>Multiplier</b> goes up by 1. It starts at 1.\n" +
                $"- {Unlucky("Unlucky card")}: you receive an <b>Omen Mark</b>. Its number still adds to Luck " +
                "(the Joker adds 0).\n" +
                $"Collect 2 Omen Marks and the reading is <b>Ruined</b>: {Unlucky("this customer gives you 0 points")}.");
            var ruinRow = Row(content, "RuinedCards", 16f);
            Card(ruinRow, 4, Omen.Unlucky, 110f, Unlucky("Mark 1"));
            Card(ruinRow, 0, Omen.Unlucky, 110f, Unlucky("Mark 2"));
            Word(ruinRow, Unlucky("Ruined!\n0 points"), 220f, 110f);

            // 5. 引き際
            Heading(content, TitleMenuLayout.GameTitle);
            Body(content,
                "You may <b>fold</b> at any time to end the reading. This customer's score is:");
            Formula(content, "Luck  x  Multiplier");
            Body(content,
                "When you fold, the next card in the deck is turned over. If it is an unlucky card, you folded just " +
                "in time: the <b>Foresight Bonus</b> multiplies your score by 1.5.\n" +
                "If the deck runs out, you have to fold.");
            Caption(content, "Example (Lucky Number 7):");
            var foldRow = Row(content, "FoldExample", 16f);
            Card(foldRow, 5, Omen.Neutral, 110f, "+5");
            Card(foldRow, 7, Omen.Lucky, 110f, Lucky("+7, x2"));
            Card(foldRow, 3, Omen.Neutral, 110f, "+3");
            Word(foldRow, $"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>Fold</color>", 160f, 110f);
            Card(foldRow, 0, Omen.Unlucky, 110f, "Next card");
            Body(content,
                "Luck 5 + 7 + 3 = 15 and Multiplier 2, so 15 x 2 = 30 points.\n" +
                "The next card is the Joker, so the Foresight Bonus makes it 30 x 1.5 = 45 points.");

            // 6. 霊力と占い道具
            Heading(content, "Spirit and Fortune Tools");
            Body(content,
                "Fortune tools let you peek at or change the deck, but each one costs <b>Spirit</b>.\n" +
                "You start with 3 Spirit. You gain 1 after every reading that is not ruined, and 2 when you meet " +
                "a night's Quota. Spirit carries over between customers and nights.\n" +
                "Using a tool does not draw a card, so you can use several before you decide.");
            Tool(content, "Crystal Ball", 1, "Tells you whether the next card is unlucky.");
            Tool(content, "Tarot", 2, "Shows the numbers of the next 3 cards, but not their order.");
            Tool(content, "Incense", 2, "Removes one unlucky card from the deck. Once per customer.");
            Tool(content, "Palm Reading", 1, "Moves the top card to the bottom of the deck without showing it.");

            // 7. 操作
            Heading(content, "Controls");
            Body(content,
                "Draw a card<pos=48%>Click the deck, or Space\n" +
                "Fold (end the reading)<pos=48%>Enter\n" +
                "Fortune tools<pos=48%>T  (close with T or Esc)\n" +
                "Next customer<pos=48%>Enter\n" +
                "You can also use the buttons on the screen.");
        }

        /// <summary>CPU対戦（タイトルの CPU）のルール。1人用と違うところだけを説明します。</summary>
        private static void BuildVersusCpu(RectTransform content)
        {
            PartTitle(content, "VS CPU",
                "Cards, Luck, Multiplier and fortune tools work just like Solo. Here is what changes.");

            // 1. 手番
            Heading(content, "Taking Turns", first: true);
            Body(content,
                "A match has 3 rounds. In each round, you and the CPU take turns drawing from one shared deck, " +
                "which is freshly shuffled every round. Who goes first is decided at random each round.\n" +
                "On your turn, either draw a card or fold. Using fortune tools does not end your turn.\n" +
                "Once one of you folds or is ruined, the other keeps drawing alone until they fold too.");

            // 2. 共通の数字
            Heading(content, "Shared Numbers");
            Body(content,
                "You and the CPU share the same numbers, shown at the top of the screen: " +
                $"one {Lucky("Lucky Number")} and " +
                $"<b>two</b> {Unlucky("Unlucky Numbers")}. With the Joker, that makes 5 unlucky cards in the deck.\n" +
                "Luck, Multiplier and Omen Marks are kept separately for each of you.");
            Caption(content, "For example, Lucky Number 7 and Unlucky Numbers 4 and 8:");
            var sharedRow = Row(content, "SharedNumberCards", 14f);
            Card(sharedRow, 7, Omen.Lucky, 100f, Lucky("Lucky"));
            Card(sharedRow, 7, Omen.Lucky, 100f, Lucky("Lucky"));
            Spacer(sharedRow, 30f);
            Card(sharedRow, 4, Omen.Unlucky, 100f, Unlucky("Unlucky"));
            Card(sharedRow, 4, Omen.Unlucky, 100f, Unlucky("Unlucky"));
            Card(sharedRow, 8, Omen.Unlucky, 100f, Unlucky("Unlucky"));
            Card(sharedRow, 8, Omen.Unlucky, 100f, Unlucky("Unlucky"));
            Card(sharedRow, 0, Omen.Unlucky, 100f, Unlucky("Unlucky"));

            // 3. 予感
            Heading(content, "Who Gets the Foresight Bonus");
            Body(content,
                "Only the one who folds <b>last</b> can get the Foresight Bonus. If you fold first, your score is " +
                "fixed right away and no card is turned over.\n" +
                "Wait until the CPU has folded or been ruined, then fold: the next card is turned over, and if it is " +
                "unlucky, your score is multiplied by 1.5.\n" +
                "If the deck runs out, anyone still reading is scored without the bonus.");

            // 4. 霊力と勝敗
            Heading(content, "Spirit and Winning");
            Body(content,
                "You and the CPU each start with 3 Spirit and gain 1 after every round that is not ruined. " +
                "Spirit carries over between rounds. The CPU never uses fortune tools.\n" +
                "There are no Quotas. After 3 rounds, the higher total score wins. Equal scores are a draw.");

            // 5. 操作
            Heading(content, "Controls");
            Body(content,
                "The same as Solo. Press Enter to go to the next round.");
        }

        // ───────────── 部品 ─────────────

        /// <summary>
        /// 部の見出し（Solo / VS CPU）。章の見出しより大きく中央に出し、その下に一言の説明を添えます。
        /// 最初の部以外は、上に余白と太めの金色の線を入れて、前の部と区切ります。
        /// </summary>
        private static void PartTitle(RectTransform parent, string title, string subtitle, bool first = false)
        {
            if (!first)
            {
                var divider = NewRect("PartDivider", parent);
                AddLayout(divider, preferredHeight: 90f);
                var line = NewRect("Line", divider);
                line.anchorMin = new Vector2(0.1f, 0.5f);
                line.anchorMax = new Vector2(0.9f, 0.5f);
                line.sizeDelta = new Vector2(0f, 3f);
                var image = line.gameObject.AddComponent<Image>();
                image.color = new Color(Gold.r, Gold.g, Gold.b, 0.6f);
                image.raycastTarget = false;
            }

            var label = MakeText(parent, $"Part ({title})", title, 60f, Gold, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            MakeText(parent, "PartSubtitle", subtitle, 28f, CreamDim, TextAlignmentOptions.Center);
        }

        /// <summary>章の見出し。最初の章以外は、上に金色の細い線を引きます。</summary>
        private static void Heading(RectTransform parent, string text, bool first = false)
        {
            if (!first)
            {
                var divider = NewRect("Divider", parent);
                AddLayout(divider, preferredHeight: 34f);
                var line = NewRect("Line", divider);
                line.anchorMin = new Vector2(0f, 0.5f);
                line.anchorMax = new Vector2(1f, 0.5f);
                line.sizeDelta = new Vector2(0f, 2f);
                var image = line.gameObject.AddComponent<Image>();
                image.color = DividerColor;
                image.raycastTarget = false;
            }

            var label = MakeText(parent, "Heading", text, 46f, Gold, TextAlignmentOptions.Left);
            label.fontStyle = FontStyles.Bold;
        }

        private static void Body(RectTransform parent, string text)
        {
            var label = MakeText(parent, "Body", text, 30f, Cream, TextAlignmentOptions.TopLeft);
            label.lineSpacing = 12f;
            label.paragraphSpacing = 14f;
        }

        private static void Caption(RectTransform parent, string text)
        {
            MakeText(parent, "Caption", text, 26f, CreamDim, TextAlignmentOptions.TopLeft);
        }

        /// <summary>計算式を大きく中央に出します。</summary>
        private static void Formula(RectTransform parent, string text)
        {
            var label = MakeText(parent, "Formula", text, 44f, Gold, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
        }

        /// <summary>占い道具1つ分。名前と霊力のコストを金色で、説明をその下に出します。</summary>
        private static void Tool(RectTransform parent, string name, int cost, string description)
        {
            var label = MakeText(parent, $"Tool ({name})",
                $"<color=#{ColorUtility.ToHtmlStringRGB(Gold)}>{name}</color>" +
                $"<size=26>   {cost} Spirit</size>\n" +
                $"<indent=28>{description}</indent>",
                30f, Cream, TextAlignmentOptions.TopLeft);
            label.lineSpacing = 6f;
        }

        private static TextMeshProUGUI MakeText(RectTransform parent, string name, string text, float size, Color color,
            TextAlignmentOptions alignment)
        {
            var rect = NewRect(name, parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.fontSharedMaterial = fontMaterial;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.richText = true;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>カードを横に並べる行。行の中では、それぞれの部品の大きさをそのまま使います。</summary>
        private static RectTransform Row(RectTransform parent, string name, float spacing)
        {
            var row = NewRect(name, parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 4, 4);
            return row;
        }

        private static void Spacer(RectTransform row, float width)
        {
            AddLayout(NewRect("Spacer", row), preferredWidth: width, preferredHeight: 1f);
        }

        /// <summary>行の中に置く文字（「Fold」「Ruined!」など）。カードと同じ高さの中央に出します。</summary>
        private static void Word(RectTransform row, string text, float width, float cardWidth)
        {
            float height = CardHeight(cardWidth) + CaptionHeight;
            var item = NewRect("Word", row);
            AddLayout(item, preferredWidth: width, preferredHeight: height);
            var label = MakeText(item, "Text", text, 36f, Cream, TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
            var rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(0f, CardHeight(cardWidth));
        }

        private const float CaptionHeight = 44f;

        private static float CardHeight(float width) => width * 1008f / 712f;

        /// <summary>
        /// ゲーム中のカードと同じ見た目の、小さなカード。number が 0 ならジョーカー。
        /// caption を渡すと、カードの下に短い説明を出します。
        /// </summary>
        private static void Card(RectTransform row, int number, Omen omen, float width, string caption)
        {
            float height = CardHeight(width);
            bool hasCaption = !string.IsNullOrEmpty(caption);

            var item = NewRect(number == 0 ? "Card (Joker)" : $"Card ({number})", row);
            AddLayout(item, preferredWidth: width, preferredHeight: height + (hasCaption ? CaptionHeight : 0f));

            var face = NewRect("Face", item);
            face.anchorMin = face.anchorMax = new Vector2(0.5f, 1f);
            face.pivot = new Vector2(0.5f, 1f);
            face.sizeDelta = new Vector2(width, height);
            var image = face.gameObject.AddComponent<RawImage>();
            image.texture = number == 0 ? joker : cardFace;
            image.raycastTarget = false;

            if (number != 0)
            {
                Color ink = omen == Omen.Lucky ? LuckyInk : omen == Omen.Unlucky ? UnluckyInk : InkColor;
                string value = number.ToString();
                // 中央の大きな数字と、左上・右下の小さな数字（ゲーム中のカードと同じ配置）
                CardText(face, value, height * 0.42f, ink, Vector2.zero, new Vector2(width, height * 0.5f), 0f);
                CardText(face, value, height * 0.12f, ink, new Vector2(-width * 0.36f, height * 0.39f),
                    new Vector2(width * 0.25f, height * 0.16f), 0f);
                CardText(face, value, height * 0.12f, ink, new Vector2(width * 0.36f, -height * 0.39f),
                    new Vector2(width * 0.25f, height * 0.16f), 180f);
                if (omen != Omen.Neutral)
                {
                    CardText(face, omen == Omen.Lucky ? "LUCKY" : "UNLUCKY", height * 0.09f, ink,
                        new Vector2(0f, -height * 0.3f), new Vector2(width, height * 0.14f), 0f);
                }
            }

            if (hasCaption)
            {
                var label = MakeText(item, "Caption", caption, 24f, CreamDim, TextAlignmentOptions.Top);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                var rect = label.rectTransform;
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(0.5f, 0f);
                rect.sizeDelta = new Vector2(40f, CaptionHeight - 8f);
            }
        }

        private static void CardText(RectTransform face, string value, float size, Color color, Vector2 position,
            Vector2 box, float angle)
        {
            var label = MakeText(face, value, value, size, color, TextAlignmentOptions.Center);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        // ───────────── 共通 ─────────────

        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.gameObject.layer = parent.gameObject.layer;
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void AddLayout(RectTransform rect, float preferredWidth = -1f, float preferredHeight = -1f)
        {
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredWidth = preferredWidth;
            layout.preferredHeight = preferredHeight;
        }

        private static Sprite BuiltinSprite() =>
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        private static void DestroyChild(Transform parent, string name)
        {
            Transform old = parent.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        }

        private static void SetReference(Object target, string property, Object value)
        {
            var data = new SerializedObject(target);
            var field = data.FindProperty(property);
            if (field == null)
            {
                Debug.LogError($"{Title}: {target.GetType().Name} に {property} がありません。");
                return;
            }
            field.objectReferenceValue = value;
            data.ApplyModifiedProperties();
        }
    }
}
