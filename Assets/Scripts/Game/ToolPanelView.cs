using System;
using Hikigiwa.CardSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Hikigiwa.Game
{
    /// <summary>占い道具パネルのタイル1枚分の部品。</summary>
    [Serializable]
    public sealed class ToolTile
    {
        public Button Button;
        public Text ButtonLabel;
        public Text Status;
        public Text Cost;
    }

    /// <summary>
    /// ソロとCPU対戦で共通の占い道具パネル（背景を暗くする暗幕と、4種類の道具のタイル）。
    /// 部品を作る処理と、霊力・使えるかどうかの表示を更新する処理を持つ。
    /// </summary>
    public static class ToolPanelView
    {
        /// <summary>タイルの並び順（水晶・タロット・お香・手相）。</summary>
        public static readonly FortuneTool[] Tools =
            { FortuneTool.Crystal, FortuneTool.Tarot, FortuneTool.Incense, FortuneTool.Palm };

        /// <summary>タイルに出す道具の効果の説明。</summary>
        public static string DescriptionOf(FortuneTool tool)
        {
            switch (tool)
            {
                case FortuneTool.Crystal: return "次のカードが凶かどうかが分かる";
                case FortuneTool.Tarot: return "次の3枚の数字が分かる\n（順番は分からない）";
                case FortuneTool.Incense: return "山札から凶札を1枚取り除く\n（1回の鑑定で1回まで）";
                default: return "一番上のカードを一番下へ回す\n（中身は見えない）";
            }
        }

        /// <summary>
        /// 暗幕・パネル・4枚のタイルを作る。作った暗幕（パネル全体の親）を返し、最初は隠しておく。
        /// </summary>
        public static GameObject Build(Transform root, Font font, out Text spiritLabel, out Button closeButton,
            out ToolTile[] tiles)
        {
            // 背景を暗くし、後ろの山札やボタンをクリックできないようにする
            var blocker = new GameObject("ToolOverlay", typeof(RectTransform)).GetComponent<RectTransform>();
            blocker.SetParent(root, false);
            blocker.anchorMin = Vector2.zero;
            blocker.anchorMax = Vector2.one;
            blocker.offsetMin = blocker.offsetMax = Vector2.zero;
            blocker.gameObject.AddComponent<Image>().color = HudStyle.OverlayColor;

            RectTransform panel = HudFactory.Panel(blocker, "Panel", new Vector2(0.5f, 0.5f), new Vector2(0, 40),
                new Vector2(1280, 560));
            HudFactory.Named(HudFactory.Label(font, panel, "占い道具", 44, HudStyle.Gold, new Vector2(36, -24),
                new Vector2(600, 60)), "Header");
            spiritLabel = HudFactory.Named(HudFactory.Label(font, panel, "霊力　3", 34, HudStyle.Gold,
                new Vector2(-330, -30), new Vector2(400, 50), TextAnchor.UpperRight), "SpiritLabel");
            closeButton = HudFactory.Named(HudFactory.MakeButton(font, panel, HudStyle.CloseToolsCaption, HudStyle.Paper,
                new Vector2(1, 1), new Vector2(-28, -24), new Vector2(280, 64), out _), "CloseButton");

            tiles = new ToolTile[Tools.Length];
            for (int i = 0; i < Tools.Length; i++)
            {
                FortuneTool tool = Tools[i];
                var tileRect = new GameObject(tool + "Tile", typeof(RectTransform)).GetComponent<RectTransform>();
                tileRect.SetParent(panel, false);
                tileRect.anchorMin = tileRect.anchorMax = tileRect.pivot = new Vector2(0, 1);
                tileRect.anchoredPosition = new Vector2(36 + i * 306, -112);
                tileRect.sizeDelta = new Vector2(288, 410);
                tileRect.gameObject.AddComponent<Image>().color = HudStyle.TileColor;

                HudFactory.Named(HudFactory.Label(font, tileRect, HudStyle.NameOf(tool), 42, HudStyle.Paper,
                    new Vector2(0, -20), new Vector2(288, 56), TextAnchor.MiddleCenter), "NameLabel");
                Text description = HudFactory.Named(HudFactory.Label(font, tileRect, DescriptionOf(tool), 24, HudStyle.Dim,
                    new Vector2(0, -142), new Vector2(260, 120), TextAnchor.UpperCenter), "DescriptionLabel");
                description.horizontalOverflow = HorizontalWrapMode.Wrap;

                var tile = new ToolTile();
                tile.Cost = HudFactory.Named(HudFactory.Label(font, tileRect, "霊力 1", 30, HudStyle.Gold,
                    new Vector2(0, -84), new Vector2(288, 44), TextAnchor.MiddleCenter), "CostLabel");
                tile.Status = HudFactory.Named(HudFactory.Label(font, tileRect, "", 22, HudStyle.Red,
                    new Vector2(0, -268), new Vector2(260, 34), TextAnchor.MiddleCenter), "StatusLabel");
                tile.Button = HudFactory.Named(HudFactory.MakeButton(font, tileRect, "使う", HudStyle.Gold,
                    new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(240, 80), out tile.ButtonLabel), "UseButton");
                tiles[i] = tile;
            }

            blocker.gameObject.SetActive(false);
            return blocker.gameObject;
        }

        /// <summary>タイルがすべてそろっているか（古い形式のパネルが残っていないか）。</summary>
        public static bool IsComplete(ToolTile[] tiles, Text spiritLabel)
        {
            if (tiles == null || tiles.Length != Tools.Length || spiritLabel == null) return false;
            foreach (ToolTile tile in tiles)
            {
                if (tile == null || tile.Button == null || tile.ButtonLabel == null || tile.Status == null || tile.Cost == null)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 霊力・各道具のコスト・使えるかどうかを表示する。canAct が false（相手の手番や演出中）のときはすべて使えない。
        /// </summary>
        public static void Refresh(ToolTile[] tiles, Text spiritLabel, ReadingSession session, int spirit, bool canAct)
        {
            spiritLabel.text = $"霊力　{spirit}";
            for (int i = 0; i < Tools.Length; i++)
            {
                FortuneTool tool = Tools[i];
                ToolTile tile = tiles[i];
                tile.Cost.text = $"霊力 {session.CostOf(tool)}";
                ToolBlockReason reason = canAct ? session.CheckTool(tool, spirit) : ToolBlockReason.NotReading;
                bool usable = reason == ToolBlockReason.None;
                tile.Button.interactable = usable;
                tile.ButtonLabel.text = usable ? "使う" : "使えない";
                tile.Status.text = HudStyle.ReasonOf(reason);
            }
        }
    }
}
