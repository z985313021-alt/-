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
using ESRI.ArcGIS.Geodatabase; 
using ESRI.ArcGIS.Geometry;

namespace WindowsFormsMap1
{
    public partial class FormSelectByLocation : Form
    {
        public IMap CurrentMap { get; set; }
        public FormSelectByLocation()
        {
            InitializeComponent();
        }

        private void FormSelectByLocation_Load(object sender, EventArgs e)
        {
            // 检查CurrentMap是否为空，防止主窗体没有正确传递地图对象
            if (this.CurrentMap == null) return;

           
            checkedListBoxTargetLayers.Items.Clear();
            comboBoxSourceLayer.Items.Clear();

            string layerName; 

            for (int i = 0; i < this.CurrentMap.LayerCount; i++)
            {
                ILayer layer = this.CurrentMap.get_Layer(i);

                if (layer is IGroupLayer)
                {
                    ICompositeLayer compositeLayer = layer as ICompositeLayer;
                    for (int j = 0; j < compositeLayer.Count; j++)
                    {
                        ILayer subLayer = compositeLayer.get_Layer(j);
                        if (subLayer is IFeatureLayer)
                        {
                            layerName = subLayer.Name;
                            checkedListBoxTargetLayers.Items.Add(layerName);
                            comboBoxSourceLayer.Items.Add(layerName);
                        }
                    }
                }
                else
                {
                    // 如果不是图层组，直接添加
                    // 确保是要素图层才添加
                    if (layer is IFeatureLayer)
                    {
                        layerName = layer.Name;
                        checkedListBoxTargetLayers.Items.Add(layerName);
                        comboBoxSourceLayer.Items.Add(layerName);
                    }
                }
            }

            // 设置默认选中的项
            if (comboBoxSourceLayer.Items.Count > 0)
            {
                comboBoxSourceLayer.SelectedIndex = 0;
            }
            if (comboBoxMethods.Items.Count > 0)
            {
                comboBoxMethods.SelectedIndex = 0;
            }
        }

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void buttonClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void SelectFeaturesBySpatial()
        {

            List<string> targetLayerNames = new List<string>();
            foreach (var item in checkedListBoxTargetLayers.CheckedItems)
            {
                targetLayerNames.Add(item.ToString());
            }

            // 如果没有选择任何目标图层，则提示并退出
            if (targetLayerNames.Count == 0)
            {
                MessageBox.Show("请至少选择一个目标图层！");
                return;
            }

            // 获取源图层
            IFeatureLayer sourceFeatureLayer = GetFeatureLayerByName(this.CurrentMap, comboBoxSourceLayer.SelectedItem.ToString());
            if (sourceFeatureLayer == null)
            {
                MessageBox.Show("未找到源图层！");
                return;
            }

            // --- 2. 创建空间过滤器 (ISpatialFilter) ---

            ISpatialFilter spatialFilter = new SpatialFilterClass();

            spatialFilter.Geometry = sourceFeatureLayer.AreaOfInterest;
            spatialFilter.GeometryField = sourceFeatureLayer.FeatureClass.ShapeFieldName;

            switch (comboBoxMethods.SelectedIndex)
            {
                case 0: // 相交 (intersect)
                    spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                    break;
                case 1: // 在一定距离内 (within a distance)
                    spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelWithin;

                    break;
                case 2: // 包含 (contain)
                    spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelContains;
                    break;
                case 3: // 在...之内 (within)
                    spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelWithin;
                    break;
                case 4: // 边界相接 (touch)
                    spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelTouches;
                    break;
                case 5: // 被...穿过 (cross)
                    spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelCrosses;
                    break;
            }

            this.CurrentMap.ClearSelection();

            // 遍历所有被勾选的目标图层
            foreach (string name in targetLayerNames)
            {
                IFeatureLayer targetFeatureLayer = GetFeatureLayerByName(this.CurrentMap, name);
                if (targetFeatureLayer != null)
                {
                    // 获取目标图层的要素类
                    IFeatureClass featureClass = targetFeatureLayer.FeatureClass;
                    // 使用过滤器选择要素
                    IFeatureSelection featureSelection = targetFeatureLayer as IFeatureSelection;
                    featureSelection.SelectFeatures(spatialFilter, esriSelectionResultEnum.esriSelectionResultNew, false);
                }
            }

            // --- 5. 刷新地图 ---

            // 刷新地图以高亮显示新选择的要素
            IActiveView activeView = this.CurrentMap as IActiveView;
            activeView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
        }

    
        private IFeatureLayer GetFeatureLayerByName(IMap map, string layerName)
        {
            for (int i = 0; i < map.LayerCount; i++)
            {
                ILayer layer = map.get_Layer(i);

                if (layer is IGroupLayer) // 如果是图层组
                {
                    ICompositeLayer compositeLayer = layer as ICompositeLayer;
                    for (int j = 0; j < compositeLayer.Count; j++)
                    {
                        ILayer subLayer = compositeLayer.get_Layer(j);
                        if (subLayer.Name.Equals(layerName) && subLayer is IFeatureLayer)
                        {
                            return subLayer as IFeatureLayer;
                        }
                    }
                }
                else // 如果是普通图层
                {
                    if (layer.Name.Equals(layerName) && layer is IFeatureLayer)
                    {
                        return layer as IFeatureLayer;
                    }
                }
            }
            return null; // 未找到
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            try
            {
                SelectFeaturesBySpatial();
                this.Close(); // 执行查询后关闭窗体
            }
            catch (Exception ex)
            {
                MessageBox.Show("查询时发生错误: " + ex.Message);
            }
        }

        private void buttonApply_Click(object sender, EventArgs e)
        {
            try
            {
                SelectFeaturesBySpatial();
            }
            catch (Exception ex)
            {
                MessageBox.Show("查询时发生错误: " + ex.Message);
            }
        }
    }
}
