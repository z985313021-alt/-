using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace WindowsFormsMap1
{
    public partial class FormAddXYData : Form
    {
        private AxMapControl _mapControl;
        private string[] _csvHeaders;

        public FormAddXYData(AxMapControl mapControl)
        {
            InitializeComponent();
            _mapControl = mapControl;
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "CSV Files (*.csv)|*.csv|Text Files (*.txt)|*.txt";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                txtFilePath.Text = ofd.FileName;
                LoadCSVHeaders(ofd.FileName);
            }
        }

        private void LoadCSVHeaders(string filePath)
        {
            try
            {
                using (StreamReader sr = new StreamReader(filePath, Encoding.Default))
                {
                    string headerLine = sr.ReadLine();
                    if (!string.IsNullOrEmpty(headerLine))
                    {
                        // 假设逗号分隔
                        _csvHeaders = headerLine.Split(',');
                        cmbX.Items.Clear();
                        cmbY.Items.Clear();
                        foreach (string header in _csvHeaders)
                        {
                            cmbX.Items.Add(header);
                            cmbY.Items.Add(header);
                        }
                        if (cmbX.Items.Count > 0) cmbX.SelectedIndex = 0;
                        if (cmbY.Items.Count > 1) cmbY.SelectedIndex = 1;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("读取文件头失败: " + ex.Message);
            }
        }

        private void btnBrowseOutput_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Shapefile (*.shp)|*.shp";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                txtOutput.Text = sfd.FileName;
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(txtFilePath.Text) || string.IsNullOrEmpty(txtOutput.Text) || cmbX.SelectedItem == null || cmbY.SelectedItem == null)
            {
                MessageBox.Show("请完善信息！");
                return;
            }

            try
            {
                string inputPath = txtFilePath.Text;
                string outputPath = txtOutput.Text;
                string xField = cmbX.SelectedItem.ToString();
                string yField = cmbY.SelectedItem.ToString();
                int xIndex = cmbX.SelectedIndex;
                int yIndex = cmbY.SelectedIndex;

                // 创建 Shapefile
                CreateShapefile(outputPath);

                // 打开 FeatureClass
                string folder = System.IO.Path.GetDirectoryName(outputPath);
                string fileName = System.IO.Path.GetFileName(outputPath);
                IWorkspaceFactory workspaceFactory = new ShapefileWorkspaceFactoryClass();
                IFeatureWorkspace featureWorkspace = (IFeatureWorkspace)workspaceFactory.OpenFromFile(folder, 0);
                IFeatureClass featureClass = featureWorkspace.OpenFeatureClass(System.IO.Path.GetFileNameWithoutExtension(fileName));

                // 读取 CSV 并写入要素
                using (StreamReader sr = new StreamReader(inputPath, Encoding.Default))
                {
                    string line;
                    bool isHeader = true;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (isHeader)
                        {
                            isHeader = false;
                            continue; // 跳过表头
                        }

                        if (string.IsNullOrWhiteSpace(line)) continue;

                        string[] values = line.Split(',');
                        if (values.Length > Math.Max(xIndex, yIndex))
                        {
                            double x, y;
                            if (double.TryParse(values[xIndex], out x) && double.TryParse(values[yIndex], out y))
                            {
                                IFeature feature = featureClass.CreateFeature();
                                IPoint point = new PointClass();
                                point.PutCoords(x, y);
                                feature.Shape = point;
                                feature.Store();
                            }
                        }
                    }
                }

                MessageBox.Show("转换成功！");

                // 加载图层
                if (MessageBox.Show("是否加载结果图层？", "提示", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    _mapControl.AddShapeFile(folder, fileName);
                    _mapControl.ActiveView.Refresh();
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("处理失败: " + ex.Message);
            }
        }

        private void CreateShapefile(string shapeFilePath)
        {
            string folder = System.IO.Path.GetDirectoryName(shapeFilePath);
            string fileName = System.IO.Path.GetFileNameWithoutExtension(shapeFilePath);

            IWorkspaceFactory workspaceFactory = new ShapefileWorkspaceFactoryClass();
            IFeatureWorkspace featureWorkspace = (IFeatureWorkspace)workspaceFactory.OpenFromFile(folder, 0);

            // 如果存在则删除
            if (File.Exists(shapeFilePath))
            {
                 // 简单处理：提示已存在或直接覆盖 (这里假设覆盖，实际应用最好先删除)
                 // 由于 ArcEngine 删除需要独占锁，这里暂不处理复杂逻辑，由用户保证路径或手动删除
                 // GP 工具的 OverwriteOutput = true 会比较方便，但这里手动写 FeatureClass
                 // 尝试删除
                 try { File.Delete(shapeFilePath); File.Delete(shapeFilePath.Replace(".shp", ".shx")); File.Delete(shapeFilePath.Replace(".shp", ".dbf")); } catch { }
            }

            // 定义字段
            IFields fields = new FieldsClass();
            IFieldsEdit fieldsEdit = (IFieldsEdit)fields;

            // ObjectID 字段
            IField oidField = new FieldClass();
            IFieldEdit oidFieldEdit = (IFieldEdit)oidField;
            oidFieldEdit.Name_2 = "OID";
            oidFieldEdit.Type_2 = esriFieldType.esriFieldTypeOID;
            fieldsEdit.AddField(oidField);

            // Geometry 字段
            IField geoField = new FieldClass();
            IFieldEdit geoFieldEdit = (IFieldEdit)geoField;
            geoFieldEdit.Name_2 = "Shape";
            geoFieldEdit.Type_2 = esriFieldType.esriFieldTypeGeometry;

            // 定义几何类型 - 点
            IGeometryDef geometryDef = new GeometryDefClass();
            IGeometryDefEdit geometryDefEdit = (IGeometryDefEdit)geometryDef;
            geometryDefEdit.GeometryType_2 = esriGeometryType.esriGeometryPoint;
            
            // 空间参考 - 未知
            ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass();
            ISpatialReference spatialReference = new UnknownCoordinateSystemClass(); // 暂时使用未知坐标系
            geometryDefEdit.SpatialReference_2 = spatialReference;

            geoFieldEdit.GeometryDef_2 = geometryDef;
            fieldsEdit.AddField(geoField);

            featureWorkspace.CreateFeatureClass(fileName, fields, null, null, esriFeatureType.esriFTSimple, "Shape", "");
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
