using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ESRI.ArcGIS.Geodatabase;

namespace WindowsFormsMap1
{
    public partial class FormICHDetails : Form
    {
        private IFeature _feature;

        public FormICHDetails(IFeature feature)
        {
            InitializeComponent();
            _feature = feature;
            ApplyModernStyle();
            LoadAttributes();
        }

        // [Agent Add] Added: 美化界面样式，使其更像现代卡片
        private void ApplyModernStyle()
        {
            this.BackColor = System.Drawing.Color.White;
            this.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            this.Font = new System.Drawing.Font("微软雅黑", 9F);
            this.Text = " 📜 非遗项目详情";
            this.ShowInTaskbar = false;
            this.TopMost = true;

            // DataGridView 样式
            dataGridView1.BackgroundColor = System.Drawing.Color.White;
            dataGridView1.BorderStyle = BorderStyle.None;
            dataGridView1.GridColor = System.Drawing.Color.FromArgb(240, 240, 240);
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.AlternatingRowsDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(248, 250, 252);
            dataGridView1.DefaultCellStyle.SelectionBackColor = System.Drawing.Color.FromArgb(226, 232, 240);
            dataGridView1.DefaultCellStyle.SelectionForeColor = System.Drawing.Color.Black;
            dataGridView1.ColumnHeadersDefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(241, 245, 249);
            dataGridView1.EnableHeadersVisualStyles = false;

            // 按钮样式
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
        public void AlignToSidebar(Form parentForm, Panel eaglePanel)
        {
            if (parentForm == null || eaglePanel == null) return;

            // 获取鹰眼面板在屏幕上的坐标
            System.Drawing.Point screenPoint = eaglePanel.PointToScreen(System.Drawing.Point.Empty);

            // 设置位置：右对齐鹰眼（保持 5px 边距），垂直紧贴鹰眼下方
            this.StartPosition = FormStartPosition.Manual;
            this.Left = screenPoint.X + eaglePanel.Width - this.Width;
            this.Top = screenPoint.Y + eaglePanel.Height + 5;

            // 简单防溢出检查
            var workingArea = Screen.FromControl(parentForm).WorkingArea;
            if (this.Right > workingArea.Right)
            {
                this.Left = workingArea.Right - this.Width - 10;
            }
            if (this.Bottom > workingArea.Bottom)
            {
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

        private void btnSearch_Click(object sender, EventArgs e)
        {
            try
            {
                if (_feature == null) return;

                // 尝试找名称字段，支持多种命名
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
                        // 智能判断上下文
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
                        MessageBox.Show("该要素名称为空，无法搜索。");
                    }
                }
                else
                {
                    MessageBox.Show("未找到有效的名称字段。");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开浏览器失败: " + ex.Message);
            }
        }
    }
}
