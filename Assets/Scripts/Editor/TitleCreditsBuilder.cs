using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Hikigiwa.Title;
using Hikigiwa.UI.Credits;
using Hikigiwa.UI.Option;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.EditorTools
{
    /// <summary>
    /// タイトルシーンに「Credits」ボタンとクレジット画面を作ります。
    /// 画面の枠（暗転・パネル・見出し・戻るボタン・スクロール）はルール画面と同じ作りです（TitleRulesBuilder）。
    /// 作ったあとは、文章や配置をシーン上で自由に編集できます。
    /// </summary>
    /// <remarks>
    /// もう一度実行すると、CreditsButton と CreditsOverlay を作り直します（シーン上で手を加えた部分は消えます）。
    /// タイトルの英字フォント（Yeseva One）は ASCII の文字しか持たないため、名前や曲名は英字で書きます。
    /// 素材を足したり外したりしたら、下の BuildContent を直してから実行し直してください。
    /// </remarks>
    public static class TitleCreditsBuilder
    {
        private const string Title = "クレジット画面を作成";
        private const string ScenePath = "Assets/Scenes/Title.unity";

        private static readonly Vector2 PanelSize = new(1100f, 940f);

        [MenuItem("Hikigiwa/タイトルにクレジット画面を作成")]
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
            Debug.Log("クレジット画面を作成しました。シーンを保存（Ctrl+S / Cmd+S）してください。");
        }

        /// <summary>コマンドラインから実行する用：Title.unity を開いて作成し、保存します。</summary>
        public static void BuildAndSave()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var canvas = Object.FindAnyObjectByType<TitleCanvas>(FindObjectsInactive.Include);
            if (canvas == null) throw new System.InvalidOperationException($"{ScenePath} に TitleCanvas がありません。");
            if (!BuildInto(canvas)) throw new System.InvalidOperationException("クレジット画面を作成できませんでした。");
            EditorSceneManager.SaveScene(scene);
            Debug.Log("クレジット画面を作成し、Title.unity を保存しました。");
        }

        private static bool BuildInto(TitleCanvas canvas)
        {
            if (!TitleRulesBuilder.Prepare(canvas, Title, out TitleView titleView, out OptionView optionView)) return false;

            Button creditsButton = TitleRulesBuilder.CreateMenuButton(titleView, "CreditsButton", "Credits", Title);
            CreditsPresenter credits = TitleRulesBuilder.CreateOverlay<CreditsView, CreditsPresenter>(canvas, optionView,
                "CreditsOverlay", "Credits", PanelSize, Title, out RectTransform content);
            BuildContent(content);

            TitleRulesBuilder.SetReference(titleView, "creditsButton", creditsButton, Title);
            TitleRulesBuilder.SetReference(canvas, "credits", credits, Title);

            // Credits ボタンを含めて、タイトルのボタンを決まった順番・間隔に並べ直す
            TitleMenuLayout.Arrange(titleView);
            return true;
        }

        // ───────────── クレジットの中身 ─────────────

        private static void BuildContent(RectTransform content)
        {
            // ゲーム名
            var title = TitleRulesBuilder.MakeText(content, "GameTitle", TitleMenuLayout.GameTitle, 64f,
                TitleRulesBuilder.Gold, TextAlignmentOptions.Center);
            title.fontStyle = FontStyles.Bold;

            // 制作メンバー
            Section(content, "Created by");
            Entry(content, "CobanzaMeg3");
            Entry(content, "aoba5550o0");
            Entry(content, "KZ");
            Entry(content, "Kei");

            // 音楽（OpenTracks）。曲名の英字は公式の表記がないため、読みをそのままローマ字にしている
            Section(content, "Music");
            Entry(content, "\"Mahoutsukai no Heya\"");
            Note(content, "Yuli Audio Craft  -  OpenTracks");

            // 効果音（OpenTracks）
            Section(content, "Sound Effects");
            Entry(content, "Causality Sound");
            Entry(content, "KK");
            Entry(content, "Make a field Music");
            Entry(content, "NaruIDEA");
            Note(content, "OpenTracks");

            // イラスト
            Section(content, "Illustrations");
            Entry(content, "Joker  -  fool");
            Note(content, "chicodeza");
            Entry(content, "Card back  -  Jun AC100V");
            Note(content, "illust AC");

            // フォント
            Section(content, "Fonts");
            Entry(content, "Yeseva One");
            Note(content, "Copyright 2012 The Yeseva One Project Authors");
            Entry(content, "Liberation Sans");
            Note(content, "SIL Open Font License 1.1");

            // ライブラリ
            Section(content, "Libraries");
            Entry(content, "UniTask");
            Note(content, "Copyright (c) 2019 Yoshifumi Kawai / Cysharp, Inc.  -  MIT License");
            Entry(content, "R3");
            Note(content, "Copyright (c) 2024 Cysharp, Inc.  -  MIT License");
            Entry(content, ".NET Libraries");
            Note(content, "Microsoft.Bcl.TimeProvider / System.Threading.Channels / System.ComponentModel.Annotations");
            Note(content, "Copyright (c) .NET Foundation and Contributors  -  MIT License");
            Entry(content, "DOTween");
            Note(content, "Copyright (c) 2014 Daniele Giardini - Demigiant");

            Section(content, "Thank you for playing!");

            // ライセンスの全文（ライブラリとフォント）。長いので一番最後に、小さい文字で左寄せにして並べる
            BuildLicenses(content);
        }

        // ───────────── ライセンスの全文 ─────────────

        /// <summary>ライブラリの通知文。「====」の行で、名前と本文が交互に区切られています。</summary>
        private const string NoticesPath = "Assets/Art/Licenses/ThirdPartyNotices.txt";

        /// <summary>フォントのライセンス文（名前と、ライセンス文のファイル）。</summary>
        private static readonly (string name, string path)[] FontLicenses =
        {
            ("Yeseva One (font)", "Assets/Art/Fonts/OFL.txt"),
            ("Liberation Sans (font)", "Assets/TextMesh Pro/Fonts/LiberationSans - OFL.txt"),
        };

        private static void BuildLicenses(RectTransform content)
        {
            Section(content, "Third-Party Licenses");

            if (File.Exists(NoticesPath))
            {
                // parts[0] は前書き。そのあとは「名前」「本文」「名前」「本文」… の順に並ぶ
                string[] parts = Regex.Split(File.ReadAllText(NoticesPath), @"^=+\s*$", RegexOptions.Multiline);
                for (int i = 1; i + 1 < parts.Length; i += 2) License(content, parts[i], parts[i + 1]);
            }
            else
            {
                Debug.LogWarning($"{Title}: ライセンスの通知文（{NoticesPath}）が見つかりません。");
            }

            foreach ((string name, string path) in FontLicenses)
            {
                if (File.Exists(path)) License(content, name, File.ReadAllText(path));
                else Debug.LogWarning($"{Title}: フォントのライセンス文（{path}）が見つかりません。");
            }
        }

        /// <summary>ライセンス1つ分。名前を少し大きく、本文を小さい文字で左寄せにして出します。</summary>
        private static void License(RectTransform parent, string name, string body)
        {
            var title = TitleRulesBuilder.MakeText(parent, "LicenseName", Clean(name), 26f, TitleRulesBuilder.Cream,
                TextAlignmentOptions.TopLeft);
            title.richText = false;
            title.fontStyle = FontStyles.Bold;

            var text = TitleRulesBuilder.MakeText(parent, "LicenseText", Clean(body), 18f, TitleRulesBuilder.CreamDim,
                TextAlignmentOptions.TopLeft);
            // ライセンス文は原文のまま出す（「<」「>」などをタグとして解釈させない）
            text.richText = false;
        }

        /// <summary>
        /// タイトルのフォント（Yeseva One）で表示できる形に整えます。
        /// 前後の空行を取り、タブを空白にし、ASCII 以外の文字は近い ASCII の文字に置き換えます。
        /// </summary>
        private static string Clean(string text)
        {
            var result = new StringBuilder(text.Length);
            foreach (char c in text.Replace("\r\n", "\n").Trim('\n', ' ', '\t'))
            {
                if (c == '\t') result.Append("    ");
                else if (c == '\n' || (c >= ' ' && c <= '~')) result.Append(c);
                else if (c == '‘' || c == '’') result.Append('\'');
                else if (c == '“' || c == '”') result.Append('"');
                else if (c == '–' || c == '—') result.Append('-');
                else if (c == '©') result.Append("(c)");
                else result.Append('?');
            }
            return result.ToString();
        }

        // ───────────── 部品（どれも中央寄せ） ─────────────

        /// <summary>区切りの線と、金色の見出し。</summary>
        private static void Section(RectTransform parent, string text)
        {
            var divider = new GameObject("Divider", typeof(RectTransform)).GetComponent<RectTransform>();
            divider.SetParent(parent, false);
            divider.gameObject.layer = parent.gameObject.layer;
            var space = divider.gameObject.AddComponent<LayoutElement>();
            space.preferredHeight = 40f;

            var line = new GameObject("Line", typeof(RectTransform)).GetComponent<RectTransform>();
            line.SetParent(divider, false);
            line.gameObject.layer = parent.gameObject.layer;
            line.anchorMin = new Vector2(0.3f, 0.5f);
            line.anchorMax = new Vector2(0.7f, 0.5f);
            line.sizeDelta = new Vector2(0f, 2f);
            var image = line.gameObject.AddComponent<Image>();
            image.color = new Color(TitleRulesBuilder.Gold.r, TitleRulesBuilder.Gold.g, TitleRulesBuilder.Gold.b, 0.35f);
            image.raycastTarget = false;

            var label = TitleRulesBuilder.MakeText(parent, "Section", text, 42f, TitleRulesBuilder.Gold,
                TextAlignmentOptions.Center);
            label.fontStyle = FontStyles.Bold;
        }

        /// <summary>名前や素材名の1行。</summary>
        private static void Entry(RectTransform parent, string text)
        {
            TitleRulesBuilder.MakeText(parent, "Entry", text, 34f, TitleRulesBuilder.Cream, TextAlignmentOptions.Center);
        }

        /// <summary>配布元やライセンスなどの補足。直前の行より小さく、薄い色で出します。</summary>
        private static void Note(RectTransform parent, string text)
        {
            TitleRulesBuilder.MakeText(parent, "Note", text, 24f, TitleRulesBuilder.CreamDim, TextAlignmentOptions.Center);
        }
    }
}
