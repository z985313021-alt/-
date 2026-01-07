// [Agent (通用辅助)] Modified: 全量中文化注释深挖
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            this._feature = feature;
            LoadProperties();
        }

        private void LoadProperties()
        {
            try
            {
                DataTable dt = new DataTable();
                dt.Columns.Add("字段项");
                dt.Columns.Add("内容值");

                if (_feature == null) return;
                IFields fields = _feature.Fields;
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    IField field = fields.get_Field(i);
                    // 过滤掉几个不适合展示的内部字段
                    if (field.Type == esriFieldType.esriFieldTypeGeometry ||
                        field.Name.ToLower() == "shape" ||
                        field.Name.ToLower() == "fid") continue;

                    object val = _feature.get_Value(i);
                    dt.Rows.Add(field.AliasName, (val == null || val is DBNull) ? "" : val.ToString());
                }

                dataGridView1.DataSource = dt;

                // [Member A] 修改：修复当字段值为 null 时的 NullReferenceException
                // 尝试抓取名称作为标题显示
                int nameIdx = _feature.Fields.FindField("名称");
                if (nameIdx != -1)
                {
                    object nameVal = _feature.get_Value(nameIdx);
                    this.Text = "非遗详情: " + ((nameVal == null || nameVal is DBNull) ? "未知" : nameVal.ToString());
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("加载详情失败: " + ex.Message);
            }
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
                        if (fields.get_Field(i).Type == esriFieldType.esriFieldTypeString && i > 0)
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
                        string url = "https://www.baidu.com/s?wd=" + System.Uri.EscapeDataString("非遗 " + keyword);
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
