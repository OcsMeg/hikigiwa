using UnityEngine;

namespace Hikigiwa.Game
{
    /// <summary>キャラクターを傾けず、体の正面をメインカメラへ向ける。</summary>
    [DisallowMultipleComponent]
    public sealed class CameraFacingCharacter : MonoBehaviour
    {
        [SerializeField] private Vector3 localForward = Vector3.forward;
        [SerializeField] private Camera cameraOverride;

        public Vector3 LocalForward => localForward;

        /// <summary>モデルから測った正面と、対象カメラを設定する。</summary>
        public void Configure(Vector3 forward, Camera view = null)
        {
            localForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            cameraOverride = view;
            FaceCamera();
        }

        private void LateUpdate() => FaceCamera();

        public void FaceCamera()
        {
            Camera view = cameraOverride != null ? cameraOverride : Camera.main;
            if (view == null || localForward.sqrMagnitude < 0.001f) return;
            Vector3 towardCamera = Vector3.ProjectOnPlane(view.transform.position - transform.position, Vector3.up);
            if (towardCamera.sqrMagnitude < 0.001f) return;
            transform.rotation = Quaternion.LookRotation(towardCamera, Vector3.up)
                * Quaternion.Inverse(Quaternion.LookRotation(localForward, Vector3.up));
        }
    }
}
