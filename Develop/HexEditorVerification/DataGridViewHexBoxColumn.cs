using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace HexEditorVerification
{
    // ════════════════════════════════════════════════════════════════
    //  DataGridView に HexBox を埋め込むためのカスタムコントロール群
    //
    //  構成:
    //    DataGridViewHexBoxEditingControl  … HexBox を継承した編集コントロール
    //    DataGridViewHexBoxCell            … DataGridViewTextBoxCell を継承したセル
    //    DataGridViewHexBoxColumn          … DataGridViewColumn を継承した列
    // ════════════════════════════════════════════════════════════════

    // ────────────────────────────────────────────────────────────────
    //  ① 編集コントロール
    //     HexBox を継承し、IDataGridViewEditingControl を実装する。
    //     DataGridView はこのクラスのインスタンスをセル編集時に生成・配置する。
    // ────────────────────────────────────────────────────────────────
    internal class DataGridViewHexBoxEditingControl : HexBox, IDataGridViewEditingControl
    {
        private DataGridView _dataGridView;
        private int          _rowIndex;
        private bool         _valueChanged;

        public DataGridViewHexBoxEditingControl()
        {
            Font                 = new Font("Consolas", 9.5f);
            LineInfoVisible      = false;
            StringViewVisible    = false;
            ColumnInfoVisible    = false;
            VScrollBarVisible    = false;
            UseFixedBytesPerLine = true;
        }

        // ── IDataGridViewEditingControl ──────────────────────────

        /// <summary>DataGridView への参照（DataGridView 側から自動設定される）</summary>
        public DataGridView EditingControlDataGridView
        {
            get { return _dataGridView; }
            set { _dataGridView = value; }
        }

        /// <summary>
        /// 編集中の値を byte[] で取得 / 設定する。
        /// DataGridView はこのプロパティを通じてセル値と編集コントロールを同期する。
        /// </summary>
        public object EditingControlFormattedValue
        {
            get
            {
                DynamicByteProvider prov = ByteProvider as DynamicByteProvider;
                if (prov == null) return new byte[0];
                byte[] bytes = new byte[prov.Length];
                for (long i = 0; i < prov.Length; i++)
                    bytes[i] = prov.ReadByte(i);
                return bytes;
            }
            set
            {
                byte[] bytes = value as byte[];
                if (bytes == null) bytes = new byte[0];

                DynamicByteProvider prov = new DynamicByteProvider(bytes);
                // HexBox でバイトが変更されるたびに DataGridView へ「値が変わった」と通知する
                prov.Changed += (s, e) =>
                {
                    _valueChanged = true;
                    if (_dataGridView != null)
                        _dataGridView.NotifyCurrentCellDirty(true);
                };
                ByteProvider = prov;
            }
        }

        public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context)
        {
            return EditingControlFormattedValue;
        }

        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
        {
            // 背景色は InitializeEditingControl 側で ReadOnly に応じて設定するため、ここでは何もしない
        }

        public int EditingControlRowIndex
        {
            get { return _rowIndex; }
            set { _rowIndex = value; }
        }

        /// <summary>
        /// true のとき DataGridView は CommitEdit を発行して値を確定する。
        /// DynamicByteProvider.Changed で true にセットし、NotifyCurrentCellDirty で通知する。
        /// </summary>
        public bool EditingControlValueChanged
        {
            get { return _valueChanged; }
            set { _valueChanged = value; }
        }

        public Cursor EditingPanelCursor
        {
            get { return Cursors.IBeam; }
        }

        /// <summary>
        /// 値が変わったとき DataGridView がコントロールを再配置するかどうか。
        /// HexBox はサイズが変わらないため false。
        /// </summary>
        public bool RepositionEditingControlOnValueChange
        {
            get { return false; }
        }

        /// <summary>
        /// 各キーを HexBox が処理するか DataGridView が処理するかを返す。
        /// 矢印キーは DataGridView に渡してセルナビゲーションを維持する。
        /// それ以外は HexBox で処理する。
        /// </summary>
        public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                    return false; // DataGridView で行移動
                case Keys.Left:
                case Keys.Right:
                case Keys.Back:
                case Keys.Delete:
                    return true;  // HexBox 内カーソル移動・削除
                default:
                    return !dataGridViewWantsInputKey;
            }
        }

        public void PrepareEditingControlForEdit(bool selectAll) { }
    }

    // ────────────────────────────────────────────────────────────────
    //  ② カスタムセル
    //     DataGridViewTextBoxCell を継承する。
    //     ・EditType  → DataGridViewHexBoxEditingControl を返す
    //     ・ValueType → byte[]
    //     ・非編集時は hex 文字列 ("XX XX XX") を表示する
    //     ・編集開始時に byte[] を HexBox に渡す
    //     ・編集確定時に byte[] をそのまま値として返す
    // ────────────────────────────────────────────────────────────────
    internal class DataGridViewHexBoxCell : DataGridViewTextBoxCell
    {
        /// <summary>
        /// DataGridView はこの型のインスタンスを生成して編集コントロールとして使う。
        /// DataGridViewTextBoxCell の EditType を上書きすることで HexBox に切り替える。
        /// </summary>
        public override Type EditType
        {
            get { return typeof(DataGridViewHexBoxEditingControl); }
        }

        /// <summary>セルが保持する値の型。byte[] を直接格納する。</summary>
        public override Type ValueType
        {
            get { return typeof(byte[]); }
        }

        public override object DefaultNewRowValue
        {
            get { return new byte[0]; }
        }

        /// <summary>
        /// 編集開始時に DataGridView から呼ばれる。
        /// base（DataGridViewTextBoxCell）を呼ぶことで DataGridViewCell レベルの
        /// 配置処理が実行される。その後 HexBox 固有の設定を上書きする。
        /// </summary>
        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            // base を呼ぶと DataGridViewCell のレベルで編集コントロールの
            // 配置・可視化が行われる（TextBox 向け設定は EditType が違うため無効になる）
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);

            DataGridViewHexBoxEditingControl ctl =
                DataGridView.EditingControl as DataGridViewHexBoxEditingControl;
            if (ctl == null) return;

            byte[] bytes = Value as byte[];
            if (bytes == null) bytes = new byte[0];

            // BytesPerLine: 全バイトを 1 行に収める
            int bpl = Math.Max(1, bytes.Length);
            ctl.BytesPerLine             = bpl;
            ctl.UseFixedBytesPerLine     = true;
            ctl.ReadOnly                 = this.ReadOnly;
            ctl.BackColor                = this.ReadOnly
                                             ? Color.FromArgb(235, 235, 235)
                                             : Color.FromArgb(255, 255, 225);
            ctl.EditingControlFormattedValue = bytes;
        }

        /// <summary>
        /// 編集コントロールの位置・サイズを調整する。
        /// base を先に呼んで DataGridView 内部状態を正常に保ったうえで、
        /// HexBox を縦方向に中央寄せする。
        /// </summary>
        public override void PositionEditingControl(
            bool setLocation, bool setSize,
            Rectangle cellBounds, Rectangle cellClip,
            DataGridViewCellStyle cellStyle,
            bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded,
            bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
        {
            // base を先に呼ぶことで DataGridView の内部管理コントロールが正しく配置される
            base.PositionEditingControl(setLocation, setSize, cellBounds, cellClip, cellStyle,
                singleVerticalBorderAdded, singleHorizontalBorderAdded,
                isFirstDisplayedColumn, isFirstDisplayedRow);

            if (DataGridView == null || DataGridView.EditingControl == null) return;
            Control ctl = DataGridView.EditingControl;

            // HexBox の 1 行表示に必要な高さ（フォント高さ + 上下の内部余白）
            int contentH = ctl.Font.Height + 8;
            if (ctl.Height > contentH)
            {
                int topOffset = (ctl.Height - contentH) / 2;
                ctl.SetBounds(ctl.Left, ctl.Top + topOffset, ctl.Width, contentH);
            }
        }

        /// <summary>
        /// 編集確定時に DataGridView から呼ばれる。
        /// 編集コントロールが返す byte[] をそのままセル値として受け取る。
        /// </summary>
        public override object ParseFormattedValue(object formattedValue,
            DataGridViewCellStyle cellStyle,
            TypeConverter formattedValueTypeConverter,
            TypeConverter valueTypeConverter)
        {
            if (formattedValue is byte[]) return formattedValue;
            return new byte[0];
        }

        /// <summary>
        /// 非編集時の表示文字列を返す。byte[] を "XX XX XX" 形式に変換する。
        /// DataGridView はこの文字列を使って TextBox セルと同様にセルを描画する。
        /// </summary>
        protected override object GetFormattedValue(object value, int rowIndex,
            ref DataGridViewCellStyle cellStyle,
            TypeConverter valueTypeConverter,
            TypeConverter formattedValueTypeConverter,
            DataGridViewDataErrorContexts context)
        {
            byte[] bytes = value as byte[];
            if (bytes == null || bytes.Length == 0) return string.Empty;

            StringBuilder sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  ③ カスタム列
    //     DataGridViewColumn を継承し、CellTemplate に
    //     DataGridViewHexBoxCell を設定する。
    // ────────────────────────────────────────────────────────────────
    internal class DataGridViewHexBoxColumn : DataGridViewColumn
    {
        public DataGridViewHexBoxColumn() : base(new DataGridViewHexBoxCell())
        {
            SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        public override DataGridViewCell CellTemplate
        {
            get { return base.CellTemplate; }
            set
            {
                if (value != null && !(value is DataGridViewHexBoxCell))
                    throw new InvalidCastException(
                        "CellTemplate には DataGridViewHexBoxCell を指定してください。");
                base.CellTemplate = value;
            }
        }
    }
}
