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
            LoadAttributes();
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
