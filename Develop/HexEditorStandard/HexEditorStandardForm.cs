using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace HexEditorStandard
{
    /// <summary>
    /// 標準コントロールのみで実装した Hex エディタ検証フォーム（3 タブ構成）
    ///
    /// Tab① バイナリ全体表示  … DataGridView (Hex グリッド) で直接編集
    ///                          下位ニブル入力後に次バイトセルへ自動移動
    /// Tab② パラメータグリッド … DataGridView。バイト列セルをクリックすると
    ///                          その場にバイト単位編集の DataGridView をオーバーレイ表示
    /// Tab③ パラメータグリッド … DataGridView + TextBox Hex 入力（既存 Tab③ を保持）
    /// </summary>
    public class HexEditorStandardForm : Form
    {
        // ─── フィールド ───────────────────────────────────────────
        private byte[]               _data = SampleData.Create();
        private DataGridView         _hexViewGrid;          // Tab①: DataGridView Hex ビュー
        private DataGridView         _grid;                 // Tab②: パラメータグリッド
        private DataGridView         _hexTextGrid;          // Tab③: HexText 列
        private DataGridView         _byteOverlayDgv;       // Tab② オーバーレイ
        private int                  _overlayParamIdx = -1; // オーバーレイ対象パラメータ
        private int                  _hexViewNibbles  = 0;  // Tab① ニブルカウンタ
        private int                  _overlayNibbles  = 0;  // オーバーレイ ニブルカウンタ
        private byte                 _hexViewOldByte;       // Tab① Undo 用

        private ToolStripStatusLabel _statusLabel;
        private bool                 _suppressGridEvents;
        private TabControl           _tabs;
        private UndoManager          _undoMgr = new UndoManager();
        private ToolStripMenuItem    _undoMenuItem;
        private ToolStripMenuItem    _redoMenuItem;

        // Tab② 列インデックス
        private const int COL_NAME     = 0;
        private const int COL_OFFSET   = 1;
        private const int COL_SIZE     = 2;
        private const int COL_TYPE     = 3;
        private const int COL_RAWBYTES = 4;
        private const int COL_VALUE    = 5;

        // Tab③ 列インデックス
        private const int COL_HEX_NAME  = 0;
        private const int COL_HEX_OFF   = 1;
        private const int COL_HEX_INFO  = 2;
        private const int COL_HEX_BYTES = 3;

        // ─── エントリポイント ─────────────────────────────────────
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HexEditorStandardForm());
        }

        // ─── コンストラクタ ───────────────────────────────────────
        public HexEditorStandardForm()
        {
            Text          = "標準コントロール版 Hex エディタ検証 ― 3 画面比較";
            Size          = new Size(1100, 700);
            MinimumSize   = new Size(800, 500);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUI();
            PopulateHexViewGrid();
            RefreshGrid();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  UI 構築
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void BuildUI()
        {
            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.Selecting += OnTabSelecting;
            _tabs.TabPages.Add(BuildHexTab());
            _tabs.TabPages.Add(BuildGridTab());
            _tabs.TabPages.Add(BuildHexTextGridTab());
            Controls.Add(_tabs);

            var menu     = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("ファイル(&F)");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("開く(&O)...",             null, (s, e) => OpenFile()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("保存(&S)...",             null, (s, e) => SaveFile()));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("サンプルデータに戻す(&R)", null, (s, e) => ResetToSample()));
            menu.Items.Add(fileMenu);

            var editMenu = new ToolStripMenuItem("編集(&E)");
            _undoMenuItem = new ToolStripMenuItem("元に戻す(&Z)", null, (s, e) => PerformUndo());
            _redoMenuItem = new ToolStripMenuItem("やり直す(&Y)", null, (s, e) => PerformRedo());
            _undoMenuItem.ShortcutKeys = Keys.Control | Keys.Z;
            _redoMenuItem.ShortcutKeys = Keys.Control | Keys.Y;
            _undoMenuItem.Enabled      = false;
            _redoMenuItem.Enabled      = false;
            editMenu.DropDownItems.Add(_undoMenuItem);
            editMenu.DropDownItems.Add(_redoMenuItem);
            menu.Items.Add(editMenu);

            MainMenuStrip = menu;
            Controls.Add(menu);

            var strip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("サンプルデータを表示中")
            {
                Spring    = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Meiryo UI", 8.5f)
            };
            strip.Items.Add(_statusLabel);
            Controls.Add(strip);
        }

        // ────────────────────────────────────────────────────────
        //  Tab①: バイナリ全体表示 (DataGridView Hex ビュー)
        // ────────────────────────────────────────────────────────

        private TabPage BuildHexTab()
        {
            var tab = new TabPage("① バイナリ全体表示");

            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                BackColor = Color.FromArgb(225, 235, 255),
                Padding   = new Padding(8, 6, 8, 0)
            };
            var btn = MakeButton("Tab②③ に同期", SyncAndRefreshAll);
            btn.Location = new Point(8, 6);
            toolbar.Controls.Add(btn);
            toolbar.Controls.Add(new Label
            {
                Text      = "※ Hex グリッドで直接バイトを編集できます。下位ニブル入力後、自動で次のバイトへ移動します。",
                AutoSize  = true,
                Location  = new Point(btn.Width + 16, 10),
                ForeColor = Color.DimGray,
                Font      = new Font("Meiryo UI", 8.5f)
            });

            _hexViewGrid = BuildHexViewDataGridView();

            tab.Controls.Add(_hexViewGrid);
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
                EditMode                    = DataGridViewEditMode.EditOnKeystroke,
                MultiSelect                 = false,
            };
            gv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 85, 145);
            gv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gv.ColumnHeadersDefaultCellStyle.Font      = new Font("Consolas", 9f, FontStyle.Bold);
            gv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            gv.ColumnHeadersHeight = 24;

            // Offset 列 (読み取り専用)
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name     = "Offset", HeaderText = "Offset",
                Width    = 82, ReadOnly = true,
                Resizable = DataGridViewTriState.False,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(80, 100, 180),
                    Font      = new Font("Consolas", 9.5f),
                }
            });

            // バイト列 × 16 (00〜0F)
            for (int i = 0; i < 16; i++)
            {
                gv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name      = "B" + i.ToString("X2"),
                    HeaderText = i.ToString("X2"),
                    Width     = 28,
                    Resizable = DataGridViewTriState.False,
                    DefaultCellStyle =
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                    }
                });
            }

            // ASCII テキスト列 (読み取り専用)
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name         = "Text",
                HeaderText   = "Text",
                ReadOnly     = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                Resizable    = DataGridViewTriState.True,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.DimGray,
                }
            });

            // 偶数/奇数行の背景色
            gv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                e.CellStyle.BackColor = (e.RowIndex % 2 == 0)
                    ? Color.White
                    : Color.FromArgb(246, 249, 255);
            };

            // バイト列以外は編集禁止
            gv.CellBeginEdit += (s, e) =>
            {
                if (e.ColumnIndex < 1 || e.ColumnIndex > 16) { e.Cancel = true; return; }
                int byteIdx = e.RowIndex * 16 + (e.ColumnIndex - 1);
                if (byteIdx >= _data.Length) { e.Cancel = true; return; }

                _hexViewNibbles = 0;
                _hexViewOldByte = _data[byteIdx];
            };

            // 編集確定 → _data に書き戻し + Undo 記録
            gv.CellEndEdit += (s, e) =>
            {
                if (e.ColumnIndex < 1 || e.ColumnIndex > 16) return;
                int byteIdx = e.RowIndex * 16 + (e.ColumnIndex - 1);
                if (byteIdx >= _data.Length) return;

                var    cell = gv.Rows[e.RowIndex].Cells[e.ColumnIndex];
                string val  = cell.Value?.ToString() ?? "";

                if (byte.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    byte oldByte = _hexViewOldByte;
                    _data[byteIdx] = b;
                    cell.Value     = b.ToString("X2");

                    UpdateHexViewAsciiCell(gv, e.RowIndex);

                    if (b != oldByte)
                    {
                        _undoMgr.Push(new ByteRangeCommand(
                            byteIdx, new byte[] { oldByte }, new byte[] { b },
                            string.Format("オフセット 0x{0:X4} を {1:X2} → {2:X2} に変更",
                                byteIdx, oldByte, b)));
                        UpdateUndoRedoMenu();
                        _statusLabel.Text = string.Format(
                            "0x{0:X4}: {1:X2} → {2:X2}", byteIdx, oldByte, b);
                    }
                }
                else
                {
                    cell.Value = _data[byteIdx].ToString("X2");
                }

                _hexViewNibbles = 0;
            };

            // 編集コントロール表示時: 入力制限 + BackSpace/Delete + ニブル自動進行 + Paste
            gv.EditingControlShowing += (s, e) =>
            {
                int col = gv.CurrentCell?.ColumnIndex ?? -1;
                if (col < 1 || col > 16) return;

                var tb = e.Control as TextBox;
                if (tb == null) return;

                tb.MaxLength        = 2;
                tb.CharacterCasing  = CharacterCasing.Upper;

                tb.KeyDown  -= HexView_KeyDown;
                tb.KeyDown  += HexView_KeyDown;
                tb.KeyPress -= HexView_KeyPress;
                tb.KeyPress += HexView_KeyPress;
            };

            return gv;
        }

        private void PopulateHexViewGrid()
        {
            _hexViewGrid.Rows.Clear();
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
                _hexViewGrid.Rows.Add(cells);
            }
        }

        private void UpdateHexViewAsciiCell(DataGridView gv, int rowIndex)
        {
            int lineBase = rowIndex * 16;
            var sb = new StringBuilder();
            for (int b = 0; b < 16; b++)
            {
                int idx = lineBase + b;
                if (idx >= _data.Length) break;
                byte bv = _data[idx];
                sb.Append((bv >= 0x20 && bv < 0x7F) ? (char)bv : '.');
            }
            if (rowIndex < gv.Rows.Count)
                gv.Rows[rowIndex].Cells[17].Value = sb.ToString();
        }

        // ── Tab① キー操作ハンドラ ───────────────────────────────

        private void HexView_KeyDown(object sender, KeyEventArgs e)
        {
            int col = _hexViewGrid?.CurrentCell?.ColumnIndex ?? -1;
            int row = _hexViewGrid?.CurrentCell?.RowIndex   ?? -1;
            if (col < 1 || col > 16 || row < 0) return;
            int byteIdx = row * 16 + (col - 1);

            if (e.KeyCode == Keys.Back)
            {
                if (byteIdx < _data.Length)
                    _data[byteIdx] = 0x00;
                ((TextBox)sender).Text = "00";
                _hexViewNibbles = 0;

                e.Handled = true;
                e.SuppressKeyPress = true;

                // コミットしてから前のセルへ
                _hexViewGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                _hexViewGrid.EndEdit();

                int prevCol = col - 1;
                int prevRow = row;
                if (prevCol < 1) { prevCol = 16; prevRow--; }
                if (prevRow >= 0)
                {
                    int prevIdx = prevRow * 16 + (prevCol - 1);
                    if (prevIdx >= 0 && prevIdx < _data.Length)
                    {
                        _hexViewGrid.CurrentCell = _hexViewGrid.Rows[prevRow].Cells[prevCol];
                        _hexViewGrid.BeginEdit(true);
                    }
                }
            }
            else if (e.KeyCode == Keys.Delete)
            {
                if (byteIdx < _data.Length)
                    _data[byteIdx] = 0x00;
                ((TextBox)sender).Text = "00";
                _hexViewNibbles = 0;
                UpdateHexViewAsciiCell(_hexViewGrid, row);
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void HexView_KeyPress(object sender, KeyPressEventArgs e)
        {
            // BackSpace は KeyDown で処理済み
            if (e.KeyChar == '\b') { e.Handled = true; return; }

            // Ctrl+V
            if (e.KeyChar == '\u0016') { HexViewPaste(); e.Handled = true; return; }

            char c = char.ToUpper(e.KeyChar);
            bool isHex = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
            if (!isHex) { e.Handled = true; return; }

            _hexViewNibbles++;
            if (_hexViewNibbles >= 2)
            {
                _hexViewNibbles = 0;
                _hexViewGrid.BeginInvoke(new Action(AdvanceHexViewCell));
            }
        }

        private void AdvanceHexViewCell()
        {
            if (_hexViewGrid?.CurrentCell == null) return;
            int col = _hexViewGrid.CurrentCell.ColumnIndex;
            int row = _hexViewGrid.CurrentCell.RowIndex;

            _hexViewGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _hexViewGrid.EndEdit();

            int nextCol = col + 1;
            int nextRow = row;
            if (nextCol > 16) { nextCol = 1; nextRow++; }

            if (nextRow < _hexViewGrid.Rows.Count)
            {
                int nextByteIdx = nextRow * 16 + (nextCol - 1);
                if (nextByteIdx < _data.Length)
                {
                    _hexViewGrid.CurrentCell = _hexViewGrid.Rows[nextRow].Cells[nextCol];
                    _hexViewGrid.BeginEdit(true);
                    _hexViewNibbles = 0;
                }
            }
        }

        /// <summary>
        /// Tab① Hex ビューへの Ctrl+V 貼り付け。
        /// "1234" → 現在セルから 0x12, 0x34 を順に書き込む。
        /// バイト数不一致または無効な文字の場合は警告を表示する。
        /// </summary>
        private void HexViewPaste()
        {
            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;

            string hex = text.Replace(" ", "").Replace("-", "").Trim().ToUpper();
            if (hex.Length == 0 || hex.Length % 2 != 0)
            {
                MessageBox.Show(
                    "クリップボードのテキストが無効です。\n16 進数の偶数文字（例: 1234 または 12 34）を指定してください。",
                    "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 全文字が Hex 文字かチェック
            foreach (char c in hex)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'A' && c <= 'F')))
                {
                    MessageBox.Show(
                        "クリップボードのテキストに無効な文字が含まれています。\n16 進数の文字（0-9, A-F）のみ使用できます。",
                        "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            int col = _hexViewGrid?.CurrentCell?.ColumnIndex ?? -1;
            int row = _hexViewGrid?.CurrentCell?.RowIndex   ?? -1;
            if (col < 1 || col > 16 || row < 0) return;

            int numBytes = hex.Length / 2;
            for (int i = 0; i < numBytes; i++)
            {
                int tCol = col + i;
                int tRow = row;
                while (tCol > 16) { tCol -= 16; tRow++; }
                if (tRow >= _hexViewGrid.Rows.Count) break;
                int byteIdx = tRow * 16 + (tCol - 1);
                if (byteIdx >= _data.Length) break;

                if (byte.TryParse(hex.Substring(i * 2, 2),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                {
                    _data[byteIdx] = b;
                    _hexViewGrid.Rows[tRow].Cells[tCol].Value = b.ToString("X2");
                }
            }

            // 影響を受けた行の ASCII 列を更新
            for (int r = row; r <= row + numBytes / 16 + 1 && r < _hexViewGrid.Rows.Count; r++)
                UpdateHexViewAsciiCell(_hexViewGrid, r);
        }

        // ────────────────────────────────────────────────────────
        //  Tab②: パラメータグリッド + DataGridView バイトオーバーレイ
        // ────────────────────────────────────────────────────────

        private TabPage BuildGridTab()
        {
            var tab = new TabPage("② パラメータグリッド (バイト編集)");

            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                BackColor = Color.FromArgb(225, 255, 225),
                Padding   = new Padding(8, 6, 8, 0)
            };
            var btn = MakeButton("グリッドの編集内容 → Tab①③ へ反映", () => { PopulateHexViewGrid(); BuildHexTextGridRows(); });
            btn.Location = new Point(8, 6);
            toolbar.Controls.Add(btn);
            toolbar.Controls.Add(new Label
            {
                Text      = "※「バイト列」セルをクリックするとバイト単位の DataGridView エディタが表示されます",
                AutoSize  = true,
                Location  = new Point(btn.Width + 16, 10),
                ForeColor = Color.DimGray,
                Font      = new Font("Meiryo UI", 8.5f)
            });

            _grid = BuildTextGrid();

            // バイト列セルをクリック → オーバーレイ表示
            _grid.CellClick += (s, e) =>
            {
                if (e.ColumnIndex == COL_RAWBYTES && e.RowIndex >= 0 &&
                    e.RowIndex < SampleData.Parameters.Length &&
                    !SampleData.Parameters[e.RowIndex].IsReadOnly)
                {
                    ShowByteOverlay(e.RowIndex);
                }
                else
                {
                    HideByteOverlay();
                }
            };

            // オーバーレイを組み立てる
            _byteOverlayDgv = BuildByteOverlayDgv();

            tab.Controls.Add(_grid);
            tab.Controls.Add(toolbar);
            tab.Controls.Add(_byteOverlayDgv); // 最後に追加 → 最前面

            return tab;
        }

        private DataGridView BuildTextGrid()
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
                RowTemplate               = { Height = 24 },
                EnableHeadersVisualStyles = false,
            };
            gv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(210, 228, 255);
            gv.ColumnHeadersDefaultCellStyle.Font      = new Font("Meiryo UI", 9f, FontStyle.Bold);
            gv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(30, 60, 100);
            gv.ColumnHeadersHeight                     = 26;

            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",     HeaderText = "パラメータ名",   ReadOnly = true,  FillWeight = 16 });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Offset",   HeaderText = "オフセット",     ReadOnly = true,  FillWeight = 10 });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size",     HeaderText = "サイズ",        ReadOnly = true,  FillWeight = 7  });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type",     HeaderText = "データ型",      ReadOnly = true,  FillWeight = 14 });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "RawBytes", HeaderText = "バイト列 ✎",    ReadOnly = true,  FillWeight = 20 });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value",    HeaderText = "値  （編集可）", ReadOnly = false, FillWeight = 33 });

            gv.Columns["Offset"].DefaultCellStyle.Alignment   = DataGridViewContentAlignment.MiddleCenter;
            gv.Columns["Size"].DefaultCellStyle.Alignment     = DataGridViewContentAlignment.MiddleCenter;
            gv.Columns["RawBytes"].DefaultCellStyle.Font      = new Font("Consolas", 9f);
            gv.Columns["RawBytes"].DefaultCellStyle.ForeColor = Color.DarkSlateGray;
            // バイト列列は選択・クリック可能であることを示す背景色
            gv.Columns["RawBytes"].DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 225);

            gv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) return;
                ParameterDef p = SampleData.Parameters[e.RowIndex];
                if (p.IsReadOnly)
                { e.CellStyle.BackColor = Color.FromArgb(245, 245, 245); e.CellStyle.ForeColor = Color.Gray; }
                else if (e.ColumnIndex == COL_VALUE)
                { e.CellStyle.BackColor = Color.FromArgb(255, 255, 210); }
            };

            gv.CellBeginEdit += (s, e) =>
            {
                if (e.ColumnIndex != COL_VALUE) { e.Cancel = true; return; }
                if (e.RowIndex < SampleData.Parameters.Length && SampleData.Parameters[e.RowIndex].IsReadOnly)
                    e.Cancel = true;
            };

            gv.CellEndEdit += (s, e) =>
            {
                if (_suppressGridEvents || e.ColumnIndex != COL_VALUE) return;
                if (e.RowIndex >= SampleData.Parameters.Length) return;

                ParameterDef     param = SampleData.Parameters[e.RowIndex];
                DataGridViewCell cell  = gv.Rows[e.RowIndex].Cells[COL_VALUE];
                string           val   = cell.Value != null ? cell.Value.ToString() : string.Empty;

                byte[] oldBytes = new byte[param.Size];
                Array.Copy(_data, param.Offset, oldBytes, 0, param.Size);

                if (!param.WriteValue(_data, val))
                {
                    MessageBox.Show(
                        string.Format("'{0}' を {1} 型に変換できませんでした。", val, param.TypeLabel),
                        "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cell.Value = param.ReadValue(_data);
                }
                else
                {
                    byte[] newBytes = new byte[param.Size];
                    Array.Copy(_data, param.Offset, newBytes, 0, param.Size);
                    _undoMgr.Push(new ByteRangeCommand(param.Offset, oldBytes, newBytes,
                        string.Format("{0} を編集", param.Name)));
                    UpdateUndoRedoMenu();
                    gv.Rows[e.RowIndex].Cells[COL_RAWBYTES].Value = param.ReadRawBytes(_data);
                    _statusLabel.Text = string.Format("{0} を更新しました → {1}", param.Name, param.ReadRawBytes(_data));
                }
            };

            return gv;
        }

        // ── バイトオーバーレイ DataGridView ──────────────────────

        private DataGridView BuildByteOverlayDgv()
        {
            var gv = new DataGridView
            {
                Visible               = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible     = false,
                ColumnHeadersVisible  = false,
                ScrollBars            = ScrollBars.None,
                BorderStyle           = BorderStyle.FixedSingle,
                SelectionMode         = DataGridViewSelectionMode.CellSelect,
                BackgroundColor       = Color.FromArgb(255, 255, 225),
                GridColor             = Color.LightSteelBlue,
                Font                  = new Font("Consolas", 10f),
                MultiSelect           = false,
                EditMode              = DataGridViewEditMode.EditOnEnter,
            };

            gv.Leave += (s, e) => CommitByteOverlay();

            gv.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Escape)
                {
                    HideByteOverlay();
                    e.Handled = true;
                }
            };

            gv.EditingControlShowing += (s, e) =>
            {
                var tb = e.Control as TextBox;
                if (tb == null) return;

                _overlayNibbles    = 0;
                tb.MaxLength       = 2;
                tb.CharacterCasing = CharacterCasing.Upper;

                tb.KeyDown  -= Overlay_TextBoxKeyDown;
                tb.KeyDown  += Overlay_TextBoxKeyDown;
                tb.KeyPress -= Overlay_TextBoxKeyPress;
                tb.KeyPress += Overlay_TextBoxKeyPress;
            };

            return gv;
        }

        private void ShowByteOverlay(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= SampleData.Parameters.Length) return;
            ParameterDef p = SampleData.Parameters[rowIndex];
            if (p.IsReadOnly) return;

            _overlayParamIdx = rowIndex;
            _overlayNibbles  = 0;

            // 列をパラメータのバイト数に合わせて再構築
            _byteOverlayDgv.Columns.Clear();
            const int COL_W = 34;
            for (int i = 0; i < p.Size; i++)
            {
                _byteOverlayDgv.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Width         = COL_W,
                    MinimumWidth  = COL_W,
                    MaxInputLength = 2,
                    Resizable     = DataGridViewTriState.False,
                    DefaultCellStyle =
                    {
                        Alignment = DataGridViewContentAlignment.MiddleCenter,
                        BackColor = Color.FromArgb(255, 255, 225),
                        Font      = new Font("Consolas", 10f),
                    }
                });
            }

            // 現在のバイト値を行に投入
            _byteOverlayDgv.Rows.Clear();
            var row = new DataGridViewRow();
            row.CreateCells(_byteOverlayDgv);
            for (int i = 0; i < p.Size; i++)
                row.Cells[i].Value = _data[p.Offset + i].ToString("X2");
            _byteOverlayDgv.Rows.Add(row);
            _byteOverlayDgv.Rows[0].Height = 28;

            // バイト列セルの矩形を TabPage 座標に変換して位置決め
            Rectangle cellBounds = _grid.GetCellDisplayRectangle(COL_RAWBYTES, rowIndex, false);
            if (cellBounds.IsEmpty) return;

            Point screenPt   = _grid.PointToScreen(new Point(cellBounds.Left, cellBounds.Top));
            Point tabLocalPt = _grid.Parent.PointToClient(screenPt);

            _byteOverlayDgv.Location = tabLocalPt;
            _byteOverlayDgv.Size     = new Size(p.Size * COL_W + 4, 32);
            _byteOverlayDgv.Visible  = true;
            _byteOverlayDgv.BringToFront();
            _byteOverlayDgv.Focus();
            _byteOverlayDgv.CurrentCell = _byteOverlayDgv.Rows[0].Cells[0];
            _byteOverlayDgv.BeginEdit(true);
        }

        private void CommitByteOverlay()
        {
            if (_overlayParamIdx < 0 || _overlayParamIdx >= SampleData.Parameters.Length)
            {
                _byteOverlayDgv.Visible = false;
                _overlayParamIdx = -1;
                return;
            }

            ParameterDef p = SampleData.Parameters[_overlayParamIdx];

            _byteOverlayDgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _byteOverlayDgv.EndEdit();

            byte[] oldBytes = new byte[p.Size];
            Array.Copy(_data, p.Offset, oldBytes, 0, p.Size);

            byte[] newBytes = new byte[p.Size];
            for (int i = 0; i < p.Size; i++)
            {
                string val = _byteOverlayDgv.Rows[0].Cells[i].Value?.ToString() ?? "00";
                if (!byte.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out newBytes[i]))
                    newBytes[i] = oldBytes[i];
            }

            Array.Copy(newBytes, 0, _data, p.Offset, p.Size);

            bool changed = false;
            for (int i = 0; i < p.Size; i++)
                if (oldBytes[i] != newBytes[i]) { changed = true; break; }

            if (changed)
            {
                _undoMgr.Push(new ByteRangeCommand(p.Offset, oldBytes, newBytes,
                    string.Format("{0} を編集", p.Name)));
                UpdateUndoRedoMenu();
                _statusLabel.Text = string.Format("{0} を更新しました → {1}", p.Name, p.ReadRawBytes(_data));
            }

            _byteOverlayDgv.Visible = false;
            _overlayParamIdx = -1;

            RefreshGrid();
        }

        private void HideByteOverlay()
        {
            _byteOverlayDgv.Visible = false;
            _overlayParamIdx = -1;
        }

        // ── オーバーレイ キー操作ハンドラ ────────────────────────

        private void Overlay_TextBoxKeyDown(object sender, KeyEventArgs e)
        {
            int col = _byteOverlayDgv?.CurrentCell?.ColumnIndex ?? -1;
            if (col < 0) return;

            if (e.KeyCode == Keys.Back)
            {
                // カーソル位置のバイトを 00 にして前のセルへ移動
                _byteOverlayDgv.Rows[0].Cells[col].Value = "00";
                ((TextBox)sender).Text = "00";
                _overlayNibbles = 0;

                e.Handled = true;
                e.SuppressKeyPress = true;

                _byteOverlayDgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
                if (col > 0)
                {
                    _byteOverlayDgv.CurrentCell = _byteOverlayDgv.Rows[0].Cells[col - 1];
                    _byteOverlayDgv.BeginEdit(true);
                }
            }
            else if (e.KeyCode == Keys.Delete)
            {
                // カーソル位置のバイトを 00 にしてカーソルは移動しない
                _byteOverlayDgv.Rows[0].Cells[col].Value = "00";
                ((TextBox)sender).Text = "00";
                _overlayNibbles = 0;
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Return)
            {
                CommitByteOverlay();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private void Overlay_TextBoxKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == '\b')    { e.Handled = true; return; } // KeyDown で処理済み
            if (e.KeyChar == '\r')    { e.Handled = true; return; }
            if (e.KeyChar == '\u001b'){ e.Handled = true; return; }

            // Ctrl+V
            if (e.KeyChar == '\u0016') { OverlayPaste(); e.Handled = true; return; }

            char c = char.ToUpper(e.KeyChar);
            bool isHex = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'F');
            if (!isHex) { e.Handled = true; return; }

            _overlayNibbles++;
            if (_overlayNibbles >= 2)
            {
                _overlayNibbles = 0;
                _byteOverlayDgv.BeginInvoke(new Action(AdvanceOverlayCell));
            }
        }

        private void AdvanceOverlayCell()
        {
            if (_byteOverlayDgv == null || _overlayParamIdx < 0) return;
            if (_overlayParamIdx >= SampleData.Parameters.Length) return;
            ParameterDef p = SampleData.Parameters[_overlayParamIdx];

            int col = _byteOverlayDgv.CurrentCell?.ColumnIndex ?? -1;
            if (col < 0) return;

            _byteOverlayDgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            _byteOverlayDgv.EndEdit();

            if (col < p.Size - 1)
            {
                // 次のバイトセルへ移動
                _byteOverlayDgv.CurrentCell = _byteOverlayDgv.Rows[0].Cells[col + 1];
                _byteOverlayDgv.BeginEdit(true);
                _overlayNibbles = 0;
            }
            // 最後のバイトの場合はカーソルをとどめる（移動しない）
        }

        /// <summary>
        /// オーバーレイへの Ctrl+V 貼り付け。
        /// "1234" → cells[0]="12", cells[1]="34" として書き込む。
        /// パラメータのバイト数と一致しない場合は警告を表示する。
        /// </summary>
        private void OverlayPaste()
        {
            if (_overlayParamIdx < 0 || _overlayParamIdx >= SampleData.Parameters.Length) return;
            ParameterDef p = SampleData.Parameters[_overlayParamIdx];

            string text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text)) return;

            string hex = text.Replace(" ", "").Replace("-", "").Trim().ToUpper();

            if (hex.Length != p.Size * 2)
            {
                MessageBox.Show(
                    string.Format(
                        "貼り付けデータのバイト数がパラメータと一致しません。\n" +
                        "期待: {0} バイト（{1} 文字）\n実際: {2} 文字",
                        p.Size, p.Size * 2, hex.Length),
                    "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            for (int i = 0; i < p.Size; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                {
                    MessageBox.Show(
                        "クリップボードのテキストに無効な文字が含まれています。\n16 進数の文字（0-9, A-F）のみ使用できます。",
                        "貼り付けエラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            for (int i = 0; i < p.Size; i++)
                _byteOverlayDgv.Rows[0].Cells[i].Value = hex.Substring(i * 2, 2);
        }

        // ────────────────────────────────────────────────────────
        //  Tab③: パラメータグリッド (DataGridView + TextBox Hex 入力)
        // ────────────────────────────────────────────────────────

        private TabPage BuildHexTextGridTab()
        {
            var tab = new TabPage("③ パラメータグリッド (TextBox Hex 入力)");

            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                BackColor = Color.FromArgb(255, 240, 210),
                Padding   = new Padding(8, 6, 8, 0)
            };
            var btn = MakeButton("Tab①へ反映", () => PopulateHexViewGrid());
            btn.Location = new Point(8, 6);
            toolbar.Controls.Add(btn);
            toolbar.Controls.Add(new Label
            {
                Text      = "※ セルをクリックして Hex 文字列を直接編集できます（例: 01 00）",
                AutoSize  = true,
                Location  = new Point(btn.Width + 16, 10),
                ForeColor = Color.FromArgb(120, 60, 0),
                Font      = new Font("Meiryo UI", 8.5f)
            });

            _hexTextGrid = BuildHexTextDataGridView();
            BuildHexTextGridRows();

            tab.Controls.Add(_hexTextGrid);
            tab.Controls.Add(toolbar);
            return tab;
        }

        private DataGridView BuildHexTextDataGridView()
        {
            var gv = new DataGridView
            {
                Dock                      = DockStyle.Fill,
                AllowUserToAddRows        = false,
                AllowUserToDeleteRows     = false,
                AllowUserToResizeRows     = false,
                RowHeadersVisible         = false,
                SelectionMode             = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor           = Color.White,
                GridColor                 = Color.LightSteelBlue,
                BorderStyle               = BorderStyle.None,
                Font                      = new Font("Meiryo UI", 9.5f),
                EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode       = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode                  = DataGridViewEditMode.EditOnEnter,
            };
            gv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 85, 145);
            gv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gv.ColumnHeadersDefaultCellStyle.Font      = new Font("Meiryo UI", 9f, FontStyle.Bold);
            gv.ColumnHeadersHeight                     = 28;

            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HexName", HeaderText = "パラメータ名", ReadOnly = true, FillWeight = 18
            });
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HexOffset", HeaderText = "オフセット", ReadOnly = true, FillWeight = 10,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HexInfo", HeaderText = "サイズ / データ型", ReadOnly = true, FillWeight = 16
            });

            var hexBytesCol = new DataGridViewHexTextColumn
            {
                Name       = "HexBytes",
                HeaderText = "バイト列（クリックして Hex 編集）",
                FillWeight = 56,
            };
            hexBytesCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            hexBytesCol.DefaultCellStyle.Font      = new Font("Consolas", 10f);
            gv.Columns.Add(hexBytesCol);

            gv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) return;
                ParameterDef p     = SampleData.Parameters[e.RowIndex];
                Color        rowBg = (e.RowIndex % 2 == 0) ? Color.White : Color.FromArgb(243, 247, 255);
                e.CellStyle.BackColor = p.IsReadOnly ? Color.FromArgb(235, 235, 235) : rowBg;
                e.CellStyle.ForeColor = p.IsReadOnly ? Color.FromArgb(130, 130, 130) : Color.FromArgb(20, 20, 20);
            };

            gv.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) return;
                if (e.ColumnIndex != COL_HEX_BYTES) return;

                ParameterDef p        = SampleData.Parameters[e.RowIndex];
                byte[]       newBytes = gv.Rows[e.RowIndex].Cells[COL_HEX_BYTES].Value as byte[];
                if (newBytes == null || newBytes.Length != p.Size) return;

                byte[] oldBytes = new byte[p.Size];
                Array.Copy(_data, p.Offset, oldBytes, 0, p.Size);

                Array.Copy(newBytes, 0, _data, p.Offset, p.Size);

                bool changed = false;
                for (int i = 0; i < p.Size; i++)
                    if (oldBytes[i] != newBytes[i]) { changed = true; break; }

                if (changed)
                {
                    _undoMgr.Push(new ByteRangeCommand(p.Offset, oldBytes, newBytes,
                        string.Format("{0} を編集", p.Name)));
                    UpdateUndoRedoMenu();
                }

                _statusLabel.Text = string.Format("{0} を更新しました → {1}", p.Name, p.ReadRawBytes(_data));
            };

            gv.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (gv.IsCurrentCellDirty)
                    gv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            return gv;
        }

        private void BuildHexTextGridRows()
        {
            _hexTextGrid.Rows.Clear();
            for (int i = 0; i < SampleData.Parameters.Length; i++)
            {
                ParameterDef p = SampleData.Parameters[i];
                byte[] paramBytes = new byte[p.Size];
                Array.Copy(_data, p.Offset, paramBytes, 0, p.Size);

                int rowIdx = _hexTextGrid.Rows.Add(
                    p.Name,
                    string.Format("0x{0:X4}", p.Offset),
                    string.Format("{0} byte  {1}", p.Size, p.TypeLabel),
                    paramBytes);

                _hexTextGrid.Rows[rowIdx].Cells[COL_HEX_BYTES].ReadOnly = p.IsReadOnly;
                _hexTextGrid.Rows[rowIdx].Height = 46;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  タブ切り替え
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnTabSelecting(object sender, TabControlCancelEventArgs e)
        {
            int from = _tabs.SelectedIndex;
            int to   = e.TabPageIndex;
            if (from == to) return;

            // Tab① を離れるとき: 編集中のセルをコミット
            if (from == 0 && _hexViewGrid != null)
                _hexViewGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);

            // Tab② を離れるとき: オーバーレイを閉じる
            if (from == 1)
                HideByteOverlay();

            switch (to)
            {
                case 0: PopulateHexViewGrid(); break;
                case 1: RefreshGrid();         break;
                case 2: BuildHexTextGridRows(); break;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  データ同期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void RefreshHex()
        {
            PopulateHexViewGrid();
            _statusLabel.Text = string.Format(
                "Hex ビュー (Tab①) を更新しました ({0:N0} bytes)", _data.Length);
        }

        private void RefreshGrid()
        {
            _suppressGridEvents = true;
            _grid.Rows.Clear();
            foreach (ParameterDef p in SampleData.Parameters)
            {
                int rowIdx = _grid.Rows.Add(
                    p.Name,
                    string.Format("0x{0:X4}", p.Offset),
                    p.Size,
                    p.TypeLabel,
                    p.ReadRawBytes(_data),
                    p.ReadValue(_data));
                if (p.IsReadOnly)
                    _grid.Rows[rowIdx].Cells[COL_VALUE].ReadOnly = true;
            }
            _suppressGridEvents = false;
        }

        private void SyncAndRefreshAll()
        {
            RefreshGrid();
            BuildHexTextGridRows();
            _statusLabel.Text = string.Format(
                "Tab②③ に同期しました ({0:N0} bytes)", _data.Length);
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
                _undoMgr.Clear();
                UpdateUndoRedoMenu();
                PopulateHexViewGrid();
                RefreshGrid();
                BuildHexTextGridRows();
                _statusLabel.Text = string.Format(
                    "読み込み完了: {0}  ({1:N0} bytes)", Path.GetFileName(dlg.FileName), _data.Length);
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
            _undoMgr.Clear();
            UpdateUndoRedoMenu();
            PopulateHexViewGrid();
            RefreshGrid();
            BuildHexTextGridRows();
            _statusLabel.Text = "サンプルデータにリセットしました";
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  Undo / Redo
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Z)) { PerformUndo(); return true; }
            if (keyData == (Keys.Control | Keys.Y)) { PerformRedo(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void PerformUndo()
        {
            if (!_undoMgr.CanUndo) return;
            string desc = _undoMgr.UndoDescription;
            _undoMgr.Undo(_data);
            RefreshCurrentTab();
            UpdateUndoRedoMenu();
            _statusLabel.Text = string.Format("元に戻しました: {0}", desc);
        }

        private void PerformRedo()
        {
            if (!_undoMgr.CanRedo) return;
            string desc = _undoMgr.RedoDescription;
            _undoMgr.Redo(_data);
            RefreshCurrentTab();
            UpdateUndoRedoMenu();
            _statusLabel.Text = string.Format("やり直しました: {0}", desc);
        }

        private void RefreshCurrentTab()
        {
            switch (_tabs.SelectedIndex)
            {
                case 0: PopulateHexViewGrid(); break;
                case 1: RefreshGrid();          break;
                case 2: BuildHexTextGridRows(); break;
            }
        }

        private void UpdateUndoRedoMenu()
        {
            _undoMenuItem.Enabled = _undoMgr.CanUndo;
            _redoMenuItem.Enabled = _undoMgr.CanRedo;
            _undoMenuItem.Text = _undoMgr.CanUndo
                ? string.Format("元に戻す: {0} (&Z)", _undoMgr.UndoDescription)
                : "元に戻す (&Z)";
            _redoMenuItem.Text = _undoMgr.CanRedo
                ? string.Format("やり直す: {0} (&Y)", _undoMgr.RedoDescription)
                : "やり直す (&Y)";
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  ユーティリティ
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static Button MakeButton(string text, Action onClick)
        {
            var b = new Button
            {
                Text      = text,
                AutoSize  = true,
                Height    = 27,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.White,
                Font      = new Font("Meiryo UI", 9f)
            };
            b.Click += (s, e) => onClick();
            return b;
        }
    }
}
