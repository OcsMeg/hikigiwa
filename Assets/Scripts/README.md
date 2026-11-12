# スクリプト構成

| フォルダ | 役割 | Unityへの依存 |
|---|---|---|
| `CardSystem/` | ルール（山札・吉凶・運気・倍率・破綻・精算） | なし（asmdefで禁止） |
| `Game/` | 3D表示・HUD・入力・進行のつなぎ込み | あり |
| `Editor/` | メインシーンの自動生成 | エディタのみ |
| `Development/` | 山札だけを確認する旧テスト表示 | あり |

## ルールの流れ
1. `Customer.CreateRandom` で客を作る（凶数＝ランダムな1つの数字2枚＋ジョーカー、吉数＝別の1つの数字2枚）
2. `ReadingSession` に客と `Deck` を渡す
3. `TryDraw` で1枚引く／`TryStop` で鑑定を終える
4. 結果は `ReadingResult`（売上・予感的中・チップ・霊力）

数値の調整は `ReadingRules`（鑑定・道具のコスト）と `RunRules`（客数・ノルマ・初期霊力）だけを変えます。

## 夜と占い道具
- `PlayRecord`：夜・ノルマ・スコア・霊力。1夜＝客3人×3夜。ノルマ未達でも最後の夜まで続き（`RunRules.QuotaFailEndsGame` で切り替え）、最後に夜ごとのスコアと合計を表示
- 占い道具は `ReadingSession.TryUseTool` で使う（霊力は `PlayRecord` から払う）
  - 水晶（1）：次が凶か分かる／タロット（2）：次の3枚の数字（順不同）
  - お香（2）：凶札を1枚除く（1人1回）／手相（1）：一番上を一番下へ

## ReadingSession のイベント
| イベント | タイミング | 引数 |
|---|---|---|
| `CardDrawn` | カードを引いた | `DrawResult`（カード・吉凶・運気・倍率・凶の数） |
| `LuckyDrawn` | 吉数を引いた | 新しい倍率 |
| `BadOmenDrawn` | 凶を引いた | 凶の印の数 |
| `Broken` | 破綻した | `ReadingResult` |
| `Settled` | 鑑定を終えて確定した | `ReadingResult` |
| `Finished` | 破綻・確定のどちらでも | `ReadingResult` |
| `ToolUsed` | 占い道具を使った | `ToolResult` |

イベントは引いた瞬間に発生します。演出の後に表示を変えたい場合は、`ReadingGameController` のように演出が終わってから状態を読みます。

## メインシーン
メニューの **Hikigiwa → メインシーンを作成** で `Assets/Scenes/Main.unity` を作り直します。
カメラ・照明・仮のテーブルもここで作るので、部屋を作り込む段階になったらシーンを直接編集し、このメニューは使わないでください。
