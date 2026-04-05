using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HexEditorStandard
{
    /// <summary>
    /// 標準コントロールのみで実装した Hex エディタ検証フォーム（3 タブ構成）
    ///
    /// Tab① バイナリ全体表示  … HexEditorPanel (カスタム描画)
    /// Tab② パラメータグリッド (DataGridView + テキスト入力)
    /// Tab③ パラメータグリッド (DataGridView + TextBox Hex 入力)
    /// </summary>
    public class HexEditorStandardForm : Form
    {
        // ─── フィールド ───────────────────────────────────────────
        private byte[]               _data = SampleData.Create();
        private HexEditorPanel       _hexPanel;             // Tab①
        private DataGridView         _grid;                 // Tab②
        private DataGridView         _hexTextGrid;          // Tab③
        private ToolStripStatusLabel _statusLabel;
        private bool                 _suppressGridEvents;
        private TabControl           _tabs;

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
            RefreshHex();
            RefreshGrid();
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  UI 構築
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void BuildUI()
        {
            // TabControl は最初に追加する（DockStyle.Fill）
            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.Selecting += OnTabSelecting;
            _tabs.TabPages.Add(BuildHexTab());
            _tabs.TabPages.Add(BuildGridTab());
            _tabs.TabPages.Add(BuildHexTextGridTab());
            Controls.Add(_tabs);

            // メニュー
            var menu     = new MenuStrip();
            var fileMenu = new ToolStripMenuItem("ファイル(&F)");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("開く(&O)...",             null, (s, e) => OpenFile()));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("保存(&S)...",             null, (s, e) => SaveFile()));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("サンプルデータに戻す(&R)", null, (s, e) => ResetToSample()));
            menu.Items.Add(fileMenu);
            MainMenuStrip = menu;
            Controls.Add(menu);

            // ステータスバー
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
        //  Tab①: バイナリ全体表示 (HexEditorPanel)
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
            var btn = MakeButton("HexPanel の編集内容 → グリッドへ反映", SyncAndRefreshAll);
            btn.Location = new Point(8, 6);
            toolbar.Controls.Add(btn);
            toolbar.Controls.Add(new Label
            {
                Text      = "※ パネルで直接バイトを編集後、このボタンで Tab②③ に同期します",
                AutoSize  = true,
                Location  = new Point(btn.Width + 16, 10),
                ForeColor = Color.DimGray,
                Font      = new Font("Meiryo UI", 8.5f)
            });

            _hexPanel = new HexEditorPanel
            {
                Dock      = DockStyle.Fill,
                BackColor = Color.White,
            };
            _hexPanel.Changed += (s, e) =>
            {
                // HexEditorPanel は _data の配列を直接編集するため同期は自動的
                _statusLabel.Text = "バイナリデータを編集しました";
            };

            tab.Controls.Add(_hexPanel);
            tab.Controls.Add(toolbar);
            return tab;
        }

        // ────────────────────────────────────────────────────────
        //  Tab②: パラメータグリッド (DataGridView + テキスト入力)
        // ────────────────────────────────────────────────────────

        private TabPage BuildGridTab()
        {
            var tab = new TabPage("② パラメータグリッド (テキスト入力)");

            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                BackColor = Color.FromArgb(225, 255, 225),
                Padding   = new Padding(8, 6, 8, 0)
            };
            var btn = MakeButton("グリッドの編集内容 → HexPanel へ反映", RefreshHex);
            btn.Location = new Point(8, 6);
            toolbar.Controls.Add(btn);
            toolbar.Controls.Add(new Label
            {
                Text      = "※「値」列を直接編集 → _data に即時反映",
                AutoSize  = true,
                Location  = new Point(btn.Width + 16, 10),
                ForeColor = Color.DimGray,
                Font      = new Font("Meiryo UI", 8.5f)
            });

            _grid = BuildTextGrid();
            tab.Controls.Add(_grid);
            tab.Controls.Add(toolbar);
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

            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",     HeaderText = "パラメータ名",    ReadOnly = true,  FillWeight = 16 });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Offset",   HeaderText = "オフセット",      ReadOnly = true,  FillWeight = 10 });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size",     HeaderText = "サイズ",         ReadOnly = true,  FillWeight = 7  });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type",     HeaderText = "データ型",       ReadOnly = true,  FillWeight = 14 });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "RawBytes", HeaderText = "生バイト列",      ReadOnly = true,  FillWeight = 20 });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value",    HeaderText = "値  （編集可）",  ReadOnly = false, FillWeight = 33 });

            gv.Columns["Offset"].DefaultCellStyle.Alignment   = DataGridViewContentAlignment.MiddleCenter;
            gv.Columns["Size"].DefaultCellStyle.Alignment     = DataGridViewContentAlignment.MiddleCenter;
            gv.Columns["RawBytes"].DefaultCellStyle.Font      = new Font("Consolas", 9f);
            gv.Columns["RawBytes"].DefaultCellStyle.ForeColor = Color.DarkSlateGray;

            gv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) return;
                ParameterDef p = SampleData.Parameters[e.RowIndex];
                if (p.IsReadOnly)
                {
                    e.CellStyle.BackColor = Color.FromArgb(245, 245, 245);
                    e.CellStyle.ForeColor = Color.Gray;
                }
                else if (e.ColumnIndex == COL_VALUE)
                {
                    e.CellStyle.BackColor = Color.FromArgb(255, 255, 210);
                }
            };

            gv.CellBeginEdit += (s, e) =>
            {
                if (e.ColumnIndex != COL_VALUE) { e.Cancel = true; return; }
                if (e.RowIndex < SampleData.Parameters.Length &&
                    SampleData.Parameters[e.RowIndex].IsReadOnly)
                    e.Cancel = true;
            };

            gv.CellEndEdit += (s, e) =>
            {
                if (_suppressGridEvents || e.ColumnIndex != COL_VALUE) return;
                if (e.RowIndex >= SampleData.Parameters.Length) return;

                ParameterDef       param = SampleData.Parameters[e.RowIndex];
                DataGridViewCell   cell  = gv.Rows[e.RowIndex].Cells[COL_VALUE];
                string             val   = cell.Value != null ? cell.Value.ToString() : string.Empty;

                if (!param.WriteValue(_data, val))
                {
                    MessageBox.Show(
                        string.Format("'{0}' を {1} 型に変換できませんでした。", val, param.TypeLabel),
                        "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cell.Value = param.ReadValue(_data);
                }
                else
                {
                    gv.Rows[e.RowIndex].Cells[COL_RAWBYTES].Value = param.ReadRawBytes(_data);
                    _statusLabel.Text = string.Format("{0} を更新しました → {1}",
                        param.Name, param.ReadRawBytes(_data));
                }
            };

            return gv;
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
            var btn = MakeButton("HexPanel①へ反映", RefreshHex);
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

            // 行背景色
            gv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) return;
                ParameterDef p     = SampleData.Parameters[e.RowIndex];
                Color        rowBg = (e.RowIndex % 2 == 0) ? Color.White : Color.FromArgb(243, 247, 255);
                e.CellStyle.BackColor = p.IsReadOnly ? Color.FromArgb(235, 235, 235) : rowBg;
                e.CellStyle.ForeColor = p.IsReadOnly ? Color.FromArgb(130, 130, 130) : Color.FromArgb(20, 20, 20);
            };

            // 値確定 → _data に書き戻す
            gv.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) return;
                if (e.ColumnIndex != COL_HEX_BYTES) return;

                ParameterDef p     = SampleData.Parameters[e.RowIndex];
                byte[]       bytes = gv.Rows[e.RowIndex].Cells[COL_HEX_BYTES].Value as byte[];
                if (bytes == null || bytes.Length != p.Size) return;

                Array.Copy(bytes, 0, _data, p.Offset, p.Size);
                _statusLabel.Text = string.Format("{0} を更新しました → {1}",
                    p.Name, p.ReadRawBytes(_data));
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

            switch (to)
            {
                case 0: RefreshHex();              break;
                case 1: RefreshGrid();             break;
                case 2: BuildHexTextGridRows();    break;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  データ同期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>_data の内容で HexEditorPanel を更新する</summary>
        private void RefreshHex()
        {
            _hexPanel.Data = _data;
            _statusLabel.Text = string.Format(
                "HexPanel (Tab①) を更新しました ({0:N0} bytes)", _data.Length);
        }

        /// <summary>_data の内容で Tab② DataGridView を更新する</summary>
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
            // HexEditorPanel は _data を直接編集するので追加同期は不要
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
                RefreshHex();
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
            RefreshHex();
            RefreshGrid();
            BuildHexTextGridRows();
            _statusLabel.Text = "サンプルデータにリセットしました";
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
