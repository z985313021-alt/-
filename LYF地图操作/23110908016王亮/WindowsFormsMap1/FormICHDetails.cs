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

                IFields fields = _feature.Fields;
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    IField field = fields.get_Field(i);
                    // 过滤掉几个不适合展示的内部字段
                    if (field.Type == esriFieldType.esriFieldTypeGeometry || 
                        field.Name.ToLower() == "shape" || 
                        field.Name.ToLower() == "fid") continue;

                    object val = _feature.get_Value(i);
                    dt.Rows.Add(field.AliasName, val == null ? "" : val.ToString());
                }

                dataGridView1.DataSource = dt;
                
                // 尝试抓取名称作为标题
                int nameIdx = _feature.Fields.FindField("名称");
                if (nameIdx != -1)
                {
                    this.Text = "非遗详情: " + _feature.get_Value(nameIdx).ToString();
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
    }
}
