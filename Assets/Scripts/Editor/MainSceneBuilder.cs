using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hikigiwa.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Hikigiwa.EditorTools
{
    /// <summary>
    /// メニュー「Hikigiwa/メインシーンを作成」で、メインのプレイシーンを組み立てて保存します。
    /// 素材（FBX・テクスチャ・フォント）の割り当てもここで行います。
    /// </summary>
    public static class MainSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string CardsFolder = "Assets/Art/Cards";
        private const string MaterialsFolder = "Assets/Art/Cards/Materials";
        private const string FontPath = "Assets/Art/Fonts/NotoSansJP-Medium-Subset.ttf";

        [MenuItem("Hikigiwa/メインシーンを作成")]
        public static void Build()
        {
            if (File.Exists(ScenePath) &&
                !EditorUtility.DisplayDialog("メインシーンを作成",
                    $"{ScenePath} はすでにあります。作り直しますか？\n（シーン内で手作業で変えた部分は消えます）",
                    "作り直す", "やめる"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            ConfigureTextures();
            var font = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var cardModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{CardsFolder}/Card.fbx");
            var stackModel = AssetDatabase.LoadAssetAtPath<GameObject>($"{CardsFolder}/CardStack.fbx");
            if (cardModel == null || font == null)
            {
                EditorUtility.DisplayDialog("メインシーンを作成",
                    "Card.fbx またはフォントが見つかりません。Assets/Art 以下を確認してください。", "OK");
                return;
            }

            // 本体と束は紙の色（縁に見える部分）。裏面の絵柄は別の板として貼る
            Material back = EnsureMaterial("CardBack", new Color32(240, 234, 222, 255), FindTexture("Tex_Card_1"));
            Material stack = EnsureMaterial("CardStack", new Color32(240, 234, 222, 255), FindTexture("Tex_Stack"));
            Material backImage = EnsureMaterial("CardBackImage", Color.white, FindTexture("CardBackRed"));
            Material face = EnsureMaterial("CardFace", Color.white, FindTexture("CardFace"));
            Material joker = EnsureMaterial("CardJoker", Color.white, FindTexture("Joker"));
            Material cloth = EnsureMaterial("TableCloth", new Color32(38, 22, 48, 255), null);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 環境光：暗めで紫寄り
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(40, 32, 58, 255);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color32(12, 9, 20, 255);
            RenderSettings.fogDensity = 0.04f;

            // カメラ
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.transform.position = new Vector3(0f, 2.55f, -2.45f);
            camera.transform.LookAt(new Vector3(0f, 0.15f, 0.35f));
            camera.fieldOfView = 50f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(12, 9, 20, 255);

            // 照明
            var environment = new GameObject("環境（3D担当が差し替える仮の部屋）").transform;
            var moon = new GameObject("月明かり", typeof(Light)).GetComponent<Light>();
            moon.transform.SetParent(environment, false);
            moon.type = LightType.Directional;
            moon.color = new Color32(120, 130, 220, 255);
            moon.intensity = 0.25f;
            moon.transform.rotation = Quaternion.Euler(55f, -30f, 0f);

            var lamp = new GameObject("テーブルの灯り", typeof(Light)).GetComponent<Light>();
            lamp.transform.SetParent(environment, false);
            lamp.type = LightType.Spot;
            lamp.color = new Color32(255, 196, 130, 255);
            lamp.intensity = 15f;
            lamp.range = 8f;
            lamp.spotAngle = 75f;
            lamp.shadows = LightShadows.Soft;
            lamp.transform.position = new Vector3(0f, 3.2f, 0.2f);
            lamp.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // テーブル（天板の上面が y=0）
            var table = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            table.name = "仮のテーブル";
            table.transform.SetParent(environment, false);
            table.transform.position = new Vector3(0f, -0.05f, 0.3f);
            table.transform.localScale = new Vector3(4.6f, 0.05f, 4.6f);
            table.GetComponent<Renderer>().sharedMaterial = cloth;
            Object.DestroyImmediate(table.GetComponent<Collider>());

            // 吉凶の光
            var omenLight = new GameObject("吉凶の光", typeof(Light)).GetComponent<Light>();
            omenLight.type = LightType.Point;
            omenLight.range = 4f;
            omenLight.intensity = 0f;
            omenLight.transform.position = new Vector3(0f, 1.6f, -0.9f);

            // カードの置き場
            var tableObject = new GameObject("カードテーブル");
            var cardTable = tableObject.AddComponent<CardTable>();
            Transform stackAnchor = Anchor(tableObject.transform, "山札の位置", new Vector3(-1.55f, 0f, 0.75f));
            Vector3 reveal = camera.transform.position + camera.transform.forward * 1.9f;
            Transform revealAnchor = Anchor(tableObject.transform, "めくる位置", reveal);
            Transform rowAnchor = Anchor(tableObject.transform, "引いたカードの列", new Vector3(-0.85f, 0f, 1.0f));
            Transform foresightAnchor = Anchor(tableObject.transform, "予感のカードの位置",
                new Vector3(-1.05f, 0.75f, -0.15f));

            var tableData = new SerializedObject(cardTable);
            tableData.FindProperty("appearance.cardModel").objectReferenceValue = cardModel;
            tableData.FindProperty("appearance.backMaterial").objectReferenceValue = back;
            tableData.FindProperty("appearance.backImageMaterial").objectReferenceValue = backImage;
            tableData.FindProperty("appearance.faceMaterial").objectReferenceValue = face;
            tableData.FindProperty("appearance.jokerMaterial").objectReferenceValue = joker;
            tableData.FindProperty("appearance.font").objectReferenceValue = font;
            tableData.FindProperty("stackModel").objectReferenceValue = stackModel;
            tableData.FindProperty("stackMaterial").objectReferenceValue = stack;
            tableData.FindProperty("stackAnchor").objectReferenceValue = stackAnchor;
            tableData.FindProperty("revealAnchor").objectReferenceValue = revealAnchor;
            tableData.FindProperty("rowAnchor").objectReferenceValue = rowAnchor;
            tableData.FindProperty("foresightAnchor").objectReferenceValue = foresightAnchor;
            tableData.FindProperty("viewCamera").objectReferenceValue = camera;
            tableData.FindProperty("omenLight").objectReferenceValue = omenLight;
            tableData.ApplyModifiedPropertiesWithoutUndo();

            // HUD
            var hudObject = new GameObject("HUD");
            var hud = hudObject.AddComponent<ReadingHud>();
            var hudData = new SerializedObject(hud);
            hudData.FindProperty("font").objectReferenceValue = font;
            hudData.ApplyModifiedPropertiesWithoutUndo();
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // 進行
            var controller = new GameObject("ゲーム進行").AddComponent<ReadingGameController>();
            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("table").objectReferenceValue = cardTable;
            controllerData.FindProperty("hud").objectReferenceValue = hud;
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();
            Debug.Log($"メインシーンを作成しました：{ScenePath}");
        }

        private static Transform Anchor(Transform parent, string name, Vector3 position)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.position = position;
            return anchor;
        }

        private static Texture2D FindTexture(string name)
        {
            string guid = AssetDatabase.FindAssets($"{name} t:Texture2D", new[] { "Assets/Art" }).FirstOrDefault(g =>
                Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g)) == name);
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
        }

        /// <summary>カードの絵柄は端がにじまないよう Clamp にする。</summary>
        private static void ConfigureTextures()
        {
            foreach (string name in new[] { "CardFace", "Joker", "CardBackRed" })
            {
                Texture2D texture = FindTexture(name);
                if (texture == null) continue;
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
                if (importer.wrapMode == TextureWrapMode.Clamp) continue;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
            }
        }

        /// <summary>URP/Litのマテリアルを作成（または更新）します。テクスチャがあれば貼り、色は白にします。</summary>
        private static Material EnsureMaterial(string name, Color color, Texture2D texture)
        {
            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder(CardsFolder, "Materials");

            string path = $"{MaterialsFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", texture != null ? Color.white : color);
            material.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            scenes.RemoveAll(s => s.path == ScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
