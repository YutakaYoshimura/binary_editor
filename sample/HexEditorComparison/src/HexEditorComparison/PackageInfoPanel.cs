using System;
using System.Drawing;
using System.Windows.Forms;

namespace HexEditorComparison
{
    /// <summary>
    /// 各パッケージの比較情報を表示するパネル
    /// </summary>
    public class PackageInfoPanel : UserControl
    {
        public PackageInfoPanel()
        {
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            var scroll = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            var layout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Padding = new Padding(20),
                Width = 1060
            };

            // タイトル
            layout.Controls.Add(MakeTitle("📋 バイナリエディタ NuGetパッケージ 比較情報"));
            layout.Controls.Add(MakeSeparator());

            // パッケージ1
            layout.Controls.Add(MakePackageCard(
                "① Be.Windows.Forms.HexBox",
                "NuGet: Be.Windows.Forms.HexBox  |  ライセンス: MIT  |  種別: UIコントロール (WinForms専用)",
                new[]
                {
                    ("有償/無償",     "無償（MIT）"),
                    ("商用利用",     "✅ 可能"),
                    ("WinForms対応", "✅ ネイティブ対応"),
                    ("WPF対応",      "❌ 非対応（ラッパー作成は可能）"),
                    ("更新状況",     "❌ 2014年で更新停止"),
                    ("編集機能",     "✅ 上書き・挿入・削除"),
                    ("検索/置換",    "✅ FindFirst / FindNext"),
                    ("差分比較",     "❌ なし"),
                    ("Undo/Redo",   "△ 限定的"),
                    ("大容量対応",   "✅ ストリームベースで無制限"),
                },
                Color.FromArgb(240, 245, 255),
                "WinFormsに最もシンプルに組み込める。情報・実績が豊富だが、2014年から更新がなく高度な機能は持たない。"
            ));

            layout.Controls.Add(MakeSeparator());

            // パッケージ2
            layout.Controls.Add(MakePackageCard(
                "② WPFHexaEditor",
                "NuGet: WPFHexaEditor  |  ライセンス: MIT  |  種別: UIコントロール (WPF/WinForms両対応)",
                new[]
                {
                    ("有償/無償",     "無償（MIT）"),
                    ("商用利用",     "✅ 可能（明示的に記載あり）"),
                    ("WinForms対応", "✅ ElementHost経由で対応"),
                    ("WPF対応",      "✅ ネイティブ対応"),
                    ("更新状況",     "✅ 継続更新中"),
                    ("編集機能",     "✅ 挿入・上書き・削除すべて対応"),
                    ("検索/置換",    "✅ FindReplaceService（並列検索）"),
                    ("差分比較",     "✅ BinaryFilesDifference対応"),
                    ("Undo/Redo",   "✅ UndoRedoService"),
                    ("大容量対応",   "✅ MemoryMappedFile対応"),
                },
                Color.FromArgb(240, 255, 240),
                "機能・更新状況・ドキュメント量のバランスが最も良い。WinFormsでも使えるが元々WPF向けのためElementHost経由になる。"
            ));

            layout.Controls.Add(MakeSeparator());

            // パッケージ3
            layout.Controls.Add(MakePackageCard(
                "③ HexEditor.Wpf",
                "NuGet: HexEditor.Wpf  |  ライセンス: MIT  |  種別: UIコントロール (WPF/WinForms両対応)",
                new[]
                {
                    ("有償/無償",     "無償（MIT）"),
                    ("商用利用",     "✅ 可能"),
                    ("WinForms対応", "✅ ElementHost経由で対応"),
                    ("WPF対応",      "✅ ネイティブ対応"),
                    ("更新状況",     "✅ 更新中"),
                    ("備考",        "WPFHexaEditorと実質同一コードベースの別パッケージ"),
                },
                Color.FromArgb(245, 255, 245),
                "WPFHexaEditorと同一コードベース。機能は②と同等。パッケージ名を分けたい場合の選択肢。"
            ));

            layout.Controls.Add(MakeSeparator());

            // その他パッケージ一覧
            layout.Controls.Add(MakeTitle("その他パッケージ（参考）"));

            var otherData = new[]
            {
                new { Name="④ HexView.Wpf",         License="MIT",  Commercial="✅", WinForms="❌", Edit="△ 表示特化", Search="❌", Diff="❌", Update="△" },
                new { Name="⑤ Spooksoft.HexEditor",  License="不明", Commercial="△要確認", WinForms="❌", Edit="✅", Search="△", Diff="❌", Update="△" },
                new { Name="⑥ AvaloniaHex",          License="MIT",  Commercial="✅", WinForms="❌(Avalonia専用)", Edit="✅", Search="△", Diff="❌", Update="✅実験的" },
                new { Name="⑦ HexIO",                License="MIT",  Commercial="✅", WinForms="❌(UIなし)", Edit="△", Search="❌", Diff="❌", Update="✅" },
                new { Name="⑧ ByteViewer(.NET標準)",  License=".NET", Commercial="✅", WinForms="✅", Edit="❌ 閲覧専用", Search="❌", Diff="❌", Update="△" },
            };

            var dgv = new DataGridView
            {
                Width = 1020,
                Height = 160,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,
                Font = new Font("Meiryo UI", 8.5f)
            };
            dgv.Columns.Add("name", "パッケージ名");
            dgv.Columns.Add("commercial", "商用利用");
            dgv.Columns.Add("winforms", "WinForms");
            dgv.Columns.Add("edit", "編集機能");
            dgv.Columns.Add("search", "検索/置換");
            dgv.Columns.Add("diff", "差分比較");
            dgv.Columns.Add("update", "更新状況");

            foreach (var p in otherData)
                dgv.Rows.Add(p.Name, p.Commercial, p.WinForms, p.Edit, p.Search, p.Diff, p.Update);

            layout.Controls.Add(dgv);

            // 有償パッケージ注記
            layout.Controls.Add(MakeSeparator());
            layout.Controls.Add(MakeNote(
                "⚠️  有償パッケージについて",
                "バイナリ（Hex）エディタに特化した有償NuGetパッケージは現時点でほぼ存在しません。\n" +
                "Telerik・Syncfusion・DevExpressなどの大手商用ベンダーもHexエディタコントロールは提供していません。\n" +
                "（SyncfusionにはユーザーからHexエディタ追加のリクエストが上がっている状態です）\n\n" +
                "この分野はOSS中心で市場が成立しており、商用プロジェクトでも上記の無償OSSパッケージを使用するのが一般的です。\n" +
                "商用利用可能なMITライセンスのパッケージを選択すれば問題ありません。"
            ));

            scroll.Controls.Add(layout);
            this.Controls.Add(scroll);
        }

        private Label MakeTitle(string text)
        {
            return new Label
            {
                Text = text,
                Font = new Font("Meiryo UI", 13f, FontStyle.Bold),
                AutoSize = true,
                ForeColor = Color.FromArgb(30, 60, 100),
                Margin = new Padding(0, 8, 0, 8)
            };
        }

        private Panel MakeSeparator()
        {
            return new Panel
            {
                Height = 1,
                Width = 1020,
                BackColor = Color.FromArgb(200, 210, 220),
                Margin = new Padding(0, 8, 0, 8)
            };
        }

        private Panel MakePackageCard(string title, string subtitle, (string Key, string Value)[] items, Color bgColor, string summary)
        {
            var card = new Panel
            {
                Width = 1020,
                AutoSize = true,
                BackColor = bgColor,
                Padding = new Padding(12),
                Margin = new Padding(0, 4, 0, 4),
                BorderStyle = BorderStyle.FixedSingle
            };

            var layout = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoSize = true,
                Width = 990
            };

            layout.Controls.Add(new Label
            {
                Text = title,
                Font = new Font("Meiryo UI", 11f, FontStyle.Bold),
                AutoSize = true,
                ForeColor = Color.FromArgb(20, 50, 90)
            });
            layout.Controls.Add(new Label
            {
                Text = subtitle,
                Font = new Font("Consolas", 8.5f),
                AutoSize = true,
                ForeColor = Color.Gray
            });

            var grid = new TableLayoutPanel
            {
                ColumnCount = 4,
                AutoSize = true,
                Width = 990,
                Margin = new Padding(0, 4, 0, 4)
            };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));

            for (int i = 0; i < items.Length; i++)
            {
                grid.Controls.Add(new Label { Text = items[i].Key + ":", AutoSize = true, Font = new Font("Meiryo UI", 8.5f, FontStyle.Bold), ForeColor = Color.DimGray });
                grid.Controls.Add(new Label { Text = items[i].Value, AutoSize = true, Font = new Font("Meiryo UI", 8.5f) });
            }

            layout.Controls.Add(grid);
            layout.Controls.Add(new Label
            {
                Text = "📝 " + summary,
                Font = new Font("Meiryo UI", 8.5f, FontStyle.Italic),
                AutoSize = true,
                ForeColor = Color.FromArgb(60, 80, 60),
                Width = 980
            });

            card.Controls.Add(layout);
            return card;
        }

        private Panel MakeNote(string title, string body)
        {
            var panel = new Panel
            {
                Width = 1020,
                AutoSize = true,
                BackColor = Color.FromArgb(255, 250, 230),
                Padding = new Padding(12),
                BorderStyle = BorderStyle.FixedSingle
            };
            var layout = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Width = 990 };
            layout.Controls.Add(new Label { Text = title, Font = new Font("Meiryo UI", 10f, FontStyle.Bold), AutoSize = true, ForeColor = Color.FromArgb(140, 90, 0) });
            layout.Controls.Add(new Label { Text = body, Font = new Font("Meiryo UI", 8.5f), AutoSize = true, Width = 980 });
            panel.Controls.Add(layout);
            return panel;
        }
    }
}
