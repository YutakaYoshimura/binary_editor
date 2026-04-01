using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Spooksoft.HexEditor.Controls;
using Spooksoft.HexEditor.Models;

namespace HexEditorSample
{
    /// <summary>
    /// ⑤ Spooksoft.HexEditor 動作確認
    ///
    /// パフォーマンスと見た目を重視したWPF向けHexエディタコントロールです。
    ///
    /// 確認できる機能:
    ///   - 16進数表示 + ASCII表示
    ///   - ファイル読み込み（byte[] モデル経由）
    ///   - バイト編集
    ///   - 選択・ハイライト
    ///   - 他パッケージとのUI・パフォーマンス比較
    /// </summary>
    public class MainForm : Form
    {
        private HexEditorControl hexEditorControl = null!;
        private ToolStripStatusLabel statusLabel = null!;
        private byte[] currentBytes = Array.Empty<byte>();

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public MainForm()
        {
            Text = "⑤ Spooksoft.HexEditor - 動作確認";
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
                BackColor = Color.FromArgb(255, 235, 235),
                Padding = new Padding(8)
            };

            // 行1: ファイル操作
            var row1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            row1.Controls.Add(Btn("📂 ファイルを開く", () => { var p = SampleHelper.BrowseFile(); if (p != null) LoadFile(p); }));
            row1.Controls.Add(Btn("💾 保存", SaveFile));
            row1.Controls.Add(SampleHelper.CreateSampleButtons(LoadFile));

            // 行2: 操作
            var row2 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };
            row2.Controls.Add(new Label
            {
                Text = "Spooksoft.HexEditor: パフォーマンス重視のWPF専用Hexエディタ。ElementHost経由でWinFormsに組み込みます。",
                AutoSize = true,
                ForeColor = Color.DarkSlateGray,
                TextAlign = ContentAlignment.MiddleLeft,
                Height = 28,
                Font = new Font("Meiryo UI", 8.5f)
            });

            toolPanel.Controls.Add(row2);
            toolPanel.Controls.Add(row1);
            Controls.Add(toolPanel);

            // 情報バー
            var infoBar = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.FromArgb(255, 215, 215),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Consolas", 8.5f),
                Text = "ファイルサイズ: -"
            };

            // Spooksoft.HexEditor コントロールのセットアップ
            hexEditorControl = new HexEditorControl();

            var host = new ElementHost
            {
                Dock = DockStyle.Fill,
                Child = hexEditorControl
            };

            Controls.Add(host);
            Controls.Add(infoBar);
        }

        private void LoadFile(string path)
        {
            try
            {
                currentBytes = File.ReadAllBytes(path);
                // Spooksoft.HexEditor は ByteContainer 経由でデータをバインドする
                var document = new ByteContainer(currentBytes);
                hexEditorControl.Document = document;
                statusLabel.Text = $"読み込み完了: {Path.GetFileName(path)}  ({currentBytes.Length:N0} bytes)";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveFile()
        {
            if (hexEditorControl.Document == null) { MessageBox.Show("データがありません。"); return; }
            var path = SampleHelper.BrowseSaveFile();
            if (path == null) return;
            try
            {
                // Document からバイト列を取得して保存
                var doc = hexEditorControl.Document;
                var bytes = new byte[doc.Count];
                for (int i = 0; i < doc.Count; i++)
                    bytes[i] = doc[i];
                File.WriteAllBytes(path, bytes);
                statusLabel.Text = $"保存完了: {path}";
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
