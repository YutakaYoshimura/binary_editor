using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace EdsHexBoxBase
{
    /// <summary>
    /// パターン[1] Be.Windows.Forms.HexBox 利用
    ///
    /// Tab①「バイナリ全体表示」
    ///   HexBox コントロールで EDS バイナリ全データを編集可能な状態で表示する。
    ///
    /// Tab②「パラメータグリッド」
    ///   DataGridView でパラメータ一覧を表示する。
    ///   バイト列セルをクリックすると、そのパラメータのバイトを
    ///   Be.HexBox コントロールをオーバーレイ表示して直接編集できる。
    ///   ・BackSpace: カーソルのバイトを 00 にして 1 バイト前に移動
    ///   ・Delete:    カーソルのバイトを 00 にしてカーソル移動なし
    ///   ・Ctrl+V:    "1234" → 0x12, 0x34 として貼り付け（バイト数不一致は警告）
    ///   ・最終バイトの下位ニブル入力後はカーソルをそこに保持
    /// </summary>
    public class EdsHexBoxBaseForm : Form
    {
        // ─── フィールド ───────────────────────────────────────────
        private byte[]               _data = SampleData.Create();
        private HexBox               _hexBox;           // Tab①
        private DataGridView         _paramGrid;        // Tab②
        private ParamHexBoxOverlay   _paramHexBox;      // Tab② オーバーレイ
        private int                  _overlayParamIdx = -1;
        private TabControl           _tabs;
        private ToolStripStatusLabel _statusLabel;

        // Tab② 列インデックス
        private const int COL_NAME     = 0;
        private const int COL_OFFSET   = 1;
        private const int COL_SIZEINFO = 2;
        private const int COL_RAWBYTES = 3;

        // ─── エントリポイント ─────────────────────────────────────
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new EdsHexBoxBaseForm());
        }

        // ─── コンストラクタ ───────────────────────────────────────
        public EdsHexBoxBaseForm()
        {
            Text          = "パターン[1] Be.HexBox ― ベース画面";
            Size          = new Size(1000, 660);
            MinimumSize   = new Size(700, 460);
            StartPosition = FormStartPosition.CenterScreen;
            BuildUI();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  UI 構築
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void BuildUI()
        {
            _tabs = new TabControl { Dock = DockStyle.Fill };
            // Tab① を離れるとき HexBox の編集内容を _data に書き戻す
            _tabs.Selecting += (s, e) =>
            {
                if (_tabs.SelectedIndex == 0 && e.TabPageIndex != 0)
                    SyncHexBoxToData();
                // Tab② を離れるとき HexBox オーバーレイを非表示にする
                if (_tabs.SelectedIndex == 1 && e.TabPageIndex != 1)
                    HideParamHexOverlay();
                // Tab② に移動するとき _data の最新値でグリッドを更新
                if (e.TabPageIndex == 1)
                    PopulateParamGrid();
            };
            _tabs.TabPages.Add(BuildHexViewTab());
            _tabs.TabPages.Add(BuildParamGridTab());

            var menu     = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("ファイル(&F)");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("開く(&O)...",             null, (s, e) => OpenFile()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("保存(&S)...",             null, (s, e) => SaveFile()));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("サンプルデータに戻す(&R)", null, (s, e) => ResetToSample()));
            menu.Items.Add(fileMenu);
            MainMenuStrip = menu;

            var strip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("サンプルデータを表示中")
            {
                Spring    = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Meiryo UI", 8.5f)
            };
            strip.Items.Add(_statusLabel);

            Controls.Add(_tabs);
            Controls.Add(menu);
            Controls.Add(strip);
        }

        // ────────────────────────────────────────────────────────
        //  Tab①: バイナリ全体表示 (HexBox・編集可能)
        // ────────────────────────────────────────────────────────

        private TabPage BuildHexViewTab()
        {
            var tab = new TabPage("① バイナリ全体表示");

            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 52,
                BackColor = Color.FromArgb(225, 235, 255),
                Padding   = new Padding(10, 0, 10, 0),
            };
            toolbar.Controls.Add(new Label
            {
                Text = "【パターン[1]】 Be.Windows.Forms.HexBox コントロールで EDS バイナリ全データを表示します。\n" +
                       "バイトを直接クリックして編集できます。Tab② に切り替えると編集内容が反映されます。",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(30, 60, 120),
                Font      = new Font("Meiryo UI", 8.5f),
            });

            _hexBox = new HexBox
            {
                Dock                 = DockStyle.Fill,
                Font                 = new Font("Consolas", 10.5f),
                BackColor            = Color.White,
                LineInfoVisible      = true,
                StringViewVisible    = true,
                VScrollBarVisible    = true,
                ColumnInfoVisible    = true,
                BytesPerLine         = 16,
                UseFixedBytesPerLine = true,
                SelectionBackColor   = Color.FromArgb(150, 190, 240),
                SelectionForeColor   = Color.Black,
                InfoForeColor        = Color.SlateGray,
                ReadOnly             = false,   // 編集可能
            };
            _hexBox.ByteProvider = new DynamicByteProvider(_data);

            tab.Controls.Add(_hexBox);
            tab.Controls.Add(toolbar);
            return tab;
        }

        /// <summary>HexBox の DynamicByteProvider から _data に書き戻す</summary>
        private void SyncHexBoxToData()
        {
            DynamicByteProvider prov = _hexBox.ByteProvider as DynamicByteProvider;
            if (prov == null) return;
            long len = Math.Min(prov.Length, _data.Length);
            for (long i = 0; i < len; i++)
                _data[i] = prov.ReadByte(i);
        }

        // ────────────────────────────────────────────────────────
        //  Tab②: パラメータグリッド + HexBox オーバーレイ
        // ────────────────────────────────────────────────────────

        private TabPage BuildParamGridTab()
        {
            var tab = new TabPage("② パラメータグリッド");

            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 52,
                BackColor = Color.FromArgb(225, 255, 225),
                Padding   = new Padding(10, 0, 10, 0),
            };
            toolbar.Controls.Add(new Label
            {
                Text = "【パターン[1]】 EDS パラメータ一覧を DataGridView で表示します。\n" +
                       "「バイト列」列をクリックすると HexBox エディタが表示され、バイトを直接編集できます。",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(30, 80, 40),
                Font      = new Font("Meiryo UI", 8.5f),
            });

            _paramGrid = BuildParamDataGridView();
            PopulateParamGrid();

            // バイト列セルをクリック → HexBox オーバーレイ表示
            _paramGrid.CellClick += (s, e) =>
            {
                if (e.ColumnIndex == COL_RAWBYTES && e.RowIndex >= 0 &&
                    e.RowIndex < SampleData.Parameters.Length &&
                    !SampleData.Parameters[e.RowIndex].IsReadOnly)
                {
                    ShowParamHexOverlay(e.RowIndex);
                }
                else
                {
                    HideParamHexOverlay();
                }
            };

            // オーバーレイ HexBox を生成
            _paramHexBox = new ParamHexBoxOverlay
            {
                Visible              = false,
                Font                 = new Font("Consolas", 10f),
                BackColor            = Color.FromArgb(255, 255, 225),
                LineInfoVisible      = false,
                StringViewVisible    = false,
                ColumnInfoVisible    = false,
                VScrollBarVisible    = false,
                UseFixedBytesPerLine = true,
                SelectionBackColor   = Color.FromArgb(150, 190, 240),
                SelectionForeColor   = Color.Black,
                InsertActive         = false,
                ReadOnly             = false,
            };
            _paramHexBox.Leave += (s, e) => CommitParamHexOverlay();

            tab.Controls.Add(_paramGrid);
            tab.Controls.Add(toolbar);
            tab.Controls.Add(_paramHexBox); // 最後に追加 → 最前面

            return tab;
        }

        private DataGridView BuildParamDataGridView()
        {
            var gv = new DataGridView
            {
                Dock                      = DockStyle.Fill,
                AutoSizeColumnsMode       = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows        = false,
                AllowUserToDeleteRows     = false,
                RowHeadersVisible         = false,
                SelectionMode             = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor           = Color.White,
                GridColor                 = Color.LightSteelBlue,
                BorderStyle               = BorderStyle.None,
                Font                      = new Font("Meiryo UI", 9.5f),
                RowTemplate               = { Height = 26 },
                EnableHeadersVisualStyles = false,
                ReadOnly                  = true,
            };
            gv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 85, 145);
            gv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gv.ColumnHeadersDefaultCellStyle.Font      = new Font("Meiryo UI", 9f, FontStyle.Bold);
            gv.ColumnHeadersHeight = 28;

            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name", HeaderText = "パラメータ名", FillWeight = 18,
            });
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Offset", HeaderText = "オフセット", FillWeight = 10,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter },
            });
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SizeInfo", HeaderText = "サイズ / データ型", FillWeight = 16,
            });
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name       = "RawBytes",
                HeaderText = "バイト列 ✎",
                FillWeight = 56,
                DefaultCellStyle =
                {
                    Font      = new Font("Consolas", 9.5f),
                    BackColor = Color.FromArgb(255, 255, 210),
                }
            });

            gv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) return;
                if (SampleData.Parameters[e.RowIndex].IsReadOnly)
                {
                    e.CellStyle.BackColor = Color.FromArgb(240, 240, 240);
                    e.CellStyle.ForeColor = Color.Gray;
                }
            };

            return gv;
        }

        private void PopulateParamGrid()
        {
            _paramGrid.Rows.Clear();
            foreach (ParameterDef p in SampleData.Parameters)
            {
                _paramGrid.Rows.Add(
                    p.Name,
                    string.Format("0x{0:X4}", p.Offset),
                    string.Format("{0} byte   {1}", p.Size, p.TypeLabel),
                    p.ReadRawBytes(_data));
            }
        }

        // ── HexBox オーバーレイ操作 ──────────────────────────────

        private void ShowParamHexOverlay(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= SampleData.Parameters.Length) return;
            ParameterDef p = SampleData.Parameters[rowIndex];
            if (p.IsReadOnly) return;

            _overlayParamIdx = rowIndex;

            // バイトを FixedByteProvider に渡す
            byte[] paramBytes = new byte[p.Size];
            Array.Copy(_data, p.Offset, paramBytes, 0, p.Size);

            FixedByteProvider prov = new FixedByteProvider(paramBytes);
            _paramHexBox.ByteProvider    = prov;
            _paramHexBox.BytesPerLine    = p.Size;   // 全バイトを 1 行に表示

            // バイト列セルの位置を TabPage 座標に変換
            Rectangle cellBounds = _paramGrid.GetCellDisplayRectangle(COL_RAWBYTES, rowIndex, false);
            if (cellBounds.IsEmpty) return;

            Point screenPt = _paramGrid.PointToScreen(new Point(cellBounds.Left, cellBounds.Top));
            Point tabPt    = _paramGrid.Parent.PointToClient(screenPt);

            // HexBox の幅: セル幅と "バイト数 × 24px" の大きい方
            int hexW = Math.Max(cellBounds.Width, p.Size * 24 + 20);
            int hexH = _paramHexBox.Font.Height + 10;

            _paramHexBox.Location = tabPt;
            _paramHexBox.Size     = new Size(hexW, hexH);
            _paramHexBox.Visible  = true;
            _paramHexBox.BringToFront();
            _paramHexBox.Focus();
            _paramHexBox.SelectionStart  = 0;
            _paramHexBox.SelectionLength = 1;
        }

        private void CommitParamHexOverlay()
        {
            if (_overlayParamIdx < 0 || _overlayParamIdx >= SampleData.Parameters.Length)
            {
                _paramHexBox.Visible = false;
                _overlayParamIdx = -1;
                return;
            }

            ParameterDef  p    = SampleData.Parameters[_overlayParamIdx];
            IByteProvider prov = _paramHexBox.ByteProvider;

            if (prov != null)
            {
                long len = Math.Min(prov.Length, p.Size);
                for (long i = 0; i < len; i++)
                    _data[p.Offset + i] = prov.ReadByte(i);
            }

            _paramHexBox.Visible = false;
            _overlayParamIdx     = -1;

            // パラメータグリッドを最新の _data で更新
            PopulateParamGrid();
            _statusLabel.Text = string.Format("{0} を更新しました", p.Name);
        }

        private void HideParamHexOverlay()
        {
            _paramHexBox.Visible = false;
            _overlayParamIdx     = -1;
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  ファイル操作
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OpenFile()
        {
            using (OpenFileDialog dlg = new OpenFileDialog
                   { Title = "バイナリファイルを開く", Filter = "バイナリ|*.bin;*.eds|すべて|*.*" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                _data = File.ReadAllBytes(dlg.FileName);
                RefreshAll();
                _statusLabel.Text = string.Format("読み込み完了: {0}  ({1:N0} bytes)",
                    Path.GetFileName(dlg.FileName), _data.Length);
            }
        }

        private void SaveFile()
        {
            using (SaveFileDialog dlg = new SaveFileDialog
                   { Title = "保存", Filter = "バイナリ|*.bin;*.eds|すべて|*.*" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                // 保存前に HexBox の最新状態を反映
                SyncHexBoxToData();
                File.WriteAllBytes(dlg.FileName, _data);
                _statusLabel.Text = string.Format("保存完了: {0}", dlg.FileName);
            }
        }

        private void ResetToSample()
        {
            _data = SampleData.Create();
            RefreshAll();
            _statusLabel.Text = "サンプルデータにリセットしました";
        }

        private void RefreshAll()
        {
            HideParamHexOverlay();
            _hexBox.ByteProvider = new DynamicByteProvider(_data);
            PopulateParamGrid();
        }
    }

    // ════════════════════════════════════════════════════════════════
    //  固定長バイトプロバイダ
    //  SupportsInsertBytes / SupportsDeleteBytes を false にすることで
    //  HexBox のバイト挿入・削除操作を根本から無効化し、バイト数を固定する。
    // ════════════════════════════════════════════════════════════════
    internal class FixedByteProvider : IByteProvider
    {
        private readonly byte[] _buf;
        public event EventHandler Changed;
        public event EventHandler LengthChanged;

        public FixedByteProvider(byte[] data)
        {
            _buf = (byte[])data.Clone();
        }

        public long Length            { get { return _buf.Length; } }
        public byte ReadByte(long i)  { return _buf[i]; }

        public void WriteByte(long i, byte value)
        {
            _buf[i] = value;
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        public void InsertBytes(long i, byte[] bs) { }
        public void DeleteBytes(long i, long len)  { }
        public bool SupportsWriteByte()   { return true;  }
        public bool SupportsInsertBytes() { return false; }
        public bool SupportsDeleteBytes() { return false; }
        public bool HasChanges()          { return false; }
        public void ApplyChanges()        { }
    }

    // ════════════════════════════════════════════════════════════════
    //  パラメータ編集用 HexBox オーバーレイ
    //
    //  BackSpace: カーソルのバイトを 00 にして 1 バイト前に移動
    //  Delete:    カーソルのバイトを 00 にしてカーソル移動なし
    //  Ctrl+V:    "1234" → 0x12, 0x34 として貼り付け（バイト数不一致は警告）
    //  最終バイトの下位ニブル後: FixedByteProvider により自動的にカーソル保持
    // ════════════════════════════════════════════════════════════════
    internal class ParamHexBoxOverlay : HexBox
    {
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back)
            {
                ZeroCurrentByte(moveToPrev: true);
                return; // base を呼ばない = HexBox 既定の BackSpace をキャンセル
            }
            if (e.KeyCode == Keys.Delete)
            {
                ZeroCurrentByte(moveToPrev: false);
                return;
            }
            base.OnKeyDown(e);
        }

        private void ZeroCurrentByte(bool moveToPrev)
        {
            IByteProvider prov = ByteProvider;
            if (prov == null || prov.Length == 0) return;

            long pos = SelectionStart;
            if (pos < 0) pos = 0;
            if (pos >= prov.Length) pos = prov.Length - 1;

            prov.WriteByte(pos, 0x00);

            if (moveToPrev && pos > 0)
            {
                SelectionStart  = pos - 1;
                SelectionLength = 1;
            }

            Invalidate();
        }

        /// <summary>
        /// Ctrl+V: クリップボードのテキストを Hex バイト列として解釈して貼り付ける。
        /// "1234" → 0x12, 0x34 として書き込む。
        /// バイト数不一致・無効文字の場合は警告ダイアログを表示する。
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.V))
            {
                string text = Clipboard.GetText();
                if (!string.IsNullOrEmpty(text))
                {
                    string hex = text.Replace(" ", "").Replace("-", "").Trim().ToUpper();
                    IByteProvider prov = ByteProvider;
                    if (prov != null)
                    {
                        if (hex.Length != prov.Length * 2)
                        {
                            MessageBox.Show(
                                string.Format(
                                    "貼り付けデータのバイト数がパラメータと一致しません。\n" +
                                    "期待: {0} バイト（{1} 文字）\n実際: {2} 文字（{3} バイト相当）",
                                    prov.Length, prov.Length * 2,
                                    hex.Length, hex.Length / 2),
                                "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                        else
                        {
                            byte[] bytes = new byte[prov.Length];
                            bool   valid  = true;
                            for (int i = 0; i < bytes.Length; i++)
                            {
                                if (!byte.TryParse(
                                        hex.Substring(i * 2, 2),
                                        System.Globalization.NumberStyles.HexNumber,
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        out bytes[i]))
                                { valid = false; break; }
                            }

                            if (valid)
                            {
                                for (long i = 0; i < prov.Length; i++)
                                    prov.WriteByte(i, bytes[i]);
                                Invalidate();
                            }
                            else
                            {
                                MessageBox.Show(
                                    "クリップボードのテキストに無効な文字が含まれています。\n" +
                                    "16 進数の文字（0-9, A-F）のみ使用できます。",
                                    "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            }
                        }
                    }
                }
                return true; // HexBox 既定の貼り付けを抑止
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }
}
