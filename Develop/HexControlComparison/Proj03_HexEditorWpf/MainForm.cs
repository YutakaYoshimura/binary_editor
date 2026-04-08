using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using WpfHexaEditor;

namespace HexControlComparison
{
    // ════════════════════════════════════════════════════════════════
    //  Proj03: HexEditor.Wpf 2.1.8  (WPFHexaEditor と同一コードベース)
    //
    //  Tab②: DataGridViewWpfHexColumn を使用。
    //         DataGridView の標準編集機構により HexEditor が
    //         そのままセル枠に合わせて表示される。
    // ════════════════════════════════════════════════════════════════
    public class MainForm : Form
    {
        byte[] _data = SampleData.Create();

        // Tab①
        HexEditor   _fullHex;
        ElementHost _fullHost;

        // Tab②
        DataGridView _grid;

        ToolStripStatusLabel _status;

        const int RowH = 36;

        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public MainForm()
        {
            Text          = "③ HexEditor.Wpf 2.1.8 (WPFHexaEditor 同一コードベース)";
            Size          = new Size(1000, 700);
            MinimumSize   = new Size(800, 500);
            StartPosition = FormStartPosition.CenterScreen;
            BuildUI();
        }

        void BuildUI()
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };
            tabs.TabPages.Add(BuildTab1());
            tabs.TabPages.Add(BuildTab2());
            Controls.Add(tabs);

            var ss = new StatusStrip();
            _status = new ToolStripStatusLabel("HexEditor.Wpf 2.1.8 (ElementHost)")
                      { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            ss.Items.Add(_status);
            Controls.Add(ss);
        }

        TabPage BuildTab1()
        {
            var tab = new TabPage("① バイナリ全体表示");

            var toolbar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(4, 4, 0, 0),
                BackColor = Color.FromArgb(245, 240, 255),
            };
            toolbar.Controls.Add(MakeBtn("ファイルを開く",      OpenFile));
            toolbar.Controls.Add(MakeBtn("ファイルを保存",      SaveFile));
            toolbar.Controls.Add(MakeBtn("サンプルデータに戻す", ResetData));

            _fullHex = new HexEditor
            {
                Background  = System.Windows.Media.Brushes.White,
                FontSize    = 13,
                BytePerLine = 16,
                AllowExtend = false,
            };
            _fullHost = WrapInHost(_fullHex);

            var note = MakeNotePanel(
                "設定パラメータ: AllowExtend=false（バイト追加禁止）\r\n" +
                "① 超過バイト数: 改善見込み   ② 不足バイト数: 要確認   " +
                "③ 1byte削除: 要確認   ④ 横スクロール追従: WPF レンダリングにより改善期待");

            tab.Controls.Add(_fullHost);
            tab.Controls.Add(note);
            tab.Controls.Add(toolbar);
            LoadFullHex();
            return tab;
        }

        TabPage BuildTab2()
        {
            var tab = new TabPage("② パラメータグリッド");
            _grid = BuildParamGrid();
            tab.Controls.Add(_grid);
            return tab;
        }

        DataGridView BuildParamGrid()
        {
            var gv = new DataGridView
            {
                Dock                      = DockStyle.Fill,
                AllowUserToAddRows        = false,
                AllowUserToDeleteRows     = false,
                RowHeadersVisible         = false,
                SelectionMode             = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect               = false,
                AutoSizeColumnsMode       = DataGridViewAutoSizeColumnsMode.Fill,
                Font                      = new Font("Meiryo UI", 9f),
                RowTemplate               = { Height = RowH },
                EnableHeadersVisualStyles = false,
                EditMode                  = DataGridViewEditMode.EditOnKeystrokeOrF2,
            };
            gv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(90, 50, 140);
            gv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            gv.ColumnHeadersDefaultCellStyle.Font      = new Font("Meiryo UI", 9f, FontStyle.Bold);

            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name",   HeaderText = "パラメータ名",      FillWeight = 18, ReadOnly = true });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Offset", HeaderText = "オフセット",        FillWeight = 10, ReadOnly = true });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Size",   HeaderText = "サイズ",           FillWeight =  7, ReadOnly = true });
            gv.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type",   HeaderText = "データ型",         FillWeight = 13, ReadOnly = true });
            gv.Columns.Add(new DataGridViewWpfHexColumn  { Name = "HexBytes", HeaderText = "バイト値（HexEditor）", FillWeight = 52 });

            for (int i = 0; i < SampleData.Parameters.Length; i++)
            {
                ParameterDef p     = SampleData.Parameters[i];
                byte[]       bytes = new byte[p.Size];
                Array.Copy(_data, p.Offset, bytes, 0, p.Size);

                int rowIdx = gv.Rows.Add(
                    p.Name,
                    string.Format("0x{0:X4}", p.Offset),
                    string.Format("{0}B",     p.Size),
                    p.TypeLabel,
                    bytes);

                var cell = gv.Rows[rowIdx].Cells["HexBytes"];
                cell.ReadOnly = p.IsReadOnly;
                if (p.IsReadOnly)
                    cell.Style.BackColor = Color.FromArgb(235, 235, 235);
            }

            gv.CellEndEdit += (s, e) =>
            {
                if (e.ColumnIndex != gv.Columns["HexBytes"].Index) return;
                byte[]       bytes = gv.Rows[e.RowIndex].Cells["HexBytes"].Value as byte[];
                ParameterDef p     = SampleData.Parameters[e.RowIndex];

                if (bytes == null || bytes.Length != p.Size)
                {
                    byte[] orig = new byte[p.Size];
                    Array.Copy(_data, p.Offset, orig, 0, p.Size);
                    gv.Rows[e.RowIndex].Cells["HexBytes"].Value = orig;
                    _status.Text = string.Format("バイト数エラー: 期待 {0}B に対し {1}B → 変更を破棄しました",
                        p.Size, bytes == null ? 0 : bytes.Length);
                    return;
                }

                Array.Copy(bytes, 0, _data, p.Offset, p.Size);
                _status.Text = string.Format("{0} を更新しました → {1}", p.Name, p.ReadRawBytes(_data));
            };

            return gv;
        }

        void LoadFullHex() { _fullHex.Stream = new MemoryStream(_data); }

        void RefreshParamGrid()
        {
            if (_grid == null) return;
            for (int i = 0; i < SampleData.Parameters.Length; i++)
            {
                ParameterDef p     = SampleData.Parameters[i];
                byte[]       bytes = new byte[p.Size];
                Array.Copy(_data, p.Offset, bytes, 0, p.Size);
                _grid.Rows[i].Cells["HexBytes"].Value = bytes;
            }
        }

        void OpenFile()
        {
            using (var dlg = new OpenFileDialog { Filter = "バイナリ|*.bin;*.eds|すべて|*.*" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                _data = File.ReadAllBytes(dlg.FileName);
                LoadFullHex(); RefreshParamGrid();
                _status.Text = string.Format("読み込み: {0} ({1:N0}B)", Path.GetFileName(dlg.FileName), _data.Length);
            }
        }

        void SaveFile()
        {
            Stream s = _fullHex.Stream;
            if (s != null) { s.Position = 0; _data = new byte[s.Length]; s.Read(_data, 0, _data.Length); }
            using (var dlg = new SaveFileDialog { Filter = "バイナリ|*.bin;*.eds|すべて|*.*" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                File.WriteAllBytes(dlg.FileName, _data);
                _status.Text = string.Format("保存完了: {0}", dlg.FileName);
            }
        }

        void ResetData()
        {
            _data = SampleData.Create();
            LoadFullHex(); RefreshParamGrid();
            _status.Text = "サンプルデータにリセットしました";
        }

        static ElementHost WrapInHost(System.Windows.UIElement ctrl)
        {
            var g = new System.Windows.Controls.Grid();
            g.Margin = new System.Windows.Thickness(0);
            g.Children.Add(ctrl);
            return new ElementHost { Dock = DockStyle.Fill, Child = g };
        }

        static Button MakeBtn(string text, Action onClick)
        {
            var b = new Button
            {
                Text = text, AutoSize = true, Height = 28,
                FlatStyle = FlatStyle.Flat, BackColor = Color.White,
                Font = new Font("Meiryo UI", 9f), Margin = new Padding(2, 0, 2, 0),
            };
            b.Click += (s, ev) => onClick();
            return b;
        }

        static Panel MakeNotePanel(string text)
        {
            var p = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Color.FromArgb(248, 240, 255) };
            p.Controls.Add(new Label
            {
                Text = text, Dock = DockStyle.Fill,
                Font = new Font("Meiryo UI", 8.5f), ForeColor = Color.DimGray,
                Padding = new Padding(6, 4, 6, 4),
            });
            return p;
        }
    }
}
