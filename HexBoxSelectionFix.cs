// リフレクション用キャッシュ（初回のみ取得）
private PropertyInfo _bpiIndexProperty;
private FieldInfo    _bpiStartField;
private FieldInfo    _keyInterpreterField;

private long GetAnchorPos()
{
    try
    {
        if (_keyInterpreterField == null)
        {
            _keyInterpreterField = typeof(HexBox).GetField("_keyInterpreter", BindingFlags.NonPublic | BindingFlags.Instance);
            var kiType = Array.Find(typeof(HexBox).GetNestedTypes(BindingFlags.NonPublic), t => t.Name == "KeyInterpreter");
            _bpiStartField    = kiType.GetField("_bpiStart", BindingFlags.NonPublic | BindingFlags.Instance);
            _bpiIndexProperty = _bpiStartField.FieldType.GetProperty("Index");
        }
        return (long)_bpiIndexProperty.GetValue(_bpiStartField.GetValue(_keyInterpreterField.GetValue(hexBox1)));
    }
    catch { return -1; }
}

private void hexBox1_KeyDown(object sender, KeyEventArgs e)
{
    if (e.KeyCode != Keys.Up || !e.Shift) return;

    long anchor  = GetAnchorPos();
    long newSelEnd = (hexBox1.SelectionStart + hexBox1.SelectionLength) - hexBox1.BytesPerLine;

    // バグ発動条件以外はOSSに移譲
    if (!(hexBox1.SelectionStart - hexBox1.BytesPerLine < 0 && hexBox1.SelectionStart <= anchor && hexBox1.SelectionLength > 0)) return;

    e.Handled = true;
    e.SuppressKeyPress = true;

    if (newSelEnd < anchor)
    {
        // アンカーを超える → 選択反転
        hexBox1.SelectionStart  = Math.Max(0, newSelEnd);
        hexBox1.SelectionLength = anchor - Math.Max(0, newSelEnd);
    }
    else
    {
        // 通常縮小（newSelEnd == anchor の場合は SelectionLength が 0 になり選択解除）
        hexBox1.SelectionLength = newSelEnd - hexBox1.SelectionStart;
    }

    hexBox1.Invalidate();
}
