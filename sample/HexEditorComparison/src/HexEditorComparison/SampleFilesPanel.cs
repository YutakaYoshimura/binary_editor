using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace HexEditorComparison
{
    /// <summary>
    /// サンプルバイナリファイルの内容と用途を説明するパネル
    /// </summary>
    public class SampleFilesPanel : UserControl
    {
        private readonly string samplesDir;
        private RichTextBox hexPreview = null!;

        public SampleFilesPanel(string samplesDir)
        {
            this.samplesDir = samplesDir;
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 420,
                Panel1MinSize = 300,
                Panel2MinSize = 300
            };

            // 左側: ファイル一覧と説明
            var leftScroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            var leftLayout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(16)
            };

            leftLayout.Controls.Add(MakeLabel("📂 サンプルファイル一覧", 13, FontStyle.Bold, Color.FromArgb(30, 60, 100)));
            leftLayout.Controls.Add(MakeLabel("各タブでこれらのファイルを開いて動作確認できます。\nダブルクリックで16進数プレビューを表示します。", 9, FontStyle.Regular, Color.Gray));

            var files = new[]
            {
                new {
                    Name = "simple.bin",
                    Icon = "🔵",
                    Title = "シンプルなバイナリファイル",
                    Desc = "基本的なバイナリ操作の確認用。\n構造: マジックヘッダー(8B) + バージョン(2B) + データ長(4B) + データ(16B) + テキスト(16B) + 末尾マーカー(4B)",
                    UseCase = "用途: 基本的な16進数表示・編集・検索の動作確認"
                },
                new {
                    Name = "structured.bin",
                    Icon = "🟢",
                    Title = "構造体を持つバイナリファイル",
                    Desc = "ファイルフォーマットを模倣した構造体データ。\n構造: マジックナンバー(8B) + ファイルサイズ(4B) + タイムスタンプ(4B) + CRC32(4B) + レコード数(2B) + レコード×3",
                    UseCase = "用途: 構造体テンプレート機能・エンディアン確認（ビッグ/リトル混在）"
                },
                new {
                    Name = "text_and_binary.bin",
                    Icon = "🟡",
                    Title = "テキストとバイナリが混在するファイル",
                    Desc = "テキスト部分とバイナリ部分が混在するファイル。\n構造: テキスト設定ヘッダー + NUL区切り + バイナリデータ + 繰り返しパターン + テキスト末尾",
                    UseCase = "用途: ASCII表示の確認・混在データの可視化"
                },
                new {
                    Name = "diff_base.bin",
                    Icon = "🔴",
                    Title = "差分比較用ベースファイル",
                    Desc = "差分比較テスト用のオリジナルファイル。\n構造: ヘッダー\"DIFFTEST\"(8B) + 連番データ(64B)\nペアファイルのdiff_modified.binと比較することで差分確認が可能",
                    UseCase = "用途: 差分比較（Diff）機能の確認"
                },
                new {
                    Name = "diff_modified.bin",
                    Icon = "🟠",
                    Title = "差分比較用変更済みファイル",
                    Desc = "diff_base.binから5箇所を意図的に変更したファイル。\n変更箇所: オフセット0x12, 0x13, 0x26, 0x27, 0x3A\nWPFHexaEditorの差分比較サンプルで使用できます",
                    UseCase = "用途: 差分比較（Diff）機能の確認"
                },
                new {
                    Name = "intel_hex_sample.hex",
                    Icon = "⚪",
                    Title = "Intel HEX形式ファイル",
                    Desc = "組込み開発で標準的なIntel HEXフォーマット。\nマイコンファームウェアを模倣したサンプル。\nHexIOパッケージ（バックエンドライブラリ）での読み込み確認用",
                    UseCase = "用途: HexIOパッケージのIntel HEXファイル読み書きAPI確認"
                },
            };

            foreach (var f in files)
            {
                var card = MakeFileCard(f.Icon, f.Name, f.Title, f.Desc, f.UseCase);
                leftLayout.Controls.Add(card);
            }

            leftScroll.Controls.Add(leftLayout);
            split.Panel1.Controls.Add(leftScroll);

            // 右側: HEXプレビュー
            var rightLayout = new Panel { Dock = DockStyle.Fill };
            var previewTitle = MakeLabel("🔍 HEXプレビュー（左のファイルをダブルクリック）", 10, FontStyle.Bold, Color.FromArgb(30, 60, 100));
            previewTitle.Dock = DockStyle.Top;
            previewTitle.Height = 28;
            previewTitle.BackColor = Color.FromArgb(240, 245, 255);
            previewTitle.Padding = new Padding(8, 4, 0, 0);

            hexPreview = new RichTextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9f),
                BackColor = Color.FromArgb(20, 20, 30),
                ForeColor = Color.FromArgb(180, 220, 180),
                ReadOnly = true,
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                Text = "← 左のファイル名をダブルクリックするとHEXダンプが表示されます"
            };

            rightLayout.Controls.Add(hexPreview);
            rightLayout.Controls.Add(previewTitle);
            split.Panel2.Controls.Add(rightLayout);

            this.Controls.Add(split);
        }

        private Panel MakeFileCard(string icon, string filename, string title, string desc, string useCase)
        {
            var card = new Panel
            {
                Width = 380,
                AutoSize = true,
                BackColor = Color.FromArgb(248, 249, 252),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(10)
            };

            var layout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Width = 360
            };

            var titleLbl = new Label
            {
                Text = $"{icon} {filename}",
                Font = new Font("Consolas", 10f, FontStyle.Bold),
                AutoSize = true,
                ForeColor = Color.FromArgb(20, 60, 120),
                Cursor = Cursors.Hand,
                Tag = filename
            };
            titleLbl.DoubleClick += (s, e) => ShowHexPreview(filename);

            layout.Controls.Add(titleLbl);
            layout.Controls.Add(MakeLabel(title, 9, FontStyle.Bold, Color.FromArgb(60, 60, 60)));
            layout.Controls.Add(MakeLabel(desc, 8, FontStyle.Regular, Color.FromArgb(80, 80, 80)));
            layout.Controls.Add(MakeLabel(useCase, 8, FontStyle.Italic, Color.FromArgb(0, 100, 60)));

            var path = Path.Combine(samplesDir, filename);
            if (File.Exists(path))
            {
                var size = new FileInfo(path).Length;
                layout.Controls.Add(MakeLabel($"ファイルサイズ: {size} bytes  |  ダブルクリックでプレビュー →", 8, FontStyle.Regular, Color.Gray));
            }
            else
            {
                layout.Controls.Add(MakeLabel("⚠️ ファイルが見つかりません（ビルド後に生成されます）", 8, FontStyle.Regular, Color.OrangeRed));
            }

            card.Controls.Add(layout);
            return card;
        }

        private void ShowHexPreview(string filename)
        {
            var path = Path.Combine(samplesDir, filename);
            if (!File.Exists(path))
            {
                hexPreview.Text = $"ファイルが見つかりません:\n{path}";
                return;
            }

            var bytes = File.ReadAllBytes(path);
            var sb = new StringBuilder();
            sb.AppendLine($"File: {filename}  ({bytes.Length} bytes)");
            sb.AppendLine(new string('─', 68));
            sb.AppendLine("Offset   00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  ASCII");
            sb.AppendLine(new string('─', 68));

            for (int i = 0; i < bytes.Length; i += 16)
            {
                sb.Append($"{i:X8}  ");
                var ascii = new StringBuilder();
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < bytes.Length)
                    {
                        sb.Append($"{bytes[i + j]:X2} ");
                        ascii.Append(bytes[i + j] >= 0x20 && bytes[i + j] < 0x7F ? (char)bytes[i + j] : '.');
                    }
                    else
                    {
                        sb.Append("   ");
                        ascii.Append(' ');
                    }
                    if (j == 7) sb.Append(' ');
                }
                sb.AppendLine($" {ascii}");
            }

            hexPreview.Text = sb.ToString();
        }

        private Label MakeLabel(string text, float size, FontStyle style, Color color)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Meiryo UI", size, style),
                AutoSize = true,
                ForeColor = color,
                Margin = new Padding(0, 1, 0, 1),
                Width = 360
            };
        }
    }
}
