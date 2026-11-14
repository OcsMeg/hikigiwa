using Hikigiwa.UI.Overlay;

namespace Hikigiwa.UI.Rules
{
    /// <summary>
    /// ルール説明画面。「戻る」で CloseRequested を発行するだけです（ScrollOverlayPresenterBase）。
    /// ゲーム中のポーズ画面などからも同じものを使えます。
    /// </summary>
    public sealed class RulesPresenter : ScrollOverlayPresenterBase<RulesView>
    {
    }
}
