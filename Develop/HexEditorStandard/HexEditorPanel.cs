using System;
using System.Drawing;
using System.Windows.Forms;

namespace HexEditorStandard
{
    /// <summary>
    /// 標準コントロールのみで実装した 16 進数エディタパネル。
    /// OnPaint でアドレス列・Hex 列・ASCII 列を描画し、
    /// マウスクリックで選択、キー入力でバイト編集を行う。
    /// </summary>
    internal class HexEditorPanel : Control
    {
        // ── レイアウト定数 ──────────────────────────────────────────
        private const int BPL   = 16;  // bytes per line
        private const int HDR_H = 22;  // ヘッダー行の高さ (px)
        private const int PAD_L = 6;   // 左パディング (px)

        // ── 配色 ───────────────────────────────────────────────────
        private static readonly Color C_HDR_BG  = Color.FromArgb(50, 85, 145);
        private static readonly Color C_HDR_FG  = Color.White;
        private static readonly Color C_ADDR_FG = Color.FromArgb(90, 100, 180);
        private static readonly Color C_TEXT_FG = Color.FromArgb(20, 20, 20);
        private static readonly Color C_SEL_BG  = Color.FromArgb(50, 120, 200);
        private static readonly Color C_SEL_FG  = Color.White;
        private static readonly Color C_EVEN_BG = Color.White;
        private static readonly Color C_ODD_BG  = Color.FromArgb(247, 249, 255);
        private static readonly Color C_RO_FG   = Color.FromArgb(150, 150, 150);
        private static readonly Color C_SEP     = Color.LightSteelBlue;

        // ── 状態 ───────────────────────────────────────────────────
        private byte[]     _data    = new byte[0];
        private bool       _ro      = false;
        private int        _selIdx  = -1;   // 選択中バイトのインデックス
        private bool       _hiNib   = true; // true = 上位ニブル編集中
        private int        _topLine = 0;    // スクロール先頭行

        // ── レイアウト (フォント計測後に確定) ──────────────────────
        private readonly Font _font;
        private int  _cw;    // 1 文字の幅 (px)
        private int  _lh;    // 1 行の高さ (px)
        private int  _hexX;  // Hex 列の開始 X
        private int  _ascX;  // ASCII 列の開始 X

        // ── スクロールバー ─────────────────────────────────────────
        private readonly VScrollBar _scroll;

        // ── イベント ───────────────────────────────────────────────
        public event EventHandler Changed;

        // ────────────────────────────────────────────────────────────
        //  プロパティ
        // ────────────────────────────────────────────────────────────

        public byte[] Data
        {
            get { return _data; }
            set
            {
                _data    = value ?? new byte[0];
                _selIdx  = _data.Length > 0 ? 0 : -1;
                _hiNib   = true;
                _topLine = 0;
                UpdateScrollBar();
                Invalidate();
            }
        }

        public new bool ReadOnly
        {
            get { return _ro; }
            set { _ro = value; Invalidate(); }
        }

        // ────────────────────────────────────────────────────────────
        //  コンストラクタ
        // ────────────────────────────────────────────────────────────

        public HexEditorPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.UserPaint           |
                     ControlStyles.DoubleBuffer        |
                     ControlStyles.Selectable, true);
            BackColor = Color.White;
            TabStop   = true;

            _font = new Font("Consolas", 10f);
            MeasureFont();

            _scroll = new VScrollBar { Dock = DockStyle.Right };
            _scroll.Scroll += (s, e) =>
            {
                _topLine = _scroll.Value;
                Invalidate();
            };
            Controls.Add(_scroll);
        }

        // ────────────────────────────────────────────────────────────
        //  フォント計測・レイアウト計算
        // ────────────────────────────────────────────────────────────

        private void MeasureFont()
        {
            // TextRenderer で等幅フォントの 1 文字サイズを計測する
            Size sz = TextRenderer.MeasureText("0", _font, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
            _cw = Math.Max(1, sz.Width);
            _lh = Math.Max(1, sz.Height + 4);

            // アドレス列: "00000000  " = 10 文字
            _hexX = PAD_L + 10 * _cw;
            // Hex 列: "XX " × 16 = 48 文字
            // ASCII 列: 2 文字のギャップ + 16 文字
            _ascX = _hexX + 48 * _cw + 2 * _cw;
        }

        private int TotalLines   => (_data.Length + BPL - 1) / BPL;
        private int VisibleLines => Math.Max(1, (Height - HDR_H) / _lh);
        private int PanelWidth   => Width - (_scroll.Visible ? _scroll.Width : 0);

        private void UpdateScrollBar()
        {
            int total   = TotalLines;
            int visible = VisibleLines;
            _scroll.Minimum     = 0;
            _scroll.Maximum     = Math.Max(0, total - 1);
            _scroll.LargeChange = Math.Max(1, visible);
            _scroll.SmallChange = 1;
            _scroll.Visible     = total > visible;
            _topLine            = Math.Min(_topLine, Math.Max(0, total - visible));
            if (_scroll.Visible)
                _scroll.Value = Math.Min(_topLine, _scroll.Maximum);
        }

        // ────────────────────────────────────────────────────────────
        //  描画
        // ────────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            int pw = PanelWidth;

            // ── ヘッダー ──
            using (var br = new SolidBrush(C_HDR_BG))
                g.FillRectangle(br, 0, 0, pw, HDR_H);

            int hdrY = (HDR_H - _lh) / 2;
            DrawText(g, "Offset  ", C_HDR_FG, PAD_L, hdrY);
            for (int i = 0; i < BPL; i++)
                DrawText(g, i.ToString("X2"), C_HDR_FG, _hexX + i * 3 * _cw, hdrY);
            DrawText(g, "Text", C_HDR_FG, _ascX, hdrY);

            // ── データ行 ──
            int visLines = VisibleLines;
            for (int li = 0; li < visLines; li++)
            {
                int line    = _topLine + li;
                int lineOff = line * BPL;
                if (lineOff >= _data.Length) break;

                int   y     = HDR_H + li * _lh;
                Color rowBg = (line % 2 == 0) ? C_EVEN_BG : C_ODD_BG;

                // 行背景
                using (var br = new SolidBrush(rowBg))
                    g.FillRectangle(br, 0, y, pw, _lh);

                // アドレス
                DrawText(g, lineOff.ToString("X8"), C_ADDR_FG, PAD_L, y + 2);

                // Hex バイト
                for (int bi = 0; bi < BPL; bi++)
                {
                    int idx = lineOff + bi;
                    if (idx >= _data.Length) break;

                    int  hx  = _hexX + bi * 3 * _cw;
                    bool sel = (idx == _selIdx);

                    if (sel)
                        using (var br = new SolidBrush(C_SEL_BG))
                            g.FillRectangle(br, hx - 1, y, _cw * 2 + 2, _lh);

                    Color fg = sel ? C_SEL_FG : (_ro ? C_RO_FG : C_TEXT_FG);
                    DrawText(g, _data[idx].ToString("X2"), fg, hx, y + 2);
                }

                // ASCII
                for (int bi = 0; bi < BPL; bi++)
                {
                    int idx = lineOff + bi;
                    if (idx >= _data.Length) break;

                    int  ax  = _ascX + bi * _cw;
                    bool sel = (idx == _selIdx);

                    if (sel)
                        using (var br = new SolidBrush(Color.FromArgb(100, C_SEL_BG)))
                            g.FillRectangle(br, ax, y, _cw, _lh);

                    char ch = (_data[idx] >= 0x20 && _data[idx] < 0x7F) ? (char)_data[idx] : '.';
                    DrawText(g, ch.ToString(), sel ? C_SEL_FG : Color.DimGray, ax, y + 2);
                }
            }

            // ── 列区切り線 ──
            using (var pen = new Pen(C_SEP))
            {
                int sepHex = _hexX - _cw;
                int sepAsc = _ascX - _cw;
                g.DrawLine(pen, sepHex, HDR_H, sepHex, Height);
                g.DrawLine(pen, sepAsc, HDR_H, sepAsc, Height);
            }
        }

        private void DrawText(Graphics g, string text, Color color, int x, int y)
        {
            TextRenderer.DrawText(g, text, _font, new Point(x, y), color,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        // ────────────────────────────────────────────────────────────
        //  マウス操作
        // ────────────────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Y < HDR_H || _data.Length == 0) return;

            int li  = (e.Y - HDR_H) / _lh;
            int line = _topLine + li;

            // Hex 列クリック
            if (e.X >= _hexX && e.X < _ascX - _cw)
            {
                int col = (e.X - _hexX) / (_cw * 3);
                SetSelection(line * BPL + Math.Max(0, Math.Min(BPL - 1, col)));
            }
            // ASCII 列クリック
            else if (e.X >= _ascX)
            {
                int col = (e.X - _ascX) / _cw;
                SetSelection(line * BPL + Math.Max(0, Math.Min(BPL - 1, col)));
            }
        }

        private void SetSelection(int idx)
        {
            if (idx < 0 || idx >= _data.Length) return;
            _selIdx = idx;
            _hiNib  = true;
            Invalidate();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            int lines = e.Delta > 0 ? -3 : 3;
            _topLine = Math.Max(0, Math.Min(_topLine + lines,
                Math.Max(0, TotalLines - VisibleLines)));
            if (_scroll.Visible)
                _scroll.Value = Math.Min(_topLine, _scroll.Maximum);
            Invalidate();
        }

        // ────────────────────────────────────────────────────────────
        //  キーボード操作
        // ────────────────────────────────────────────────────────────

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Left: case Keys.Right:
                case Keys.Up:   case Keys.Down:
                case Keys.Home: case Keys.End:
                    return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (_selIdx < 0) return;

            switch (e.KeyCode)
            {
                case Keys.Left:
                    if (_selIdx > 0) { _selIdx--; _hiNib = true; EnsureVisible(); }
                    e.Handled = true; break;
                case Keys.Right:
                    if (_selIdx < _data.Length - 1) { _selIdx++; _hiNib = true; EnsureVisible(); }
                    e.Handled = true; break;
                case Keys.Up:
                    if (_selIdx >= BPL) { _selIdx -= BPL; EnsureVisible(); }
                    e.Handled = true; break;
                case Keys.Down:
                    if (_selIdx + BPL < _data.Length) { _selIdx += BPL; EnsureVisible(); }
                    e.Handled = true; break;
                case Keys.Home:
                    _selIdx = (_selIdx / BPL) * BPL; _hiNib = true; Invalidate();
                    e.Handled = true; break;
                case Keys.End:
                    _selIdx = Math.Min((_selIdx / BPL + 1) * BPL - 1, _data.Length - 1);
                    _hiNib = true; Invalidate();
                    e.Handled = true; break;
            }
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (_ro || _selIdx < 0) return;

            char c      = char.ToUpper(e.KeyChar);
            int  nibble = -1;
            if (c >= '0' && c <= '9')      nibble = c - '0';
            else if (c >= 'A' && c <= 'F') nibble = 10 + (c - 'A');
            if (nibble < 0) return;

            e.Handled = true;

            if (_hiNib)
            {
                _data[_selIdx] = (byte)((nibble << 4) | (_data[_selIdx] & 0x0F));
                _hiNib = false;
            }
            else
            {
                _data[_selIdx] = (byte)((_data[_selIdx] & 0xF0) | nibble);
                _hiNib = true;
                if (_selIdx < _data.Length - 1)
                {
                    _selIdx++;
                    EnsureVisible();
                }
            }

            Changed?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        // ────────────────────────────────────────────────────────────
        //  リサイズ
        // ────────────────────────────────────────────────────────────

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateScrollBar();
            Invalidate();
        }

        // ────────────────────────────────────────────────────────────
        //  スクロール補助
        // ────────────────────────────────────────────────────────────

        private void EnsureVisible()
        {
            if (_selIdx < 0) return;
            int line    = _selIdx / BPL;
            int visible = VisibleLines;
            if      (line < _topLine)          _topLine = line;
            else if (line >= _topLine + visible) _topLine = line - visible + 1;
            _topLine = Math.Max(0, _topLine);
            if (_scroll.Visible)
                _scroll.Value = Math.Min(_topLine, _scroll.Maximum);
            Invalidate();
        }
    }
}
