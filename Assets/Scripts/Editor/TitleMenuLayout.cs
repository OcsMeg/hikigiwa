using System.Collections.Generic;
using System.IO;
using Hikigiwa.Title;
using Hikigiwa.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.EditorTools
{
    /// <summary>
    /// タイトル画面のロゴにゲーム名を入れ（仮置きのサブタイトルは消す）、ボタンを「Solo・CPU・Options・Rules」の順に縦に並べます。Credits ボタンがあれば、画面の右下の端に小さめに置きます。
    /// CPU ボタンがまだなければ Solo ボタンを複製して作り、TitleView に割り当てます。
    /// </summary>
    /// <remarks>
    /// ボタンの位置・順番・文字を決め、見た目を「濃い紫の板＋金の縁」にそろえます（各画面の Back や Reset も含む）。
    /// 何度実行しても同じ結果になります。
    /// </remarks>
    public static class TitleMenuLayout
    {
        /// <summary>ゲーム名。タイトルのロゴとルール説明に使います。</summary>
        public const string GameTitle = "Fortune or Fold";

        private const string Title = "タイトルのボタンを並べる";
        private const string ScenePath = "Assets/Scenes/Title.unity";

        /// <summary>ロゴの文字の大きさの上限と下限。ゲーム名が長くても枠からはみ出さないよう自動で縮めます。</summary>
        private const float LogoMaxSize = 130f;
        private const float LogoMinSize = 70f;

        /// <summary>ロゴの高さ（画面中央が 0）。</summary>
        private const float LogoY = 290f;

        /// <summary>ボタンの文字の大きさ。すべてこの大きさにそろえます。</summary>
        private const float ButtonFontSize = 50f;

        /// <summary>一番上のボタンの高さ（画面中央が 0）。</summary>
        public const float FirstY = -30f;

        /// <summary>縦に並ぶボタン1つの大きさ。</summary>
        private static readonly Vector2 ButtonSize = new(310f, 90f);

        /// <summary>ボタンの縦の間隔（ボタンの高さ 90 ＋ すき間 30）。</summary>
        public const float Step = 120f;

        /// <summary>Credits ボタンは縦の並びに入れず、画面の右下の端に小さめに置きます。</summary>
        private static readonly Vector2 CreditsSize = new(240f, 70f);
        private static readonly Vector2 CreditsMargin = new(50f, 36f); // 右端・下端からの余白
        private const float CreditsFontSize = 36f;

        [MenuItem("Hikigiwa/タイトルのボタンを並べる")]
        public static void Arrange()
        {
            var view = Object.FindAnyObjectByType<TitleView>(FindObjectsInactive.Include);
            if (view == null)
            {
                EditorUtility.DisplayDialog(Title, "開いているシーンに TitleView が見つかりません。Title.unity を開いてください。", "OK");
                return;
            }

            if (!Arrange(view)) return;

            EditorSceneManager.MarkSceneDirty(view.gameObject.scene);
            Debug.Log("タイトルのボタンを並べました。シーンを保存（Ctrl+S / Cmd+S）してください。");
        }

        /// <summary>コマンドラインから実行する用：Title.unity を開いて並べ、保存します。</summary>
        public static void ArrangeAndSave()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var view = Object.FindAnyObjectByType<TitleView>(FindObjectsInactive.Include);
            if (view == null || !Arrange(view))
                throw new System.InvalidOperationException($"{ScenePath} のボタンを並べられませんでした。");
            EditorSceneManager.SaveScene(scene);
        }

        public static bool Arrange(TitleView view)
        {
            Button solo = view.SoloButton;
            if (solo == null)
            {
                Debug.LogError($"{Title}: TitleView に Solo ボタン（以前の Start ボタン）が割り当てられていません。");
                return false;
            }

            SetLogo(view);

            Undo.RecordObject(solo.gameObject, Title);
            solo.name = "SoloButton";
            SetLabel(solo, "Solo");

            Button cpu = view.CpuButton;
            if (cpu == null)
            {
                cpu = Object.Instantiate(solo.gameObject, solo.transform.parent).GetComponent<Button>();
                Undo.RegisterCreatedObjectUndo(cpu.gameObject, Title);
                cpu.name = "CpuButton";

                var data = new SerializedObject(view);
                data.FindProperty("cpuButton").objectReferenceValue = cpu;
                data.ApplyModifiedProperties();
            }
            SetLabel(cpu, "CPU");

            var order = new List<Button> { solo, cpu };
            if (view.OptionButton != null) order.Add(view.OptionButton);
            if (view.RulesButton != null) order.Add(view.RulesButton);

            // 上から順に並べ、Hierarchy の順番もそろえる（キーボード操作で選ばれる順番が見た目と合うように）
            int firstIndex = int.MaxValue;
            foreach (var button in order) firstIndex = Mathf.Min(firstIndex, button.transform.GetSiblingIndex());

            for (int i = 0; i < order.Count; i++)
            {
                var rect = (RectTransform)order[i].transform;
                Undo.RecordObject(rect, Title);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, FirstY - Step * i);
                rect.sizeDelta = ButtonSize;
                rect.SetSiblingIndex(firstIndex + i);
                SetFontSize(order[i], ButtonFontSize);
            }

            PlaceCredits(view, firstIndex + order.Count);
            StyleButtons(view);
            return true;
        }

        // ───────────── ボタンの見た目（濃い紫の板＋金の縁） ─────────────

        /// <summary>縁の太さ。</summary>
        private const float BorderWidth = 3f;

        /// <summary>板の色（設定画面やルール画面のパネルと同じ濃い紫）。</summary>
        private static readonly Color PlateColor = new(0.14117648f, 0.09803922f, 0.21176471f, 0.92f);

        /// <summary>縁の明るさ。ふだんは控えめにして、触れたときに明るい金色にする。</summary>
        private const float BorderNormalAlpha = 0.55f;

        /// <summary>
        /// タイトルシーンのボタンすべて（各画面の Back や Reset も含む）を、同じ見た目にそろえます。
        /// ボタン自身の画像を金の縁にし、その内側に濃い紫の板（Fill）を敷きます。
        /// 触れたときは、縁が明るくなり（Button の Color Tint）、文字が金色になります（ButtonLabelTint）。
        /// </summary>
        private static void StyleButtons(TitleView view)
        {
            Canvas canvas = view.GetComponentInParent<Canvas>(true);
            if (canvas == null) return;

            foreach (Button button in canvas.GetComponentsInChildren<Button>(true)) StyleButton(button);
        }

        /// <summary>角の丸い板の画像。なければ作ります（白一色。色は Image 側で付ける）。</summary>
        private const string RoundedSpritePath = "Assets/Art/UI/RoundedRect.png";
        private const int RoundedTextureSize = 64;

        /// <summary>角の丸みの半径。大きくするほど丸くなります（RoundedTextureSize の半分未満にすること）。</summary>
        private const int CornerRadius = 28;

        /// <summary>
        /// 角の丸い板のスプライトを返します。まだない、または半径の設定が変わっていたら、画像を作り直します。
        /// 9スライス（角を伸ばさず、辺と中央だけを伸ばす）で使うので、ボタンの大きさが違っても角の丸みは同じです。
        /// </summary>
        private static Sprite RoundedSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
            if (sprite != null && Mathf.RoundToInt(sprite.border.x) == CornerRadius) return sprite;

            int size = RoundedTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 角丸の四角形の縁までの距離（内側が負）。縁の1ピクセルをなめらかにする
                    float qx = Mathf.Abs(x + 0.5f - half) - (half - CornerRadius);
                    float qy = Mathf.Abs(y + 0.5f - half) - (half - CornerRadius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - CornerRadius;
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(0.5f - distance) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            Directory.CreateDirectory(Path.GetDirectoryName(RoundedSpritePath));
            File.WriteAllBytes(RoundedSpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(RoundedSpritePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(RoundedSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(RoundedSpritePath);
        }

        private static void StyleButton(Button button)
        {
            Sprite sprite = RoundedSprite();
            if (sprite == null)
            {
                Debug.LogError($"{Title}: 角の丸い板の画像（{RoundedSpritePath}）を作れませんでした。");
                return;
            }

            // 縁：ボタン自身の画像（クリックの当たり判定もここ）
            var border = button.targetGraphic as Image;
            if (border == null) border = button.GetComponent<Image>();
            if (border == null) return;
            Undo.RecordObject(border, Title);
            border.sprite = sprite;
            border.type = Image.Type.Sliced;
            border.pixelsPerUnitMultiplier = 1f;
            border.color = TitleRulesBuilder.Gold;

            Undo.RecordObject(button, Title);
            button.targetGraphic = border;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(1f, 1f, 1f, BorderNormalAlpha);
            colors.highlightedColor = Color.white;
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            // 画面の切り替え中（操作不可の間）に暗くなってちらつかないよう、ふだんと同じにする
            colors.disabledColor = colors.normalColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.1f;
            button.colors = colors;

            // 板：縁の内側に敷く。文字より後ろ（最初の子）に置く
            var fill = button.transform.Find("Fill") as RectTransform;
            if (fill == null)
            {
                fill = new GameObject("Fill", typeof(RectTransform)).GetComponent<RectTransform>();
                fill.SetParent(button.transform, false);
                fill.gameObject.layer = button.gameObject.layer;
                fill.gameObject.AddComponent<Image>();
                Undo.RegisterCreatedObjectUndo(fill.gameObject, Title);
            }
            fill.SetAsFirstSibling();
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = Vector2.one;
            fill.offsetMin = new Vector2(BorderWidth, BorderWidth);
            fill.offsetMax = new Vector2(-BorderWidth, -BorderWidth);
            var plate = fill.GetComponent<Image>();
            plate.sprite = sprite;
            plate.type = Image.Type.Sliced;
            // 板は縁の太さだけ内側にあるので、角の丸みもその分だけ小さくして、縁の太さを角でも一定にする
            plate.pixelsPerUnitMultiplier = CornerRadius / (CornerRadius - BorderWidth);
            plate.color = PlateColor;
            plate.raycastTarget = false;

            // 文字：触れたときに金色にする
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            Undo.RecordObject(label, Title);
            label.color = TitleRulesBuilder.Cream;

            var tint = button.GetComponent<ButtonLabelTint>();
            if (tint == null) tint = Undo.AddComponent<ButtonLabelTint>(button.gameObject);
            var data = new SerializedObject(tint);
            data.FindProperty("label").objectReferenceValue = label;
            data.FindProperty("normalColor").colorValue = TitleRulesBuilder.Cream;
            data.FindProperty("highlightColor").colorValue = TitleRulesBuilder.Gold;
            data.ApplyModifiedProperties();
        }

        /// <summary>Credits ボタンを画面の右下の端に置きます。ボタンがなければ何もしません。</summary>
        private static void PlaceCredits(TitleView view, int siblingIndex)
        {
            Button credits = view.CreditsButton;
            if (credits == null) return;

            var rect = (RectTransform)credits.transform;
            Undo.RecordObject(rect, Title);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = CreditsSize;
            rect.anchoredPosition = new Vector2(-CreditsMargin.x, CreditsMargin.y);
            // 縦に並ぶボタンのあとに置き、キーボード操作では最後に選ばれるようにする
            rect.SetSiblingIndex(siblingIndex);
            SetFontSize(credits, CreditsFontSize);
        }

        /// <summary>タイトル画面の「Logo」で始まる名前の文字にゲーム名を入れます。見つからなければ何もしません。</summary>
        private static void SetLogo(TitleView view)
        {
            foreach (Transform child in view.transform)
            {
                if (!child.name.StartsWith("Logo")) continue;
                var logo = child.GetComponent<TMP_Text>();
                if (logo == null) continue;

                Undo.RecordObject(logo, Title);
                logo.text = GameTitle;
                logo.enableAutoSizing = true;
                logo.fontSizeMax = LogoMaxSize;
                logo.fontSizeMin = LogoMinSize;

                var rect = (RectTransform)child;
                Undo.RecordObject(rect, Title);
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, LogoY);

                RemoveSubtitle(view);
                return;
            }
            Debug.LogWarning($"{Title}: タイトル画面にロゴ（名前が Logo で始まる文字）が見つかりません。");
            RemoveSubtitle(view);
        }

        /// <summary>仮置きのサブタイトル（名前が Subtitle で始まるもの）を消します。なければ何もしません。</summary>
        private static void RemoveSubtitle(TitleView view)
        {
            for (int i = view.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = view.transform.GetChild(i);
                if (child.name.StartsWith("Subtitle")) Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        private static void SetFontSize(Button button, float size)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            Undo.RecordObject(label, Title);
            label.fontSize = size;
        }

        private static void SetLabel(Button button, string text)
        {
            var label = button.GetComponentInChildren<TMP_Text>(true);
            if (label == null) return;
            Undo.RecordObject(label, Title);
            label.text = text;
        }
    }
}
