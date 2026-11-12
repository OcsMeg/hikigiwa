using System;
using System.Collections;
using Hikigiwa.CardSystem;
using Hikigiwa.Game;
using UnityEngine;

namespace Hikigiwa.Battle
{
    /// <summary>カードの生成・めくり・二人分の置き場を担当。判定はBattleMatchに任せる。</summary>
    public sealed class BattleCardDealer : MonoBehaviour
    {
        // 1段の枚数を変更する場合はここを調整する。段番号と最後の段の中央寄せにも共通で使う。
        private const int CardsPerRow = 7;
        private const int ParticipantCount = 2;
        // 机との隙間と重なり面の高さ補正（ワールド単位）。
        private const float TableClearance = 0.015f;
        private const float RowSurfaceClearance = 0.006f;
        private const float ColumnHeightOffset = 0.0003f;

        /// <summary>Mainと共通のカード生成・移動・吉凶演出を行う表示コンポーネント。</summary>
        [SerializeField] private CardTable table;
        /// <summary>プレイヤーの最初のカードの基準位置。rightが画面の横方向、forwardが部屋の奥方向。</summary>
        [SerializeField] private Transform playerRow;
        /// <summary>CPUの最初のカードの基準位置。プレイヤーと同じ横方向に並べる。</summary>
        [SerializeField] private Transform cpuRow;
        /// <summary>同じ段で隣り合うカード中心の間隔。カード幅より小さくして重ねて表示する。</summary>
        [SerializeField] private float columnStep = 0.33f;
        /// <summary>折り返した次の段へずらす机上の奥行き間隔。</summary>
        [SerializeField] private float rowStep = 0.68f;
        /// <summary>2枚目以降の左端を持ち上げる角度。最初の1枚には適用しない。</summary>
        [SerializeField, Range(0f, 20f), Tooltip("重なった左端を持ち上げる角度")] private float cardTiltDegrees = 8f;
        /// <summary>側ごとの表示枚数。添字はBattleSideの値で、ラウンド開始時に両方0へ戻す。</summary>
        private readonly int[] counts = new int[ParticipantCount];
        /// <summary>進行側が山札クリックを購読するための表示コンポーネントへの参照。</summary>
        public CardTable Table => table;
        /// <summary>列を中央へ配置するための、最初と最後のカード中心間の幅の半分。</summary>
        public float RowHalfWidth => (CardsPerRow - 1) * columnStep / 2f;

        /// <summary>前ラウンドのカードと表示枚数を消し、共有山札の見た目を初期化する。</summary>
        public void ResetCards(int remaining)
        {
            Array.Clear(counts, 0, counts.Length);
            table.Clear();
            UpdateStack(remaining);
        }

        /// <summary>共有山札の残数を束の厚さへ反映する。道具で枚数が減った場合にも使う。</summary>
        /// <param name="remaining">表示へ反映する共有山札の残り枚数。</param>
        public void UpdateStack(int remaining) => table.SetStackCount(remaining, Deck.TotalCards);

        /// <summary>本人の置き場・枚数から位置と傾きを計算し、めくって机へ置く演出を待つ。</summary>
        public IEnumerator Draw(BattleSide side, DrawResult draw, int remaining)
        {
            // この側が置くカードの0始まりの通し番号。もう片方の枚数とは独立して数える。
            int index = counts[(int)side]++;
            // 今回の側の基準Transform。位置と向きを一緒に参照してカメラ基準の配置を保つ。
            Transform row = side == BattleSide.Player ? playerRow : cpuRow;
            // 最初の1枚だけ水平に置く。位置の高さ補正にも同じ角度を使う。
            float appliedTiltDegrees = index == 0 ? 0f : cardTiltDegrees;
            Vector3 slot = CalculatePosition(side, row, index, appliedTiltDegrees);
            Quaternion rotation = Quaternion.LookRotation(Vector3.down, row.forward) *
                                  Quaternion.Euler(0, -appliedTiltDegrees, 0);
            yield return table.PlayDraw(draw, remaining, Deck.TotalCards, slot, rotation);
        }

        /// <summary>折り返し・最後の段の中央寄せ・傾きによる持ち上げをまとめて配置位置を求める。</summary>
        private Vector3 CalculatePosition(BattleSide side, Transform row, int index, float tiltDegrees)
        {
            int rowIndex = index / CardsPerRow;
            int column = index % CardsPerRow;

            // 山札の総数から最後の段を求める。折り返し枚数を変えても中央寄せを追従させる。
            int lastRowIndex = (Deck.TotalCards - 1) / CardsPerRow;
            int cardsInLastRow = Deck.TotalCards - lastRowIndex * CardsPerRow;
            int emptyColumns = CardsPerRow - cardsInLastRow;
            float lastRowInset = rowIndex == lastRowIndex ? emptyColumns * columnStep / 2f : 0f;

            // プレイヤーの追加段は手前、CPUの追加段は奥へ広げる。
            float depthDirection = side == BattleSide.Player ? -1f : 1f;
            float halfCardWidth = table.CardSize.x / 2f;
            float lift = TableClearance + halfCardWidth * Mathf.Sin(tiltDegrees * Mathf.Deg2Rad);
            float layerHeight = rowIndex * (table.CardSize.z + RowSurfaceClearance) +
                                column * ColumnHeightOffset;
            return row.position + row.right * (column * columnStep + lastRowInset) +
                   row.forward * (rowIndex * rowStep * depthDirection) + Vector3.up * (lift + layerHeight);
        }

        /// <summary>精算結果に予感カードがある場合だけ公開する。山札や得点はここでは変更しない。</summary>
        public IEnumerator Reveal(ReadingResult result, Customer conditions)
        {
            if (result.ForesightCard.HasValue)
            {
                // 精算時に記録された次の1枚。山札から改めて引かず、見た目だけを公開する。
                Card card = result.ForesightCard.Value;
                yield return table.PlayForesight(card, conditions.Judge(card));
            }
        }
    }
}
