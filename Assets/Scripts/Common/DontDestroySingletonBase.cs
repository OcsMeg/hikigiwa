namespace Hikigiwa.Common
{
    /// <summary>
    /// シーンをまたいで残るシングルトンの基底クラス。
    /// DontDestroyOnLoad はルートの GameObject にしか効かないため、親がいれば外します。
    /// </summary>
    public abstract class DontDestroySingletonBase<T> : SingletonBase<T> where T : DontDestroySingletonBase<T>
    {
        protected override void OnAwake()
        {
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
    }
}
