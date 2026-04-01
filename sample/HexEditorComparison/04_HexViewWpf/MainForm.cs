using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using HexView.Wpf;

namespace HexEditorSample
{
    /// <summary>
    /// ④ HexView.Wpf 動作確認
    ///
    /// 表示特化（ビューア専用）のWPFコントロールです。
    /// 編集機能はなく、バイナリデータの閲覧に特化しています。
    ///
    /// 確認できる機能:
    ///   - バイナリデータの16進数 + ASCII 表示
    ///   - ファイル読み込み（byte[] をバインド）
    ///   - 列数（bytes per row）の変更
    ///   - フォントサイズの変更
    ///   - ①〜③との「編集できない」差の確認
    /// </summary>
    public class MainForm : Form
    {
        private HexViewer hexViewer = null!;
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
            Text = "④ HexView.Wpf - 動作確認（表示専用ビューア）";
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
                Height = 100,
                BackColor = Color.FromArgb(255, 245, 225),
                Padding = new Padding(8)
            };

            // 行1: ファイル操作
            var row1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            row1.Controls.Add(Btn("📂 ファイルを開く", () => { var p = SampleHelper.BrowseFile(); if (p != null) LoadFile(p); }));
            row1.Controls.Add(SampleHelper.CreateSampleButtons(LoadFile));

            // 行2: 表示設定
            var row2 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };

            var lblColumns = new Label { Text = "列数 (bytes/row):", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Height = 28 };
            var nudColumns = new NumericUpDown { Minimum = 4, Maximum = 32, Value = 16, Width = 55, Height = 28 };
            nudColumns.ValueChanged += (s, e) =>
            {
                hexViewer.Columns = (int)nudColumns.Value;
                statusLabel.Text = $"列数変更: {nudColumns.Value}";
            };

            var lblFontSize = new Label { Text = "  フォントサイズ:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Height = 28 };
            var nudFont = new NumericUpDown { Minimum = 8, Maximum = 20, Value = 12, Width = 55, Height = 28 };
            nudFont.ValueChanged += (s, e) =>
            {
                hexViewer.FontSize = (double)nudFont.Value;
                statusLabel.Text = $"フォントサイズ変更: {nudFont.Value}";
            };

            // 注意ラベル
            var noteLbl = new Label
            {
                Text = "  ⚠️ HexView.Wpf は表示専用です。バイトを直接クリックしても編集できません。",
                AutoSize = true,
                ForeColor = Color.DarkOrange,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 28,
                Font = new Font("Meiryo UI", 8.5f, FontStyle.Bold)
            };

            row2.Controls.AddRange(new Control[] { lblColumns, nudColumns, lblFontSize, nudFont, noteLbl });

            toolPanel.Controls.Add(row2);
            toolPanel.Controls.Add(row1);
            Controls.Add(toolPanel);

            // 情報バー
            var infoBar = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.FromArgb(255, 235, 200),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Consolas", 8.5f),
                Text = "ファイルサイズ: -  |  列数: 16  |  ※編集不可（ビューア専用）"
            };

            // HexViewer のセットアップ
            hexViewer = new HexViewer
            {
                Columns = 16,
                FontSize = 12
            };

            var host = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = hexViewer
            };

            Controls.Add(host);
            Controls.Add(infoBar);
        }

        private void LoadFile(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                // HexView.Wpf は DataSource プロパティに byte[] を渡す
                hexViewer.DataSource = bytes;
                statusLabel.Text = $"読み込み完了: {Path.GetFileName(path)}  ({bytes.Length:N0} bytes)  ※表示専用";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Button Btn(string text, Action onClick)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(2), FlatStyle = FlatStyle.Flat, BackColor = Color.White };
            b.Click += (s, e) => onClick();
            return b;
        }
    }
}
