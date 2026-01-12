using System;
using System.Drawing;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;

namespace WindowsFormsMap1
{
    public partial class FormQueryResult : Form
    {
        private IFeatureLayer _layer;
        private int _count;

        private Label lblInfo;
        private Button btnExport;
        private Button btnClose;

        public FormQueryResult(IFeatureLayer layer, int count)
        {
            _layer = layer;
            _count = count;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "查询结果";
            this.Size = new Size(300, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            lblInfo = new Label { Location = new Point(20, 20), Size = new Size(250, 40), Text = $"图层: {_layer.Name}\n选中要素: {_count} 个" };
            
            btnExport = new Button { Text = "导出选中要素", Location = new Point(20, 70), Size = new Size(120, 30) };
            btnClose = new Button { Text = "关闭", Location = new Point(150, 70), Size = new Size(100, 30) };

            btnExport.Click += BtnExport_Click;
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(lblInfo);
            this.Controls.Add(btnExport);
            this.Controls.Add(btnClose);
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (_count == 0)
            {
                MessageBox.Show("没有选中要素，无法导出。", "提示");
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Shapefile (*.shp)|*.shp";
            sfd.FileName = $"{_layer.Name}_Selection.shp";

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    DataHelper.ExportSelectionToShapefile(_layer, sfd.FileName);
                    MessageBox.Show("导出成功！\n" + sfd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导出失败: " + ex.Message);
                }
            }
        }
    }
}
