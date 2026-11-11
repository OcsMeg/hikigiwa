using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Hikigiwa.CardSystem.Development
{
    /// <summary>山札ロジックを3Dの仮カードで確認する、テストシーン専用の表示。</summary>
    public sealed class CardSystemTestView : MonoBehaviour
    {
        [SerializeField] private Font japaneseFont;

        private static readonly Color Background = new Color32(25, 22, 36, 255);
        private static readonly Color Paper = new Color32(246, 229, 199, 255);
        private static readonly Color Gold = new Color32(231, 191, 115, 255);
        private static readonly Color Purple = new Color32(69, 47, 93, 255);
        private static readonly Vector3 CardSize = new Vector3(1.45f, 2f, .035f);
        private static readonly Vector3 DisplayPosition = new Vector3(0, .9f, -.1f);

        private readonly Deck deck = new Deck();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<string> history = new List<string>();
        private readonly List<GameObject> stackCards = new List<GameObject>();
        private Camera viewCamera;
        private Transform pile;
        private BoxCollider pileCollider;
        private Transform revealedCard;
        private GameObject revealedFace;
        private Material backMaterial;
        private Material paperMaterial;
        private Material goldMaterial;
        private Text remainingText;
        private Text latestText;
        private Text historyText;
        private Button drawButton;
        private Button resetButton;
        private bool drawing;
        private Card lastCard;

        private void Awake()
        {
            if (japaneseFont == null)
            {
                Debug.LogError("CardSystemTestViewの日本語フォントが未設定です。", this);
                return;
            }

            CreateTable();
            CreateHud();
            ResetDeck();
        }

        private void CreateTable()
        {
            viewCamera = Camera.main;
            if (viewCamera == null)
                viewCamera = new GameObject("Card Test Camera", typeof(Camera)).GetComponent<Camera>();
            viewCamera.transform.position = new Vector3(0, 5f, -7f);
            viewCamera.transform.LookAt(DisplayPosition);
            viewCamera.fieldOfView = 40f;
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = Background;

            backMaterial = MakeMaterial(Purple);
            paperMaterial = MakeMaterial(Paper);
            goldMaterial = MakeMaterial(Gold);
            Material tableMaterial = MakeMaterial(new Color32(34, 67, 64, 255));
            Cube("仮のテーブル", transform, new Vector3(0, -.22f, .7f),
                new Vector3(9f, .4f, 6.5f), tableMaterial);

            pile = new GameObject("山札の束").transform;
            pile.SetParent(transform, false);
            pile.localPosition = new Vector3(-2.1f, .04f, .25f);
            pileCollider = pile.gameObject.AddComponent<BoxCollider>();
        }

        private void CreateHud()
        {
            if (EventSystem.current == null)
                new GameObject("Card Test Input", typeof(EventSystem), typeof(InputSystemUIInputModule))
                    .transform.SetParent(transform, false);

            var canvasObject = new GameObject("Card Test HUD", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 540);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var panel = CreateRect(canvasObject.transform, "Test Panel", 0, 0, 960, 540);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;

            Label(panel, "カードシステム 3Dテスト", 28, 12, 650, 48, 27, Gold);
            remainingText = Label(panel, "", 696, 12, 236, 48, 25, Paper, TextAnchor.MiddleRight);
            Label(panel, "山札をクリック、または「1枚引く」で中央にカードを出します。",
                28, 61, 904, 37, 19, Paper);
            latestText = Label(panel, "", 250, 365, 682, 42, 23, Gold, TextAnchor.MiddleCenter);
            historyText = Label(panel, "", 28, 407, 904, 35, 18, Paper);
            drawButton = MakeButton(panel, "1枚引く", 28, 460, 430, Gold, DrawCard);
            resetButton = MakeButton(panel, "山札を作り直す", 478, 460, 454, Paper, ResetDeck);
        }

        private void Update()
        {
            if (drawing || deck.Remaining == 0 || Pointer.current == null || pileCollider == null) return;
            if (!Pointer.current.press.wasPressedThisFrame) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

            Ray ray = viewCamera.ScreenPointToRay(Pointer.current.position.ReadValue());
            if (pileCollider.Raycast(ray, out _, 100f)) DrawCard();
        }

        private void DrawCard()
        {
            if (drawing || !deck.TryDraw(out Card card)) return;
            drawing = true;
            lastCard = card;
            history.Add(card.IsJoker ? "J" : card.Number.ToString());
            SetButtons();
            latestText.text = "カードをめくっています";
            UpdateRemaining();
            GameObject top = stackCards[stackCards.Count - 1];
            stackCards.RemoveAt(stackCards.Count - 1);
            top.SetActive(false);
            Destroy(top);
            UpdatePileCollider();
            if (revealedCard != null) Destroy(revealedCard.gameObject);
            StartCoroutine(Reveal(card));
        }

        private IEnumerator Reveal(Card card)
        {
            revealedCard = new GameObject("引いたカード").transform;
            revealedCard.SetParent(transform, false);
            revealedCard.position = pile.position + Vector3.up * (.018f * deck.Remaining + .1f);
            revealedCard.rotation = Quaternion.Euler(90, 0, 0);
            Cube("カード本体", revealedCard, Vector3.zero, CardSize, backMaterial);
            AddBackDecoration(revealedCard);
            revealedFace = CreateFace(revealedCard, card);
            revealedFace.SetActive(false);

            Vector3 origin = revealedCard.position;
            Quaternion startRotation = revealedCard.rotation;
            Quaternion endRotation = viewCamera.transform.rotation;
            const float duration = .65f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                revealedCard.position = Vector3.Lerp(origin, DisplayPosition, eased) +
                    Vector3.up * (Mathf.Sin(t * Mathf.PI) * .5f);
                revealedCard.rotation = Quaternion.Slerp(startRotation, endRotation, eased);
                revealedFace.SetActive(t >= .55f);
                yield return null;
            }
            FinishReveal();
        }

        private GameObject CreateFace(Transform card, Card value)
        {
            var face = new GameObject("カードの表");
            face.transform.SetParent(card, false);
            Cube("紙", face.transform, new Vector3(0, 0, -.05f),
                new Vector3(1.37f, 1.92f, .012f), paperMaterial);

            var canvasObject = new GameObject("カードの数字", typeof(RectTransform), typeof(Canvas));
            canvasObject.transform.SetParent(face.transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(180, 260);
            rect.localPosition = new Vector3(0, 0, -.065f);
            rect.localScale = Vector3.one * .007f;
            Label(rect, value.IsJoker ? "J" : value.Number.ToString(),
                0, 0, 180, 260, 140, Background, TextAnchor.MiddleCenter);
            return face;
        }

        private void ResetDeck()
        {
            if (drawing) return;
            deck.Reset();
            history.Clear();
            historyText.text = "引いた順";
            latestText.text = "左の山札から1枚引いてみましょう";
            if (revealedCard != null) Destroy(revealedCard.gameObject);
            foreach (GameObject card in stackCards)
            {
                card.SetActive(false);
                Destroy(card);
            }
            stackCards.Clear();
            for (int i = 0; i < Deck.TotalCards; i++)
            {
                var card = new GameObject($"山札 {i + 1}");
                card.transform.SetParent(pile, false);
                card.transform.localPosition = new Vector3(0, .018f * i, 0);
                card.transform.localRotation = Quaternion.Euler(90, 0, 0);
                Cube("裏向きカード", card.transform, Vector3.zero, CardSize, backMaterial);
                AddBackDecoration(card.transform);
                stackCards.Add(card);
            }
            UpdatePileCollider();
            UpdateRemaining();
            SetButtons();
        }

        private void AddBackDecoration(Transform card)
        {
            Cube("金の縁", card, new Vector3(0, 0, -.022f), new Vector3(1.28f, 1.82f, .006f), goldMaterial);
            Cube("紫の裏面", card, new Vector3(0, 0, -.027f), new Vector3(1.19f, 1.73f, .006f), backMaterial);
            Transform seal = Cube("印", card, new Vector3(0, 0, -.032f),
                new Vector3(.3f, .3f, .006f), goldMaterial);
            seal.localRotation = Quaternion.Euler(0, 0, 45);
        }

        private void UpdatePileCollider()
        {
            pileCollider.enabled = deck.Remaining > 0;
            float height = .018f * deck.Remaining + .08f;
            pileCollider.size = new Vector3(CardSize.x, height, CardSize.y);
            pileCollider.center = new Vector3(0, height * .5f, 0);
        }

        private void SetButtons()
        {
            drawButton.interactable = !drawing && deck.Remaining > 0;
            resetButton.interactable = !drawing;
        }

        private void UpdateRemaining() => remainingText.text = $"残り {deck.Remaining} / {Deck.TotalCards} 枚";

        private Material MakeMaterial(Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = new Material(shader);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", .18f);
            materials.Add(material);
            return material;
        }

        private static Transform Cube(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            var collider = cube.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            return cube.transform;
        }

        private void FinishReveal()
        {
            revealedCard.SetPositionAndRotation(DisplayPosition, viewCamera.transform.rotation);
            revealedFace.SetActive(true);
            latestText.text = lastCard.IsJoker ? "引いた札　ジョーカー" : $"引いた札　{lastCard.Number}";
            historyText.text = "引いた順　" + string.Join("  ", history);
            drawing = false;
            SetButtons();
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            if (drawing) FinishReveal();
        }

        private void OnDestroy()
        {
            foreach (Material material in materials) Destroy(material);
        }

        private static RectTransform CreateRect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        private Text Label(Transform parent, string value, float x, float y, float width, float height,
            int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var text = CreateRect(parent, "Text", x, y, width, height).gameObject.AddComponent<Text>();
            text.font = japaneseFont;
            text.fontSize = size;
            text.text = value;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        private Button MakeButton(Transform parent, string caption, float x, float y, float width,
            Color color, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateRect(parent, caption, x, y, width, 60);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(onClick);
            Label(rect, caption, 0, 0, width, 60, 25, Background, TextAnchor.MiddleCenter);
            return button;
        }
    }
}
