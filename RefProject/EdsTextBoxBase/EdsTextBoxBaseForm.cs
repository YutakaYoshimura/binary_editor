using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace EdsTextBoxBase
{
    /// <summary>
    /// パターン[3] TextBox インライン編集
    ///
    /// Tab①「バイナリ全体表示」
    ///   パターン[2] と同一: DataGridView で EDS バイナリ全データを Hex 表示・編集。
    ///
    /// Tab②「パラメータグリッド」
    ///   バイト列セルをクリックするとそのまま TextBox で直接編集できる。
    ///   ・BackSpace: カーソルのバイトを 00 にして 1 バイト前に移動（パターン[1] 準拠）
    ///   ・Delete:    カーソルのバイトを 00 にしてカーソル移動なし
    ///   ・Ctrl+V:    "1234" → 0x12, 0x34 としてカーソル位置から貼り付け
    ///   ・最終バイトの下位ニブル入力後は即時確定
    /// </summary>
    public class EdsTextBoxBaseForm : Form
    {
        // ─── フィールド ────────────────────────────────────────────
        private byte[]               _data = SampleData.Create();
        private DataGridView         _hexGrid;    // Tab①
        private DataGridView         _paramGrid;  // Tab②
        private TabControl           _tabs;
        private ToolStripStatusLabel _statusLabel;

        private int _hexGridNibbleCount = 0;

        // Tab② 列インデックス
        private const int COL_NAME     = 0;
        private const int COL_OFFSET   = 1;
        private const int COL_SIZEINFO = 2;
        private const int COL_RAWBYTES = 3;

        // ─── エントリポイント ──────────────────────────────────────
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new EdsTextBoxBaseForm());
        }

        // ─── コンストラクタ ────────────────────────────────────────
        public EdsTextBoxBaseForm()
        {
            Text          = "パターン[3] TextBox インライン編集 ― ベース画面";
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
            _tabs.Selecting += (s, e) =>
            {
                // Tab② → Tab①: パラメータ編集中であれば確定してからグリッドを更新
                if (_tabs.SelectedIndex == 1 && e.TabPageIndex != 1)
                    if (_paramGrid != null && _paramGrid.IsCurrentCellInEditMode)
                        _paramGrid.EndEdit();
                // Tab① へ移動: _data の最新値でグリッドを更新
                if (e.TabPageIndex == 0)
                    PopulateHexGrid();
                // Tab② へ移動: _data の最新値でグリッドを更新
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

        // ════════════════════════════════════════════════════════════════
        //  Tab①: バイナリ全体表示（パターン[2] と同一）
        // ════════════════════════════════════════════════════════════════

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
                Text = "【パターン[3]】 DataGridView で EDS バイナリ全データを Hex ビュー表示します。\n" +
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

            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name       = "Offset",
                HeaderText = "Offset",
                Width      = 82,
                Resizable  = DataGridViewTriState.False,
                ReadOnly   = true,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(80, 100, 180),
                    Font      = new Font("Consolas", 9.5f),
                    BackColor = Color.FromArgb(240, 244, 255),
                }
            });

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
            _hexGrid.Rows[row].Cells[col].Value = _data[byteIdx].ToString("X2");
            UpdateHexGridAsciiCell(row);
        }

        private void HexGrid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            TextBox tb = e.Control as TextBox;
            if (tb == null) return;

            tb.MaxLength       = 2;
            tb.CharacterCasing = CharacterCasing.Upper;
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
            if (e.KeyChar == '\x16') // Ctrl+V
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

        private void HexGrid_DGV_KeyDown(object sender, KeyEventArgs e)
        {
            if (_hexGrid.CurrentCell == null) return;

            int col = _hexGrid.CurrentCell.ColumnIndex;
            int row = _hexGrid.CurrentCell.RowIndex;

            if (e.KeyCode == Keys.V && e.Control)
            {
                if (_hexGrid.IsCurrentCellInEditMode) return;
                e.Handled          = true;
                e.SuppressKeyPress = true;
                if (col >= 1 && col <= 16 && row >= 0) PasteToHexGrid(row, col);
                return;
            }

            if (e.KeyCode != Keys.Back && e.KeyCode != Keys.Delete) return;
            if (_hexGrid.IsCurrentCellInEditMode) return;
            if (col < 1 || col > 16) return;
            int byteIdx = row * 16 + (col - 1);
            if (byteIdx >= _data.Length) return;

            e.Handled          = true;
            e.SuppressKeyPress = true;

            _data[byteIdx] = 0x00;
            _hexGrid.Rows[row].Cells[col].Value = "00";
            UpdateHexGridAsciiCell(row);

            if (e.KeyCode == Keys.Back)
                MoveToPrevByteCell(row, col);
        }

        // ════════════════════════════════════════════════════════════════
        //  Tab②: パラメータグリッド（TextBox インライン編集）
        //
        //  バイト列セルをクリックするとそのまま TextBox で直接編集できる。
        //  スペースなし 16 進数形式（"1234ABCD"）でニブル単位の上書き編集。
        //  BackSpace/Delete/Ctrl+V の挙動はパターン[1] の HexBox 相当。
        // ════════════════════════════════════════════════════════════════

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
                Text = "【パターン[3]】 EDS パラメータ一覧を DataGridView で表示します。\n" +
                       "「バイト列」列をクリックするとセルをそのまま Hex 入力で編集できます（パターン[1] と同等の操作感）。",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(30, 80, 40),
                Font      = new Font("Meiryo UI", 8.5f),
            });

            _paramGrid = BuildParamDataGridView();
            PopulateParamGrid();

            tab.Controls.Add(_paramGrid);
            tab.Controls.Add(toolbar);
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
                ReadOnly                  = false,
                EditMode                  = DataGridViewEditMode.EditOnKeystrokeOrF2,
            };
            gv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 85, 145);
            gv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gv.ColumnHeadersDefaultCellStyle.Font      = new Font("Meiryo UI", 9f, FontStyle.Bold);
            gv.ColumnHeadersHeight = 28;

            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Name", HeaderText = "パラメータ名", FillWeight = 18, ReadOnly = true,
            });
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name     = "Offset",
                HeaderText = "オフセット",
                FillWeight = 10,
                ReadOnly   = true,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter },
            });
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "SizeInfo", HeaderText = "サイズ / データ型", FillWeight = 16, ReadOnly = true,
            });
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name       = "RawBytes",
                HeaderText = "バイト列 ✎",
                FillWeight = 56,
                ReadOnly   = false,
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

            gv.CellClick             += ParamGrid_CellClick;
            gv.CellBeginEdit         += ParamGrid_CellBeginEdit;
            gv.CellEndEdit           += ParamGrid_CellEndEdit;
            gv.EditingControlShowing += ParamGrid_EditingControlShowing;
            gv.KeyDown               += ParamGrid_DGV_KeyDown;

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

        // ── パラメータグリッド編集イベント ────────────────────────────

        /// <summary>バイト列セルをクリックしたら即座に編集モードに入る。</summary>
        private void ParamGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != COL_RAWBYTES || e.RowIndex < 0) return;
            if (e.RowIndex >= SampleData.Parameters.Length) return;
            if (SampleData.Parameters[e.RowIndex].IsReadOnly) return;
            if (!_paramGrid.IsCurrentCellInEditMode)
                _paramGrid.BeginEdit(true);
        }

        /// <summary>
        /// バイト列列以外および読み取り専用パラメータの編集開始をキャンセルする。
        /// </summary>
        private void ParamGrid_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            if (e.ColumnIndex != COL_RAWBYTES) { e.Cancel = true; return; }
            if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) { e.Cancel = true; return; }
            if (SampleData.Parameters[e.RowIndex].IsReadOnly) { e.Cancel = true; return; }
        }

        /// <summary>
        /// 編集終了時: TextBox のテキスト（スペースなし 16 進数形式）を解析して _data に書き込む。
        /// </summary>
        private void ParamGrid_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex != COL_RAWBYTES) return;
            if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) return;
            ParameterDef p = SampleData.Parameters[e.RowIndex];

            object val  = _paramGrid.Rows[e.RowIndex].Cells[COL_RAWBYTES].Value;
            string text = val != null ? val.ToString().Replace(" ", "").ToUpper() : "";

            for (int i = 0; i < p.Size; i++)
            {
                if (i * 2 + 2 > text.Length) break;
                byte b;
                if (byte.TryParse(text.Substring(i * 2, 2),
                    System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out b))
                    _data[p.Offset + i] = b;
            }

            // 表示をスペース区切り形式（"12 34 AB CD"）に更新
            _paramGrid.Rows[e.RowIndex].Cells[COL_RAWBYTES].Value = p.ReadRawBytes(_data);
            _statusLabel.Text = string.Format("{0} を更新しました", p.Name);
        }

        /// <summary>
        /// 編集コントロール（TextBox）表示時:
        /// ・スペースを除去してコンパクト形式（"1234ABCD"）にする
        /// ・MaxLength / CharacterCasing を設定
        /// ・BackSpace / Delete / Hex 入力ハンドラを接続する
        /// </summary>
        private void ParamGrid_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            if (_paramGrid.CurrentCell == null || _paramGrid.CurrentCell.ColumnIndex != COL_RAWBYTES) return;
            TextBox tb = e.Control as TextBox;
            if (tb == null) return;

            int paramIdx = _paramGrid.CurrentCell.RowIndex;
            if (paramIdx < 0 || paramIdx >= SampleData.Parameters.Length) return;
            ParameterDef p = SampleData.Parameters[paramIdx];

            tb.MaxLength       = p.Size * 2;
            tb.CharacterCasing = CharacterCasing.Upper;

            tb.KeyDown  -= ParamGridTB_KeyDown;
            tb.KeyPress -= ParamGridTB_KeyPress;
            tb.KeyDown  += ParamGridTB_KeyDown;
            tb.KeyPress += ParamGridTB_KeyPress;

            // BeginInvoke でセル値が確定してからスペースを除去する
            _paramGrid.BeginInvoke(new Action(() =>
            {
                TextBox t = _paramGrid.EditingControl as TextBox;
                if (t == null) return;

                string stripped = t.Text.Replace(" ", "").ToUpper();
                if (stripped.Length < p.Size * 2)
                    stripped = stripped.PadRight(p.Size * 2, '0');
                else if (stripped.Length > p.Size * 2)
                    stripped = stripped.Substring(0, p.Size * 2);

                t.Text            = stripped;
                t.SelectionStart  = 0;
                t.SelectionLength = 0;
            }));
        }

        /// <summary>
        /// BackSpace: 現バイトを 00 にして 1 バイト前の上位ニブルへ移動。
        /// Delete:    現バイトを 00 にしてカーソル位置を現バイトの上位ニブルに留める。
        /// Enter:     確定（EndEdit）。
        /// </summary>
        private void ParamGridTB_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (_paramGrid.CurrentCell == null) return;
            int paramIdx = _paramGrid.CurrentCell.RowIndex;
            if (paramIdx < 0 || paramIdx >= SampleData.Parameters.Length) return;
            ParameterDef p = SampleData.Parameters[paramIdx];

            if (e.KeyCode == Keys.Back)
            {
                e.SuppressKeyPress = true;
                int pos     = tb.SelectionStart;
                int byteIdx = Math.Min(pos / 2, p.Size - 1);

                char[] chars = tb.Text.ToCharArray();
                if (chars.Length == p.Size * 2)
                {
                    chars[byteIdx * 2]     = '0';
                    chars[byteIdx * 2 + 1] = '0';
                    tb.Text = new string(chars);
                }
                tb.SelectionStart  = byteIdx > 0 ? (byteIdx - 1) * 2 : 0;
                tb.SelectionLength = 0;
            }
            else if (e.KeyCode == Keys.Delete)
            {
                e.SuppressKeyPress = true;
                int pos     = tb.SelectionStart;
                int byteIdx = Math.Min(pos / 2, p.Size - 1);

                char[] chars = tb.Text.ToCharArray();
                if (chars.Length == p.Size * 2)
                {
                    chars[byteIdx * 2]     = '0';
                    chars[byteIdx * 2 + 1] = '0';
                    tb.Text = new string(chars);
                }
                tb.SelectionStart  = byteIdx * 2;
                tb.SelectionLength = 0;
            }
            else if (e.KeyCode == Keys.Return)
            {
                e.SuppressKeyPress = true;
                _paramGrid.BeginInvoke(new Action(() => _paramGrid.EndEdit()));
            }
        }

        /// <summary>
        /// Ctrl+V: カーソル位置のバイトからペースト。
        /// Hex 文字: 上書きモードでニブルを書き換え、最終ニブルで即時確定。
        /// </summary>
        private void ParamGridTB_KeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox tb = (TextBox)sender;

            if (e.KeyChar == '\x16') // Ctrl+V
            {
                e.Handled = true;
                if (_paramGrid.CurrentCell != null)
                {
                    int paramIdx     = _paramGrid.CurrentCell.RowIndex;
                    int startByteIdx = tb.SelectionStart / 2;
                    PasteToParamGrid(paramIdx, startByteIdx, tb);
                }
                return;
            }

            if (char.IsControl(e.KeyChar)) return;

            char c = char.ToUpper(e.KeyChar);
            if (!IsHexChar(c)) { e.Handled = true; return; }

            e.Handled = true;

            if (_paramGrid.CurrentCell == null) return;
            int pIdx = _paramGrid.CurrentCell.RowIndex;
            if (pIdx < 0 || pIdx >= SampleData.Parameters.Length) return;
            ParameterDef param = SampleData.Parameters[pIdx];

            int curPos = tb.SelectionStart;
            if (curPos >= tb.Text.Length || tb.Text.Length != param.Size * 2) return;

            // 上書きモード: 現ニブルを書き換えてカーソルを 1 つ進める
            char[] chars = tb.Text.ToCharArray();
            chars[curPos] = c;
            tb.Text = new string(chars);

            int nextPos = curPos + 1;
            if (nextPos >= param.Size * 2)
            {
                // 最終バイトの下位ニブル → 即時確定
                _paramGrid.BeginInvoke(new Action(() => _paramGrid.EndEdit()));
            }
            else
            {
                tb.SelectionStart  = nextPos;
                tb.SelectionLength = 0;
            }
        }

        /// <summary>
        /// Ctrl+V: クリップボードの Hex 文字列をカーソル位置のバイトから上書きペースト。
        /// 末尾まで書き込んだ場合は即時確定する。
        /// </summary>
        private void PasteToParamGrid(int paramIdx, int startByteIdx, TextBox tb)
        {
            if (paramIdx < 0 || paramIdx >= SampleData.Parameters.Length) return;
            ParameterDef p = SampleData.Parameters[paramIdx];
            if (tb.Text.Length != p.Size * 2) return; // BeginInvoke 未完了の場合は無視

            string clipText = Clipboard.GetText();
            string hex      = clipText.Replace(" ", "").Replace("-", "").Trim().ToUpper();

            if (hex.Length < 2)
            {
                MessageBox.Show(
                    "クリップボードのテキストに無効な文字が含まれています。\n16 進数の文字（0-9, A-F）のみ使用できます。",
                    "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int byteCount  = hex.Length / 2;
            int writeCount = Math.Min(byteCount, p.Size - startByteIdx);

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

            // TextBox のテキストに直接書き込む
            char[] chars = tb.Text.ToCharArray();
            for (int i = 0; i < writeCount; i++)
            {
                string bh = bytes[i].ToString("X2");
                chars[(startByteIdx + i) * 2]     = bh[0];
                chars[(startByteIdx + i) * 2 + 1] = bh[1];
            }
            tb.Text = new string(chars);

            int nextPos = (startByteIdx + writeCount) * 2;
            if (nextPos >= p.Size * 2)
            {
                // 末尾まで書き込んだ → 即時確定
                _paramGrid.BeginInvoke(new Action(() => _paramGrid.EndEdit()));
            }
            else
            {
                tb.SelectionStart  = nextPos;
                tb.SelectionLength = 0;
            }
        }

        /// <summary>
        /// DGV が直接フォーカスを持つとき（非編集状態）の Ctrl+V を抑止する。
        /// （DGV 既定の貼り付けをキャンセル。編集中は ParamGridTB_KeyPress が処理する。）
        /// </summary>
        private void ParamGrid_DGV_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.V && e.Control)
            {
                e.Handled          = true;
                e.SuppressKeyPress = true;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  共通ユーティリティ
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static bool IsHexChar(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
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
            if (_paramGrid != null && _paramGrid.IsCurrentCellInEditMode)
                _paramGrid.CancelEdit();
            PopulateHexGrid();
            PopulateParamGrid();
        }
    }
}
