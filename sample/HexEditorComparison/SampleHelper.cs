using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace HexEditorSample
{
    /// <summary>
    /// 各サンプルプロジェクト共通のヘルパークラス
    /// </summary>
    public static class SampleHelper
    {
        /// <summary>
        /// samplesディレクトリのパスを返す
        /// 実行ファイルと同じ階層にある samples フォルダを参照
        /// </summary>
        public static string SamplesDir =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples");

        /// <summary>
        /// サンプルファイルのフルパスを返す
        /// </summary>
        public static string SamplePath(string filename) =>
            Path.Combine(SamplesDir, filename);

        /// <summary>
        /// バイト配列をHEXダンプ文字列に変換する
        /// </summary>
        public static string ToHexDump(byte[] bytes, int maxBytes = 512)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Size: {bytes.Length} bytes");
            sb.AppendLine(new string('─', 68));
            sb.AppendLine("Offset   00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F  ASCII");
            sb.AppendLine(new string('─', 68));

            int limit = Math.Min(bytes.Length, maxBytes);
            for (int i = 0; i < limit; i += 16)
            {
                sb.Append($"{i:X8}  ");
                var ascii = new StringBuilder();
                for (int j = 0; j < 16; j++)
                {
                    if (i + j < limit)
                    {
                        sb.Append($"{bytes[i + j]:X2} ");
                        ascii.Append(bytes[i + j] >= 0x20 && bytes[i + j] < 0x7F
                            ? (char)bytes[i + j] : '.');
                    }
                    else { sb.Append("   "); ascii.Append(' '); }
                    if (j == 7) sb.Append(' ');
                }
                sb.AppendLine($" {ascii}");
            }
            if (bytes.Length > maxBytes)
                sb.AppendLine($"... (以下 {bytes.Length - maxBytes} bytes 省略)");
            return sb.ToString();
        }

        /// <summary>
        /// 統一スタイルのToolStripStatusLabelを持つStatusStripを生成する
        /// </summary>
        public static (StatusStrip strip, ToolStripStatusLabel label) CreateStatusBar()
        {
            var strip = new StatusStrip();
            var label = new ToolStripStatusLabel("ファイルを開くか、サンプルボタンを押してください")
            {
                Spring = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            strip.Items.Add(label);
            return (strip, label);
        }

        /// <summary>
        /// サンプルファイル選択ボタン群を生成する
        /// </summary>
        public static FlowLayoutPanel CreateSampleButtons(Action<string> onLoad)
        {
            var panel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                AutoSize = true
            };

            var samples = new[]
            {
                ("simple.bin",           "simple.bin"),
                ("structured.bin",       "structured.bin"),
                ("text_and_binary.bin",  "text+bin"),
                ("diff_base.bin",        "diff_base.bin"),
                ("diff_modified.bin",    "diff_modified.bin"),
            };

            var lbl = new Label
            {
                Text = "サンプル:",
                AutoSize = true,
                Height = 28,
                TextAlign = ContentAlignment.MiddleLeft
            };
            panel.Controls.Add(lbl);

            foreach (var (file, caption) in samples)
            {
                var f = file; // closure
                var btn = new Button
                {
                    Text = caption,
                    AutoSize = true,
                    Height = 28,
                    Margin = new Padding(2),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.White
                };
                btn.Click += (s, e) => onLoad(SamplePath(f));
                panel.Controls.Add(btn);
            }
            return panel;
        }

        /// <summary>
        /// ファイルを開くダイアログを表示してパスを返す
        /// </summary>
        public static string? BrowseFile()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "バイナリファイルを選択",
                Filter = "すべてのファイル (*.*)|*.*|バイナリファイル (*.bin)|*.bin"
            };
            return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
        }

        /// <summary>
        /// ファイル保存ダイアログを表示してパスを返す
        /// </summary>
        public static string? BrowseSaveFile()
        {
            using var dlg = new SaveFileDialog
            {
                Title = "保存先を選択",
                Filter = "バイナリファイル (*.bin)|*.bin|すべてのファイル (*.*)|*.*"
            };
            return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
        }
    }
}
