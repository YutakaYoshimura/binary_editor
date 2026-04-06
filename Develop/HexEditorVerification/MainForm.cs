using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace HexEditorVerification
{
    /// <summary>
    /// Be.Windows.Forms.HexBox 検証フォーム（3 タブ構成）
    ///
    /// Tab①「バイナリ全体表示」
    ///   HexBox でファイル全体を16進数＋ASCII で表示・直接編集
    ///
    /// Tab②「パラメータグリッド (DataGridView)」
    ///   DataGridView で各パラメータを行表示。「値」列を直接テキスト編集
    ///
    /// Tab③「パラメータグリッド (HexBox 埋め込み)」  ← NEW
    ///   各パラメータ行に小さな HexBox を埋め込み、生バイトを直接 hex 入力で編集
    /// </summary>
    public class MainForm : Form
    {
        // ─── フィールド ───────────────────────────────────────────
        private byte[]               _data       = SampleData.Create();
        private HexBox               _hexBox;               // Tab①
        private DataGridView         _grid;                 // Tab②
        private DataGridView         _hexBoxGrid;           // Tab③
        private ToolStripStatusLabel _statusLabel;
        private bool                 _suppressGridEvents;
        private TabControl           _tabs;
        private UndoManager          _undoMgr = new UndoManager();
        private ToolStripMenuItem    _undoMenuItem;
        private ToolStripMenuItem    _redoMenuItem;

        // DataGridView 列インデックス (Tab②)
        private const int COL_NAME     = 0;
        private const int COL_OFFSET   = 1;
        private const int COL_SIZE     = 2;
        private const int COL_TYPE     = 3;
        private const int COL_RAWBYTES = 4;
        private const int COL_VALUE    = 5;

        // DataGridView 列インデックス (Tab③)
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
            Application.Run(new MainForm());
        }

        // ─── コンストラクタ ───────────────────────────────────────
        public MainForm()
        {
            Text          = "Be.Windows.Forms.HexBox 検証 ― 3 画面比較";
            Size          = new Size(1100, 700);
            MinimumSize   = new Size(800, 500);
            StartPosition = FormStartPosition.CenterScreen;

            BuildUI();
            // 初期描画
            RefreshHex();
            RefreshGrid();
            // Tab③ の行は BuildHexGridTab() 内で初回構築済み
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  UI 構築
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void BuildUI()
        {
            // ── TabControl（DockStyle.Fill は最初に追加する必要がある）──
            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.Selecting += OnTabSelecting;
            _tabs.TabPages.Add(BuildHexTab());      // Tab①
            _tabs.TabPages.Add(BuildGridTab());     // Tab②
            _tabs.TabPages.Add(BuildHexGridTab());  // Tab③
            Controls.Add(_tabs);

            // ── メニュー ──
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

            // ── ステータスバー ──
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
        //  Tab①: バイナリ全体表示 (HexBox)
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
            var btn = MakeButton("HexBox の編集内容 → グリッドへ反映", SyncHexToDataAndRefreshAll);
            btn.Location = new Point(8, 6);
            toolbar.Controls.Add(btn);
            toolbar.Controls.Add(new Label
            {
                Text     = "※ HexBox で直接バイトを編集後、このボタンで Tab②③ に同期します",
                AutoSize = true, Location = new Point(btn.Width + 16, 10),
                ForeColor = Color.DimGray, Font = new Font("Meiryo UI", 8.5f)
            });

            _hexBox = new HexBox
            {
                Dock                = DockStyle.Fill,
                Font                = new Font("Consolas", 10.5f),
                BackColor           = Color.White,
                LineInfoVisible     = true,
                StringViewVisible   = true,
                VScrollBarVisible   = true,
                ColumnInfoVisible   = true,
                BytesPerLine        = 16,
                UseFixedBytesPerLine = true,
                SelectionBackColor  = Color.FromArgb(150, 190, 240),
                SelectionForeColor  = Color.Black,
                InfoForeColor       = Color.SlateGray
            };

            tab.Controls.Add(_hexBox);
            tab.Controls.Add(toolbar);
            return tab;
        }

        // ────────────────────────────────────────────────────────
        //  Tab②: パラメータグリッド (DataGridView)
        // ────────────────────────────────────────────────────────

        private TabPage BuildGridTab()
        {
            var tab = new TabPage("② パラメータグリッド (DataGridView)");

            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                BackColor = Color.FromArgb(225, 255, 225),
                Padding   = new Padding(8, 6, 8, 0)
            };
            var btn = MakeButton("グリッドの編集内容 → HexBox へ反映", RefreshHex);
            btn.Location = new Point(8, 6);
            toolbar.Controls.Add(btn);
            toolbar.Controls.Add(new Label
            {
                Text     = "※「値」列を直接編集 → _data に即時反映。HexBox に同期するにはこのボタンを押してください",
                AutoSize = true, Location = new Point(btn.Width + 16, 10),
                ForeColor = Color.DimGray, Font = new Font("Meiryo UI", 8.5f)
            });

            _grid = BuildGrid();
            tab.Controls.Add(_grid);
            tab.Controls.Add(toolbar);
            return tab;
        }

        private DataGridView BuildGrid()
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
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "RawBytes", HeaderText = "生バイト列",     ReadOnly = true,  FillWeight = 20 });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Value",    HeaderText = "値  （編集可）", ReadOnly = false, FillWeight = 33 });

            gv.Columns["Offset"].DefaultCellStyle.Alignment   = DataGridViewContentAlignment.MiddleCenter;
            gv.Columns["Size"].DefaultCellStyle.Alignment     = DataGridViewContentAlignment.MiddleCenter;
            gv.Columns["RawBytes"].DefaultCellStyle.Font      = new Font("Consolas", 9f);
            gv.Columns["RawBytes"].DefaultCellStyle.ForeColor = Color.DarkSlateGray;

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
                ParameterDef param = SampleData.Parameters[e.RowIndex];
                DataGridViewCell cell = gv.Rows[e.RowIndex].Cells[COL_VALUE];
                string newVal = cell.Value != null ? cell.Value.ToString() : string.Empty;
                if (!param.WriteValue(_data, newVal))
                {
                    MessageBox.Show(
                        string.Format("'{0}' を {1} 型に変換できませんでした。\n入力例: {2}",
                                      newVal, param.TypeLabel, GetExample(param)),
                        "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    cell.Value = param.ReadValue(_data);
                }
                else
                {
                    gv.Rows[e.RowIndex].Cells[COL_RAWBYTES].Value = param.ReadRawBytes(_data);
                    _statusLabel.Text = string.Format("{0} を更新しました  →  {1}", param.Name, param.ReadRawBytes(_data));
                }
            };

            return gv;
        }

        // ────────────────────────────────────────────────────────
        //  Tab③: パラメータグリッド (DataGridView + HexBox 埋め込み)
        // ────────────────────────────────────────────────────────

        private TabPage BuildHexGridTab()
        {
            var tab = new TabPage("③ パラメータグリッド (HexBox 埋め込み)");

            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 40,
                BackColor = Color.FromArgb(255, 240, 210),
                Padding   = new Padding(8, 6, 8, 0)
            };
            var btnToHex = MakeButton("HexBox①へ反映", RefreshHex);
            btnToHex.Location = new Point(8, 6);
            toolbar.Controls.Add(btnToHex);
            toolbar.Controls.Add(new Label
            {
                Text      = "※ セルをクリックすると HexBox で生バイトを直接編集できます。Tab① に同期するにはこのボタンを押してください",
                AutoSize  = true,
                Location  = new Point(btnToHex.Width + 16, 10),
                ForeColor = Color.FromArgb(120, 60, 0),
                Font      = new Font("Meiryo UI", 8.5f)
            });

            _hexBoxGrid = BuildHexBoxDataGridView();
            BuildHexGridRows();  // 初回データ投入

            tab.Controls.Add(_hexBoxGrid);
            tab.Controls.Add(toolbar);
            return tab;
        }

        /// <summary>Tab③ 用 DataGridView を構築して返す（列定義・イベント設定）</summary>
        private DataGridView BuildHexBoxDataGridView()
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

            // ── 列定義 ──
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HexName", HeaderText = "パラメータ名",
                ReadOnly = true, FillWeight = 18
            });
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HexOffset", HeaderText = "オフセット",
                ReadOnly = true, FillWeight = 10,
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            gv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "HexInfo", HeaderText = "サイズ / データ型",
                ReadOnly = true, FillWeight = 16
            });
            var hexBytesCol = new DataGridViewHexBoxColumn
            {
                Name       = "HexBytes",
                HeaderText = "バイト列（クリックして HexBox 編集）",
                FillWeight = 56,
            };
            // 非編集時: 縦中央・横左寄せ
            hexBytesCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
            hexBytesCol.DefaultCellStyle.Font      = new Font("Consolas", 9.5f);
            gv.Columns.Add(hexBytesCol);

            // ── 行の背景色・フォント ──
            gv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) return;
                ParameterDef p = SampleData.Parameters[e.RowIndex];
                Color rowBg = (e.RowIndex % 2 == 0)
                    ? Color.White
                    : Color.FromArgb(243, 247, 255);
                e.CellStyle.BackColor = p.IsReadOnly
                    ? Color.FromArgb(235, 235, 235)
                    : rowBg;
                e.CellStyle.ForeColor = p.IsReadOnly
                    ? Color.FromArgb(130, 130, 130)
                    : Color.FromArgb(20, 20, 20);
            };

            // ── 値確定 → _data に書き戻す ──
            gv.CellValueChanged += OnHexBoxGridCellValueChanged;

            // ── 編集中に値が変わったら即コミット（Changed イベント経由で呼ばれる） ──
            gv.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (gv.IsCurrentCellDirty)
                    gv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };

            return gv;
        }

        /// <summary>_data の現在値で Tab③ DataGridView の行を更新する</summary>
        private void BuildHexGridRows()
        {
            _hexBoxGrid.Rows.Clear();

            for (int i = 0; i < SampleData.Parameters.Length; i++)
            {
                ParameterDef p = SampleData.Parameters[i];

                // パラメータに対応するバイト列を _data から切り出す
                byte[] paramBytes = new byte[p.Size];
                Array.Copy(_data, p.Offset, paramBytes, 0, p.Size);

                int rowIdx = _hexBoxGrid.Rows.Add(
                    p.Name,
                    string.Format("0x{0:X4}", p.Offset),
                    string.Format("{0} byte  {1}", p.Size, p.TypeLabel),
                    paramBytes);   // DataGridViewHexBoxCell が byte[] を受け取る

                // 読み取り専用パラメータは HexBytes セルを ReadOnly にする
                _hexBoxGrid.Rows[rowIdx].Cells[COL_HEX_BYTES].ReadOnly = p.IsReadOnly;

                // 常に 1 行表示なので固定高さ
                _hexBoxGrid.Rows[rowIdx].Height = 46;
            }
        }

        /// <summary>DataGridView の HexBytes 列が確定したとき _data に書き戻す</summary>
        private void OnHexBoxGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= SampleData.Parameters.Length) return;
            if (e.ColumnIndex != COL_HEX_BYTES) return;

            ParameterDef p        = SampleData.Parameters[e.RowIndex];
            byte[]       newBytes = _hexBoxGrid.Rows[e.RowIndex].Cells[COL_HEX_BYTES].Value as byte[];
            if (newBytes == null || newBytes.Length != p.Size) return;

            // 書き戻し前の値を保存
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

            _statusLabel.Text = string.Format(
                "{0} を更新しました  →  {1}", p.Name, p.ReadRawBytes(_data));
        }

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
                case 0: RefreshHex();        break;
                case 1: RefreshGrid();       break;
                case 2: BuildHexGridRows();  break;
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
        //  タブ切り替え時の自動同期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private void OnTabSelecting(object sender, TabControlCancelEventArgs e)
        {
            int from = _tabs.SelectedIndex;
            int to   = e.TabPageIndex;
            if (from == to) return;

            // Tab① を離れるとき: HexBox の編集内容を _data に書き戻す
            if (from == 0)
                SyncHexToData();

            // 移動先のタブを最新の _data で更新
            switch (to)
            {
                case 0: RefreshHex();                      break; // Tab①
                case 1: RefreshGrid();                     break; // Tab②
                case 2: BuildHexGridRows(); /* Tab③ 再構築 */ break;
            }
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  データ同期
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        /// <summary>HexBox の DynamicByteProvider から _data に読み戻す</summary>
        private void SyncHexToData()
        {
            DynamicByteProvider provider = _hexBox.ByteProvider as DynamicByteProvider;
            if (provider == null) return;
            _data = new byte[provider.Length];
            for (long i = 0; i < provider.Length; i++)
                _data[i] = provider.ReadByte(i);
        }

        /// <summary>HexBox から同期してグリッド・HexGrid 両方を更新する（Tab① ツールバーボタン用）</summary>
        private void SyncHexToDataAndRefreshAll()
        {
            SyncHexToData();
            RefreshGrid();
            BuildHexGridRows();
            _statusLabel.Text = string.Format(
                "HexBox → Tab②③ に同期しました  ({0:N0} bytes)", _data.Length);
        }

        /// <summary>_data の内容で HexBox を再描画する</summary>
        private void RefreshHex()
        {
            _hexBox.ByteProvider = new DynamicByteProvider(_data);
            _statusLabel.Text = string.Format(
                "HexBox (Tab①) を更新しました  ({0:N0} bytes)", _data.Length);
        }

        /// <summary>_data の内容で DataGridView を再描画する</summary>
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
                BuildHexGridRows();
                _statusLabel.Text = string.Format(
                    "読み込み完了: {0}  ({1:N0} bytes)", Path.GetFileName(dlg.FileName), _data.Length);
            }
        }

        private void SaveFile()
        {
            SyncHexToData(); // 保存前に HexBox の最新状態を取り込む
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
            BuildHexGridRows();
            _statusLabel.Text = "サンプルデータにリセットしました";
        }

        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
        //  ユーティリティ
        // ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static Label GridHeader(string text)
        {
            return new Label
            {
                Text      = text,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font      = new Font("Meiryo UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 60, 100),
                BackColor = Color.FromArgb(210, 228, 255),
                Padding   = new Padding(4, 0, 4, 0),
            };
        }

        private static Label GridCell(string text, bool readOnly)
        {
            return new Label
            {
                Text      = text,
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("Meiryo UI", 9f),
                ForeColor = readOnly ? Color.Gray : Color.FromArgb(20, 20, 20),
                BackColor = readOnly ? Color.FromArgb(245, 245, 245) : Color.White,
                Padding   = new Padding(8, 0, 4, 0),
            };
        }

        private static Button MakeButton(string text, Action onClick)
        {
            Button b = new Button
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

        private static string GetExample(ParameterDef p)
        {
            switch (p.Type)
            {
                case ParamType.UInt8:       return "0 〜 255";
                case ParamType.UInt16LE:    return "0 〜 65535";
                case ParamType.UInt32LE:    return "0 〜 4294967295";
                case ParamType.AsciiString: return "半角英数字（例: DEVICE_02）";
                default:                   return "(編集不可)";
            }
        }
    }
}
