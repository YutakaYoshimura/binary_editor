using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using Spooksoft.HexEditor.Controls;
using Spooksoft.HexEditor.Infrastructure;

namespace HexControlComparison
{
    // ────────────────────────────────────────────────────────────────
    //  Proj04
    //  Spooksoft.HexEditor の HexEditorDisplay を IDataGridViewEditingControl
    //  でラップし DataGridView の標準編集機構に乗せる。
    //
    //  【制約】HexEditorDisplay はヘッダー行・オフセット列を隠す公開 API を
    //  持たない（独自 DrawingContext 描画のため XAML 要素も存在しない）。
    //  そのため private フィールド metrics をリフレクションで取得し、
    //  HeaderArea.Rectangle.Height と MarginArea.Rectangle.Width を読み出して
    //  Border(ClipToBounds=true) + 負の Margin でクロムを切り取る。
    //
    //  【重要】ElementHost を直接継承する。UserControl でラップすると
    //  WPF コンテンツが描画されない。
    // ────────────────────────────────────────────────────────────────

    // ① 編集コントロール
    internal class DataGridViewSpooksoftHexEditingControl : ElementHost, IDataGridViewEditingControl
    {
        HexEditorDisplay                    _hex;
        HexByteContainer                    _doc;
        System.Windows.Controls.Border      _border;
        DataGridView                        _dataGridView;
        int                                 _rowIndex;
        bool                                _valueChanged;
        bool                                _chromeHidden;

        public DataGridViewSpooksoftHexEditingControl()
        {
            _hex = new HexEditorDisplay();

            // Border で ClipToBounds を有効にし、負 Margin によるクロム隠しを可能にする
            _border = new System.Windows.Controls.Border
            {
                ClipToBounds = true,
            };
            _border.Child = _hex;

            // ElementHost に直接 WPF ツリーをセット
            Child = _border;

            // レイアウト確定後に metrics をリフレクションで取得してクロムを隠す
            _hex.LayoutUpdated += OnHexLayoutUpdated;
        }

        void OnHexLayoutUpdated(object sender, EventArgs e)
        {
            if (_chromeHidden) return;
            try
            {
                double headerH = GetMetricsValue("HeaderArea");
                double marginW = GetMetricsValue("MarginArea");
                double footerH = GetMetricsValue("FooterArea");

                // FooterArea が取得できない場合はヘッダーと同じ高さと仮定
                if (footerH <= 0) footerH = headerH;

                if (headerH <= 0 && marginW <= 0) return;

                // 負の Margin でクロム分だけ HexEditorDisplay をシフトし
                // Border の ClipToBounds でクリッピングする
                _hex.Margin = new Thickness(-marginW, -headerH, 0, -footerH);
                _chromeHidden = true;
            }
            catch { /* リフレクション失敗時は何もしない */ }
        }

        // metrics フィールドから指定 Area の矩形サイズ（Height or Width）を取得
        double GetMetricsValue(string areaName)
        {
            FieldInfo fi = typeof(HexEditorDisplay).GetField("metrics",
                BindingFlags.NonPublic | BindingFlags.Instance);
            if (fi == null) return 0;

            object metrics = fi.GetValue(_hex);
            if (metrics == null) return 0;

            // metrics.Control
            object control = GetPropValue(metrics, "Control");
            if (control == null) return 0;

            // metrics.Control.HeaderArea / MarginArea / FooterArea
            object area = GetPropValue(control, areaName);
            if (area == null) return 0;

            // area.Rectangle
            object rect = GetPropValue(area, "Rectangle");
            if (rect == null) return 0;

            // areaName が MarginArea なら Width、それ以外は Height
            string dim = areaName == "MarginArea" ? "Width" : "Height";
            object val = GetPropValue(rect, dim);
            if (val == null) return 0;

            return Convert.ToDouble(val);
        }

        static object GetPropValue(object obj, string propName)
        {
            PropertyInfo pi = obj.GetType().GetProperty(propName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return pi?.GetValue(obj);
        }

        // ── IDataGridViewEditingControl ──────────────────────────────

        public DataGridView EditingControlDataGridView
        {
            get { return _dataGridView; }
            set { _dataGridView = value; }
        }
        public int  EditingControlRowIndex     { get { return _rowIndex; }    set { _rowIndex = value; } }
        public bool EditingControlValueChanged { get { return _valueChanged; } set { _valueChanged = value; } }
        public Cursor EditingPanelCursor       { get { return Cursors.Default; } }
        public bool RepositionEditingControlOnValueChange { get { return false; } }

        public object EditingControlFormattedValue
        {
            get
            {
                if (_doc == null) return new byte[0];
                byte[] b = new byte[_doc.Size];
                for (int i = 0; i < _doc.Size; i++) b[i] = _doc.GetByte(i);
                return b;
            }
            set
            {
                byte[] bytes = value as byte[] ?? new byte[0];
                _doc = new HexByteContainer(new MemoryStream(bytes), 4096);
                _doc.Changed += (s, e) =>
                {
                    _valueChanged = true;
                    if (_dataGridView != null) _dataGridView.NotifyCurrentCellDirty(true);
                };
                _hex.IsReadOnly = false;
                _hex.Document   = _doc;

                // ドキュメントを差し替えたのでクロム再計算が必要
                _chromeHidden = false;
            }
        }

        public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context)
        {
            return EditingControlFormattedValue;
        }

        public void ApplyCellStyleToEditingControl(DataGridViewCellStyle s) { }

        public bool EditingControlWantsInputKey(Keys keyData, bool dataGridViewWantsInputKey)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up:
                case Keys.Down: return false;
                default:        return true;
            }
        }

        public void PrepareEditingControlForEdit(bool selectAll) { }
    }

    // ② カスタムセル
    internal class DataGridViewSpooksoftHexCell : DataGridViewTextBoxCell
    {
        public override Type EditType           { get { return typeof(DataGridViewSpooksoftHexEditingControl); } }
        public override Type ValueType          { get { return typeof(byte[]); } }
        public override object DefaultNewRowValue { get { return new byte[0]; } }

        public override void InitializeEditingControl(int rowIndex, object initialFormattedValue,
            DataGridViewCellStyle dataGridViewCellStyle)
        {
            base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
            var ctl = DataGridView.EditingControl as DataGridViewSpooksoftHexEditingControl;
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
    internal class DataGridViewSpooksoftHexColumn : DataGridViewColumn
    {
        public DataGridViewSpooksoftHexColumn() : base(new DataGridViewSpooksoftHexCell())
        {
            SortMode = DataGridViewColumnSortMode.NotSortable;
        }

        public override DataGridViewCell CellTemplate
        {
            get { return base.CellTemplate; }
            set
            {
                if (value != null && !(value is DataGridViewSpooksoftHexCell))
                    throw new InvalidCastException("DataGridViewSpooksoftHexCell を指定してください。");
                base.CellTemplate = value;
            }
        }
    }
}
