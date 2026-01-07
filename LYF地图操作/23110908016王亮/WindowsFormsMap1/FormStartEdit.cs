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
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geodatabase;

namespace WindowsFormsMap1
{
    public partial class FormStartEdit : Form
    {
        private AxMapControl _mapControl;
        public IFeatureLayer SelectedLayer { get; private set; }

        public FormStartEdit(AxMapControl mapControl)
        {
            InitializeComponent();
            _mapControl = mapControl;
        }

        private void FormStartEdit_Load(object sender, EventArgs e)
        {
            if (_mapControl == null) return;
            
            // 遍历图层列表并添加要素图层到下拉框
            for (int i = 0; i < _mapControl.LayerCount; i++)
            {
                ILayer layer = _mapControl.get_Layer(i);
                if (layer is IFeatureLayer)
                {
                    IFeatureLayer featureLayer = layer as IFeatureLayer;
                    // 过滤：确保其包含有效的数据集
                    if (featureLayer.FeatureClass != null) 
                    {
                        cmbLayerList.Items.Add(layer.Name);
                    }
                }
            }

            if (cmbLayerList.Items.Count > 0)
            {
                cmbLayerList.SelectedIndex = 0;
            }
            else
            {
                MessageBox.Show("当前地图没有可编辑的矢量图层。");
                this.Close();
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (cmbLayerList.SelectedItem == null) return;
            string selectedName = cmbLayerList.SelectedItem.ToString();
            
            // 遍历寻找对应的图层对象
            for (int i = 0; i < _mapControl.LayerCount; i++)
            {
                ILayer layer = _mapControl.get_Layer(i);
                if (layer.Name == selectedName && layer is IFeatureLayer)
                {
                    SelectedLayer = layer as IFeatureLayer;
                    break;
                }
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
