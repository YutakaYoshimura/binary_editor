using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace HexEditorSample
{
    /// <summary>
    /// ① Be.Windows.Forms.HexBox 動作確認
    ///
    /// 確認できる機能:
    ///   - 16進数表示 + ASCII表示（サイドバイサイド）
    ///   - ファイル読み込み・保存
    ///   - バイト直接編集（上書き）
    ///   - 検索 (FindFirst / FindNext)
    ///   - 行バイト数の変更
    ///   - 読み取り専用モード切り替え
    ///   - カーソル位置・選択情報の表示
    /// </summary>
    public class MainForm : Form
    {
        private HexBox hexBox = null!;
        private ToolStripStatusLabel statusLabel = null!;
        private FindOptions? lastFindOptions;

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public MainForm()
        {
            Text = "① Be.Windows.Forms.HexBox - 動作確認";
            Size = new Size(1000, 700);
            MinimumSize = new Size(800, 500);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUI();
        }

        private void BuildUI()
        {
            // ── ステータスバー ──
            var (statusStrip, lbl) = SampleHelper.CreateStatusBar();
            statusLabel = lbl;
            Controls.Add(statusStrip);

            // ── ツールパネル ──
            var toolPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 100,
                BackColor = Color.FromArgb(230, 240, 255),
                Padding = new Padding(8)
            };

            // 行1: ファイル操作
            var row1 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true, Margin = new Padding(0, 0, 0, 4) };
            row1.Controls.Add(Btn("📂 ファイルを開く", () => { var p = SampleHelper.BrowseFile(); if (p != null) LoadFile(p); }));
            row1.Controls.Add(Btn("💾 上書き保存", SaveFile));
            row1.Controls.Add(new Label { Text = "  ", AutoSize = true });
            row1.Controls.Add(SampleHelper.CreateSampleButtons(LoadFile));

            // 行2: 機能操作
            var row2 = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, WrapContents = false, AutoSize = true };
            row2.Controls.Add(Btn("🔍 検索", ShowFindDialog));
            row2.Controls.Add(Btn("次を検索 ▶", FindNext));

            var chkReadOnly = new CheckBox { Text = "読み取り専用", AutoSize = true, Height = 28, Margin = new Padding(6, 4, 6, 0) };
            chkReadOnly.CheckedChanged += (s, e) => hexBox.ReadOnly = chkReadOnly.Checked;

            var chkAscii = new CheckBox { Text = "ASCII表示", AutoSize = true, Checked = true, Height = 28, Margin = new Padding(6, 4, 6, 0) };
            chkAscii.CheckedChanged += (s, e) => hexBox.StringViewVisible = chkAscii.Checked;

            var chkLineInfo = new CheckBox { Text = "オフセット表示", AutoSize = true, Checked = true, Height = 28, Margin = new Padding(6, 4, 6, 0) };
            chkLineInfo.CheckedChanged += (s, e) => hexBox.LineInfoVisible = chkLineInfo.Checked;

            var chkColInfo = new CheckBox { Text = "列ヘッダー", AutoSize = true, Checked = true, Height = 28, Margin = new Padding(6, 4, 6, 0) };
            chkColInfo.CheckedChanged += (s, e) => hexBox.ColumnInfoVisible = chkColInfo.Checked;

            var lblBpl = new Label { Text = "  行バイト数:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Height = 28 };
            var nudBpl = new NumericUpDown { Minimum = 4, Maximum = 64, Value = 16, Width = 55, Height = 28 };
            nudBpl.ValueChanged += (s, e) => hexBox.BytesPerLine = (int)nudBpl.Value;

            row2.Controls.AddRange(new Control[] { chkReadOnly, chkAscii, chkLineInfo, chkColInfo, lblBpl, nudBpl });

            toolPanel.Controls.Add(row2);
            toolPanel.Controls.Add(row1);
            Controls.Add(toolPanel);

            // ── 情報バー ──
            var infoBar = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.FromArgb(210, 225, 245),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Consolas", 8.5f),
                Text = "カーソル: -  |  選択: 0 bytes  |  合計: -"
            };

            // ── HexBox 本体 ──
            hexBox = new HexBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10.5f),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(20, 20, 20),
                LineInfoVisible = true,
                StringViewVisible = true,
                VScrollBarVisible = true,
                ColumnInfoVisible = true,
                BytesPerLine = 16,
                UseFixedBytesPerLine = true,
                SelectionBackColor = Color.FromArgb(160, 190, 230),
                SelectionForeColor = Color.Black,
                InfoForeColor = Color.Gray
            };
            hexBox.SelectionStartChanged  += (s, e) => UpdateInfoBar(infoBar);
            hexBox.SelectionLengthChanged += (s, e) => UpdateInfoBar(infoBar);

            Controls.Add(hexBox);
            Controls.Add(infoBar);
        }

        // ── ファイル操作 ──

        private void LoadFile(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                hexBox.ByteProvider = new DynamicByteProvider(bytes);
                statusLabel.Text = $"読み込み完了: {Path.GetFileName(path)}  ({bytes.Length:N0} bytes)";
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void SaveFile()
        {
            if (hexBox.ByteProvider == null) { MessageBox.Show("データがありません。"); return; }
            var path = SampleHelper.BrowseSaveFile();
            if (path == null) return;
            try
            {
                var p = hexBox.ByteProvider;
                var buf = new byte[p.Length];
                for (long i = 0; i < p.Length; i++) buf[i] = p.ReadByte(i);
                File.WriteAllBytes(path, buf);
                statusLabel.Text = $"保存完了: {path}";
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        // ── 検索 ──

        private void ShowFindDialog()
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "検索するテキストまたは HEX 値を入力\n例: Hello  または  48 65 6C 6C 6F",
                "検索", "");
            if (string.IsNullOrWhiteSpace(input)) return;
            try
            {
                byte[] target;
                if (System.Text.RegularExpressions.Regex.IsMatch(input.Trim(), @"^([0-9A-Fa-f]{2}\s*)+$"))
                    target = Array.ConvertAll(input.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries),
                                             h => Convert.ToByte(h, 16));
                else
                    target = System.Text.Encoding.ASCII.GetBytes(input);

                lastFindOptions = new FindOptions { Find = target, Type = FindType.Hex };
                var pos = hexBox.Find(lastFindOptions);
                statusLabel.Text = pos < 0 ? "見つかりませんでした" : $"見つかりました: オフセット 0x{pos:X8} ({pos})";
            }
            catch (Exception ex) { ShowError(ex.Message); }
        }

        private void FindNext()
        {
            if (lastFindOptions == null) { ShowFindDialog(); return; }
            var pos = hexBox.Find(lastFindOptions);
            statusLabel.Text = pos < 0 ? "次の結果はありません" : $"次の結果: オフセット 0x{pos:X8} ({pos})";
        }

        // ── ユーティリティ ──

        private void UpdateInfoBar(Label bar)
        {
            if (hexBox.ByteProvider == null) return;
            bar.Text = $"カーソル: 0x{hexBox.SelectionStart:X8} ({hexBox.SelectionStart})" +
                       $"  |  選択: {hexBox.SelectionLength} bytes" +
                       $"  |  合計: {hexBox.ByteProvider.Length:N0} bytes";
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
