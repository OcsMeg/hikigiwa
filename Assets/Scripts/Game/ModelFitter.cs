using UnityEngine;

namespace Hikigiwa.Game
{
    /// <summary>
    /// 読み込んだモデルの向きと大きさを、決まった軸にそろえるための補助。
    /// Blenderから書き出したときの軸の違いを吸収します。
    /// </summary>
    public static class ModelFitter
    {
        /// <summary>
        /// pivotの子にmodelを生成し、モデルの「最も短い軸」と「最も長い軸」を指定の向きへ回転させ、
        /// 中心を原点に合わせます。戻り値は回転後・拡大前の寸法（x, y, z）。
        /// </summary>
        public static Vector3 Fit(Transform pivot, GameObject model, Vector3 shortAxisTo, Vector3 longAxisTo,
            float scale, out GameObject instance)
        {
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
            pivot.localScale = Vector3.one;
            instance = Object.Instantiate(model, pivot, false);

            Bounds bounds = LocalBounds(pivot, instance);
            Vector3 size = bounds.size;
            int shortAxis = 0, longAxis = 0;
            for (int i = 1; i < 3; i++)
            {
                if (size[i] < size[shortAxis]) shortAxis = i;
                if (size[i] > size[longAxis]) longAxis = i;
            }

            // モデル側の軸 → 目的の軸 への回転
            Quaternion fromModel = Quaternion.LookRotation(Axis(shortAxis), Axis(longAxis));
            Quaternion toTarget = Quaternion.LookRotation(shortAxisTo, longAxisTo);
            Quaternion rotation = toTarget * Quaternion.Inverse(fromModel);

            pivot.localRotation = rotation;
            pivot.localScale = Vector3.one * scale;
            pivot.localPosition = -(rotation * bounds.center) * scale;

            Vector3 rotated = rotation * size;
            return new Vector3(Mathf.Abs(rotated.x), Mathf.Abs(rotated.y), Mathf.Abs(rotated.z));
        }

        /// <summary>モデルの最も長い辺の長さ（拡大前）。</summary>
        public static float LongestSide(GameObject model)
        {
            var temp = new GameObject("寸法計測用");
            GameObject instance = Object.Instantiate(model, temp.transform, false);
            Vector3 size = LocalBounds(temp.transform, instance).size;
            Object.DestroyImmediate(temp);
            return Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        }

        private static Vector3 Axis(int index) => index == 0 ? Vector3.right : index == 1 ? Vector3.up : Vector3.forward;

        /// <summary>space座標系での、モデル全体のメッシュの境界。</summary>
        private static Bounds LocalBounds(Transform space, GameObject instance)
        {
            bool has = false;
            var bounds = new Bounds();
            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                Bounds mesh = filter.sharedMesh.bounds;
                Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3(
                        (i & 1) == 0 ? mesh.min.x : mesh.max.x,
                        (i & 2) == 0 ? mesh.min.y : mesh.max.y,
                        (i & 4) == 0 ? mesh.min.z : mesh.max.z);
                    Vector3 point = toSpace.MultiplyPoint3x4(corner);
                    if (!has)
                    {
                        bounds = new Bounds(point, Vector3.zero);
                        has = true;
                    }
                    else bounds.Encapsulate(point);
                }
            }
            return has ? bounds : new Bounds(Vector3.zero, Vector3.one);
        }
    }
}
