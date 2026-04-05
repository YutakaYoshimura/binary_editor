using System;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace HexEditorStandard
{
    // ════════════════════════════════════════════════════════════════
    //  DataGridView に TextBox ベースの Hex 入力セルを埋め込む
    //
    //  構成:
    //    DataGridViewHexTextEditingControl  … TextBox を継承した編集コントロール
    //    DataGridViewHexTextCell            … DataGridViewTextBoxCell を継承したセル
    //    DataGridViewHexTextColumn          … DataGridViewColumn を継承した列
    // ════════════════════════════════════════════════════════════════

    // ────────────────────────────────────────────────────────────────
    //  ① 編集コントロール (TextBox + IDataGridViewEditingControl)
    // ────────────────────────────────────────────────────────────────
    internal class DataGridViewHexTextEditingControl : TextBox, IDataGridViewEditingControl
    {
        private DataGridView _dgv;
        private int          _rowIndex;
        private bool         _valueChanged;

        public DataGridViewHexTextEditingControl()
        {
            Font            = new Font("Consolas", 10f);
            CharacterCasing = CharacterCasing.Upper;
            BorderStyle     = BorderStyle.None;
            TextChanged    += (s, e) =>
            {
                _valueChanged = true;
                if (_dgv != null) _dgv.NotifyCurrentCellDirty(true);
            };
        }

        // ── IDataGridViewEditingControl ──────────────────────────

        public DataGridView EditingControlDataGridView
        {
            get { return _dgv; }
            set { _dgv = value; }
        }

        /// <summary>TextBox のテキスト（hex 文字列）をそのまま FormattedValue として返す</summary>
        public object EditingControlFormattedValue
        {
            get { return Text; }
            set { Text = value as string ?? string.Empty; }
        }

        public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context)
        {
            return EditingControlFormattedValue;
        }

        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle dataGridViewCellStyle)
        {
            BackColor = dataGridViewCellStyle.BackColor;
            ForeColor = dataGridViewCellStyle.ForeColor;
        }

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

        public Cursor EditingPanelCursor   { get { return Cursors.IBeam; } }
        public bool RepositionEditingControlOnValueChange { get { return false; } }

        public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Home:
                case Keys.End:
                case Keys.Back:
                case Keys.Delete:
                    return true;   // TextBox 内で処理
                case Keys.Up:
                case Keys.Down:
                    return false;  // DataGridView で行移動
                default:
                    return !dataGridViewWantsInputKey;
            }
        }

        public void PrepareEditingControlForEdit(bool selectAll)
        {
            if (selectAll) SelectAll();
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  ② カスタムセル
    // ────────────────────────────────────────────────────────────────
    internal class DataGridViewHexTextCell : DataGridViewTextBoxCell
    {
        public override Type EditType  { get { return typeof(DataGridViewHexTextEditingControl); } }
        public override Type ValueType { get { return typeof(byte[]); } }
        public override object DefaultNewRowValue { get { return new byte[0]; } }

        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);

            DataGridViewHexTextEditingControl ctl =
                DataGridView.EditingControl as DataGridViewHexTextEditingControl;
            if (ctl == null) return;

            byte[] bytes = Value as byte[];
            ctl.Text      = bytes != null ? BytesToHex(bytes) : string.Empty;
            ctl.ReadOnly  = this.ReadOnly;
            ctl.BackColor = this.ReadOnly
                ? Color.FromArgb(235, 235, 235)
                : Color.FromArgb(255, 255, 225);
            ctl.SelectAll();
        }

        /// <summary>編集確定時: hex 文字列 → byte[] に変換してセル値として返す</summary>
        public override object ParseFormattedValue(object formattedValue,
            DataGridViewCellStyle cellStyle,
            TypeConverter formattedValueTypeConverter,
            TypeConverter valueTypeConverter)
        {
            string  hex      = formattedValue as string ?? string.Empty;
            byte[]  existing = Value as byte[];
            int     expected = existing != null ? existing.Length : 0;
            byte[]  parsed   = HexToBytes(hex, expected);
            // パース失敗時は元の値を維持する
            return parsed ?? existing ?? new byte[0];
        }

        /// <summary>非編集時の表示: byte[] → "XX XX XX" 文字列</summary>
        protected override object GetFormattedValue(object value, int rowIndex,
            ref DataGridViewCellStyle cellStyle,
            TypeConverter valueTypeConverter,
            TypeConverter formattedValueTypeConverter,
            DataGridViewDataErrorContexts context)
        {
            byte[] bytes = value as byte[];
            if (bytes == null || bytes.Length == 0) return string.Empty;
            return BytesToHex(bytes);
        }

        // ── 変換ヘルパー ────────────────────────────────────────────

        internal static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// "XX XX XX" または "XXXXXX" 形式の文字列を byte[] に変換する。
        /// 長さが一致しない場合や無効な文字が含まれる場合は null を返す。
        /// </summary>
        internal static byte[] HexToBytes(string hex, int expectedLength)
        {
            if (hex == null) return null;
            hex = hex.Replace(" ", "").Trim();
            if (hex.Length != expectedLength * 2) return null;

            byte[] result = new byte[expectedLength];
            for (int i = 0; i < expectedLength; i++)
            {
                if (!byte.TryParse(hex.Substring(i * 2, 2),
                    NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result[i]))
                    return null;
            }
            return result;
        }
    }

    // ────────────────────────────────────────────────────────────────
    //  ③ カスタム列
    // ────────────────────────────────────────────────────────────────
    internal class DataGridViewHexTextColumn : DataGridViewColumn
    {
        public DataGridViewHexTextColumn() : base(new DataGridViewHexTextCell())
        {
            SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        public override DataGridViewCell CellTemplate
        {
            get { return base.CellTemplate; }
            set
            {
                if (value != null && !(value is DataGridViewHexTextCell))
                    throw new InvalidCastException(
                        "CellTemplate には DataGridViewHexTextCell を指定してください。");
                base.CellTemplate = value;
            }
        }
    }
}
