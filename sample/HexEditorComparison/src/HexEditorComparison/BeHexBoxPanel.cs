using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace HexEditorComparison
{
    /// <summary>
    /// Be.Windows.Forms.HexBox の動作確認パネル
    /// 確認できる機能：
    ///   - 16進数表示 / ASCII表示
    ///   - ファイル読み込み・保存
    ///   - バイト編集（上書き）
    ///   - 検索（FindNext）
    ///   - 選択範囲コピー
    ///   - 行バイト数変更
    ///   - 読み取り専用モード
    /// </summary>
    public class BeHexBoxPanel : UserControl
    {
        private HexBox hexBox = null!;
        private Panel toolPanel = null!;
        private Label infoLabel = null!;
        private readonly string samplesDir;
        private readonly Action<string> updateStatus;
        private FindOptions? lastFindOptions;

        public BeHexBoxPanel(string samplesDir, Action<string> updateStatus)
        {
            this.samplesDir = samplesDir;
            this.updateStatus = updateStatus;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;

            // ツールパネル（上部）
            toolPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = Color.FromArgb(235, 240, 250),
                Padding = new Padding(8)
            };

            // 行1: ファイル操作
            var row1 = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 4)
            };

            var lblFile = new Label { Text = "ファイル操作:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Height = 28 };
            var btnOpenFile = CreateButton("📂 ファイルを開く", OpenFile);
            var btnOpenSimple = CreateButton("simple.bin", () => OpenSample("simple.bin"));
            var btnOpenStruct = CreateButton("structured.bin", () => OpenSample("structured.bin"));
            var btnOpenText = CreateButton("text_and_binary.bin", () => OpenSample("text_and_binary.bin"));
            var btnOpenDiffBase = CreateButton("diff_base.bin", () => OpenSample("diff_base.bin"));
            var btnSave = CreateButton("💾 上書き保存", SaveFile);

            row1.Controls.AddRange(new Control[] { lblFile, btnOpenFile, btnOpenSimple, btnOpenStruct, btnOpenText, btnOpenDiffBase, btnSave });

            // 行2: 表示・編集操作
            var row2 = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };

            var lblFunc = new Label { Text = "機能確認:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Height = 28 };
            var btnFind = CreateButton("🔍 検索", ShowFindDialog);
            var btnFindNext = CreateButton("次を検索", FindNext);
            var chkReadOnly = new CheckBox { Text = "読み取り専用", AutoSize = true, Height = 28, Margin = new Padding(4, 4, 4, 0) };
            chkReadOnly.CheckedChanged += (s, e) => hexBox.ReadOnly = chkReadOnly.Checked;
            var chkAscii = new CheckBox { Text = "ASCII表示", AutoSize = true, Checked = true, Height = 28, Margin = new Padding(4, 4, 4, 0) };
            chkAscii.CheckedChanged += (s, e) => hexBox.StringViewVisible = chkAscii.Checked;
            var lblBytesPerLine = new Label { Text = "行バイト数:", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Height = 28 };
            var nudBytesPerLine = new NumericUpDown { Minimum = 4, Maximum = 64, Value = 16, Width = 60, Height = 28 };
            nudBytesPerLine.ValueChanged += (s, e) => hexBox.BytesPerLine = (int)nudBytesPerLine.Value;

            row2.Controls.AddRange(new Control[] { lblFunc, btnFind, btnFindNext, chkReadOnly, chkAscii, lblBytesPerLine, nudBytesPerLine });

            toolPanel.Controls.Add(row2);
            toolPanel.Controls.Add(row1);

            // 情報ラベル（下部）
            infoLabel = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                BackColor = Color.FromArgb(220, 230, 245),
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font("Consolas", 8f),
                Text = "カーソル位置: -  |  選択バイト数: 0  |  ファイルサイズ: -"
            };

            // HexBox本体
            hexBox = new HexBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 10f),
                BackColor = Color.White,
                ForeColor = Color.FromArgb(30, 30, 30),
                LineInfoVisible = true,
                StringViewVisible = true,
                VScrollBarVisible = true,
                ColumnInfoVisible = true,
                BytesPerLine = 16,
                UseFixedBytesPerLine = true,
                SelectionBackColor = Color.FromArgb(150, 180, 230),
                SelectionForeColor = Color.Black,
                ShadowSelectionColor = Color.FromArgb(100, 150, 200, 255),
                InfoForeColor = Color.Gray
            };

            hexBox.CurrentLineChanged += (s, e) => UpdateInfo();
            hexBox.CurrentPositionInLineChanged += (s, e) => UpdateInfo();
            hexBox.SelectionStartChanged += (s, e) => UpdateInfo();
            hexBox.SelectionLengthChanged += (s, e) => UpdateInfo();

            this.Controls.Add(hexBox);
            this.Controls.Add(infoLabel);
            this.Controls.Add(toolPanel);
        }

        private Button CreateButton(string text, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                AutoSize = true,
                Height = 28,
                Margin = new Padding(2, 2, 2, 2),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White
            };
            btn.Click += (s, e) => onClick();
            return btn;
        }

        private void OpenFile()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "バイナリファイルを選択",
                Filter = "すべてのファイル (*.*)|*.*|バイナリファイル (*.bin)|*.bin|HEXファイル (*.hex)|*.hex"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                LoadFile(dlg.FileName);
        }

        private void OpenSample(string filename)
        {
            var path = Path.Combine(samplesDir, filename);
            if (File.Exists(path))
                LoadFile(path);
            else
                MessageBox.Show($"サンプルファイルが見つかりません:\n{path}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void LoadFile(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var provider = new DynamicByteProvider(bytes);
                hexBox.ByteProvider = provider;
                updateStatus($"読み込み完了: {Path.GetFileName(path)} ({bytes.Length:N0} bytes)");
                UpdateInfo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの読み込みに失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveFile()
        {
            if (hexBox.ByteProvider == null) { MessageBox.Show("保存するデータがありません。"); return; }
            using var dlg = new SaveFileDialog
            {
                Title = "保存先を選択",
                Filter = "バイナリファイル (*.bin)|*.bin|すべてのファイル (*.*)|*.*"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    var provider = hexBox.ByteProvider;
                    var bytes = new byte[provider.Length];
                    for (long i = 0; i < provider.Length; i++)
                        bytes[i] = provider.ReadByte(i);
                    File.WriteAllBytes(dlg.FileName, bytes);
                    updateStatus($"保存完了: {dlg.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"保存に失敗しました:\n{ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ShowFindDialog()
        {
            var input = Microsoft.VisualBasic.Interaction.InputBox(
                "検索するテキストまたはHex値を入力してください\n例: Hello  または  48 65 6C 6C 6F",
                "バイト列検索", "");
            if (string.IsNullOrWhiteSpace(input)) return;

            try
            {
                byte[] findBytes;
                // スペース区切りのHex値かテキストかを判定
                if (input.Contains(" ") && !input.Any(c => c > 127))
                {
                    var parts = input.Split(' ');
                    findBytes = Array.ConvertAll(parts, p => Convert.ToByte(p.Trim(), 16));
                }
                else
                {
                    findBytes = System.Text.Encoding.ASCII.GetBytes(input);
                }

                lastFindOptions = new FindOptions
                {
                    Find = findBytes,
                    Type = FindType.Hex
                };

                var pos = hexBox.Find(lastFindOptions);
                if (pos == -1)
                    MessageBox.Show("見つかりませんでした。", "検索結果");
                else
                    updateStatus($"見つかりました: オフセット 0x{pos:X8} ({pos})");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"検索エラー: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void FindNext()
        {
            if (lastFindOptions == null) { ShowFindDialog(); return; }
            var pos = hexBox.Find(lastFindOptions);
            if (pos == -1)
                MessageBox.Show("次の結果は見つかりませんでした。", "検索結果");
            else
                updateStatus($"次の結果: オフセット 0x{pos:X8} ({pos})");
        }

        private void UpdateInfo()
        {
            if (hexBox.ByteProvider == null)
            {
                infoLabel.Text = "カーソル位置: -  |  選択バイト数: 0  |  ファイルサイズ: -";
                return;
            }
            var pos = hexBox.SelectionStart;
            var selLen = hexBox.SelectionLength;
            var total = hexBox.ByteProvider.Length;
            infoLabel.Text = $"カーソル位置: 0x{pos:X8} ({pos})  |  選択バイト数: {selLen}  |  ファイルサイズ: {total:N0} bytes";
        }
    }
}
