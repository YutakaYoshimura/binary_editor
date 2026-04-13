using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace EdsStandardBase
{
    public class EdsStandardBaseForm : Form
    {
        // ─── フィールド ───────────────────────────────────────────
        private byte[]               _data = SampleData.Create();
        private DataGridView         _hexGrid;
        private DataGridView         _paramGrid;
        private ByteOverlayGrid      _byteOverlayDgv;
        private TabControl           _tabs;
        private ToolStripStatusLabel _statusLabel;

        private int _hexGridNibbleCount = 0;
        private int _overlayNibbleCount = 0;
        private int _overlayParamIdx    = -1;

        // Tab② 列インデックス
        private const int COL_NAME     = 0;
        private const int COL_OFFSET   = 1;
        private const int COL_SIZEINFO = 2;
        private const int COL_RAWBYTES = 3;

        // ─── 内部クラス: Ctrl+V をフォームへ転送する DataGridView ─
        private class ByteOverlayGrid : DataGridView
        {
            public event EventHandler<string> PasteRequest;

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                if (keyData == (Keys.Control | Keys.V))
                {
                    var h = PasteRequest;
                    if (h != null) h(this, Clipboard.GetText());
                    return true;
                }
                return base.ProcessCmdKey(ref msg, keyData);
            }
        }

        // ─── エントリポイント ─────────────────────────────────────
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new EdsStandardBaseForm());
        }

        // ─── コンストラクタ ───────────────────────────────────────
        public EdsStandardBaseForm()
        {
            Text          = "パターン[2] DataGridView ― ベース画面";
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
        //  Tab①: バイナリ全体表示
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
                Text = "【パターン[2]】 DataGridView で EDS バイナリ全データを Hex ビュー表示します。\n" +
                       "バイトセルをクリックして 2 桁 Hex 入力 → 下位ニブル確定で次のセルへ自動移動。",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(30, 60, 120),
                Font      = new Font("Meiryo UI", 8.5f),
            });

            _hexGrid = BuildHexViewDataGridView();
            PopulateHexGrid();

            tab.Controls.Add(_hexGrid);
            tab.Controls.Add(toolbar);
            return tab;
        }

        private DataGridView BuildHexViewDataGridView()
        {
            var gv = new DataGridView
            {
                Dock                        = DockStyle.Fill,
                AllowUserToAddRows          = false,
                AllowUserToDeleteRows       = false,
                AllowUserToResizeRows       = false,
                RowHeadersVisible           = false,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                SelectionMode               = DataGridViewSelectionMode.CellSelect,
                BackgroundColor             = Color.White,
                GridColor                   = Color.FromArgb(210, 220, 235),
                BorderStyle                 = BorderStyle.None,
                Font                        = new Font("Consolas", 9.5f),
                RowTemplate                 = { Height = 20 },
                EnableHeadersVisualStyles   = false,
                AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.None,
            };
            gv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 85, 145);
            gv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gv.ColumnHeadersDefaultCellStyle.Font      = new Font("Consolas", 9f, FontStyle.Bold);
            gv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            gv.ColumnHeadersHeight = 24;

            // Offset 列 (読み取り専用)
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name      = "Offset",
                HeaderText = "Offset",
                Width     = 82,
                Resizable = DataGridViewTriState.False,
                ReadOnly  = true,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(80, 100, 180),
                    Font      = new Font("Consolas", 9.5f),
                    BackColor = Color.FromArgb(240, 244, 255),
                }
            });

            // バイト列 × 16
            for (int i = 0; i < 16; i++)
            {
                gv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name       = "B" + i.ToString("X2"),
                    HeaderText = i.ToString("X2"),
                    Width      = 28,
                    Resizable  = DataGridViewTriState.False,
                    DefaultCellStyle =
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                    }
                });
            }

            // ASCII 列 (読み取り専用)
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name         = "Text",
                HeaderText   = "Text",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                Resizable    = DataGridViewTriState.True,
                ReadOnly     = true,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.DimGray,
                    BackColor = Color.FromArgb(248, 248, 248),
                }
            });

            gv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.ColumnIndex == 0 || e.ColumnIndex == 17) return;
                e.CellStyle.BackColor = (e.RowIndex % 2 == 0)
                    ? Color.White
                    : Color.FromArgb(246, 249, 255);
            };

            gv.CellBeginEdit         += HexGrid_CellBeginEdit;
            gv.CellEndEdit           += HexGrid_CellEndEdit;
            gv.EditingControlShowing += HexGrid_EditingControlShowing;
            gv.KeyDown               += HexGrid_DGV_KeyDown;

            return gv;
        }

        private void PopulateHexGrid()
        {
            _hexGrid.Rows.Clear();
            for (int lineBase = 0; lineBase < _data.Length; lineBase += 16)
            {
                object[] cells = new object[18];
                cells[0] = lineBase.ToString("X8");

                var ascii = new StringBuilder();
                for (int b = 0; b < 16; b++)
                {
                    int idx = lineBase + b;
                    if (idx < _data.Length)
                    {
                        cells[b + 1] = _data[idx].ToString("X2");
                        byte bv = _data[idx];
                        ascii.Append((bv >= 0x20 && bv < 0x7F) ? (char)bv : '.');
                    }
                    else
                    {
                        cells[b + 1] = string.Empty;
                    }
                }
                cells[17] = ascii.ToString();
                _hexGrid.Rows.Add(cells);
            }
        }

        // ── HexGrid 編集イベント ────────────────────────────────────

        private void HexGrid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            int col = e.ColumnIndex;
            int row = e.RowIndex;
            if (col < 1 || col > 16) { e.Cancel = true; return; }
            int byteIdx = row * 16 + (col - 1);
            if (byteIdx >= _data.Length) { e.Cancel = true; return; }
            _hexGridNibbleCount = 0;
        }

        private void HexGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            int col = e.ColumnIndex;
            int row = e.RowIndex;
            if (col < 1 || col > 16) return;
            int byteIdx = row * 16 + (col - 1);
            if (byteIdx >= _data.Length) return;

            object val  = _hexGrid.Rows[row].Cells[col].Value;
            string text = val != null ? val.ToString().Trim().ToUpper() : "";
            byte b;
            if (text.Length == 2 && byte.TryParse(text,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out b))
            {
                _data[byteIdx] = b;
            }
            // 常に _data から表示を再構成（不完全入力を元に戻す）
            _hexGrid.Rows[row].Cells[col].Value = _data[byteIdx].ToString("X2");
            UpdateHexGridAsciiCell(row);
        }

        private void HexGrid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            TextBox tb = e.Control as TextBox;
            if (tb == null) return;

            tb.MaxLength        = 2;
            tb.CharacterCasing  = CharacterCasing.Upper;
            tb.KeyDown  -= HexGridTB_KeyDown;
            tb.KeyPress -= HexGridTB_KeyPress;
            tb.KeyDown  += HexGridTB_KeyDown;
            tb.KeyPress += HexGridTB_KeyPress;

            _hexGridNibbleCount = 0;
            _hexGrid.BeginInvoke(new Action(() =>
            {
                TextBox t = _hexGrid.EditingControl as TextBox;
                if (t != null) t.SelectAll();
            }));
        }

        private void HexGridTB_KeyDown(object sender, KeyEventArgs e)
        {
            var tb = (TextBox)sender;
            if (_hexGrid.CurrentCell == null) return;
            int col = _hexGrid.CurrentCell.ColumnIndex;
            int row = _hexGrid.CurrentCell.RowIndex;
            if (col < 1 || col > 16) return;

            if (e.KeyCode == Keys.Back)
            {
                e.SuppressKeyPress = true;
                tb.Text = "00";
                _hexGridNibbleCount = 0;
                int cc = col, cr = row;
                _hexGrid.BeginInvoke(new Action(() =>
                {
                    _hexGrid.EndEdit();
                    MoveToPrevByteCell(cr, cc);
                }));
            }
            else if (e.KeyCode == Keys.Delete)
            {
                e.SuppressKeyPress = true;
                tb.Text = "00";
                _hexGridNibbleCount = 0;
                _hexGrid.BeginInvoke(new Action(() => _hexGrid.EndEdit()));
            }
        }

        private void HexGridTB_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\x16') // Ctrl+V → DGV KeyDown で処理
            {
                e.Handled = true;
                int col = _hexGrid.CurrentCell != null ? _hexGrid.CurrentCell.ColumnIndex : -1;
                int row = _hexGrid.CurrentCell != null ? _hexGrid.CurrentCell.RowIndex    : -1;
                if (col >= 1 && col <= 16 && row >= 0) PasteToHexGrid(row, col);
                return;
            }
            if (char.IsControl(e.KeyChar)) return;

            char c = char.ToUpper(e.KeyChar);
            if (!IsHexChar(c)) { e.Handled = true; return; }

            _hexGridNibbleCount++;
            if (_hexGridNibbleCount >= 2)
            {
                _hexGridNibbleCount = 0;
                int col = _hexGrid.CurrentCell != null ? _hexGrid.CurrentCell.ColumnIndex : -1;
                int row = _hexGrid.CurrentCell != null ? _hexGrid.CurrentCell.RowIndex    : -1;
                _hexGrid.BeginInvoke(new Action(() =>
                {
                    _hexGrid.EndEdit();
                    if (col >= 1 && col <= 16 && row >= 0)
                        MoveToNextByteCell(row, col);
                }));
            }
        }

        /// <summary>
        /// 指定セル位置からクリップボードの Hex バイト列を複数セルに跨いで貼り付ける。
        /// 編集中・非編集中どちらでも動作する。
        /// </summary>
        private void PasteToHexGrid(int startRow, int startCol)
        {
            string text = Clipboard.GetText();
            string hex  = text.Replace(" ", "").Replace("-", "").Trim().ToUpper();

            if (hex.Length < 2)
            {
                MessageBox.Show(
                    "クリップボードのテキストに無効な文字が含まれています。\n16 進数の文字（0-9, A-F）のみ使用できます。",
                    "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int startByteIdx = startRow * 16 + (startCol - 1);
            int byteCount    = hex.Length / 2;
            int writeCount   = Math.Min(byteCount, _data.Length - startByteIdx);

            byte[] bytes = new byte[writeCount];
            bool   valid = true;
            for (int i = 0; i < writeCount; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out bytes[i]))
                { valid = false; break; }
            }

            if (!valid)
            {
                MessageBox.Show(
                    "クリップボードのテキストに無効な文字が含まれています。\n16 進数の文字（0-9, A-F）のみ使用できます。",
                    "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int capturedStart = startByteIdx;
            int capturedCount = writeCount;
            byte[] capturedBytes = bytes;

            _hexGridNibbleCount = 0;
            // BeginInvoke でエディット終了後に全バイトを一括書き込みする
            _hexGrid.BeginInvoke(new Action(() =>
            {
                if (_hexGrid.IsCurrentCellInEditMode) _hexGrid.CancelEdit();

                var affectedRows = new System.Collections.Generic.HashSet<int>();
                for (int i = 0; i < capturedCount; i++)
                {
                    _data[capturedStart + i] = capturedBytes[i];
                    int r = (capturedStart + i) / 16;
                    int c = (capturedStart + i) % 16 + 1;
                    _hexGrid.Rows[r].Cells[c].Value = capturedBytes[i].ToString("X2");
                    affectedRows.Add(r);
                }
                foreach (int r in affectedRows)
                    UpdateHexGridAsciiCell(r);
            }));
        }

        private void UpdateHexGridAsciiCell(int row)
        {
            if (row < 0 || row >= _hexGrid.Rows.Count) return;
            int lineBase = row * 16;
            var sb = new StringBuilder(16);
            for (int b = 0; b < 16; b++)
            {
                int idx = lineBase + b;
                if (idx < _data.Length)
                {
                    byte bv = _data[idx];
                    sb.Append((bv >= 0x20 && bv < 0x7F) ? (char)bv : '.');
                }
            }
            _hexGrid.Rows[row].Cells[17].Value = sb.ToString();
        }

        private void MoveToPrevByteCell(int row, int col)
        {
            int newCol = col - 1, newRow = row;
            if (newCol < 1) { newRow--; newCol = 16; }
            if (newRow < 0) return;
            if (newRow * 16 + (newCol - 1) >= _data.Length) return;
            _hexGrid.CurrentCell = _hexGrid.Rows[newRow].Cells[newCol];
            _hexGrid.BeginEdit(true);
            _hexGridNibbleCount = 0;
        }

        private void MoveToNextByteCell(int row, int col)
        {
            int newCol = col + 1, newRow = row;
            if (newCol > 16) { newCol = 1; newRow++; }
            if (newRow >= _hexGrid.Rows.Count) return;
            if (newRow * 16 + (newCol - 1) >= _data.Length) return;
            _hexGrid.CurrentCell = _hexGrid.Rows[newRow].Cells[newCol];
            _hexGrid.BeginEdit(true);
            _hexGridNibbleCount = 0;
        }

        /// <summary>
        /// DataGridView が直接フォーカスを持つとき（非編集状態）の BackSpace / Delete 処理。
        /// 編集中は HexGridTB_KeyDown が担当するためスキップする。
        /// </summary>
        private void HexGrid_DGV_KeyDown(object sender, KeyEventArgs e)
        {
            if (_hexGrid.CurrentCell == null) return;

            int col = _hexGrid.CurrentCell.ColumnIndex;
            int row = _hexGrid.CurrentCell.RowIndex;

            // Ctrl+V: 編集中は HexGridTB_KeyPress が処理、非編集中はここで処理
            if (e.KeyCode == Keys.V && e.Control)
            {
                if (_hexGrid.IsCurrentCellInEditMode) return;
                e.Handled          = true;
                e.SuppressKeyPress = true;
                if (col >= 1 && col <= 16 && row >= 0) PasteToHexGrid(row, col);
                return;
            }

            if (e.KeyCode != Keys.Back && e.KeyCode != Keys.Delete) return;
            if (_hexGrid.IsCurrentCellInEditMode) return; // 編集中は TextBox ハンドラに委ねる
            if (col < 1 || col > 16) return;
            int byteIdx = row * 16 + (col - 1);
            if (byteIdx >= _data.Length) return;

            e.Handled        = true;
            e.SuppressKeyPress = true;

            _data[byteIdx] = 0x00;
            _hexGrid.Rows[row].Cells[col].Value = "00";
            UpdateHexGridAsciiCell(row);

            if (e.KeyCode == Keys.Back)
                MoveToPrevByteCell(row, col);
            // Delete: カーソル移動なし
        }

        private static bool IsHexChar(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
        }

        // ────────────────────────────────────────────────────────
        //  Tab②: パラメータグリッド
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
                Text = "【パターン[2]】 バイト列セル（✎）をクリックすると DataGridView オーバーレイで編集できます。\n" +
                       "各バイトに Hex 入力 → 下位ニブル確定で次のセルへ自動移動。",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(30, 80, 40),
                Font      = new Font("Meiryo UI", 8.5f),
            });

            _paramGrid      = BuildParamDataGridView();
            _byteOverlayDgv = BuildByteOverlayGrid();

            PopulateParamGrid();

            // 追加順: paramGrid → overlay (overlay を後から追加して BringToFront で最前面へ)
            tab.Controls.Add(_paramGrid);
            tab.Controls.Add(toolbar);
            tab.Controls.Add(_byteOverlayDgv);
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
                SelectionMode             = DataGridViewSelectionMode.CellSelect,
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

            gv.CellClick += ParamGrid_CellClick;

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

        private void ParamGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != COL_RAWBYTES) return;
            if (e.RowIndex >= SampleData.Parameters.Length) return;
            if (SampleData.Parameters[e.RowIndex].IsReadOnly) return;
            ShowByteOverlay(e.RowIndex);
        }

        // ────────────────────────────────────────────────────────
        //  バイトオーバーレイ (Tab②)
        // ────────────────────────────────────────────────────────

        private ByteOverlayGrid BuildByteOverlayGrid()
        {
            var gv = new ByteOverlayGrid
            {
                Visible                     = false,
                AllowUserToAddRows          = false,
                AllowUserToDeleteRows       = false,
                RowHeadersVisible           = false,
                ColumnHeadersVisible        = false,   // ヘッダ不要
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                SelectionMode               = DataGridViewSelectionMode.CellSelect,
                BackgroundColor             = Color.FromArgb(255, 255, 200),
                GridColor                   = Color.FromArgb(180, 160, 80),
                BorderStyle                 = BorderStyle.FixedSingle,
                Font                        = new Font("Consolas", 9.5f),
                MultiSelect                 = false,
                ScrollBars                  = ScrollBars.None,
                EnableHeadersVisualStyles   = false,
                AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.None,
            };
            gv.RowTemplate.Height = 24;

            gv.CellBeginEdit         += (s, e) => { _overlayNibbleCount = 0; };
            gv.EditingControlShowing += ByteOverlay_EditingControlShowing;
            gv.CellEndEdit           += ByteOverlay_CellEndEdit;
            gv.PasteRequest          += ByteOverlay_PasteRequest;

            gv.Leave += (s, e2) =>
            {
                gv.BeginInvoke(new Action(() =>
                {
                    if (!gv.ContainsFocus) CommitByteOverlay();
                }));
            };

            return gv;
        }

        private void ShowByteOverlay(int paramIdx)
        {
            if (_overlayParamIdx >= 0) CommitByteOverlay();

            _overlayParamIdx = paramIdx;
            ParameterDef p = SampleData.Parameters[paramIdx];

            _byteOverlayDgv.Columns.Clear();
            _byteOverlayDgv.Rows.Clear();

            for (int i = 0; i < p.Size; i++)
            {
                _byteOverlayDgv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = i.ToString("X2"),
                    Width      = 34,
                    SortMode   = DataGridViewColumnSortMode.NotSortable,
                    Resizable  = DataGridViewTriState.False,
                    DefaultCellStyle =
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                        Font      = new Font("Consolas", 9.5f),
                        BackColor = Color.FromArgb(255, 255, 200),
                    }
                });
            }

            object[] cells = new object[p.Size];
            for (int i = 0; i < p.Size; i++)
                cells[i] = _data[p.Offset + i].ToString("X2");
            _byteOverlayDgv.Rows.Add(cells);

            // セル座標 → スクリーン → タブページ座標
            Rectangle cellRect = _paramGrid.GetCellDisplayRectangle(COL_RAWBYTES, paramIdx, false);
            Point     screenPt = _paramGrid.PointToScreen(cellRect.Location);
            TabPage   tabPage  = _paramGrid.Parent as TabPage;
            if (tabPage == null) return;
            Point parentPt = tabPage.PointToClient(screenPt);

            int overlayW = Math.Max(p.Size * 34 + 4, cellRect.Width);
            int overlayH = _byteOverlayDgv.RowTemplate.Height + 4;

            _byteOverlayDgv.SetBounds(parentPt.X, parentPt.Y, overlayW, overlayH);
            _byteOverlayDgv.Visible = true;
            _byteOverlayDgv.BringToFront();

            _byteOverlayDgv.CurrentCell = _byteOverlayDgv.Rows[0].Cells[0];
            _byteOverlayDgv.BeginEdit(true);
            _overlayNibbleCount = 0;
        }

        private void CommitByteOverlay()
        {
            if (_overlayParamIdx < 0) return;
            ParameterDef p = SampleData.Parameters[_overlayParamIdx];

            _byteOverlayDgv.EndEdit();

            for (int i = 0; i < p.Size && i < _byteOverlayDgv.Columns.Count; i++)
            {
                object val  = _byteOverlayDgv.Rows[0].Cells[i].Value;
                string text = val != null ? val.ToString().Trim().ToUpper() : "";
                byte b;
                if (text.Length == 2 && byte.TryParse(text,
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out b))
                {
                    _data[p.Offset + i] = b;
                }
            }

            HideByteOverlay();
            PopulateHexGrid();
            PopulateParamGrid();
        }

        private void HideByteOverlay()
        {
            _overlayParamIdx = -1;
            _byteOverlayDgv.Visible = false;
        }

        private void ByteOverlay_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            TextBox tb = e.Control as TextBox;
            if (tb == null) return;

            tb.MaxLength       = 2;
            tb.CharacterCasing = CharacterCasing.Upper;
            tb.KeyDown  -= OverlayTB_KeyDown;
            tb.KeyPress -= OverlayTB_KeyPress;
            tb.KeyDown  += OverlayTB_KeyDown;
            tb.KeyPress += OverlayTB_KeyPress;

            _overlayNibbleCount = 0;
            _byteOverlayDgv.BeginInvoke(new Action(() =>
            {
                TextBox t = _byteOverlayDgv.EditingControl as TextBox;
                if (t != null) t.SelectAll();
            }));
        }

        private void OverlayTB_KeyDown(object sender, KeyEventArgs e)
        {
            var tb = (TextBox)sender;
            if (_byteOverlayDgv.CurrentCell == null) return;
            int col = _byteOverlayDgv.CurrentCell.ColumnIndex;

            if (e.KeyCode == Keys.Back)
            {
                e.SuppressKeyPress = true;
                tb.Text = "00";
                _overlayNibbleCount = 0;
                int cc = col;
                _byteOverlayDgv.BeginInvoke(new Action(() =>
                {
                    _byteOverlayDgv.EndEdit();
                    int prevCol = cc - 1;
                    if (prevCol >= 0 && _byteOverlayDgv.Rows.Count > 0)
                    {
                        _byteOverlayDgv.CurrentCell = _byteOverlayDgv.Rows[0].Cells[prevCol];
                        _byteOverlayDgv.BeginEdit(true);
                        _overlayNibbleCount = 0;
                    }
                }));
            }
            else if (e.KeyCode == Keys.Delete)
            {
                e.SuppressKeyPress = true;
                tb.Text = "00";
                _overlayNibbleCount = 0;
                _byteOverlayDgv.BeginInvoke(new Action(() => _byteOverlayDgv.EndEdit()));
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                _byteOverlayDgv.BeginInvoke(new Action(() => HideByteOverlay()));
            }
        }

        private void OverlayTB_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\x16') { e.Handled = true; return; } // Ctrl+V → ByteOverlayGrid で処理
            if (char.IsControl(e.KeyChar)) return;

            char c = char.ToUpper(e.KeyChar);
            if (!IsHexChar(c)) { e.Handled = true; return; }

            _overlayNibbleCount++;
            if (_overlayNibbleCount >= 2)
            {
                _overlayNibbleCount = 0;
                int col = _byteOverlayDgv.CurrentCell != null ? _byteOverlayDgv.CurrentCell.ColumnIndex : -1;
                _byteOverlayDgv.BeginInvoke(new Action(() =>
                {
                    _byteOverlayDgv.EndEdit();
                    OverlayMoveToNextCell(col);
                }));
            }
        }

        private void OverlayMoveToNextCell(int col)
        {
            int next = col + 1;
            if (next >= _byteOverlayDgv.Columns.Count) { CommitByteOverlay(); return; }
            if (_byteOverlayDgv.Rows.Count == 0) return;
            _byteOverlayDgv.CurrentCell = _byteOverlayDgv.Rows[0].Cells[next];
            _byteOverlayDgv.BeginEdit(true);
            _overlayNibbleCount = 0;
        }

        private void ByteOverlay_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex != 0 || _overlayParamIdx < 0) return;
            int col = e.ColumnIndex;
            ParameterDef p = SampleData.Parameters[_overlayParamIdx];
            if (col >= p.Size) return;

            object val  = _byteOverlayDgv.Rows[0].Cells[col].Value;
            string text = val != null ? val.ToString().Trim().ToUpper() : "";
            byte b;
            if (!(text.Length == 2 && byte.TryParse(text,
                System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out b)))
            {
                // 無効入力は _data の現在値に戻す
                _byteOverlayDgv.Rows[0].Cells[col].Value = _data[p.Offset + col].ToString("X2");
            }
        }

        private void ByteOverlay_PasteRequest(object sender, string text)
        {
            if (_overlayParamIdx < 0) return;
            ParameterDef p = SampleData.Parameters[_overlayParamIdx];

            string hex = text.Replace(" ", "").Replace("-", "").Trim().ToUpper();
            if (hex.Length < 2)
            {
                MessageBox.Show(
                    "クリップボードのテキストに無効な文字が含まれています。\n16 進数の文字（0-9, A-F）のみ使用できます。",
                    "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // カーソル（パーキング）位置から貼り付け開始
            int startCol   = _byteOverlayDgv.CurrentCell != null ? _byteOverlayDgv.CurrentCell.ColumnIndex : 0;
            int byteCount  = hex.Length / 2;
            int writeCount = Math.Min(byteCount, p.Size - startCol);

            byte[] bytes = new byte[writeCount];
            bool   valid = true;
            for (int i = 0; i < writeCount; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out bytes[i]))
                { valid = false; break; }
            }

            if (!valid)
            {
                MessageBox.Show(
                    "クリップボードのテキストに無効な文字が含まれています。\n16 進数の文字（0-9, A-F）のみ使用できます。",
                    "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _byteOverlayDgv.EndEdit();
            for (int i = 0; i < writeCount; i++)
                _byteOverlayDgv.Rows[0].Cells[startCol + i].Value = bytes[i].ToString("X2");
            // writeCount 分だけ上書き。残りのセルは既存値のまま CommitByteOverlay に渡す
            CommitByteOverlay();
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
            HideByteOverlay();
            PopulateHexGrid();
            PopulateParamGrid();
        }
    }
}
