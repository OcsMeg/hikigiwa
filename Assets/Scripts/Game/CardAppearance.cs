using System;
using UnityEngine;

namespace Hikigiwa.Game
{
    /// <summary>カードの見た目に使う素材のまとめ。シーン生成ツールが自動で設定します。</summary>
    [Serializable]
    public sealed class CardAppearance
    {
        [Tooltip("カード1枚のモデル（Card.fbx）")]
        public GameObject cardModel;

        [Tooltip("カード本体（縁）のマテリアル。空ならFBXのマテリアルをそのまま使う")]
        public Material backMaterial;

        [Tooltip("裏面の絵柄のマテリアル。カードの裏側と山札の一番上に貼る")]
        public Material backImageMaterial;

        [Tooltip("数字札の表面のマテリアル（白地に枠線）")]
        public Material faceMaterial;

        [Tooltip("ジョーカーの表面のマテリアル")]
        public Material jokerMaterial;

        [Tooltip("数字を描くフォント")]
        public Font font;

        [Tooltip("カードの縦の長さ（ワールド単位）")]
        public float cardHeight = 0.9f;

        public Color inkColor = new Color32(30, 26, 38, 255);
        public Color luckyColor = new Color32(196, 140, 24, 255);
        public Color badColor = new Color32(184, 38, 46, 255);
    }
}
