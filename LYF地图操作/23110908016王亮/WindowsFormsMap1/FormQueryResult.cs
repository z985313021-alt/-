using System;
using System.Drawing;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 【查询结果反馈窗体】：提供空间搜索结果的即时摘要展示
    /// 支持将选中的要素子集一键导出为独立的物理矢量文件 (Shapefile)
    /// </summary>
    public partial class FormQueryResult : Form
    {
        private IFeatureLayer _layer; // 来源图层句柄
        private int _count; // 成功命中的要素总数

        private Label lblInfo;
        private Button btnExport;
        private Button btnClose;

        public FormQueryResult(IFeatureLayer layer, int count)
        {
            _layer = layer;
            _count = count;
            InitializeComponent();
        }

        // 【动态界面布局】：手动注入 UI 控件以实现轻量化交互
        private void InitializeComponent()
        {
            this.Text = "空间检索结果反馈";
            this.Size = new Size(320, 160);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
 
            lblInfo = new Label { 
                Location = new Point(20, 20), 
                Size = new Size(270, 45), 
                Text = $"目标图层：{_layer.Name}\n命中统计：已在地理空间中锁定 {_count} 个要素" 
            };
            
            btnExport = new Button { Text = "导出结果集 (SHP)", Location = new Point(20, 80), Size = new Size(130, 30) };
            btnClose = new Button { Text = "放弃并关闭", Location = new Point(160, 80), Size = new Size(110, 30) };
 
            btnExport.Click += BtnExport_Click;
            btnClose.Click += (s, e) => this.Close();
 
            this.Controls.Add(lblInfo);
            this.Controls.Add(btnExport);
            this.Controls.Add(btnClose);
        }

        // 【一键物理导出】：调用 DataHelper 核心组件将选择集持久化
        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (_count == 0)
            {
                MessageBox.Show("提示：结果集为空，无需执行导出操作。", "操作拦截");
                return;
            }
 
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Shapefile 矢量数据 (*.shp)|*.shp";
            sfd.FileName = $"{_layer.Name}_检索结果";
 
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // 业务逻辑透传：执行物理 IO 写入
                    DataHelper.ExportSelectionToShapefile(_layer, sfd.FileName);
                    MessageBox.Show("导出成功！\n文件完整路径：" + sfd.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("物理导出故障，请检查磁盘权限或数据源锁定状态：\n" + ex.Message);
                }
            }
        }
    }
}
