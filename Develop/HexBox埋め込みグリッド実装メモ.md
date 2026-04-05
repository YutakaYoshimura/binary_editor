# HexBox 埋め込みグリッド 実装メモ

## 概要

`Be.Windows.Forms.HexBox` を各パラメータ行に埋め込み、生バイトを直接 16 進数入力で編集できる
グリッド UI の実装について説明する。

---

## 1. DataGridView に HexBox を入れられない理由

`DataGridView` はセル型として `DataGridViewTextBoxCell` / `DataGridViewComboBoxCell` などを
サポートしているが、**任意の WinForms コントロールをセル内に常時表示することはできない**。

| DataGridView の制約 | 内容 |
|---|---|
| カスタムセル描画 | `DataGridViewCell.Paint()` をオーバーライドして自前描画は可能だが、HexBox は内部に独自レンダリングエンジンを持つため GDI+ での再現が困難 |
| 編集コントロール | `IDataGridViewEditingControl` を実装すれば編集時だけコントロールを表示できるが、**常時表示には対応していない** |
| ホスティング制約 | HexBox は WinForms Control を継承しており、DataGridView のセル内に子コントロールとして常駐させる公式手段がない |

**結論**: DataGridView ではなく、`Panel` + `TableLayoutPanel` でグリッド外観を自作し、
各行に HexBox を常時ホストする方式を採用した。

---

## 2. 採用した構造

```
_hexGridScrollPanel  (Panel, DockStyle.Fill, AutoScroll=true)
└─ container         (Panel, DockStyle.Top, Height=全行合計)
   ├─ headerPanel    (Panel, DockStyle.Top, H=30px)  ← 列ヘッダー
   ├─ [2px 区切り線]
   ├─ rowPanel[0]    (Panel, DockStyle.Top, H=46px~) ← パラメータ行
   │   └─ rowTlp     (TableLayoutPanel, 4列)
   │       ├─ [0] パラメータ名 Label
   │       ├─ [1] オフセット   Label
   │       ├─ [2] サイズ/型    Label
   │       └─ [3] HexBox       ← ここに常時表示
   ├─ [1px 区切り線]
   ├─ rowPanel[1]
   │   └─ rowTlp (同上)
   ├─ [1px 区切り線]
   └─ ...
```

### DockStyle.Top スタック

`container` に `DockStyle.Top` パネルを順番に `Controls.Add()` すると、
追加順に上から積み重なる。これによりグリッド行として見せられる。

```csharp
container.Controls.Add(headerPanel);   // 最初 = 最上部
container.Controls.Add(separator2px);
container.Controls.Add(rowPanel[0]);
container.Controls.Add(separator1px);
container.Controls.Add(rowPanel[1]);
// ...
```

---

## 3. HexBox の初期化

各パラメータ行で、マスターデータ `_data` から対象範囲を切り出して HexBox に渡す。

```csharp
// パラメータ p のバイト範囲をコピー
byte[] paramBytes = new byte[p.Size];
Array.Copy(_data, p.Offset, paramBytes, 0, p.Size);

// DynamicByteProvider: byte[] を IByteProvider にラップする
var provider = new DynamicByteProvider(paramBytes);

var hexBox = new HexBox
{
    BytesPerLine        = p.Size <= 8 ? p.Size : 8,  // 1行あたりのバイト数
    UseFixedBytesPerLine = true,
    ByteProvider        = provider,
    ReadOnly            = p.IsReadOnly,
    LineInfoVisible     = false,   // オフセット列を非表示
    StringViewVisible   = false,   // ASCII ビューを非表示
    ColumnInfoVisible   = false,   // 列ヘッダーを非表示
    VScrollBarVisible   = false,
};
```

### BytesPerLine の設定指針

| パラメータサイズ | BytesPerLine | 表示例 |
|---|---|---|
| 1〜8 byte | = Size（1行に収める） | `01` |
| 9〜16 byte | 8 | 2行で表示 |
| 17 byte〜 | 8 | 複数行 |

---

## 4. 編集内容をマスターデータに同期する仕組み

`DynamicByteProvider.Changed` イベントは、HexBox でバイトが変更されるたびに発火する。
このイベントで `_data` の対応オフセットに即時書き戻す。

```csharp
ParameterDef cap     = p;         // ループ変数をクロージャでキャプチャ
DynamicByteProvider capProv = provider;

provider.Changed += (s, e) =>
{
    for (int j = 0; j < cap.Size; j++)
        _data[cap.Offset + j] = capProv.ReadByte(j);

    _statusLabel.Text = string.Format(
        "{0} を更新しました  →  {1}", cap.Name, cap.ReadRawBytes(_data));
};
```

#### データフロー

```
ユーザーが HexBox でキー入力
    │
    ▼
DynamicByteProvider (内部バッファ) を更新
    │
    ▼  Changed イベント発火
capProv.ReadByte(j) で全バイト読み出し
    │
    ▼
_data[cap.Offset + j] に書き込み   ← マスターデータ更新
    │
    ▼
ステータスバーに更新内容を表示
```

#### ループ変数キャプチャの注意点

C# のクロージャは変数の「参照」をキャプチャするため、`for` ループ内でそのまま使うと
全イベントハンドラが最後の `p` を参照してしまう。

```csharp
// NG: ループ終了後は p が最後の要素を指す
provider.Changed += (s, e) => { _data[p.Offset] = ...; };

// OK: ループ内でローカル変数にコピーしてキャプチャ
ParameterDef cap = p;
provider.Changed += (s, e) => { _data[cap.Offset] = ...; };
```

---

## 5. 行の再構築タイミング

グリッド行は使い回さず、表示のたびに全再構築する（`BuildHexGridRows()`）。

| タイミング | 処理 |
|---|---|
| アプリ起動時 | `BuildHexGridTab()` 内で初回呼び出し |
| Tab③ に切り替えたとき | `OnTabSelecting` → `BuildHexGridRows()` |
| ファイルを開いたとき | `OpenFile()` → `BuildHexGridRows()` |
| サンプルデータにリセット | `ResetToSample()` → `BuildHexGridRows()` |
| Tab① の「HexBox→グリッドへ反映」ボタン | `SyncHexToDataAndRefreshAll()` → `BuildHexGridRows()` |

再構築の先頭で `_hexGridScrollPanel.Controls.Clear()` を呼び、前回のコントロールを破棄する。

---

## 6. 列幅の揃え方

DataGridView と違い、各行が独立した `TableLayoutPanel` なので列幅を明示的に統一する。
全行で同じ定数を使うことで視覚的に列が揃って見える。

```csharp
const int W_NAME   = 145;  // パラメータ名列
const int W_OFFSET = 80;   // オフセット列
const int W_INFO   = 170;  // サイズ/型列
// 4列目 (HexBox) は SizeType.Percent = 100 で残り幅を全て使う
```

```csharp
private static TableLayoutPanel MakeRowTlp(Color bg, int wName, int wOffset, int wInfo)
{
    var tlp = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4 };
    tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, wName));
    tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, wOffset));
    tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, wInfo));
    tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
    return tlp;
}
```

---

## 7. Controls.Add の順序（WinForms レイアウトの注意点）

WinForms では `DockStyle.Fill` のコントロールを**最初に** `Controls.Add()` する必要がある。

```csharp
// 正しい順序
Controls.Add(_tabs);    // DockStyle.Fill → 先に追加
Controls.Add(menu);     // DockStyle.Top  → 後から追加
Controls.Add(strip);    // DockStyle.Bottom → 後から追加
```

後から追加されたコントロール（Top/Bottom）が先にスペースを確保し、
Fill が残りの領域を埋める。逆順にすると TabControl が MenuStrip/StatusStrip に
隠れて表示されなくなる。

---

## 8. 使用クラス一覧

| クラス | 名前空間 | 役割 |
|---|---|---|
| `HexBox` | `Be.Windows.Forms` | 16進数エディタ本体 |
| `DynamicByteProvider` | `Be.Windows.Forms` | `byte[]` を `IByteProvider` にラップ |
| `TableLayoutPanel` | `System.Windows.Forms` | 列幅を固定して行レイアウトを統一 |
| `Panel` (DockStyle.Top) | `System.Windows.Forms` | 行を上から積み重ねるコンテナ |
| `ParameterDef` | `HexEditorVerification` | パラメータ定義（オフセット・サイズ・型） |
