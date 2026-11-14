using UnityEngine;

namespace Hikigiwa.Title
{
    /// <summary>中央のカメラとUIを固定したまま、背景の部屋をゆっくり回す。</summary>
    public sealed class TitleRoomRotation : MonoBehaviour
    {
        [SerializeField, Tooltip("1秒あたりの回転角度。3なら2分で1周します。")]
        private float degreesPerSecond = 3f;

        private void Update()
        {
            // 設定画面や一時停止中も、背景の動きは一定に保つ。
            transform.Rotate(Vector3.up, degreesPerSecond * Time.unscaledDeltaTime, Space.Self);
        }
    }
}
