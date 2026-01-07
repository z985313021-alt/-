using System;
using System.Windows.Forms;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geometry;

namespace WindowsFormsMap1
{
    public partial class FormSelector : Form
    {
        private esriGeometryType _geometryType;
        public IStyleGalleryItem SelectedItem { get; private set; }
        public ISymbol SelectedSymbol { get; private set; }

        public FormSelector(esriGeometryType geometryType)
        {
            InitializeComponent();
            _geometryType = geometryType;
        }

        private void FormSelector_Load(object sender, EventArgs e)
        {
            // 加载样式文件
            string installPath = ESRI.ArcGIS.RuntimeManager.ActiveRuntime.Path;
            axSymbologyControl1.LoadStyleFile(installPath + @"Styles\ESRI.ServerStyle");

            // 根据几何类型设置样式类
            switch (_geometryType)
            {
                case esriGeometryType.esriGeometryPoint:
                    axSymbologyControl1.StyleClass = esriSymbologyStyleClass.esriStyleClassMarkerSymbols;
                    break;
                case esriGeometryType.esriGeometryPolyline:
                    axSymbologyControl1.StyleClass = esriSymbologyStyleClass.esriStyleClassLineSymbols;
                    break;
                case esriGeometryType.esriGeometryPolygon:
                    axSymbologyControl1.StyleClass = esriSymbologyStyleClass.esriStyleClassFillSymbols;
                    break;
                default:
                    // 默认 Marker
                    axSymbologyControl1.StyleClass = esriSymbologyStyleClass.esriStyleClassMarkerSymbols;
                    break;
            }
        }

        private void axSymbologyControl1_OnItemSelected(object sender, ISymbologyControlEvents_OnItemSelectedEvent e)
        {
            SelectedItem = e.styleGalleryItem as IStyleGalleryItem;
            if (SelectedItem != null)
            {
                SelectedSymbol = SelectedItem.Item as ISymbol;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (SelectedSymbol == null)
            {
                MessageBox.Show("请选择一个符号！");
                return;
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
