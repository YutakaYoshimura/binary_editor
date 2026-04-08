using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using WpfHexaEditor;

namespace HexControlComparison
{
    // ────────────────────────────────────────────────────────────────
    //  Proj02 / Proj03 共通
    //  WPFHexaEditor.HexEditor を IDataGridViewEditingControl でラップし、
    //  DataGridView の標準編集機構に乗せる。
    //  ヘッダー・ステータスバー・ASCII欄・オフセット欄をすべて非表示にし
    //  セル枠の中に Hex バイトだけを表示する。
    //
    //  【重要】ElementHost を直接継承する。UserControl でラップすると
    //  WPF コンテンツが描画されない（ElementHost のレンダリングが機能しない）。
    // ────────────────────────────────────────────────────────────────

    // ① 編集コントロール
    internal class DataGridViewWpfHexEditingControl : ElementHost, IDataGridViewEditingControl
    {
        HexEditor    _hex;
        DataGridView _dataGridView;
        int          _rowIndex;
        bool         _valueChanged;

        public DataGridViewWpfHexEditingControl()
        {
            _hex = new HexEditor
            {
                AllowExtend             = false,
                FontSize                = 10.5,
                Background              = System.Windows.Media.Brushes.LightYellow,
                // クロムをすべて除去してセル枠に収める
                HeaderVisibility        = Visibility.Collapsed,
                StatusBarVisibility     = Visibility.Collapsed,
                StringDataVisibility    = Visibility.Collapsed,
                LineInfoVisibility      = Visibility.Collapsed,
                BarChartPanelVisibility = Visibility.Collapsed,
            };

            _hex.BytesModified += (s, e) =>
            {
                _valueChanged = true;
                if (_dataGridView != null) _dataGridView.NotifyCurrentCellDirty(true);
            };

            var wpfGrid = new System.Windows.Controls.Grid();
            wpfGrid.Margin = new Thickness(0);
            wpfGrid.Children.Add(_hex);

            // ElementHost に直接 WPF ツリーをセットする
            Child = wpfGrid;
        }

        // ── IDataGridViewEditingControl ──────────────────────────────

        public DataGridView EditingControlDataGridView
        {
            get { return _dataGridView; }
            set { _dataGridView = value; }
        }
        public int  EditingControlRowIndex        { get { return _rowIndex; }    set { _rowIndex = value; } }
        public bool EditingControlValueChanged    { get { return _valueChanged; } set { _valueChanged = value; } }
        public Cursor EditingPanelCursor          { get { return Cursors.Default; } }
        public bool RepositionEditingControlOnValueChange { get { return false; } }

        public object EditingControlFormattedValue
        {
            get
            {
                Stream s = _hex.Stream;
                if (s == null) return new byte[0];
                s.Position = 0;
                byte[] b = new byte[s.Length];
                s.Read(b, 0, b.Length);
                return b;
            }
            set
            {
                byte[] bytes = value as byte[] ?? new byte[0];
                _hex.BytePerLine  = Math.Max(1, bytes.Length);
                _hex.Stream       = new MemoryStream((byte[])bytes.Clone());
                _hex.ReadOnlyMode = false;
            }
        }

        public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context)
        {
            return EditingControlFormattedValue;
        }

        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle s)
        {
            _hex.Background = s.BackColor.R < 240
                ? System.Windows.Media.Brushes.LightGray
                : System.Windows.Media.Brushes.LightYellow;
        }

        public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down: return false;   // DataGridView に行移動を任せる
                default:        return true;    // その他はすべて WPF コントロールへ
            }
        }

        public void PrepareEditingControlForEdit(bool selectAll) { }
    }

    // ② カスタムセル
    internal class DataGridViewWpfHexCell : DataGridViewTextBoxCell
    {
        public override Type EditType           { get { return typeof(DataGridViewWpfHexEditingControl); } }
        public override Type ValueType          { get { return typeof(byte[]); } }
        public override object DefaultNewRowValue { get { return new byte[0]; } }

        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
            var ctl = DataGridView.EditingControl as DataGridViewWpfHexEditingControl;
            if (ctl == null) return;

            byte[] bytes = Value as byte[] ?? new byte[0];
            ctl.EditingControlFormattedValue = bytes;
        }

        public override object ParseFormattedValue(object formattedValue,
            DataGridViewCellStyle cellStyle,
            System.ComponentModel.TypeConverter ftc,
            System.ComponentModel.TypeConverter vtc)
        {
            return formattedValue is byte[] ? formattedValue : new byte[0];
        }

        protected override object GetFormattedValue(object value, int rowIndex,
            ref DataGridViewCellStyle cellStyle,
            System.ComponentModel.TypeConverter vtc,
            System.ComponentModel.TypeConverter ftc,
            DataGridViewDataErrorContexts context)
        {
            byte[] bytes = value as byte[];
            if (bytes == null || bytes.Length == 0) return string.Empty;
            var sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }

    // ③ カスタム列
    internal class DataGridViewWpfHexColumn : DataGridViewColumn
    {
        public DataGridViewWpfHexColumn() : base(new DataGridViewWpfHexCell())
        {
            SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        public override DataGridViewCell CellTemplate
        {
            get { return base.CellTemplate; }
            set
            {
                if (value != null && !(value is DataGridViewWpfHexCell))
                    throw new InvalidCastException("DataGridViewWpfHexCell を指定してください。");
                base.CellTemplate = value;
            }
        }
    }
}
