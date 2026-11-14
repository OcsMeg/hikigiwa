using Hikigiwa.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.Game
{
    /// <summary>
    /// HUDの部品（パネル・ラベル・ボタン）をコードで作る共通の関数。見た目は HudStyle の値を使う。
    /// </summary>
    public static class HudFactory
    {
        /// <summary>部品のGameObjectの名前を付けて、そのまま返す。</summary>
        public static T Named<T>(T component, string name) where T : Component
        {
            component.gameObject.name = name;
            return component;
        }

        /// <summary>クリックを妨げない半透明の背景パネル。</summary>
        public static RectTransform Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = HudStyle.PanelColor;
            image.raycastTarget = false;
            return rect;
        }

        /// <summary>
        /// 親の上端を基準に置くラベル。左寄せは左上、右寄せは右上、中央寄せは上中央が基準。
        /// </summary>
        public static Text Label(Font font, Transform parent, string value, int size, Color color, Vector2 position,
            Vector2 box, TextAnchor alignment = TextAnchor.UpperLeft)
        {
            var rect = new GameObject("Label", typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Vector2 corner;
            switch (alignment)
            {
                case TextAnchor.UpperRight:
                case TextAnchor.MiddleRight:
                    corner = new Vector2(1, 1);
                    break;
                case TextAnchor.UpperCenter:
                case TextAnchor.MiddleCenter:
                    corner = new Vector2(0.5f, 1);
                    break;
                default:
                    corner = new Vector2(0, 1);
                    break;
            }
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>背景色と中央の文字を持つボタン。</summary>
        public static Button MakeButton(Font font, Transform parent, string caption, Color color, Vector2 anchor,
            Vector2 position, Vector2 size, out Text label)
        {
            var rect = new GameObject(caption, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            label = Label(font, rect, caption, HudStyle.ButtonFontSize(size), HudStyle.ButtonText, Vector2.zero, size,
                TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            AddButtonEffects(button);
            return button;
        }

        /// <summary>
        /// ボタンに、触れたときの見た目の動き（ButtonAnimation）と効果音（ButtonSound）を付けます。タイトルのボタンと同じものです。
        /// すでに付いていれば何もしません。何か変えたときは true を返します。
        /// </summary>
        /// <remarks>
        /// ButtonAnimation は基準点（pivot）を中心に大きさを変えるので、基準点が角にあると角を起点にふくらんでしまいます。
        /// そのため、見た目の位置を変えずに基準点を中央へ移してから付けます。
        /// </remarks>
        public static bool AddButtonEffects(Button button)
        {
            bool changed = CenterPivot((RectTransform)button.transform);
            if (!button.TryGetComponent(out ButtonAnimation _))
            {
                button.gameObject.AddComponent<ButtonAnimation>();
                changed = true;
            }
            if (!button.TryGetComponent(out ButtonSound _))
            {
                button.gameObject.AddComponent<ButtonSound>();
                changed = true;
            }
            return changed;
        }

        /// <summary>見た目の位置と大きさを変えずに、基準点（pivot）を中央へ移します。移したときは true を返します。</summary>
        public static bool CenterPivot(RectTransform rect)
        {
            var center = new Vector2(0.5f, 0.5f);
            if (rect.pivot == center) return false;
            // 基準点を動かした分だけ位置をずらすと、四角形そのものは同じ場所に残る
            rect.anchoredPosition += Vector2.Scale(center - rect.pivot, rect.rect.size);
            rect.pivot = center;
            return true;
        }
    }
}
