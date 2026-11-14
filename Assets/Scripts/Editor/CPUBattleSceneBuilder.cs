using System.IO;
using System.Linq;
using Hikigiwa.Battle;
using Hikigiwa.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
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
        /// <summary>
        /// 部屋全体の拡大倍率。二人分のカードを重ねずに並べられる机の広さにするため。
        /// 机と小物の比率を保ったまま、壁のない側の端をカメラから遠ざける効果もある。カードの大きさは変えない。
        /// </summary>
        private const float RoomScale = 1.5f;
        /// <summary>同じ段で隣り合うカード中心の間隔。カードの中央の数字が隠れない重なり幅にする。</summary>
        private const float CardColumnStep = 0.3f;
        /// <summary>折り返した次の段へずらす奥行き間隔。上の段の数字と吉凶の字が隠れない幅にする。</summary>
        private const float CardRowStep = 0.45f;
        /// <summary>机の中央（山札）から、プレイヤー・CPUそれぞれの1段目までの奥行き距離。山札と重ならない幅にする。</summary>
        private const float RowOffsetFromStack = 0.62f;
        /// <summary>カメラの、天板の中心からの水平距離と、天板からの高さ。</summary>
        private const float CameraDistance = 3.3f;
        private const float CameraHeight = 2.2f;

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
            ScaleRoom(room);
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
            ConfigureLighting(scene, room);

            // Mainから引き継いだカード表示コンポーネント。カード素材と演出を再利用する。
            CardTable table = Object.FindAnyObjectByType<CardTable>();
            var tableData = new SerializedObject(table);
            tableData.FindProperty("appearance.cardHeight").floatValue = 0.55f;
            SetAnchor(tableData, "stackAnchor", new Vector3(-0.94f, surfaceY, 0.85f));
            SetAnchor(tableData, "revealAnchor", view.transform.position + view.transform.forward * 1.45f);
            SetAnchor(tableData, "rowAnchor", new Vector3(-0.7f, surfaceY, 0.65f));
            SetAnchor(tableData, "foresightAnchor", view.transform.position + view.transform.forward * 1.5f);
            ConfigureOmenLight(tableData);
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
            var hud = new GameObject("BattleHUD").AddComponent<BattleHud>();
            var hudData = new SerializedObject(hud);
            hudData.FindProperty("font").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>("Assets/Art/Fonts/YesevaOne-Regular.ttf");
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

        /// <summary>カード・カメラ・HUDを保ち、既存の対戦シーンの照明だけをソロに合わせる。</summary>
        public static void ApplyLightingChanges()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject room = scene.GetRootGameObjects().First(g => g.name.StartsWith("占い部屋"));
            ConfigureLighting(scene, room);
            ConfigureOmenLight(new SerializedObject(Object.FindAnyObjectByType<CardTable>()));
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>ソロの環境設定と光源を引き継ぎ、部屋モデルの座標と倍率の違いを補正する。</summary>
        private static void ConfigureLighting(Scene target, GameObject room)
        {
            Scene solo = EditorSceneManager.OpenScene("Assets/Scenes/Game.unity", OpenSceneMode.Additive);
            try
            {
                EditorSceneManager.SetActiveScene(solo);
                Transform environment = solo.GetRootGameObjects().First(g => g.name.StartsWith("環境")).transform;
                Transform soloRoom = environment.GetComponentsInChildren<Transform>()
                    .First(t => PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject) ==
                        AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomFolder}/UranaiRoom.fbx"));
                float scale = room.transform.lossyScale.x / soloRoom.lossyScale.x;
                var ambientMode = RenderSettings.ambientMode;
                Color ambientLight = RenderSettings.ambientLight;
                bool fog = RenderSettings.fog;
                FogMode fogMode = RenderSettings.fogMode;
                Color fogColor = RenderSettings.fogColor;
                float fogDensity = RenderSettings.fogDensity;
                Material skybox = RenderSettings.skybox;

                EditorSceneManager.SetActiveScene(target);
                RenderSettings.skybox = skybox;
                RenderSettings.ambientMode = ambientMode;
                RenderSettings.ambientLight = ambientLight;
                RenderSettings.fog = fog;
                RenderSettings.fogMode = fogMode;
                RenderSettings.fogColor = fogColor;
                // 同じ部屋の相対距離で同じ濃さに見えるよう、霧も倍率に合わせる。
                RenderSettings.fogDensity = fogDensity / scale;

                // 吉凶の演出光はCardTableが参照するため、そのまま残す。
                foreach (GameObject root in target.GetRootGameObjects())
                    if (root.name == "部屋の灯り" || root.name == "机の灯り" || root.name == "部屋の照明（ソロ準拠）")
                        Object.DestroyImmediate(root);
                var lighting = new GameObject("部屋の照明（ソロ準拠）");
                foreach (Light source in environment.GetComponentsInChildren<Light>())
                {
                    GameObject copy = Object.Instantiate(source.gameObject);
                    copy.name = source.name;
                    SceneManager.MoveGameObjectToScene(copy, target);
                    copy.transform.SetParent(lighting.transform, false);
                    copy.transform.position = room.transform.TransformPoint(soloRoom.InverseTransformPoint(source.transform.position));
                    copy.transform.rotation = room.transform.rotation * Quaternion.Inverse(soloRoom.rotation) * source.transform.rotation;
                    Light light = copy.GetComponent<Light>();
                    if (light.type != LightType.Directional)
                    {
                        light.range *= scale;
                        // 距離の二乗に応じて光量を補正し、机や壁の明るさを保つ。
                        light.intensity *= scale * scale;
                    }
                    if (source.name == "テーブルの灯り")
                    {
                        // 机の奥のキャラクターまで暖色の光を広げる。
                        light.range = 8f;
                        light.spotAngle = 120f;
                        light.innerSpotAngle = 90f;
                    }
                }
            }
            finally
            {
                EditorSceneManager.SetActiveScene(target);
                EditorSceneManager.CloseScene(solo, true);
            }
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
            ConfigureOmenLight(data);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>ソロと同じワールド座標差で、公開カードの近くに吉凶の演出光を置く。</summary>
        private static void ConfigureOmenLight(SerializedObject tableData)
        {
            var reveal = (Transform)tableData.FindProperty("revealAnchor").objectReferenceValue;
            var light = (Light)tableData.FindProperty("omenLight").objectReferenceValue;
            // Game.unityの「吉凶の光」(0, 1.6, -0.9) − カード提示位置(0, 1.3134962, -1.0074127)。
            Vector3 soloOmenOffset = new Vector3(0f, 0.2865038f, 0.1074127f);
            light.transform.position = reveal.position + soloOmenOffset;
            // CPU対戦用に調整した控えめな光量を保つ。
            tableData.FindProperty("omenLightIntensity").floatValue = 4.5f;
            tableData.ApplyModifiedPropertiesWithoutUndo();
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
            dealerData.FindProperty("cardTiltDegrees").floatValue = 3f;
            dealerData.FindProperty("columnStep").floatValue = CardColumnStep;
            dealerData.FindProperty("rowStep").floatValue = CardRowStep;
            dealerData.ApplyModifiedPropertiesWithoutUndo();
            // 折り返し枚数とカード間隔から求め、7枚用の固定幅に依存させない。
            float halfRowWidth = dealer.RowHalfWidth;
            // プレイヤーの1枚目の置き場。中央から手前へ、列の左端へずらして置く。
            Transform player = (Transform)dealerData.FindProperty("playerRow").objectReferenceValue;
            // CPUの1枚目の置き場。中央から奥へ、列の左端へずらして置く。
            Transform cpu = (Transform)dealerData.FindProperty("cpuRow").objectReferenceValue;
            // カメラの右方向を列の横方向にすることで、画面上で水平に並ぶ。
            player.SetPositionAndRotation(center - forward * RowOffsetFromStack - right * halfRowWidth, facing);
            cpu.SetPositionAndRotation(center + forward * RowOffsetFromStack - right * halfRowWidth, facing);
        }

        /// <summary>机上の水晶玉だけを隠し、開口部から机と部屋の奥を見るカメラを設定する。</summary>
        /// <param name="surfaceY">天板上面に余裕を足した、配置の基準となるワールド高さ。</param>
        private static void ConfigureView(GameObject room, Camera view, float surfaceY)
        {
            // 机上の水晶玉だけを非表示にする。棚の水晶玉は残す。
            // 非表示にする机上の水晶玉。棚の水晶玉とは名前で区別している。
            Transform crystal = room.GetComponentsInChildren<Transform>(true).First(t => t.name == "CrystalBall_Table");
            crystal.gameObject.SetActive(false);

            // 天板の中心。カメラはここを見る。
            Renderer cloth = room.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Tablecloth"));
            Vector3 center = new Vector3(cloth.bounds.center.x, surfaceY, cloth.bounds.center.z);
            // 部屋の壁がある2辺（+X側と-Z側）が交わる奥の角。部屋の外が映らないよう、この角へまっすぐ向く。
            // 横の視野（16:9で約79°）は壁どうしの角度（90°）より狭いので、角へ向けば画面の左右の端は必ず壁に当たる。
            Renderer roomRenderer = room.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "Room");
            Vector3 corner = roomRenderer != null
                ? new Vector3(roomRenderer.bounds.max.x, surfaceY, roomRenderer.bounds.min.z)
                : center + new Vector3(1f, 0f, -1f);
            // 天板の中心から奥の角へ向かう水平方向。カメラはその反対側（部屋の内側）に置く。
            Vector3 toCorner = (corner - center).normalized;
            view.transform.position = center - toCorner * CameraDistance + Vector3.up * CameraHeight;
            view.transform.LookAt(center);
            view.fieldOfView = 50;
            view.nearClipPlane = 0.05f;
            view.farClipPlane = 50;
        }

        /// <summary>
        /// 部屋全体を RoomScale 倍に拡大する。天板の中心が横方向（X・Z）で元の位置に残るよう、拡大後にずらす。
        /// 高さは床を基準に拡大されるので、天板の高さは拡大後に測り直す。
        /// </summary>
        private static void ScaleRoom(GameObject room)
        {
            Renderer cloth = room.GetComponentsInChildren<Renderer>().First(r => r.name.StartsWith("Tablecloth"));
            // 拡大前の天板の中心。
            Vector3 before = cloth.bounds.center;
            room.transform.localScale *= RoomScale;
            // 拡大で天板の中心がずれた分を、横方向だけ戻す。
            Vector3 shift = before - cloth.bounds.center;
            shift.y = 0f;
            room.transform.position += shift;
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
