using System;
using System.Collections.Generic;

namespace Be.Windows.Forms
{
    /// <summary>
    /// 最大バイト数（行数 × BytesPerLine）で入力を制限する ByteProvider。
    /// HexBox.cs / DynamicByteProvider.cs の改修不要。
    /// DynamicByteProvider を継承し、SupportsInsertBytes() で上限チェックを行う。
    /// </summary>
    /// <example>
    /// // 1行16バイト × 4行 = 最大64バイトに制限する例
    /// var provider = new LimitedByteProvider(new byte[0], maxLines: 4, bytesPerLine: 16);
    /// hexBox.BytesPerLine = 16;
    /// hexBox.UseFixedBytesPerLine = true;
    /// hexBox.ByteProvider = provider;
    /// </example>
    public class LimitedByteProvider : DynamicByteProvider
    {
        /// <summary>
        /// 許容する最大バイト数（maxLines × bytesPerLine）
        /// </summary>
        private readonly long _maxBytes;

        /// <summary>
        /// LimitedByteProvider のコンストラクタ。
        /// </summary>
        /// <param name="data">初期バイトデータ</param>
        /// <param name="maxLines">最大行数</param>
        /// <param name="bytesPerLine">1行あたりのバイト数（HexBox.BytesPerLine と合わせること）</param>
        public LimitedByteProvider(byte[] data, int maxLines, int bytesPerLine)
            : base(data)
        {
            if (maxLines <= 0) throw new ArgumentOutOfRangeException("maxLines", "maxLines は 1 以上を指定してください。");
            if (bytesPerLine <= 0) throw new ArgumentOutOfRangeException("bytesPerLine", "bytesPerLine は 1 以上を指定してください。");
            _maxBytes = (long)maxLines * bytesPerLine;
        }

        /// <summary>
        /// LimitedByteProvider のコンストラクタ（List版）。
        /// </summary>
        /// <param name="bytes">初期バイトリスト</param>
        /// <param name="maxLines">最大行数</param>
        /// <param name="bytesPerLine">1行あたりのバイト数</param>
        public LimitedByteProvider(List<byte> bytes, int maxLines, int bytesPerLine)
            : base(bytes)
        {
            if (maxLines <= 0) throw new ArgumentOutOfRangeException("maxLines", "maxLines は 1 以上を指定してください。");
            if (bytesPerLine <= 0) throw new ArgumentOutOfRangeException("bytesPerLine", "bytesPerLine は 1 以上を指定してください。");
            _maxBytes = (long)maxLines * bytesPerLine;
        }

        /// <summary>
        /// 許容する最大バイト数を返します。
        /// </summary>
        public long MaxBytes
        {
            get { return _maxBytes; }
        }

        /// <summary>
        /// 現在のバイト数が上限に達していない場合のみ true を返します。
        /// HexBox はこのメソッドが false を返すと挿入入力を自動的に拒否します。
        /// </summary>
        public override bool SupportsInsertBytes()
        {
            return Length < _maxBytes;
        }
    }
}
