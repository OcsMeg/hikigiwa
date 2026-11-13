using Hikigiwa.Battle;
using Hikigiwa.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hikigiwa.EditorTools
{
    /// <summary>
    /// メニュー「Hikigiwa/UIをシーンに配置」で、開いているシーンのHUD（ソロの ReadingHud・CPU対戦の BattleHud）を
    /// Hierarchy に書き出します。書き出したUIは、位置・大きさ・色・文字などをエディタで自由に変えられ、プレイ時もそのまま使われます。
    /// </summary>
    public static class HudScenePlacer
    {
        private const string Title = "UIをシーンに配置";
        private const string FontPath = "Assets/Art/Fonts/NotoSansJP-Medium-Subset.ttf";

        [MenuItem("Hikigiwa/UIをシーンに配置")]
        public static void Place()
        {
            var reading = Object.FindAnyObjectByType<ReadingHud>(FindObjectsInactive.Include);
            var battle = Object.FindAnyObjectByType<BattleHud>(FindObjectsInactive.Include);
            if (reading == null && battle == null)
            {
                EditorUtility.DisplayDialog(Title,
                    "開いているシーンにHUD（ReadingHud / BattleHud）が見つかりません。Game.unity か CPUBattle.unity を開いてください。", "OK");
                return;
            }

            bool placed = false;
            if (reading != null) placed |= Place(reading, reading.HasLayout, reading.CreateLayout);
            if (battle != null) placed |= Place(battle, battle.HasLayout, battle.CreateLayout);
            if (!placed) return;

            EditorSceneManager.MarkSceneDirty((reading != null ? reading.gameObject : battle.gameObject).scene);
            Debug.Log("UIをシーンに配置しました。シーンを保存（Ctrl+S / Cmd+S）してください。");
        }

        private static bool Place(MonoBehaviour hud, bool hasLayout, System.Action createLayout)
        {
            var data = new SerializedObject(hud);
            if (hasLayout)
            {
                if (!EditorUtility.DisplayDialog(Title,
                        $"{hud.gameObject.name} のUIはすでに置かれています。作り直しますか？\n（エディタで変えた位置や見た目は元に戻ります）",
                        "作り直す", "やめる"))
                    return false;
                var old = data.FindProperty("canvasRoot").objectReferenceValue as GameObject;
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            // フォントが未設定なら、ゲームで使っている日本語フォントを入れる
            SerializedProperty font = data.FindProperty("font");
            if (font.objectReferenceValue == null)
            {
                font.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
                data.ApplyModifiedPropertiesWithoutUndo();
            }

            Undo.RegisterCompleteObjectUndo(hud, Title);
            createLayout();
            data.Update();
            var created = data.FindProperty("canvasRoot").objectReferenceValue as GameObject;
            if (created != null) Undo.RegisterCreatedObjectUndo(created, Title);
            EditorUtility.SetDirty(hud);
            Selection.activeGameObject = created;
            return true;
        }
    }
}
