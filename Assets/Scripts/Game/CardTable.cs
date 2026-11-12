using System;
using System.Collections;
using System.Collections.Generic;
using Hikigiwa.CardSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Hikigiwa.Game
{
    /// <summary>
    /// テーブル上の3D表示（山札の束・引いたカード・演出の光）。
    /// ルールは持たず、ReadingGameControllerから呼ばれて動くだけです。
    /// </summary>
    public sealed class CardTable : MonoBehaviour
    {
        [SerializeField] private CardAppearance appearance = new CardAppearance();

        [Header("山札の束")]
        [SerializeField] private GameObject stackModel;
        [SerializeField] private Material stackMaterial;

        [Header("配置（子オブジェクトの位置を動かすと配置が変わります）")]
        [SerializeField] private Transform stackAnchor;
        [SerializeField] private Transform revealAnchor;
        [SerializeField] private Transform rowAnchor;
        [SerializeField] private Transform foresightAnchor;
        [SerializeField] private float rowStep = 0.42f;
        [SerializeField] private float rowSpacing = 0.98f;
        [SerializeField] private int cardsPerRow = 7;

        [Header("演出")]
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Light omenLight;
        [SerializeField] private float omenLightIntensity = 6f;
        [SerializeField] private Color luckyLightColor = new Color32(255, 200, 90, 255);
        [SerializeField] private Color badLightColor = new Color32(255, 50, 60, 255);

        /// <summary>山札の束がクリックされた。</summary>
        public event Action StackClicked;

        /// <summary>生成時に計測したカードの寸法（横・縦・厚み）。</summary>
        public Vector3 CardSize { get; private set; }

        private readonly List<CardView> cards = new List<CardView>();
        private float cardScale = 1f;
        private float cardThickness;
        private Transform stackRoot;
        private Transform stackTop;
        private Vector3 stackSize;
        private BoxCollider stackCollider;
        private float stackFill = 1f;
        private Coroutine lightRoutine;

        private static Quaternion FaceUpOnTable => Quaternion.LookRotation(Vector3.down, Vector3.forward);
        private static Quaternion FaceDownOnTable => Quaternion.LookRotation(Vector3.up, Vector3.forward);
        private Quaternion FacingCamera => viewCamera.transform.rotation;

        private void Awake()
        {
            if (viewCamera == null) viewCamera = Camera.main;
            if (appearance.cardModel == null)
            {
                Debug.LogError("CardTable：カードのモデルが未設定です。メニューの「Hikigiwa/メインシーンを作成」でシーンを作り直してください。", this);
                enabled = false;
                return;
            }

            cardScale = appearance.cardHeight / ModelFitter.LongestSide(appearance.cardModel);
            CreateStack();
            if (omenLight != null) omenLight.intensity = 0f;
        }

        private void CreateStack()
        {
            stackRoot = new GameObject("山札の束").transform;
            stackRoot.SetParent(stackAnchor, false);
            var pivot = new GameObject("モデル").transform;
            pivot.SetParent(stackRoot, false);

            if (stackModel != null)
            {
                // 束は高さ（最も短い軸）を上、長辺を奥へ向ける。カードと同じ縮尺で表示する
                stackSize = ModelFitter.Fit(pivot, stackModel, Vector3.up, Vector3.forward, cardScale,
                    out GameObject model) * cardScale;
                if (stackMaterial != null)
                    foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
                        renderer.sharedMaterial = stackMaterial;
                foreach (Collider collider in model.GetComponentsInChildren<Collider>())
                    Destroy(collider);
            }
            else
            {
                stackSize = new Vector3(appearance.cardHeight * 0.71f, 0.12f, appearance.cardHeight);
            }

            // 1枚のカードの寸法を測っておく（引いたカードの置き場所や、束の上の絵柄に使う）
            CardView probe = CardView.Create(appearance, cardScale, transform, "寸法計測用");
            CardSize = probe.Size;
            cardThickness = CardSize.z;
            Vector3 cardSize = probe.Size;
            Destroy(probe.gameObject);

            if (appearance.backImageMaterial != null)
            {
                // 束の一番上の裏面。上向きに寝かせ、縦を奥へ向ける
                stackTop = CardView.CreateQuad("束の一番上の裏面", stackAnchor, appearance.backImageMaterial).transform;
                stackTop.localRotation = Quaternion.Euler(90, 0, 0);
                stackTop.localScale = new Vector3(cardSize.x * 0.97f, cardSize.y * 0.97f, 1f);
            }

            stackCollider = stackRoot.gameObject.AddComponent<BoxCollider>();
            stackCollider.size = stackSize;
            SetStackFill(1f);
        }

        /// <summary>山札の束の高さを、残り枚数の割合に合わせます。</summary>
        public void SetStackCount(int remaining, int total)
        {
            SetStackFill(total <= 0 ? 0f : (float)remaining / total);
        }

        private void SetStackFill(float fill)
        {
            stackFill = Mathf.Clamp01(fill);
            stackRoot.gameObject.SetActive(stackFill > 0f);
            float shown = Mathf.Max(stackFill, 0.08f);
            stackRoot.localScale = new Vector3(1f, shown, 1f);
            stackRoot.localPosition = Vector3.up * (stackSize.y * shown * 0.5f);
            if (stackTop != null)
            {
                stackTop.gameObject.SetActive(stackFill > 0f);
                stackTop.localPosition = Vector3.up * (stackSize.y * shown + 0.002f);
            }
        }

        private Vector3 StackTop => stackAnchor.position +
                                    Vector3.up * (stackSize.y * Mathf.Max(stackFill, 0.08f) + cardThickness);

        /// <summary>テーブル上のカードをすべて片付けます。</summary>
        public void Clear()
        {
            StopAllCoroutines();
            lightRoutine = null;
            if (omenLight != null) omenLight.intensity = 0f;
            foreach (CardView card in cards)
                if (card != null) Destroy(card.gameObject);
            cards.Clear();
        }

        /// <summary>山札から1枚引いて、カメラの前でめくり、テーブルの列に並べます。</summary>
        public IEnumerator PlayDraw(DrawResult draw, int remainingAfter, int total,
            Vector3? destination = null, Quaternion? tableRotation = null)
        {
            CardView card = SpawnFromStack(draw.Card, draw.Omen, $"引いたカード {cards.Count + 1}");
            SetStackCount(remainingAfter, total);

            yield return card.MoveTo(revealAnchor.position, FacingCamera, 0.55f, 0.35f);
            FlashOmen(draw.Omen, draw.CausedBreak);
            yield return new WaitForSeconds(draw.Omen == CardOmen.Neutral ? 0.35f : 0.7f);

            int index = cards.Count - 1;
            Vector3 slot = rowAnchor.position +
                           Vector3.right * (index % cardsPerRow * rowStep) +
                           Vector3.back * (index / cardsPerRow * rowSpacing) +
                           Vector3.up * (cardThickness * 0.5f + index * 0.002f);
            yield return card.MoveTo(destination ?? slot, tableRotation ?? FaceUpOnTable, 0.4f, 0.15f);
        }

        /// <summary>鑑定終了時に、山札の一番上を公開します（予感的中ボーナスの判定）。</summary>
        public IEnumerator PlayForesight(Card top, CardOmen omen)
        {
            CardView card = SpawnFromStack(top, omen, "予感のカード");
            yield return card.MoveTo(foresightAnchor.position, FacingCamera, 0.6f, 0.3f);
            FlashOmen(omen, false);
            yield return new WaitForSeconds(0.6f);
        }

        private CardView SpawnFromStack(Card value, CardOmen omen, string name)
        {
            CardView card = CardView.Create(appearance, cardScale, transform, name);
            card.SetFace(value, omen, appearance);
            card.transform.SetPositionAndRotation(StackTop, stackAnchor.rotation * FaceDownOnTable);
            cards.Add(card);
            return card;
        }

        /// <summary>吉凶の光を一瞬光らせます（ふつうのカードでは光らない）。</summary>
        public void FlashOmen(CardOmen omen, bool broke)
        {
            if (omenLight == null || omen == CardOmen.Neutral) return;
            omenLight.color = omen == CardOmen.Lucky ? luckyLightColor : badLightColor;
            if (lightRoutine != null) StopCoroutine(lightRoutine);
            lightRoutine = StartCoroutine(Flash(broke ? 1.6f : 0.9f));
        }

        private IEnumerator Flash(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                omenLight.intensity = omenLightIntensity * Mathf.Sin(Mathf.Min(t * 4f, 1f) * Mathf.PI * 0.5f) * (1f - t);
                yield return null;
            }
            omenLight.intensity = 0f;
            lightRoutine = null;
        }

        private void Update()
        {
            if (Pointer.current == null || !Pointer.current.press.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (stackCollider == null || !stackRoot.gameObject.activeInHierarchy) return;

            Ray ray = viewCamera.ScreenPointToRay(Pointer.current.position.ReadValue());
            if (stackCollider.Raycast(ray, out _, 100f)) StackClicked?.Invoke();
        }
    }
}
