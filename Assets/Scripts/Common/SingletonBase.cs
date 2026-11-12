using UnityEngine;

namespace Hikigiwa.Common
{
    /// <summary>
    /// シーンに1つだけ置く MonoBehaviour の基底クラス。
    /// 2つ目が生成されたら GameObject ごと破棄します。
    /// </summary>
    /// <remarks>
    /// 子クラスでは Awake / OnDestroy を書かず、OnAwake / OnDestroyed を override してください。
    /// （重複して破棄されるときに、子の初期化処理が動かないようにするため）
    /// </remarks>
    public abstract class SingletonBase<T> : MonoBehaviour where T : SingletonBase<T>
    {
        private static T instance;

        /// <summary>インスタンス。まだ登録されていなければシーンから探します。</summary>
        public static T Instance
        {
            get
            {
                if (instance == null) instance = FindFirstObjectByType<T>();
                return instance;
            }
        }

        /// <summary>インスタンスが存在するか（探索はしません）。</summary>
        public static bool HasInstance => instance != null;

        private void Awake()
        {
            if (instance == null)
            {
                instance = (T)this;
            }
            else if (instance != this)
            {
                Destroy(gameObject);
                return;
            }

            OnAwake();
        }

        private void OnDestroy()
        {
            if (instance != this) return;

            OnDestroyed();
            instance = null;
        }

        /// <summary>自分が正式なインスタンスに決まったときに呼ばれます。</summary>
        protected virtual void OnAwake() { }

        /// <summary>正式なインスタンスが破棄されるときに呼ばれます。</summary>
        protected virtual void OnDestroyed() { }
    }
}
