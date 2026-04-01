# HexEditorComparison - バイナリエディタ パッケージ比較プロジェクト

C# / Visual Studio 2019 で各バイナリエディタNuGetパッケージを
**個別プロジェクトとして実際に動かしながら機能比較**するためのサンプル集です。

---

## 動作環境

| 項目 | 要件 |
|------|------|
| IDE | Visual Studio 2019 以上 |
| .NET | .NET Framework 4.8 |
| OS | Windows 10 / 11 |

---

## セットアップ手順

1. `HexEditorComparison.sln` を Visual Studio 2019 で開く
2. メニュー → ビルド → **「ソリューションのNuGetパッケージの復元」**
3. 確認したいプロジェクトを右クリック → **「スタートアッププロジェクトに設定」**
4. **F5** でビルド・起動

---

## プロジェクト一覧

| フォルダ | パッケージ | 種別 | NuGet | 確認のポイント |
|---------|-----------|------|-------|--------------|
| `01_BeHexBox` | Be.Windows.Forms.HexBox 1.6.1 | UIコントロール | 必要 | WinForms専用・編集・検索 |
| `02_WPFHexaEditor` | WPFHexaEditor 2.1.7 | UIコントロール | 必要 | 16進/10進切替・Undo/Redo・各言語コピー |
| `03_HexEditorWpf` | HexEditor.Wpf 2.1.8 | UIコントロール | 必要 | ②と同一コードベースの確認・多言語コピー |
| `04_HexViewWpf` | HexView.Wpf 0.1.0 | UIコントロール（表示専用） | 必要 | 表示専用であることの確認 |
| `05_SpooksoftHexEditor` | Spooksoft.HexEditor 1.0.3 | UIコントロール | 必要 | パフォーマンス・UI感触の比較 |
| `08_ByteViewer` | ByteViewer (.NET標準) | UIコントロール（表示専用） | **不要** | NuGet不要・表示モード切替 |

---

## サンプルファイル (`samples/`)

各プロジェクトの `samples/` ディレクトリ（ビルド後は `bin/Debug/samples/`）に含まれます。

| ファイル名 | サイズ | 内容 | 確認用途 |
|-----------|--------|------|---------|
| `simple.bin` | 50 bytes | マジックヘッダー + バージョン + データ + テキスト | 基本操作 |
| `structured.bin` | 94 bytes | 構造体（レコード×3）・BE/LE混在 | 構造体・エンディアン |
| `text_and_binary.bin` | 126 bytes | テキスト設定とバイナリが混在 | ASCII表示 |
| `diff_base.bin` | 72 bytes | 差分比較のベースファイル | 差分比較 |
| `diff_modified.bin` | 72 bytes | diff_base.binから5箇所を変更済み | 差分比較 |
| `intel_hex_sample.hex` | 174 bytes | Intel HEXフォーマット | HexIO確認用 |

---

## 各プロジェクトで確認できる機能

### 01_BeHexBox
- ✅ 16進数 + ASCII のサイドバイサイド表示
- ✅ バイトの直接編集（セルをクリックして16進数で入力）
- ✅ 検索（テキスト / HEXバイト列）・次を検索
- ✅ 読み取り専用モード切り替え
- ✅ 行バイト数の変更（4〜64）
- ✅ オフセット表示 / 列ヘッダー の ON/OFF
- ✅ ファイル読み込み・保存

### 02_WPFHexaEditor
- ✅ 16進数 / 10進数 表示切り替え
- ✅ バイト編集（挿入・上書き・削除）
- ✅ Undo（UndoRedoService）
- ✅ 読み取り専用モード切り替え
- ✅ クリップボードコピー（HEX文字列 / C# / VB.NET 形式）
- ✅ ファイル読み込み（FileName プロパティ）

### 03_HexEditorWpf
- ✅ ②と同一コードベースの別パッケージであることを確認
- ✅ 行バイト数の変更（BytePerLine）
- ✅ クリップボードコピー（HEX / C# / Java / C/C++ 形式）
- ✅ 読み取り専用・Undo

### 04_HexViewWpf
- ✅ バイナリデータの16進数 + ASCII 表示（閲覧のみ）
- ✅ 列数（bytes/row）の変更
- ✅ フォントサイズの変更
- ❌ 編集不可（表示専用であることを確認）

### 05_SpooksoftHexEditor
- ✅ バイナリデータの表示・編集
- ✅ ByteContainer 経由のデータバインド
- ✅ ファイル読み込み・保存
- ✅ 他パッケージとのUI・パフォーマンス比較

### 08_ByteViewer
- ✅ Hexdump / Ansi / Auto モードの切り替え
- ✅ SetFile() / SetBytes() でデータ読み込み
- ✅ NuGetパッケージ追加なしで動作する点を確認
- ❌ 編集不可（閲覧専用であることを確認）

---

## ファイル構成

```
HexEditorComparison/
├── HexEditorComparison.sln        ← ソリューション（全6プロジェクト）
├── README.md
├── SampleHelper.cs                ← 全プロジェクト共通ヘルパー
├── samples/                       ← サンプルバイナリファイル
│   ├── simple.bin
│   ├── structured.bin
│   ├── text_and_binary.bin
│   ├── diff_base.bin
│   ├── diff_modified.bin
│   └── intel_hex_sample.hex
├── 01_BeHexBox/
│   ├── Sample01_BeHexBox.csproj
│   └── MainForm.cs
├── 02_WPFHexaEditor/
│   ├── Sample02_WPFHexaEditor.csproj
│   └── MainForm.cs
├── 03_HexEditorWpf/
│   ├── Sample03_HexEditorWpf.csproj
│   └── MainForm.cs
├── 04_HexViewWpf/
│   ├── Sample04_HexViewWpf.csproj
│   └── MainForm.cs
├── 05_SpooksoftHexEditor/
│   ├── Sample05_SpooksoftHexEditor.csproj
│   └── MainForm.cs
└── 08_ByteViewer/
    ├── Sample08_ByteViewer.csproj
    └── MainForm.cs
```

---

## 注意事項

### ⑤ Spooksoft.HexEditor について
情報・ドキュメントが少ないパッケージです。APIが変更されている場合は
`MainForm.cs` 内の `ByteContainer` や `Document` プロパティ名を
NuGetパッケージの実際の型に合わせて修正してください。

### WPF系（②③④⑤）について
ElementHost 経由でWinFormsに組み込んでいます。
プロジェクトのターゲットフレームワークが `net48` であることを確認してください。

### ⑧ ByteViewer について
`System.Design` アセンブリへの参照が必要です。
`.csproj` に `<Reference Include="System.Design" />` が記載されています。
