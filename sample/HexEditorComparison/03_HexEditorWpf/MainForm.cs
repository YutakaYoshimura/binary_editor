using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using WpfHexaEditor;

namespace HexEditorSample
{
    /// <summary>
    /// ③ HexEditor.Wpf 動作確認
    ///
    /// WPFHexaEditor と同一コードベースの別パッケージです。
    /// namespace・アセンブリ名が異なる点以外は②と同等の機能を持ちます。
    ///
    /// 確認できる機能（②との違いを中心に確認）:
    ///   - パッケージ名が違っても同じ API で動作することの確認
    ///   - 16進数 / 10進数 表示切り替え
    ///   - バイト編集・Undo/Redo
    ///   - カスタム文字エンコーディング (ASCII / UTF-8 / UTF-16)
    ///   - 行バイト数の変更 (BytePerLine)
    ///   - 選択範囲のハイライト色変更
    /// </summary>
    public class MainForm : Form
    {
        private HexEditor hexEditor = null!;
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
            Text = "③ HexEditor.Wpf - 動作確認（WPFHexaEditorと同一コードベース）";
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
                Height = 130,
                BackColor = Color.FromArgb(245, 240, 255),
                Padding = new Padding(8)
            };

            // 行1: ファイル操作
            var row1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            row1.Controls.Add(Btn("📂 ファイルを開く", () => { var p = SampleHelper.BrowseFile(); if (p != null) LoadFile(p); }));
            row1.Controls.Add(SampleHelper.CreateSampleButtons(LoadFile));

            // 行2: 表示切り替え
            var row2 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            row2.Controls.Add(new Label { Text = "表示形式:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Height = 28 });
            row2.Controls.Add(Btn("16進数", () => SetVisual(DataVisualType.Hexadecimal)));
            row2.Controls.Add(Btn("10進数", () => SetVisual(DataVisualType.Decimal)));

            var lblBpl = new Label { Text = "  行バイト数:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Height = 28 };
            var nudBpl = new NumericUpDown { Minimum = 4, Maximum = 32, Value = 16, Width = 55, Height = 28 };
            nudBpl.ValueChanged += (s, e) => hexEditor.BytePerLine = (int)nudBpl.Value;
            row2.Controls.AddRange(new Control[] { lblBpl, nudBpl });

            // 行3: 編集・コピー
            var row3 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };
            row3.Controls.Add(Btn("↩ Undo", () => { if (hexEditor.UndoCount > 0) { hexEditor.Undo(); statusLabel.Text = "Undo 実行"; } }));
            row3.Controls.Add(Btn("HEXコピー",   () => CopyAs(CopyPasteMode.HexaString)));
            row3.Controls.Add(Btn("C#コピー",    () => CopyAs(CopyPasteMode.CSharpCode)));
            row3.Controls.Add(Btn("Javaコピー",  () => CopyAs(CopyPasteMode.JavaCode)));
            row3.Controls.Add(Btn("C/C++コピー", () => CopyAs(CopyPasteMode.CCode)));

            var chkReadOnly = new CheckBox { Text = "読み取り専用", AutoSize = true, Height = 28, Margin = new Padding(6, 4, 4, 0) };
            chkReadOnly.CheckedChanged += (s, e) => hexEditor.ReadOnlyMode = chkReadOnly.Checked;
            row3.Controls.Add(chkReadOnly);

            toolPanel.Controls.Add(row3);
            toolPanel.Controls.Add(row2);
            toolPanel.Controls.Add(row1);
            Controls.Add(toolPanel);

            // 情報バー
            var infoBar = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.FromArgb(230, 220, 255),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Consolas", 8.5f),
                Text = "選択開始: -  |  選択終了: -  |  選択バイト数: -"
            };

            hexEditor = new HexEditor
            {
                Background = System.Windows.Media.Brushes.White,
                FontSize = 13,
                BytePerLine = 16,
                AllowExtend = true
            };
            hexEditor.SelectionStartChanged += (s, e) => UpdateInfoBar(infoBar);
            hexEditor.SelectionStopChanged  += (s, e) => UpdateInfoBar(infoBar);

            var host = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = new System.Windows.Controls.Grid()
            };
            ((System.Windows.Controls.Grid)host.Child).Children.Add(hexEditor);

            Controls.Add(host);
            Controls.Add(infoBar);
        }

        private void LoadFile(string path)
        {
            try { hexEditor.FileName = path; statusLabel.Text = $"読み込み完了: {Path.GetFileName(path)}"; }
            catch (Exception ex) { MessageBox.Show(ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void SetVisual(DataVisualType type)
        {
            hexEditor.DataStringVisual = type;
            statusLabel.Text = $"表示形式: {type}";
        }

        private void CopyAs(CopyPasteMode mode)
        {
            hexEditor.CopyToClipboard(mode);
            statusLabel.Text = $"クリップボードにコピー ({mode})";
        }

        private void UpdateInfoBar(Label bar)
        {
            try
            {
                var start = hexEditor.SelectionStart;
                var stop  = hexEditor.SelectionStop;
                bar.Text = $"選択開始: 0x{start:X8}  |  選択終了: 0x{stop:X8}  |  選択バイト数: {Math.Abs(stop - start)}";
            }
            catch { }
        }

        private static Button Btn(string text, Action onClick)
        {
            var b = new Button { Text = text, AutoSize = true, Height = 28, Margin = new Padding(2), FlatStyle = FlatStyle.Flat, BackColor = Color.White };
            b.Click += (s, e) => onClick();
            return b;
        }
    }
}
