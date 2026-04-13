using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace EdsStandardBase
{
    /// <summary>
    /// パターン[2] 標準コントロール（DataGridView）利用 ― ベース画面
    ///
    /// Tab①「バイナリ全体表示」
    ///   DataGridView でバイナリ全データを16バイト×1行の Hex ビューで表示する。
    ///   列構成: Offset | 00 〜 0F の16バイト列 | ASCII テキスト列
    ///
    /// Tab②「パラメータグリッド」
    ///   DataGridView でパラメータ一覧を表示する。
    /// </summary>
    public class EdsStandardBaseForm : Form
    {
        // ─── フィールド ───────────────────────────────────────────
        private byte[]               _data = SampleData.Create();
        private DataGridView         _hexGrid;    // Tab①: バイナリ全体表示
        private DataGridView         _paramGrid;  // Tab②: パラメータ一覧
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
        //  Tab①: バイナリ全体表示 (DataGridView Hex ビュー)
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
                       "列構成: Offset | 00〜0F の 16 バイト | ASCII テキスト。",
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
                ReadOnly                    = true,
            };
            gv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(50, 85, 145);
            gv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gv.ColumnHeadersDefaultCellStyle.Font      = new Font("Consolas", 9f, FontStyle.Bold);
            gv.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            gv.ColumnHeadersHeight = 24;

            // Offset 列
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name       = "Offset",
                HeaderText = "Offset",
                Width      = 82,
                Resizable  = DataGridViewTriState.False,
                DefaultCellStyle =
                {
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(80, 100, 180),
                    Font      = new Font("Consolas", 9.5f),
                }
            });

            // バイト列 × 16（00〜0F）
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

            // ASCII テキスト列
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name         = "Text",
                HeaderText   = "Text",
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

            return gv;
        }

        private void PopulateHexGrid()
        {
            _hexGrid.Rows.Clear();
            for (int lineBase = 0; lineBase < _data.Length; lineBase += 16)
            {
                // cells[0]=Offset, cells[1..16]=バイト, cells[17]=ASCII
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
                Text = "【パターン[2]】 EDS パラメータ一覧を DataGridView で表示します。\n" +
                       "バイナリ値入力コントロールは今後実装予定です。",
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
                HeaderText = "バイト列",
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
            PopulateHexGrid();
            PopulateParamGrid();
        }
    }
}
