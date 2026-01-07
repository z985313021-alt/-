using System;
using System.Drawing;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.esriSystem; // 必须引用，用于颜色转换
using stdole;

namespace WindowsFormsMap1
{
    public partial class FormLabeling : Form
    {
        // 接收主窗体的 Map 对象
        public IMap CurrentMap { get; set; }

        // 当前选中的图层
        private IGeoFeatureLayer _currentGeoLayer;

        // 存储用户选择的颜色，默认为黑色
        private Color _selectedColor = Color.Black;

        public FormLabeling()
        {
            InitializeComponent();
        }

        // 1. 窗体加载：初始化图层列表
        private void FormLabeling_Load(object sender, EventArgs e)
        {
            if (CurrentMap == null) return;

            cmbLayers.Items.Clear();
            for (int i = 0; i < CurrentMap.LayerCount; i++)
            {
                ILayer layer = CurrentMap.get_Layer(i);
                // 只有 GeoFeatureLayer 才能进行动态标注
                if (layer is IGeoFeatureLayer)
                {
                    cmbLayers.Items.Add(layer.Name);
                }
            }
            if (cmbLayers.Items.Count > 0) cmbLayers.SelectedIndex = 0;

            // 设置字号默认值
            nudFontSize.Value = 12;
        }

        // 2. 图层改变：加载该图层的字段
        private void cmbLayers_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbLayers.SelectedItem == null) return;
            string layerName = cmbLayers.SelectedItem.ToString();

            // 找到图层对象
            for (int i = 0; i < CurrentMap.LayerCount; i++)
            {
                ILayer layer = CurrentMap.get_Layer(i);
                if (layer.Name == layerName && layer is IGeoFeatureLayer)
                {
                    _currentGeoLayer = layer as IGeoFeatureLayer;
                    break;
                }
            }

            // 读取字段
            if (_currentGeoLayer != null)
            {
                cmbFields.Items.Clear();
                IFields fields = _currentGeoLayer.FeatureClass.Fields;
                for (int i = 0; i < fields.FieldCount; i++)
                {
                    IField field = fields.get_Field(i);
                    // 排除掉 Shape 几何字段，通常标注用的是字符串或数字
                    if (field.Type != esriFieldType.esriFieldTypeGeometry &&
                        field.Type != esriFieldType.esriFieldTypeBlob)
                    {
                        cmbFields.Items.Add(field.Name);
                    }
                }
                if (cmbFields.Items.Count > 0) cmbFields.SelectedIndex = 0;

                // 读取当前图层是否已经开启标注
                chkEnable.Checked = _currentGeoLayer.DisplayAnnotation;
            }
        }

        // 3. 颜色选择按钮
        private void btnColor_Click(object sender, EventArgs e)
        {
            ColorDialog colorDialog = new ColorDialog();
            if (colorDialog.ShowDialog() == DialogResult.OK)
            {
                _selectedColor = colorDialog.Color;
                btnColor.BackColor = _selectedColor; // 按钮变色提示用户
            }
        }

        // 4. 【核心实验代码】应用标注
        private void btnOk_Click(object sender, EventArgs e)
        {
            if (_currentGeoLayer == null) return;

            try
            {
                // A. 设置是否显示标注
                _currentGeoLayer.DisplayAnnotation = chkEnable.Checked;

                if (chkEnable.Checked)
                {
                    if (cmbFields.SelectedItem == null)
                    {
                        MessageBox.Show("请选择一个标注字段！");
                        return;
                    }

                    string fieldName = cmbFields.SelectedItem.ToString();

                    stdole.IFontDisp font = new stdole.StdFontClass() as stdole.IFontDisp;
                    font.Name = "微软雅黑"; // 设置字体名称
                    font.Bold = false;      // 设置是否加粗

                    // 2. 创建文本符号
                    ITextSymbol textSymbol = new TextSymbolClass();

                    // 3. 【重要】直接赋值，不要加强转 (as System.Drawing.Font 是错误的写法)
                    textSymbol.Font = font;
                    textSymbol.Size = (double)nudFontSize.Value;

                    // C. 颜色转换 (.NET Color -> ArcEngine IColor)
                    IRgbColor rgbColor = new RgbColorClass();
                    rgbColor.Red = _selectedColor.R;
                    rgbColor.Green = _selectedColor.G;
                    rgbColor.Blue = _selectedColor.B;
                    textSymbol.Color = rgbColor;

                    // D. 设置标注属性 (AnnotateLayerProperties)
                    IAnnotateLayerPropertiesCollection propertiesColl = _currentGeoLayer.AnnotationProperties;
                    propertiesColl.Clear(); // 清除之前的设置

                    // 创建标注引擎属性
                    ILabelEngineLayerProperties labelEngineProp = new LabelEngineLayerPropertiesClass();
                    labelEngineProp.Symbol = textSymbol;
                    // 设置标注表达式，格式通常为 [字段名]
                    labelEngineProp.Expression = "[" + fieldName + "]";

                    // 将设置添加到图层
                    propertiesColl.Add(labelEngineProp as IAnnotateLayerProperties);
                }

                // E. 刷新地图
                // 因为标注属于图形层，需要刷新整个 Geography
                IActiveView activeView = CurrentMap as IActiveView;
                activeView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);

                // MessageBox.Show("标注设置已应用！");
            }
            catch (Exception ex)
            {
                MessageBox.Show("标注设置失败: " + ex.Message);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}