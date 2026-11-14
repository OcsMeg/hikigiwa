namespace Hikigiwa.UI.Overlay
{
    /// <summary>
    /// 画面の種類。新しい画面を作ったら値を追加し、その画面を持つ Canvas の GetOverlay に分岐を足します。
    /// </summary>
    public enum OverlayType
    {
        None = 0,
        Title,
        Option,
        Rules,
    }
}
