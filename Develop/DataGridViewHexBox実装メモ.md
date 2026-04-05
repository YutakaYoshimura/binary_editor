# DataGridView への HexBox 埋め込み 実装メモ

## 概要

`DataGridViewColumn` / `DataGridViewCell` を継承し、
`Be.Windows.Forms.HexBox` を DataGridView のセル内で動作させる実装について説明する。

---

## 1. 構成クラス

```
DataGridViewHexBoxColumn          ← DataGridViewColumn を継承（列）
  └─ CellTemplate
       DataGridViewHexBoxCell     ← DataGridViewTextBoxCell を継承（セル）
            ↕ 編集開始 / 確定
       DataGridViewHexBoxEditingControl  ← HexBox + IDataGridViewEditingControl（編集コントロール）
```

| クラス | 継承元 | 役割 |
|---|---|---|
| `DataGridViewHexBoxColumn` | `DataGridViewColumn` | 列の定義。`CellTemplate` に `DataGridViewHexBoxCell` を設定する |
| `DataGridViewHexBoxCell` | `DataGridViewTextBoxCell` | セルの定義。値の型・編集コントロールの型・表示文字列変換を担う |
| `DataGridViewHexBoxEditingControl` | `HexBox` + `IDataGridViewEditingControl` | セル編集時に DataGridView に配置される HexBox 本体 |

---

## 2. DataGridViewHexBoxColumn

```csharp
internal class DataGridViewHexBoxColumn : DataGridViewColumn
{
    public DataGridViewHexBoxColumn() : base(new DataGridViewHexBoxCell())
    {
        SortMode = DataGridViewColumnSortMode.NotSortable;
    }

    public override DataGridViewCell CellTemplate
    {
        get { return base.CellTemplate; }
        set
        {
            // DataGridViewHexBoxCell 以外を設定しようとしたら例外を出す
            if (value != null && !(value is DataGridViewHexBoxCell))
                throw new InvalidCastException("...");
            base.CellTemplate = value;
        }
    }
}
```

**ポイント**: コンストラクタで `base(new DataGridViewHexBoxCell())` を渡すことで、
この列に追加される全セルのテンプレートが `DataGridViewHexBoxCell` になる。

---

## 3. DataGridViewHexBoxCell

### 3-1. 型の宣言

```csharp
// セル編集時に使う編集コントロールの型を指定する
public override Type EditType  { get { return typeof(DataGridViewHexBoxEditingControl); } }

// このセルが保持する値の型（string ではなく byte[]）
public override Type ValueType { get { return typeof(byte[]); } }
```

`EditType` を返すと、DataGridView はセル編集開始時に
その型のインスタンスを生成して `DataGridView.Controls` に追加する。

### 3-2. 編集開始: InitializeEditingControl

```csharp
public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
    DataGridViewCellStyle dataGridViewCellStyle)
{
    base.InitializeEditingControl(...);  // DataGridViewCell レベルの配置処理を実行

    DataGridViewHexBoxEditingControl ctl =
        DataGridView.EditingControl as DataGridViewHexBoxEditingControl;

    byte[] bytes = Value as byte[];

    // BytesPerLine: 8 バイト以下なら 1 行に収める
    int bpl = (bytes.Length >= 1 && bytes.Length <= 8) ? bytes.Length : 8;
    ctl.BytesPerLine = bpl;
    ctl.ReadOnly     = this.ReadOnly;
    ctl.BackColor    = this.ReadOnly ? Color.LightGray : Color.FromArgb(255, 255, 225);

    ctl.EditingControlFormattedValue = bytes;  // byte[] を HexBox に渡す
}
```

`base.InitializeEditingControl()` を呼ぶことで `DataGridViewCell` レベルの
コントロール配置処理が実行される。
`DataGridViewTextBoxCell` 側の TextBox 向け処理は `EditType` が異なるため無効になる。

### 3-3. 編集確定: ParseFormattedValue

```csharp
public override object ParseFormattedValue(object formattedValue, ...)
{
    if (formattedValue is byte[]) return formattedValue;
    return new byte[0];
}
```

編集確定時に DataGridView から呼ばれる。
編集コントロールが返す `byte[]` をそのままセル値として受け取る。

### 3-4. 非編集時の表示: GetFormattedValue

```csharp
protected override object GetFormattedValue(object value, int rowIndex,
    ref DataGridViewCellStyle cellStyle, ...)
{
    byte[] bytes = value as byte[];
    // byte[] → "XX XX XX" 形式の文字列に変換して返す
    // DataGridView はこの文字列を使って通常の TextBox セルと同様に描画する
}
```

---

## 4. DataGridViewHexBoxEditingControl

`HexBox` を継承し、`IDataGridViewEditingControl` を実装する。

### 4-1. 主要プロパティ

| プロパティ | 役割 |
|---|---|
| `EditingControlDataGridView` | DataGridView への参照。DataGridView 側から自動セットされる |
| `EditingControlFormattedValue` | `byte[]` の get/set。DataGridView と HexBox の値同期に使う |
| `EditingControlValueChanged` | `true` のとき DataGridView が CommitEdit を発行する |
| `RepositionEditingControlOnValueChange` | `false`（値変更でサイズが変わらないため） |

### 4-2. 値の変更通知

```csharp
// EditingControlFormattedValue の setter
set
{
    DynamicByteProvider prov = new DynamicByteProvider(bytes);
    prov.Changed += (s, e) =>
    {
        _valueChanged = true;
        _dataGridView.NotifyCurrentCellDirty(true);  // DataGridView に変更を通知
    };
    ByteProvider = prov;
}
```

`DynamicByteProvider.Changed` は HexBox でバイトが変更されるたびに発火する。
`NotifyCurrentCellDirty(true)` で DataGridView に通知すると
`CurrentCellDirtyStateChanged` イベントが発火し、`CommitEdit()` が実行される。

### 4-3. キー処理の振り分け

```csharp
public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
{
    switch (keyData & Keys.KeyCode)
    {
        case Keys.Up:
        case Keys.Down:
            return false;  // DataGridView で行移動
        case Keys.Left:
        case Keys.Right:
        case Keys.Back:
        case Keys.Delete:
            return true;   // HexBox 内のカーソル移動・削除
        default:
            return !dataGridViewWantsInputKey;
    }
}
```

`false` を返すと DataGridView がキーを処理する（セルナビゲーション）。
`true` を返すと HexBox がキーを処理する。

---

## 5. 編集の全体フロー

```
ユーザーがセルをクリック
    │
    ▼  DataGridView が EditType のインスタンスを生成・配置
InitializeEditingControl() 呼び出し
    │  → byte[] を HexBox の DynamicByteProvider に渡す
    │
    ▼  ユーザーが HexBox でキー入力
DynamicByteProvider.Changed イベント発火
    │  → NotifyCurrentCellDirty(true)
    │
    ▼  CurrentCellDirtyStateChanged イベント
CommitEdit() 呼び出し
    │
    ▼  GetEditingControlFormattedValue() 呼び出し
HexBox から byte[] を取り出す
    │
    ▼  ParseFormattedValue() 呼び出し
byte[] をセル値として確定
    │
    ▼  CellValueChanged イベント
_data[param.Offset + j] に書き戻す  ← マスターデータ更新
```

---

## 6. DataGridView 側の設定

```csharp
// 編集中に値が変わったら即コミットする
gv.CurrentCellDirtyStateChanged += (s, e) =>
{
    if (gv.IsCurrentCellDirty)
        gv.CommitEdit(DataGridViewDataErrorContexts.Commit);
};

// 値確定後に _data へ書き戻す
gv.CellValueChanged += (s, e) =>
{
    ParameterDef p     = SampleData.Parameters[e.RowIndex];
    byte[]       bytes = gv.Rows[e.RowIndex].Cells[COL_HEX_BYTES].Value as byte[];
    Array.Copy(bytes, 0, _data, p.Offset, p.Size);
};
```

---

## 7. Panel 方式との比較

| 項目 | Panel + HexBox 常時表示 | DataGridView + 継承 |
|---|---|---|
| HexBox の表示 | 常時表示 | セルクリック時のみ |
| 実装複雑度 | 低い | 高い（3クラス必要） |
| スクロール | Panel の AutoScroll | DataGridView 標準 |
| 列幅の統一 | 定数で手動管理 | DataGridView が自動管理 |
| 行選択・ソート | 独自実装が必要 | DataGridView 標準機能が使える |
| WinForms の流儀 | カスタム UI | DataGridView の拡張パターン |

---

## 8. 使用クラス一覧

| クラス | 名前空間 | 役割 |
|---|---|---|
| `HexBox` | `Be.Windows.Forms` | 16進数エディタ本体 |
| `DynamicByteProvider` | `Be.Windows.Forms` | `byte[]` を `IByteProvider` にラップ |
| `IDataGridViewEditingControl` | `System.Windows.Forms` | DataGridView 編集コントロールのインターフェース |
| `DataGridViewColumn` | `System.Windows.Forms` | カスタム列の基底クラス |
| `DataGridViewTextBoxCell` | `System.Windows.Forms` | カスタムセルの基底クラス |
