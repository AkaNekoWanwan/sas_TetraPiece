# GridImageSplitter リファクタリング完了ガイド

## 概要

AbstractGridImageSplitterの継承クラス構造をStrategy パターンベースの設計に変更しました。
これにより、新しい形状パターンの追加が容易になり、コードの保守性が大幅に向上しました。

## 主な変更点

### 1. Strategy パターンの導入

**新規ファイル: `IShapeStrategy.cs`**
- 形状固有の処理を定義する`IShapeStrategy`インターフェース
- `SquareShapeStrategy`, `TriangleShapeStrategy`, `HexShapeStrategy`の実装
- `ShapeStrategyFactory`で形状タイプごとのStrategyを管理

### 2. GridImageSplitterの拡張

**変更ファイル: `GridImageSplitter.cs`**
- `ShapeType`をSerializeFieldとして追加
- インスペクターで形状を選択可能に
- `SetShapeType()`メソッドで動的に形状を変更可能
- Strategy パターンで形状固有の処理を委譲

### 3. 既存クラスの互換性維持

**新規ファイル: `GridImageSplitterTriangleCompat.cs`, `GridImageSplitterHexCompat.cs`**
- 既存の`GridImageSplitterTriangle`/`Hex`との互換性を保つためのラッパークラス
- 内部的には`GridImageSplitter`に委譲し、ShapeTypeを自動設定
- `[Obsolete]`属性でレガシークラスであることを明示

### 4. StageCreatorの簡略化

**変更ファイル: `StageCreator.cs`**
- ShapeType別のリスト管理を削除
- 全てのSplitterを統一的に処理
- `DetermineShapeType()`で形状決定ロジックを集約
- `GetShadowSpriteForShape()`でShadowSprite取得を簡略化
- コード量を約100行削減

### 5. プレハブ移行ツール

**新規ファイル: `SplitterMigrationTool.cs`**
- 既存プレハブの自動移行ツール
- `Tools > Splitter Migration Tool`から実行
- Triangle/HexコンポーネントをGridImageSplitterに変換
- 全データを保持したまま移行

## 使用方法

### 新しいステージを作成する場合

1. GameObjectに`GridImageSplitter`コンポーネントをアタッチ
2. Inspectorで`Shape Type`を選択（Square/Triangle/Hex）
3. 他のパラメータを設定
4. 完了

### 既存プレハブを移行する場合

1. Unity Editorで`Tools > Splitter Migration Tool`を開く
2. 「全プレハブを検索して移行」ボタンをクリック
3. 処理完了を待つ
4. 変換結果を確認

### 新しい形状パターンを追加する場合

**例: Pentagon（五角形）を追加**

1. `ShapeType` enumに`Pentagon`を追加
2. `IShapeStrategy.cs`に`PentagonShapeStrategy`クラスを実装:

```csharp
public class PentagonShapeStrategy : IShapeStrategy
{
    public ShapeType ShapeType => ShapeType.Pentagon;
    
    public int GetTargetPercent() => 110;
    
    public Vector2Int GetGridSizeFromId(int gridId)
    {
        return gridId switch
        {
            5 => new Vector2Int(5, 5),
            6 => new Vector2Int(6, 6),
            _ => Vector2Int.zero
        };
    }
    
    public float GetCellSizeMultiplier() => 1.0f;
    
    public void Initialize(AbstractGridImageSplitter splitter) { }
    
    public void CustomSplitImageIfNeeded(AbstractGridImageSplitter splitter)
    {
        // Pentagon固有の分割ロジック
    }
}
```

3. `ShapeStrategyFactory`に登録:

```csharp
private static readonly Dictionary<ShapeType, IShapeStrategy> _strategies = new Dictionary<ShapeType, IShapeStrategy>
{
    { ShapeType.Square, new SquareShapeStrategy() },
    { ShapeType.Triangle, new TriangleShapeStrategy() },
    { ShapeType.Hex, new HexShapeStrategy() },
    { ShapeType.Pentagon, new PentagonShapeStrategy() } // ★追加
};
```

4. 完了！ 継承クラスを作る必要なし

## 利点

### Before（旧設計）
```csharp
// 新しい形状を追加するたびに：
public class GridImageSplitterPentagon : AbstractGridImageSplitter
{
    public override ShapeType GetShapeType() => ShapeType.Pentagon;
    public override void SplitImage() { /* 300行のコピペ... */ }
}

// StageCreatorでも追加作業:
List<AbstractGridImageSplitter> pentagonSplitters = new List<...>();
// switch文で分岐...
case ShapeType.Pentagon:
    if (indexPentagon < pentagonSplitters.Count) { ... }
```

### After（新設計）
```csharp
// IShapeStrategyを実装するだけ（30行程度）
public class PentagonShapeStrategy : IShapeStrategy { ... }

// StageCreatorは変更不要！
// 自動的に新しい形状に対応
```

## 注意事項

1. **既存プレハブの移行は必須ではありません**
   - Triangle/HexCompatクラスが互換性を保証
   - ただし、移行することを推奨（将来的なメンテナンス性向上）

2. **AbstractGridImageSplitterはabstractのまま**
   - 直接コンポーネントとしてアタッチはできません
   - `GridImageSplitter`を使用してください

3. **Strategy追加時の注意**
   - 必ず`ShapeStrategyFactory`に登録
   - `GetTargetPercent()`は適切な値を返すこと
   - `GetGridSizeFromId()`はStageDataのgridIdに対応

## トラブルシューティング

### Q: 移行後、プレハブが正しく動作しない
A: 移行ツールが正しくデータをコピーできなかった可能性があります。手動で以下を確認：
- ShapeTypeが正しく設定されているか
- cols, rows, _pieceNum等のパラメータが保持されているか

### Q: 新しい形状を追加したが認識されない
A: 以下を確認：
- `ShapeType` enumに追加したか
- Strategyを`ShapeStrategyFactory`に登録したか
- Unity Editorを再起動してみる

### Q: 既存のTriangle/Hexクラスを削除してもいいか
A: 全プレハブを移行した後であれば削除可能です。ただし、以下の手順を推奨：
1. 全プレハブを移行
2. プロジェクト全体で`GridImageSplitterTriangle`/`Hex`の参照を検索
3. 参照がないことを確認してから削除

## まとめ

この設計変更により：
- ✅ 新しい形状パターンの追加が容易に（継承不要）
- ✅ コードの重複を大幅削減
- ✅ StageCreatorの管理負担を軽減
- ✅ 既存プレハブとの互換性を完全に保持
- ✅ 将来的な拡張性を確保

今後、新しい形状を追加する際は`IShapeStrategy`を実装するだけで済みます！
