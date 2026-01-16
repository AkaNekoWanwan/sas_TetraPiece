# UserSegment 使用ガイド（enum版対応）

## 概要
`UserSegment` は、ユーザーセグメント（ABテスト）の管理を行うクラスです。
- **enum化**: `UserSegmentKey` enumでキーを安全に管理
- **型変換**: `GetValue<T>()` で任意の型で値を取得
- **Firebase自動連携**: ABテスト中のパラメータを自動でFirebase Analyticsに送信

## 新機能: UserSegmentKey enum

### enum定義
```csharp
public enum UserSegmentKey
{
    AdInterval,  // 広告表示間隔
    IsMove       // 移動回数制限の有無
}
```

### メリット
- ✅ タイポ防止（コンパイル時チェック）
- ✅ IntelliSense対応
- ✅ リファクタリングが容易
- ✅ 後方互換性維持（既存のstring版も使用可能）

## 基本的な使い方（enum版）

### 1. 広告表示間隔 (UserSegmentKey.AdInterval)

#### float型で取得（秒数）
```csharp
float interval = UserSegment.instance.GetValue<float>(UserSegmentKey.AdInterval);
// 戻り値: 60f (値が0の場合) または 90f (値が1の場合)
```

#### string型で取得（表示用テキスト）
```csharp
string intervalText = UserSegment.instance.GetValue<string>(UserSegmentKey.AdInterval);
// 戻り値: "60秒" (値が0の場合) または "90秒" (値が1の場合)
```

### 2. 移動回数制限 (UserSegmentKey.IsMove)

#### bool型で取得
```csharp
bool isMove = UserSegment.instance.GetValue<bool>(UserSegmentKey.IsMove);
// 戻り値: false (値が0の場合) または true (値が1の場合)
```

#### string型で取得（表示用テキスト）
```csharp
string isMoveText = UserSegment.instance.GetValue<string>(UserSegmentKey.IsMove);
// 戻り値: "無" (値が0の場合) または "有" (値が1の場合)
```

## 実装例

### StageManager での使用例
```csharp
// 広告表示間隔を取得
public float GetAdsInterval()
{
    return UserSegment.instance.GetValue<float>(UserSegmentKey.AdInterval);
}

// 移動回数制限が有効かチェック
private bool IsActiveMoveLimit()
{
    return UserSegment.instance.GetValue<bool>(UserSegmentKey.IsMove);
}
```

## Firebase Analytics自動連携機能

### 概要
ABテスト中（`_userPropertyDic`のvalueListのCountが2以上）のユーザーセグメントパラメータが、自動的にFirebase Analyticsイベントに追加されます。

### 対象イベント
- `Stage_Clear`
- `Stage_Failure`
- `Stage_Undo`

### 自動追加されるパラメータ
```csharp
// ABテスト中の場合、以下のパラメータが自動追加される
new Parameter("Ad_Interval", 0)  // または 1
new Parameter("IsMove", 0)       // または 1
```

### 実装詳細

#### FirebaseManager側
```csharp
// 使用例（FirebaseManager.cs）
public void StageClear(int Move_Count, float ClearTime)
{
    // LogEventWithUserSegments を使用すると自動的にABテストパラメータが追加される
    LogEventWithUserSegments("Stage_Clear",
        new Parameter("Stage", GetCurrentStage()),
        new Parameter("Move_Count", Move_Count),
        // ... 他のパラメータ
    );
    // ↓ 実際に送信されるパラメータ（ABテスト中の場合）
    // Stage, Move_Count, ..., Ad_Interval, IsMove
}
```

#### 条件
- `UserSegment.instance` が初期化されていること
- 該当キーの`_userPropertyDic[key].Count >= 2` であること（ABテスト中）

#### 例: Ad_Intervalがテスト中でIsMoveがテスト終了の場合
```csharp
// _userPropertyDic の設定
_userPropertyDic.Add("Ad_Interval", new List<int>{ 0, 1 });  // Count=2（テスト中）
_userPropertyDic.Add("IsMove", new List<int>{ 1 });          // Count=1（テスト終了）

// Firebase Analyticsに送信されるパラメータ
// Stage_Clear → Ad_Intervalのみ自動追加（IsMoveは追加されない）
```

## 対応している型とプロパティの組み合わせ

| プロパティ | float | int | bool | string |
|-----------|-------|-----|------|--------|
| UserSegmentKey.AdInterval | ✅ | ✅ | ❌ | ✅ |
| UserSegmentKey.IsMove | ❌ | ❌ | ✅ | ✅ |

## 変換ルール

### UserSegmentKey.AdInterval
- 生の値 `0` → `60f`, `60`, `"60秒"`
- 生の値 `1` → `90f`, `90`, `"90秒"`

### UserSegmentKey.IsMove
- 生の値 `0` → `false`, `"無"`
- 生の値 `1` → `true`, `"有"`

## 既存コードの移行例

### Before（従来の方法）
```csharp
// StageManager.cs
private bool IsActiveMoveLimit()
{
    int isMove = PlayerPrefs.GetInt(UserSegment.USER_SEGMENT_KEY_IS_MOVE, -1);
    if(isMove == 0)
        return false;
    return true;
}

// FirebaseManager.cs
Firebase.Analytics.FirebaseAnalytics.LogEvent("Stage_Failure",
    new Parameter("Stage", GetCurrentStage()),
    new Parameter(UserSegment.USER_SEGMENT_KEY_IS_MOVE, PlayerPrefs.GetInt(UserSegment.USER_SEGMENT_KEY_IS_MOVE, 0)),
    new Parameter(UserSegment.USER_SEGMENT_KEY_AD_INTERVAL, PlayerPrefs.GetInt(UserSegment.USER_SEGMENT_KEY_AD_INTERVAL, 0))
);
```

### After（新しい方法）
```csharp
// StageManager.cs
private bool IsActiveMoveLimit()
{
    return UserSegment.instance.GetValue<bool>(UserSegmentKey.IsMove);
}

// FirebaseManager.cs
LogEventWithUserSegments("Stage_Failure",
    new Parameter("Stage", GetCurrentStage())
    // ABテストパラメータは自動追加されるので不要！
);
```

## ヘルパーメソッド

### GetKeyString
```csharp
string keyString = UserSegment.GetKeyString(UserSegmentKey.AdInterval);
// 戻り値: "Ad_Interval"
```

### GetABTestParameters
```csharp
List<Firebase.Analytics.Parameter> parameters = UserSegment.instance.GetABTestParameters();
// ABテスト中のパラメータのみを含むリストを返す
// 例: [Parameter("Ad_Interval", 0), Parameter("IsMove", 1)]
```

## 新しいセグメントキーの追加方法

### 1. enumに追加
```csharp
public enum UserSegmentKey
{
    AdInterval,
    IsMove,
    NewFeature  // ← 新規追加
}
```

### 2. GetKeyStringに追加
```csharp
public static string GetKeyString(UserSegmentKey key)
{
    switch (key)
    {
        case UserSegmentKey.AdInterval:
            return "Ad_Interval";
        case UserSegmentKey.IsMove:
            return "IsMove";
        case UserSegmentKey.NewFeature:  // ← 新規追加
            return "New_Feature";
        default:
            return "";
    }
}
```

### 3. GetValueメソッドに変換ロジック追加
```csharp
public T GetValue<T>(string propertyName, int value = -1)
{
    // ...
    switch (propertyName)
    {
        // 既存のケース...
        
        case "New_Feature":  // ← 新規追加
            if (typeof(T) == typeof(bool))
            {
                return (T)(object)(value == 1);
            }
            else if (typeof(T) == typeof(string))
            {
                return (T)(object)(value == 0 ? "OFF" : "ON");
            }
            break;
    }
}
```

### 4. ResetUserPropertyDictionaryに追加
```csharp
private void ResetUserPropertyDictionary()
{
    if(_isResetUserPropertyDic)
    {
        _userPropertyDic = new SerializedDictionary<List<int>>();
        _userPropertyDic.Add("Ad_Interval", new List<int>{ 0, 1 });
        _userPropertyDic.Add("IsMove", new List<int>{ 0, 1 });
        _userPropertyDic.Add("New_Feature", new List<int>{ 0, 1 });  // ← 新規追加
        _isResetUserPropertyDic = false;
    }
}
```

## エラーハンドリング

- 未設定のプロパティを指定した場合: `default(T)` を返し、エラーログを出力
- サポートされていない型を指定した場合: `default(T)` を返し、エラーログを出力

## 後方互換性

既存のstring版メソッドも引き続き使用可能です：
```csharp
// 旧方式（非推奨だが動作する）
float interval = UserSegment.instance.GetValue<float>(UserSegment.USER_SEGMENT_KEY_AD_INTERVAL);

// 新方式（推奨）
float interval = UserSegment.instance.GetValue<float>(UserSegmentKey.AdInterval);
```

## 注意事項

1. `UserSegment.instance` が初期化されていることを確認してください
2. 型パラメータ `<T>` は明示的に指定する必要があります
3. Firebase Analyticsへの自動パラメータ追加はABテスト中（valueList.Count >= 2）のキーのみが対象です
