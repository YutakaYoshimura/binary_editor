using System;
using System.ComponentModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Be.Windows.Forms;

namespace HexEditorVerification
{
    // ────────────────────────────────────────────────────────────────
    //  固定長バイトプロバイダ
    //  SupportsInsertBytes / SupportsDeleteBytes を false にすることで
    //  HexBox のバイト挿入・削除操作を根本から無効化する。
    // ────────────────────────────────────────────────────────────────
    internal class FixedByteProvider : IByteProvider
    {
        private readonly byte[] _data;
        public event EventHandler Changed;
        public event EventHandler LengthChanged;

        public FixedByteProvider(byte[] data)
        {
            _data = (byte[])data.Clone();
        }

        public long Length { get { return _data.Length; } }

        public byte ReadByte(long index) { return _data[index]; }

        public void WriteByte(long index, byte value)
        {
            _data[index] = value;
            if (Changed != null) Changed(this, EventArgs.Empty);
        }

        public void InsertBytes(long index, byte[] bs) { }
        public void DeleteBytes(long index, long length) { }
        public bool SupportsWriteByte()    { return true; }
        public bool SupportsInsertBytes()  { return false; }
        public bool SupportsDeleteBytes()  { return false; }
        public bool HasChanges()           { return false; }
        public void ApplyChanges()         { }
    }


    // ════════════════════════════════════════════════════════════════
    //  DataGridView に HexBox を埋め込むためのカスタムコントロール群
    // ════════════════════════════════════════════════════════════════

    // ────────────────────────────────────────────────────────────────
    //  ① 編集コントロール
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
            InsertActive         = false;
        }

        // ── IDataGridViewEditingControl ──────────────────────────

        public DataGridView EditingControlDataGridView
        {
            get { return _dataGridView; }
            set { _dataGridView = value; }
        }

        public object EditingControlFormattedValue
        {
            get
            {
                IByteProvider prov = ByteProvider;
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

                FixedByteProvider prov = new FixedByteProvider(bytes);
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

        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle) { }

        public int EditingControlRowIndex
        {
            get { return _rowIndex; }
            set { _rowIndex = value; }
        }

        public bool EditingControlValueChanged
        {
            get { return _valueChanged; }
            set { _valueChanged = value; }
        }

        public Cursor EditingPanelCursor
        {
            get { return Cursors.IBeam; }
        }

        public bool RepositionEditingControlOnValueChange
        {
            get { return false; }
        }

        public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down:
                    return false;
                case Keys.Left:
                case Keys.Right:
                case Keys.Back:
                case Keys.Delete:
                    return true;
                default:
                    return !dataGridViewWantsInputKey;
            }
        }

        public void PrepareEditingControlForEdit(bool selectAll) { }

        // ── キー処理 ──────────────────────────────────────────────

        /// <summary>
        /// BackSpace: カーソル位置のバイトを 00 にして 1 バイト前に移動する。
        /// Delete:    カーソル位置のバイトを 00 にしてカーソルは移動しない。
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Back)
            {
                ZeroCurrentByte(moveToPrev: true);
                // base を呼ばない = HexBox 既定の BackSpace 動作をキャンセル
                return;
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

            prov.WriteByte(pos, 0x00);   // Changed イベントで _valueChanged=true になる

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
        /// バイト数が一致しない / 無効な文字が含まれる場合は警告を表示する。
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
                return true; // HexBox 既定の貼り付け（ASCII バイト化）を抑止
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  ② カスタムセル
    // ────────────────────────────────────────────────────────────────
    internal class DataGridViewHexBoxCell : DataGridViewTextBoxCell
    {
        public override Type EditType
        {
            get { return typeof(DataGridViewHexBoxEditingControl); }
        }

        public override Type ValueType
        {
            get { return typeof(byte[]); }
        }

        public override object DefaultNewRowValue
        {
            get { return new byte[0]; }
        }

        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);

            DataGridViewHexBoxEditingControl ctl =
                DataGridView.EditingControl as DataGridViewHexBoxEditingControl;
            if (ctl == null) return;

            byte[] bytes = Value as byte[];
            if (bytes == null) bytes = new byte[0];

            ctl.ReadOnly  = this.ReadOnly;
            ctl.BackColor = this.ReadOnly
                              ? Color.FromArgb(235, 235, 235)
                              : Color.FromArgb(255, 255, 225);

            ctl.EditingControlFormattedValue = bytes;

            int bpl = Math.Max(1, bytes.Length);
            ctl.UseFixedBytesPerLine = true;
            ctl.BytesPerLine         = bpl;
        }

        public override void PositionEditingControl(
            bool setLocation, bool setSize,
            Rectangle cellBounds, Rectangle cellClip,
            DataGridViewCellStyle cellStyle,
            bool singleVerticalBorderAdded, bool singleHorizontalBorderAdded,
            bool isFirstDisplayedColumn, bool isFirstDisplayedRow)
        {
            base.PositionEditingControl(setLocation, setSize, cellBounds, cellClip, cellStyle,
                singleVerticalBorderAdded, singleHorizontalBorderAdded,
                isFirstDisplayedColumn, isFirstDisplayedRow);

            if (DataGridView == null || DataGridView.EditingControl == null) return;
            Control ctl = DataGridView.EditingControl;

            int contentH = ctl.Font.Height + 8;
            if (ctl.Height > contentH)
            {
                int topOffset = (ctl.Height - contentH) / 2;
                ctl.SetBounds(ctl.Left, ctl.Top + topOffset, ctl.Width, contentH);
            }
        }

        public override object ParseFormattedValue(object formattedValue,
            DataGridViewCellStyle cellStyle,
            TypeConverter formattedValueTypeConverter,
            TypeConverter valueTypeConverter)
        {
            if (formattedValue is byte[]) return formattedValue;
            return new byte[0];
        }

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
