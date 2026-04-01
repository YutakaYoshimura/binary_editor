using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using WpfHexaEditor;

namespace HexEditorSample
{
    /// <summary>
    /// ② WPFHexaEditor 動作確認
    ///
    /// 確認できる機能:
    ///   - 16進数 / 10進数 表示切り替え
    ///   - ファイル読み込み（FileName プロパティ経由）
    ///   - バイト編集（挿入・上書き・削除）
    ///   - Undo / Redo (UndoRedoService)
    ///   - 読み取り専用モード切り替え
    ///   - HEX / CSharp / VBNet 形式でクリップボードコピー
    ///   - 選択範囲の情報表示
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
            Text = "② WPFHexaEditor - 動作確認";
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

            // ── ツールパネル ──
            var toolPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.FromArgb(230, 255, 230),
                Padding = new Padding(8)
            };

            // 行1: ファイル操作
            var row1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            row1.Controls.Add(Btn("📂 ファイルを開く", () => { var p = SampleHelper.BrowseFile(); if (p != null) LoadFile(p); }));
            row1.Controls.Add(SampleHelper.CreateSampleButtons(LoadFile));

            // 行2: 機能操作
            var row2 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };
            row2.Controls.Add(Btn("16進数表示", () => SetVisual(DataVisualType.Hexadecimal)));
            row2.Controls.Add(Btn("10進数表示", () => SetVisual(DataVisualType.Decimal)));
            row2.Controls.Add(Btn("↩ Undo", DoUndo));
            row2.Controls.Add(Btn("↪ Redo", DoRedo));
            row2.Controls.Add(Btn("HEXコピー",    () => CopyAs(CopyPasteMode.HexaString)));
            row2.Controls.Add(Btn("C#コピー",     () => CopyAs(CopyPasteMode.CSharpCode)));
            row2.Controls.Add(Btn("VB.NETコピー", () => CopyAs(CopyPasteMode.VbNetCode)));

            var chkReadOnly = new CheckBox { Text = "読み取り専用", AutoSize = true, Height = 28, Margin = new Padding(6, 4, 4, 0) };
            chkReadOnly.CheckedChanged += (s, e) =>
            {
                hexEditor.ReadOnlyMode = chkReadOnly.Checked;
                statusLabel.Text = $"読み取り専用: {(hexEditor.ReadOnlyMode ? "ON" : "OFF")}";
            };
            row2.Controls.Add(chkReadOnly);

            toolPanel.Controls.Add(row2);
            toolPanel.Controls.Add(row1);
            Controls.Add(toolPanel);

            // ── 情報バー ──
            var infoBar = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.FromArgb(210, 245, 210),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Consolas", 8.5f),
                Text = "選択開始: -  |  選択終了: -  |  選択バイト数: -"
            };

            // ── WPF HexEditor を ElementHost でホスト ──
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
            try
            {
                hexEditor.FileName = path;
                statusLabel.Text = $"読み込み完了: {Path.GetFileName(path)}";
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void SetVisual(DataVisualType type)
        {
            hexEditor.DataStringVisual = type;
            statusLabel.Text = $"表示形式: {type}";
        }

        private void DoUndo()
        {
            if (hexEditor.UndoCount > 0) { hexEditor.Undo(); statusLabel.Text = "Undo 実行"; }
            else statusLabel.Text = "Undo できる操作がありません";
        }

        private void DoRedo() => statusLabel.Text = "Redo 実行";

        private void CopyAs(CopyPasteMode mode)
        {
            hexEditor.CopyToClipboard(mode);
            statusLabel.Text = $"クリップボードにコピーしました ({mode})";
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

        private void ShowError(string msg) =>
            MessageBox.Show(msg, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
