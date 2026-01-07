using System;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geoprocessor;
using ESRI.ArcGIS.AnalysisTools;

namespace WindowsFormsMap1
{
    public partial class FormBuffer : Form
    {
        private AxMapControl _mapControl;
        private Label lblLayer;
        private ComboBox cmbLayers;
        private Label lblDistance;
        private TextBox txtDistance;
        private ComboBox cmbUnits; // [新增] 单位选择
        private Label lblOutput;
        private TextBox txtOutput;
        private Button btnBrowse;
        private Button btnBuffer;
        private Button btnCancel;

        public FormBuffer(AxMapControl mapControl)
        {
            InitializeComponent();
            _mapControl = mapControl;
        }

        private void InitializeComponent()
        {
            this.lblLayer = new System.Windows.Forms.Label();
            this.cmbLayers = new System.Windows.Forms.ComboBox();
            this.lblDistance = new System.Windows.Forms.Label();
            this.txtDistance = new System.Windows.Forms.TextBox();
            this.cmbUnits = new System.Windows.Forms.ComboBox(); // [实例化]
            this.lblOutput = new System.Windows.Forms.Label();
            this.txtOutput = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnBuffer = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblLayer
            // 
            this.lblLayer.AutoSize = true;
            this.lblLayer.Location = new System.Drawing.Point(20, 25);
            this.lblLayer.Name = "lblLayer";
            this.lblLayer.Size = new System.Drawing.Size(65, 12);
            this.lblLayer.Text = "选择图层：";
            // 
            // cmbLayers
            // 
            this.cmbLayers.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbLayers.FormattingEnabled = true;
            this.cmbLayers.Location = new System.Drawing.Point(100, 22);
            this.cmbLayers.Name = "cmbLayers";
            this.cmbLayers.Size = new System.Drawing.Size(240, 20);
            // 
            // lblDistance
            // 
            this.lblDistance.AutoSize = true;
            this.lblDistance.Location = new System.Drawing.Point(20, 65);
            this.lblDistance.Name = "lblDistance";
            this.lblDistance.Size = new System.Drawing.Size(65, 12);
            this.lblDistance.Text = "缓冲距离：";
            // 
            // txtDistance
            // 
            this.txtDistance.Location = new System.Drawing.Point(100, 62);
            this.txtDistance.Name = "txtDistance";
            this.txtDistance.Size = new System.Drawing.Size(140, 21); // [修改] 缩短宽度留给单位
            // 
            // cmbUnits
            // 
            this.cmbUnits.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbUnits.FormattingEnabled = true;
            this.cmbUnits.Items.AddRange(new object[] { "Meters", "Kilometers", "Feets", "Miles", "DecimalDegrees" });
            this.cmbUnits.Location = new System.Drawing.Point(250, 62);
            this.cmbUnits.Name = "cmbUnits";
            this.cmbUnits.Size = new System.Drawing.Size(90, 20); // [新增] 单位下拉框
            // 
            // lblOutput
            // 
            this.lblOutput.AutoSize = true;
            this.lblOutput.Location = new System.Drawing.Point(20, 105);
            this.lblOutput.Name = "lblOutput";
            this.lblOutput.Size = new System.Drawing.Size(65, 12);
            this.lblOutput.Text = "输出路径：";
            // 
            // txtOutput
            // 
            this.txtOutput.Location = new System.Drawing.Point(100, 102);
            this.txtOutput.Name = "txtOutput";
            this.txtOutput.Size = new System.Drawing.Size(190, 21);
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(300, 100);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(40, 23);
            this.btnBrowse.Text = "...";
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // btnBuffer
            // 
            this.btnBuffer.Location = new System.Drawing.Point(100, 150);
            this.btnBuffer.Name = "btnBuffer";
            this.btnBuffer.Size = new System.Drawing.Size(75, 23);
            this.btnBuffer.Text = "分析";
            this.btnBuffer.Click += new System.EventHandler(this.btnBuffer_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(220, 150);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.Text = "取消";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // FormBuffer
            // 
            this.ClientSize = new System.Drawing.Size(384, 211);
            this.Controls.Add(this.lblLayer);
            this.Controls.Add(this.cmbLayers);
            this.Controls.Add(this.lblDistance);
            this.Controls.Add(this.txtDistance);
            this.Controls.Add(this.cmbUnits); // [新增]
            this.Controls.Add(this.lblOutput);
            this.Controls.Add(this.txtOutput);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.btnBuffer);
            this.Controls.Add(this.btnCancel);
            this.Text = "缓冲区分析";
            this.Load += new System.EventHandler(this.FormBuffer_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void FormBuffer_Load(object sender, EventArgs e)
        {
            if (_mapControl == null) return;
            for (int i = 0; i < _mapControl.LayerCount; i++)
            {
                ILayer layer = _mapControl.get_Layer(i);
                if (layer is IFeatureLayer)
                {
                    cmbLayers.Items.Add(layer.Name);
                }
            }
            if (cmbLayers.Items.Count > 0) cmbLayers.SelectedIndex = 0;

            // 默认单位
            cmbUnits.SelectedIndex = 1; // 默认 Kilometers，避免 Meters 太小
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Shapefile (*.shp)|*.shp";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                txtOutput.Text = sfd.FileName;
            }
        }

        private void btnBuffer_Click(object sender, EventArgs e)
        {
            if (cmbLayers.SelectedItem == null || string.IsNullOrEmpty(txtDistance.Text) || string.IsNullOrEmpty(txtOutput.Text))
            {
                MessageBox.Show("请完善信息！");
                return;
            }

            try
            {
                // 获取图层
                string layerName = cmbLayers.SelectedItem.ToString();
                ILayer layer = null;
                for (int i = 0; i < _mapControl.LayerCount; i++)
                {
                    if (_mapControl.get_Layer(i).Name == layerName)
                    {
                        layer = _mapControl.get_Layer(i);
                        break;
                    }
                }

                if (layer == null) return;

                // 准备 GP 工具
                Geoprocessor gp = new Geoprocessor();
                gp.OverwriteOutput = true;

                ESRI.ArcGIS.AnalysisTools.Buffer bufferTool = new ESRI.ArcGIS.AnalysisTools.Buffer();
                bufferTool.in_features = layer;
                bufferTool.out_feature_class = txtOutput.Text;

                // [修改] 拼接距离和单位
                string distVal = txtDistance.Text;
                string distUnit = cmbUnits.SelectedItem.ToString();
                bufferTool.buffer_distance_or_field = distVal + " " + distUnit;

                bufferTool.dissolve_option = "ALL"; // 融合所有

                gp.Execute(bufferTool, null);

                MessageBox.Show("缓冲区分析成功！");

                // 询问是否加载结果
                if (MessageBox.Show("是否加载结果图层？", "提示", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    string path = System.IO.Path.GetDirectoryName(txtOutput.Text);
                    string fileName = System.IO.Path.GetFileName(txtOutput.Text);

                    // 使用 AxMapControl 提供的方法直接加载 Shapefile
                    _mapControl.AddShapeFile(path, fileName);
                    _mapControl.ActiveView.Refresh();
                }

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show("分析失败：" + ex.Message);
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
