using System;
using System.ComponentModel.Design;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HexEditorSample
{
    /// <summary>
    /// ⑧ ByteViewer (.NET Framework 標準) 動作確認
    ///
    /// System.Design アセンブリに含まれる組み込みコントロールです。
    /// NuGetパッケージの追加は不要ですが、閲覧専用（編集不可）です。
    ///
    /// 確認できる機能:
    ///   - バイナリデータの表示（Hexadecimal / Ansi / Unicode モード）
    ///   - SetBytes() / SetFile() でデータをロード
    ///   - 表示モードの切り替え (DisplayMode プロパティ)
    ///   - 他パッケージとの「追加インストール不要」の差の確認
    ///   - 編集ができない（＝ビューアの限界）を確認
    /// </summary>
    public class MainForm : Form
    {
        private ByteViewer byteViewer = null!;
        private ToolStripStatusLabel statusLabel = null!;

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public MainForm()
        {
            Text = "⑧ ByteViewer (.NET標準) - 動作確認（閲覧専用・NuGet不要）";
            Size = new Size(1000, 700);
            MinimumSize = new Size(800, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BuildUI();
        }

        private void BuildUI()
        {
            var (statusStrip, lbl) = SampleHelper.CreateStatusBar();
            statusLabel = lbl;
            Controls.Add(statusStrip);

            var toolPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 110,
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(8)
            };

            // 行1: ファイル操作
            var row1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            row1.Controls.Add(Btn("📂 ファイルを開く", () => { var p = SampleHelper.BrowseFile(); if (p != null) LoadFile(p); }));
            row1.Controls.Add(SampleHelper.CreateSampleButtons(LoadFile));

            // 行2: 表示モード切り替え
            var row2 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            row2.Controls.Add(new Label { Text = "表示モード:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Height = 28 });
            row2.Controls.Add(Btn("Hexadecimal",  () => SetMode(DisplayMode.Hexdump)));
            row2.Controls.Add(Btn("Ansi (テキスト)", () => SetMode(DisplayMode.Ansi)));
            row2.Controls.Add(Btn("Auto (自動判定)", () => SetMode(DisplayMode.Auto)));

            // 行3: 注意書き
            var row3 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };
            row3.Controls.Add(new Label
            {
                Text = "⚠️ ByteViewer は .NET Framework 標準の閲覧専用コントロールです。編集・検索機能はありません。NuGetパッケージの追加は不要です。",
                AutoSize = true,
                ForeColor = Color.DarkSlateGray,
                Font = new Font("Meiryo UI", 8.5f, FontStyle.Bold),
                Height = 28
            });

            toolPanel.Controls.Add(row3);
            toolPanel.Controls.Add(row2);
            toolPanel.Controls.Add(row1);
            Controls.Add(toolPanel);

            // 情報バー
            var infoBar = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.FromArgb(220, 220, 220),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Consolas", 8.5f),
                Text = "ファイルサイズ: -  |  表示モード: Hexdump  |  ※編集不可"
            };

            // ByteViewer 本体
            byteViewer = new ByteViewer
            {
                Dock = DockStyle.Fill
            };

            Controls.Add(byteViewer);
            Controls.Add(infoBar);

            // デフォルトのサンプルをロード
            LoadFile(SampleHelper.SamplePath("simple.bin"));
        }

        private void LoadFile(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    statusLabel.Text = $"ファイルが見つかりません: {path}";
                    return;
                }
                // ByteViewer は SetFile でファイルを直接読み込める
                byteViewer.SetFile(path);
                var size = new FileInfo(path).Length;
                statusLabel.Text = $"読み込み完了: {Path.GetFileName(path)}  ({size:N0} bytes)  ※閲覧専用";
            }
            catch (Exception ex)
            {
                // SetFile が失敗した場合は SetBytes にフォールバック
                try
                {
                    var bytes = File.ReadAllBytes(path);
                    byteViewer.SetBytes(bytes);
                    statusLabel.Text = $"読み込み完了 (SetBytes): {Path.GetFileName(path)}  ({bytes.Length:N0} bytes)";
                }
                catch
                {
                    MessageBox.Show(ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void SetMode(DisplayMode mode)
        {
            byteViewer.SetDisplayMode(mode);
            statusLabel.Text = $"表示モード変更: {mode}";
        }

        private static Button Btn(string text, Action onClick)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(2), FlatStyle = FlatStyle.Flat, BackColor = Color.White };
            b.Click += (s, e) => onClick();
            return b;
        }
    }
}
