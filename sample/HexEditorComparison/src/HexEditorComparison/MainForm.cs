using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace HexEditorComparison
{
    /// <summary>
    /// バイナリエディタパッケージ比較用メインフォーム
    /// タブごとに各パッケージの動作確認ができます
    /// </summary>
    public class MainForm : Form
    {
        private TabControl tabControl = null!;
        private StatusStrip statusStrip = null!;
        private ToolStripStatusLabel statusLabel = null!;

        // サンプルファイルのベースディレクトリ
        private readonly string SamplesDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "samples");

        public MainForm()
        {
            InitializeComponent();
            SetupTabs();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            // フォーム設定
            this.Text = "バイナリエディタ パッケージ比較ツール";
            this.Size = new Size(1100, 750);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 245, 245);

            // ステータスバー
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel("ファイルを開くか、サンプルを選択してください");
            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusStrip.Items.Add(statusLabel);
            this.Controls.Add(statusStrip);

            // タブコントロール
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.Font = new Font("Meiryo UI", 9f);
            this.Controls.Add(tabControl);

            this.ResumeLayout(false);
        }

        private void SetupTabs()
        {
            // ① Be.Windows.Forms.HexBox タブ
            var tabHexBox = new TabPage("① Be.HexBox (WinForms)");
            tabHexBox.Controls.Add(new BeHexBoxPanel(SamplesDir, UpdateStatus));
            tabControl.TabPages.Add(tabHexBox);

            // ② WPFHexaEditor タブ（WinForms Host経由）
            var tabWpf = new TabPage("② WPFHexaEditor (WPF→WinForms)");
            tabWpf.Controls.Add(new WpfHexEditorPanel(SamplesDir, UpdateStatus));
            tabControl.TabPages.Add(tabWpf);

            // ③ パッケージ情報・比較タブ
            var tabInfo = new TabPage("📋 パッケージ比較情報");
            tabInfo.Controls.Add(new PackageInfoPanel());
            tabControl.TabPages.Add(tabInfo);

            // ④ サンプルファイル説明タブ
            var tabSamples = new TabPage("📂 サンプルファイル一覧");
            tabSamples.Controls.Add(new SampleFilesPanel(SamplesDir));
            tabControl.TabPages.Add(tabSamples);
        }

        private void UpdateStatus(string message)
        {
            if (statusLabel.GetCurrentParent()?.InvokeRequired == true)
                statusLabel.GetCurrentParent().Invoke(new Action(() => statusLabel.Text = message));
            else
                statusLabel.Text = message;
        }
    }
}
