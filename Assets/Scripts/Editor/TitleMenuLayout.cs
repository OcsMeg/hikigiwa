using System.Collections.Generic;
using Hikigiwa.Title;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.EditorTools
{
    /// <summary>
    /// タイトル画面のロゴにゲーム名を入れ（仮置きのサブタイトルは消す）、ボタンを「Solo・CPU・Options・Rules」の順に縦に並べます。
    /// CPU ボタンがまだなければ Solo ボタンを複製して作り、TitleView に割り当てます。
    /// </summary>
    /// <remarks>
    /// ボタンの見た目（大きさ・色・文字の大きさなど）は変えず、位置・順番・文字だけを決めます。
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

        /// <summary>ボタンの文字の大きさ。4つともこの大きさにそろえます。</summary>
        private const float ButtonFontSize = 50f;

        /// <summary>一番上のボタンの高さ（画面中央が 0）。</summary>
        public const float FirstY = -60f;

        /// <summary>ボタンの縦の間隔。</summary>
        public const float Step = 120f;

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
                rect.SetSiblingIndex(firstIndex + i);
                SetFontSize(order[i], ButtonFontSize);
            }
            return true;
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
