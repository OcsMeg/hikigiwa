using System.IO;
using System.Linq;
using Hikigiwa.Battle;
using Hikigiwa.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hikigiwa.EditorTools
{
    /// <summary>Mainのカード設定を引き継ぎ、roomモデルの机で遊べる対戦シーンを作る。</summary>
    public static class CPUBattleSceneBuilder
    {
        /// <summary>生成・更新するCPU対戦シーンの保存先。Mainの保存先とは分ける。</summary>
        public const string ScenePath = "Assets/Scenes/CPUBattle.unity";
        /// <summary>部屋FBX・テクスチャ・生成するマテリアルの共通フォルダ。</summary>
        private const string RoomFolder = "Assets/Art/room";

        /// <summary>エディタメニューから呼ぶ入口。既存シーンの上書きと未保存内容の扱いを確認して生成する。</summary>
        [MenuItem("Hikigiwa/CPU対戦シーンを作成")]
        public static void BuildFromMenu()
        {
            if (File.Exists(ScenePath) && !EditorUtility.DisplayDialog("CPU対戦シーンを作成",
                "CPUBattleを作り直します。シーン内の手作業による変更は失われます。", "作り直す", "やめる")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        // バッチ検証からも呼べるよう、確認ダイアログと生成処理を分離する。
        /// <summary>Mainからカード設定を引き継ぎ、部屋・机・HUD・進行をCPU対戦用に組み立てて保存する。</summary>
        public static void Build()
        {
            // 今回保存する対戦シーン。生成時にはMainを開いた後、先に別名保存してから編集する。
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
            // Main上での編集を避けるため、先に別名で保存する。
            EditorSceneManager.SaveScene(scene, ScenePath);
            Object.DestroyImmediate(Object.FindAnyObjectByType<ReadingGameController>().gameObject);
            Object.DestroyImmediate(Object.FindAnyObjectByType<ReadingHud>().gameObject);
            GameObject environment = scene.GetRootGameObjects().First(g => g.name.StartsWith("環境"));
            Object.DestroyImmediate(environment);

            // 部屋と机・小物を含むFBXのシーン上のインスタンス。
            var room = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomFolder}/UranaiRoom.fbx"));
            room.name = "占い部屋（roomモデル・机を含む）";
            ApplyRoomMaterials(room);
            // FBXに含まれるカメラ。Mainから引き継ぐ対戦用カメラと競合しないよう除去する。
            foreach (Camera camera in room.GetComponentsInChildren<Camera>()) Object.DestroyImmediate(camera);
            // FBXに含まれるライト。対戦用に作る照明へ揃えるため除去する。
            foreach (Light light in room.GetComponentsInChildren<Light>()) Object.DestroyImmediate(light);
            // 天板の高さと中央位置を測るために使うテーブルクロスのRenderer。
            Renderer cloth = room.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Tablecloth"));
            // 天板上面にわずかな余裕を足した、カード配置の基準ワールド高さ。
            float surfaceY = cloth.bounds.max.y + 0.012f;
            // 対戦を映すメインカメラ。カードの列や公開位置もこのカメラを基準に決める。
            Camera view = Camera.main;
            ConfigureView(room, view, surfaceY);
            RenderSettings.fog = false;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.36f, 0.32f, 0.44f);
            // 部屋全体を照らす暖色の主光源。
            Light key = new GameObject("部屋の灯り", typeof(Light)).GetComponent<Light>();
            key.type = LightType.Directional;
            key.transform.rotation = Quaternion.Euler(55, -25, 0);
            key.color = new Color(1, 0.86f, 0.7f);
            key.intensity = 1.2f;
            key.shadows = LightShadows.Soft;
            // 机上のカードを読みやすく照らす補助の点光源。
            Light fill = new GameObject("机の灯り", typeof(Light)).GetComponent<Light>();
            fill.type = LightType.Point;
            fill.transform.position = new Vector3(0, surfaceY + 1.7f, 0.5f);
            fill.range = 6;
            fill.intensity = 5;

            // Mainから引き継いだカード表示コンポーネント。カード素材と演出を再利用する。
            CardTable table = Object.FindAnyObjectByType<CardTable>();
            var tableData = new SerializedObject(table);
            tableData.FindProperty("appearance.cardHeight").floatValue = 0.55f;
            SetAnchor(tableData, "stackAnchor", new Vector3(-0.94f, surfaceY, 0.85f));
            SetAnchor(tableData, "revealAnchor", view.transform.position + view.transform.forward * 1.45f);
            SetAnchor(tableData, "rowAnchor", new Vector3(-0.7f, surfaceY, 0.65f));
            SetAnchor(tableData, "foresightAnchor", view.transform.position + view.transform.forward * 1.5f);
            ((Light)tableData.FindProperty("omenLight").objectReferenceValue).transform.position = new Vector3(0, surfaceY + 0.7f, 0.5f);
            tableData.ApplyModifiedPropertiesWithoutUndo();

            // 二人分のカード配置と既存のCardTableをつなぐ表示担当。
            var dealer = new GameObject("対戦カード表示").AddComponent<BattleCardDealer>();
            var dealerData = new SerializedObject(dealer);
            dealerData.FindProperty("table").objectReferenceValue = table;
            dealerData.FindProperty("playerRow").objectReferenceValue = Anchor(dealer.transform, "プレイヤーの列", Vector3.zero);
            // CPUのカード列の基準Transform。最終的な位置と向きは共通の配置処理で決める。
            Transform cpuRow = Anchor(dealer.transform, "CPUの列", Vector3.zero);
            dealerData.FindProperty("cpuRow").objectReferenceValue = cpuRow;
            dealerData.ApplyModifiedPropertiesWithoutUndo();
            ConfigureCardLayout(room, view, table, dealer);

            // 実行時に対戦UIを作るコンポーネント。
            var hud = new GameObject("対戦HUD").AddComponent<BattleHud>();
            var hudData = new SerializedObject(hud);
            hudData.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/NotoSansJP-Medium-Subset.ttf");
            hudData.ApplyModifiedPropertiesWithoutUndo();
            // 入力・CPU・ルール・表示をつなぐ対戦進行コンポーネント。
            var controller = new GameObject("CPU対戦進行").AddComponent<BattleGameController>();
            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("dealer").objectReferenceValue = dealer;
            controllerData.FindProperty("hud").objectReferenceValue = hud;
            controllerData.FindProperty("cpuBadRateThresholdPercent").floatValue = 50f;
            controllerData.FindProperty("cpuStopProbabilityPercent").floatValue = 50f;
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
            // 既存のビルド対象一覧。元の順序を保ち、未登録の場合だけ対戦シーンを末尾へ足す。
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("CPU対戦シーンを作成しました：" + ScenePath);
        }

        /// <summary>既存のカード配置を保ち、カメラと机上の水晶玉の表示設定を保存する。</summary>
        public static void ApplyLayoutChanges()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject room = scene.GetRootGameObjects().First(g => g.name.StartsWith("占い部屋"));
            Renderer cloth = room.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Tablecloth"));
            Camera view = Camera.main;
            ConfigureView(room, view, cloth.bounds.max.y + 0.012f);
            var data = new SerializedObject(Object.FindAnyObjectByType<CardTable>());
            SetAnchor(data, "revealAnchor", view.transform.position + view.transform.forward * 1.45f);
            SetAnchor(data, "foresightAnchor", view.transform.position + view.transform.forward * 1.5f);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>既存シーンのカード列と山札だけをカメラ基準の配置へ更新する。</summary>
        public static void ApplyCardOrientationChanges()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject room = scene.GetRootGameObjects().First(g => g.name.StartsWith("占い部屋"));
            ConfigureCardLayout(room, Camera.main, Object.FindAnyObjectByType<CardTable>(),
                Object.FindAnyObjectByType<BattleCardDealer>());
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>実測した天板中央へ山札を置き、カメラの水平な右方向に二人分のカード列を揃える。</summary>
        private static void ConfigureCardLayout(GameObject room, Camera view, CardTable table, BattleCardDealer dealer)
        {
            Renderer cloth = room.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Tablecloth"));
            // テーブルクロスの中心。yだけを上面へ合わせて山札の置き場にする。
            Vector3 center = cloth.bounds.center;
            center.y = cloth.bounds.max.y + 0.012f;
            // カメラの前方向を水平面へ投影した、机上での奥方向。
            Vector3 forward = Vector3.ProjectOnPlane(view.transform.forward, Vector3.up).normalized;
            // 山札とカード列の軸をカメラへ揃える、Y軸まわりの回転。
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
            // 机上でのカメラ右方向。これに沿ってカードを置くと画面上で水平に並ぶ。
            Vector3 right = facing * Vector3.right;
            var tableData = new SerializedObject(table);
            // 共有山札を置く基準Transform。天板中央へ移動して向きも揃える。
            Transform stack = (Transform)tableData.FindProperty("stackAnchor").objectReferenceValue;
            stack.SetPositionAndRotation(center, facing);
            var dealerData = new SerializedObject(dealer);
            dealerData.FindProperty("cardTiltDegrees").floatValue = 8f;
            dealerData.FindProperty("columnStep").floatValue = 0.22f;
            dealerData.FindProperty("rowStep").floatValue = 0.14f;
            dealerData.ApplyModifiedPropertiesWithoutUndo();
            // 折り返し枚数とカード間隔から求め、7枚用の固定幅に依存させない。
            float halfRowWidth = dealer.RowHalfWidth;
            // プレイヤーの1枚目の置き場。中央から手前へ、列の左端へずらして置く。
            Transform player = (Transform)dealerData.FindProperty("playerRow").objectReferenceValue;
            // CPUの1枚目の置き場。中央から奥へ、列の左端へずらして置く。
            Transform cpu = (Transform)dealerData.FindProperty("cpuRow").objectReferenceValue;
            // カメラの右方向を列の横方向にすることで、画面上で水平に並ぶ。
            player.SetPositionAndRotation(center - forward * 0.56f - right * halfRowWidth, facing);
            cpu.SetPositionAndRotation(center + forward * 0.56f - right * halfRowWidth, facing);
        }

        /// <summary>机上の水晶玉だけを隠し、開口部から机と部屋の奥を見るカメラを設定する。</summary>
        /// <param name="surfaceY">天板上面に余裕を足した、配置の基準となるワールド高さ。</param>
        private static void ConfigureView(GameObject room, Camera view, float surfaceY)
        {
            // 机上の水晶玉だけを非表示にする。棚の水晶玉は残す。
            // 非表示にする机上の水晶玉。棚の水晶玉とは名前で区別している。
            Transform crystal = room.GetComponentsInChildren<Transform>(true).First(t => t.name == "CrystalBall_Table");
            crystal.gameObject.SetActive(false);

            // 壁のない2辺の中央方向（開いた角）から、部屋の奥へ対角線に見る。
            view.transform.position = new Vector3(-3.4f, surfaceY + 2.0f, 3.45f);
            view.transform.LookAt(new Vector3(0, surfaceY, 0.45f));
            view.fieldOfView = 50;
            view.nearClipPlane = 0.05f;
            view.farClipPlane = 50;
        }

        /// <summary>CardTableの指定したシリアライズ参照から配置用Transformを取得し、ワールド位置を設定する。</summary>
        /// <param name="property">位置を変更するTransform参照のシリアライズされたフィールド名。</param>
        /// <param name="position">配置する基準Transformのワールド位置。</param>
        private static void SetAnchor(SerializedObject data, string property, Vector3 position) =>
            ((Transform)data.FindProperty(property).objectReferenceValue).position = position;

        /// <summary>カードの置き場として使う空の子オブジェクトを生成し、ワールド位置を設定する。</summary>
        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            // 生成した空の配置用Transform。親を設定した後、ワールド位置を与える。
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = position;
            return t;
        }

        /// <summary>部屋モデルの各マテリアルへ対応する既存テクスチャを割り当て、URP用アセットとして保存する。</summary>
        private static void ApplyRoomMaterials(GameObject room)
        {
            // 生成したURPマテリアルを保存するフォルダ。
            string folder = RoomFolder + "/Materials";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(RoomFolder, "Materials");
            // 現在マテリアルを割り当てている部屋モデルの描画部品。
            foreach (Renderer renderer in room.GetComponentsInChildren<Renderer>())
            {
                // sourceはFBX側の各マテリアル。対応するURPマテリアルへ1つずつ置き換える。
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    // FBX側のマテリアル名。同名の生成済みアセットがあれば再利用する。
                    string name = source.name;
                    // マテリアル名の接尾辞を取り除いた、対応テクスチャを探すための名前。
                    string stem = name.Replace("_Material", "");
                    // 対応する既存テクスチャのパス。窓の外の画像だけ命名規則が異なる。
                    string texturePath = stem == "Window_Outside" ? $"{RoomFolder}/Window_Outside.png.png" : $"{RoomFolder}/{stem}_BaseColor.png";
                    // このマテリアルのアセット保存先。
                    string path = $"{folder}/{name}.mat";
                    // 再利用または新規作成したURP/Litマテリアル。既存画像と白い基本色を設定する。
                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        AssetDatabase.CreateAsset(material, path);
                    }
                    material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                    material.SetColor("_BaseColor", Color.white);
                    material.SetFloat("_Smoothness", 0.15f);
                    EditorUtility.SetDirty(material);
                    return material;
                }).ToArray();
            }
        }
    }
}
