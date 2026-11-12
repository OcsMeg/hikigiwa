using System.Threading;
using Cysharp.Threading.Tasks;

namespace Hikigiwa.UI.Overlay
{
    /// <summary>
    /// 画面（オーバーレイ）を型に依存せずに扱うためのインターフェース。
    /// CanvasBase は OverlayType からこれを取り出して表示を切り替えます。
    /// </summary>
    public interface IOverlay
    {
        bool IsVisible { get; }

        void Initialize();

        UniTask ShowAsync(bool animate, CancellationToken ct);

        UniTask HideAsync(bool animate, CancellationToken ct);
    }
}
