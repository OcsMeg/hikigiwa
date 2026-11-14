using System.Collections;
using Hikigiwa.CardSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.Game
{
    /// <summary>
    /// 3Dのカード1枚。
    /// ローカル座標で X＝横、Y＝縦（上端が+Y）、Z＝厚み。表面は -Z 側、裏面は +Z 側です。
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        private const float FaceOffset = 0.0015f;
        private const float FacePixelWidth = 712f;
        private const float FacePixelHeight = 1008f;

        public Vector3 Size { get; private set; }
        public Card Card { get; private set; }

        /// <summary>カードを生成します。表面はまだ付きません（SetFaceで付ける）。</summary>
        public static CardView Create(CardAppearance appearance, float scale, Transform parent, string name)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<CardView>();

            var pivot = new GameObject("モデル").transform;
            pivot.SetParent(root.transform, false);
            Vector3 size = ModelFitter.Fit(pivot, appearance.cardModel, Vector3.forward, Vector3.up, scale,
                out GameObject model);
            view.Size = size * scale;

            if (appearance.backMaterial != null)
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterial = appearance.backMaterial;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>())
                Destroy(collider);

            if (appearance.backImageMaterial != null)
            {
                // 裏面は +Z 側。Quadは -Z 側から見える向きなので、Y軸で半回転させる
                Transform back = CreateQuad("裏面", root.transform, appearance.backImageMaterial).transform;
                back.localPosition = new Vector3(0, 0, view.Size.z * 0.5f + FaceOffset);
                back.localRotation = Quaternion.Euler(0, 180, 0);
                back.localScale = new Vector3(view.Size.x * 0.97f, view.Size.y * 0.97f, 1f);
            }
            return view;
        }

        /// <summary>当たり判定のない板（Quad）を作ります。</summary>
        public static GameObject CreateQuad(string name, Transform parent, Material material)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = name;
            Destroy(quad.GetComponent<Collider>());
            quad.transform.SetParent(parent, false);
            quad.GetComponent<Renderer>().sharedMaterial = material;
            return quad;
        }

        /// <summary>表面（数字またはジョーカー）を付けます。</summary>
        public void SetFace(Card card, CardOmen omen, CardAppearance appearance)
        {
            Card = card;
            float z = -(Size.z * 0.5f + FaceOffset);

            GameObject face = CreateQuad("表面", transform,
                card.IsJoker ? appearance.jokerMaterial : appearance.faceMaterial);
            face.transform.localPosition = new Vector3(0, 0, z);
            // 枠が本体からはみ出さないよう、わずかに小さくする
            face.transform.localScale = new Vector3(Size.x * 0.97f, Size.y * 0.97f, 1f);

            if (card.IsJoker) return;

            Color color = omen == CardOmen.Lucky ? appearance.luckyColor
                : omen == CardOmen.Bad ? appearance.badColor
                : appearance.inkColor;

            var canvasObject = new GameObject("数字", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(FacePixelWidth, FacePixelHeight);
            rect.localPosition = new Vector3(0, 0, z - FaceOffset);
            rect.localScale = Vector3.one * (Size.x * 0.97f / FacePixelWidth);

            string number = card.Number.ToString();
            AddText(rect, number, 520, color, Vector2.zero, new Vector2(600, 600), 0);
            AddText(rect, number, 130, color, new Vector2(-268, 400), new Vector2(150, 150), 0);
            AddText(rect, number, 130, color, new Vector2(268, -400), new Vector2(150, 150), 180);
            if (omen != CardOmen.Neutral)
                AddText(rect, omen == CardOmen.Lucky ? "LUCKY" : "UNLUCKY", 95, color, new Vector2(0, -330),
                    new Vector2(600, 160), 0);

            void AddText(RectTransform parent, string value, int size, Color textColor, Vector2 position,
                Vector2 box, float angle)
            {
                var textRect = new GameObject(value, typeof(RectTransform)).GetComponent<RectTransform>();
                textRect.SetParent(parent, false);
                textRect.anchoredPosition = position;
                textRect.sizeDelta = box;
                textRect.localRotation = Quaternion.Euler(0, 0, angle);
                var text = textRect.gameObject.AddComponent<Text>();
                text.font = appearance.font;
                text.fontSize = size;
                text.text = value;
                text.color = textColor;
                text.alignment = TextAnchor.MiddleCenter;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.raycastTarget = false;
            }
        }

        /// <summary>位置と向きを、放物線を描きながら滑らかに変えます。</summary>
        public IEnumerator MoveTo(Vector3 position, Quaternion rotation, float duration, float arcHeight)
        {
            Vector3 startPosition = transform.position;
            Quaternion startRotation = transform.rotation;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(startPosition, position, eased) +
                                     Vector3.up * (Mathf.Sin(t * Mathf.PI) * arcHeight);
                transform.rotation = Quaternion.Slerp(startRotation, rotation, eased);
                yield return null;
            }
            transform.SetPositionAndRotation(position, rotation);
        }
    }
}
