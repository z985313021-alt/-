using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ESRI.ArcGIS.Geodatabase;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 【非遗详情展示窗体】：点击地图点位后弹出的详细属性卡片
    /// 包含属性表格、自动布局对齐算法以及基于项目名称的联网搜索功能
    /// </summary>
    public partial class FormICHDetails : Form
    {
        private IFeature _feature; // 承载当前展示的地理要素实例

        public FormICHDetails(IFeature feature)
        {
            InitializeComponent();
            _feature = feature;
            ApplyModernStyle(); // 执行 UI 美化
            LoadAttributes();   // 加载字段数据
        }

        // [Agent Add] Added: 美化界面样式，使其更像现代卡片
        // 【UI 指标配置】：手动调整控件外观，剥离默认的 WinForms 老旧风格，营造扁平化视觉效果
        private void ApplyModernStyle()
        {
            this.BackColor = System.Drawing.Color.White;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.Text = " 📜 非遗项目详情";
            this.ShowInTaskbar = false;
            this.TopMost = true; // 确保置顶显示在地图上方

            // DataGridView 栅格样式美化
            dataGridView1.BackgroundColor = System.Drawing.Color.White;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.GridColor = System.Drawing.Color.FromArgb(240, 240, 240);
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            dataGridView1.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(226, 232, 240);
            dataGridView1.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(241, 245, 249);
            dataGridView1.EnableHeadersVisualStyles = false;

            // 现代蓝色调按钮
            btnSearch.FlatStyle = FlatStyle.Flat;
            btnSearch.BackColor = System.Drawing.Color.FromArgb(37, 99, 235);
            btnSearch.ForeColor = System.Drawing.Color.White;
            btnSearch.FlatAppearance.BorderSize = 0;
            btnSearch.Text = "🔍 联网搜索";

            btnClose.FlatStyle = FlatStyle.Flat;
            btnClose.BackColor = System.Drawing.Color.FromArgb(241, 245, 249);
            btnClose.ForeColor = System.Drawing.Color.FromArgb(71, 85, 105);
            btnClose.FlatAppearance.BorderSize = 0;
        }

        // [Agent Modified] Modified: 优化定位算法，改为右对齐鹰眼面板，确保不溢出屏幕右侧
        // 【动态对齐逻辑】：确保详情卡片始终相对于侧边栏/鹰眼视图定位，并自动处理屏幕越界溢出
        public void AlignToSidebar(Form parentForm, Panel eaglePanel)
        {
            if (parentForm == null || eaglePanel == null) return;

            // 获取控件在屏幕坐标系中的锚点
            System.Drawing.Point screenPoint = eaglePanel.PointToScreen(System.Drawing.Point.Empty);

            // 主定位逻辑：右对齐鹰眼面板，预留 5px 的间距
            this.StartPosition = FormStartPosition.Manual;
            this.Left = screenPoint.X + eaglePanel.Width - this.Width;
            this.Top = screenPoint.Y + eaglePanel.Height + 5;

            // 自动越界保护检查
            var workingArea = Screen.FromControl(parentForm).WorkingArea;
            if (this.Right > workingArea.Right)
            {
                this.Left = workingArea.Right - this.Width - 10;
            }
            if (this.Bottom > workingArea.Bottom)
            {
                // 若下方空间不足，则向上弹出显示
                this.Top = screenPoint.Y - this.Height - 5; // 如果下方放不下，放上面
            }
        }

        private void LoadAttributes()
        {
            if (_feature == null) return;

            // 创建数据源
            var dataList = new List<object>();

            IFields fields = _feature.Fields;
            for (int i = 0; i < fields.FieldCount; i++)
            {
                IField field = fields.get_Field(i);
                // 跳过Shape几何字段，显示无意义
                if (field.Type == esriFieldType.esriFieldTypeGeometry) continue;

                string fieldName = field.AliasName; // 显示别名
                object value = _feature.get_Value(i);

                // 处理一些特殊类型显示
                string valueStr = (value != null) ? value.ToString() : "";

                dataList.Add(new { 字段项 = fieldName, 内容值 = valueStr });
            }

            dataGridView1.DataSource = dataList;
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // 【语义化搜索】：智能识别项目名称字段并调用系统浏览器展示外部知识库
        private void btnSearch_Click(object sender, EventArgs e)
        {
            try
            {
                if (_feature == null) return;

                // 搜索核心字段列表（适配不同版本的要素类结构）
                string nameField = "";
                string[] possibleNames = { "名称", "Name", "Title", "项目名称", "非遗名", "ProjectName" };

                IFields fields = _feature.Fields;
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    string fName = fields.get_Field(i).Name;
                    foreach (string k in possibleNames)
                    {
                        if (fName.Equals(k, StringComparison.OrdinalIgnoreCase))
                        {
                            nameField = fName;
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(nameField)) break;
                }

                // 后备策略：若无特定名称字段，选取首个有意义的文本字段
                if (string.IsNullOrEmpty(nameField))
                {
                    // 如果没找到名称字段，尝试找索引为1或2的字符串字段作为替补
                    for (int i = 0; i < fields.FieldCount; i++)
                    {
                        if (fields.get_Field(i).Type == esriFieldType.esriFieldTypeString && i > 0 && fields.get_Field(i).Name != "Shape")
                        {
                            nameField = fields.get_Field(i).Name;
                            break;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(nameField))
                {
                    int idx = fields.FindField(nameField);
                    object val = _feature.get_Value(idx);
                    if (val != null && val != DBNull.Value)
                    {
                        string keyword = val.ToString();
                        // 智能拼接百度搜索链接，增加“山东非遗”上下文以提高匹配精度
                        string queryPrefix = "山东非遗 ";
                        if (keyword.Contains("市") || keyword.Contains("县") || keyword.Contains("区"))
                        {
                            queryPrefix = ""; // 如果是行政区名，就不强制加非遗前缀，或者加"非遗情况"
                        }

                        string url = "https://www.baidu.com/s?wd=" + System.Uri.EscapeDataString(queryPrefix + keyword);
                        System.Diagnostics.Process.Start(url);
                    }
                    else
                    {
                        MessageBox.Show("该要素名称为空，目前无法进行外部搜索。");
                    }
                }
                else
                {
                    MessageBox.Show("数据库内未找到有效的名称字段标签。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("由于系统安全限制或浏览器异常，搜索启动失败: " + ex.Message);
            }
        }
    }
}
