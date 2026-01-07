using System;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geoprocessor;
using ESRI.ArcGIS.AnalysisTools;
using ESRI.ArcGIS.Geodatabase;

namespace WindowsFormsMap1
{
    public partial class FormOverlay : Form
    {
        private AxMapControl _mapControl;
        private Label lblInputLayer;
        private ComboBox cmbInputLayer;
        private Label lblOverlayLayer;
        private ComboBox cmbOverlayLayer;
        private Label lblMethod;
        private ComboBox cmbMethod;
        private Label lblOutput;
        private TextBox txtOutput;
        private Button btnBrowse;
        private Button btnAnalyze;
        private Button btnCancel;

        public FormOverlay(AxMapControl mapControl)
        {
            InitializeComponent();
            _mapControl = mapControl;
        }

        private void InitializeComponent()
        {
            this.lblInputLayer = new System.Windows.Forms.Label();
            this.cmbInputLayer = new System.Windows.Forms.ComboBox();
            this.lblOverlayLayer = new System.Windows.Forms.Label();
            this.cmbOverlayLayer = new System.Windows.Forms.ComboBox();
            this.lblMethod = new System.Windows.Forms.Label();
            this.cmbMethod = new System.Windows.Forms.ComboBox();
            this.lblOutput = new System.Windows.Forms.Label();
            this.txtOutput = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.btnAnalyze = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // lblInputLayer
            // 
            this.lblInputLayer.AutoSize = true;
            this.lblInputLayer.Location = new System.Drawing.Point(20, 25);
            this.lblInputLayer.Text = "输入图层：";
            // 
            // cmbInputLayer
            // 
            this.cmbInputLayer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbInputLayer.Location = new System.Drawing.Point(100, 22);
            this.cmbInputLayer.Size = new System.Drawing.Size(240, 20);
            // 
            // lblOverlayLayer
            // 
            this.lblOverlayLayer.AutoSize = true;
            this.lblOverlayLayer.Location = new System.Drawing.Point(20, 65);
            this.lblOverlayLayer.Text = "叠加图层：";
            // 
            // cmbOverlayLayer
            // 
            this.cmbOverlayLayer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbOverlayLayer.Location = new System.Drawing.Point(100, 62);
            this.cmbOverlayLayer.Size = new System.Drawing.Size(240, 20);
            // 
            // lblMethod
            // 
            this.lblMethod.AutoSize = true;
            this.lblMethod.Location = new System.Drawing.Point(20, 105);
            this.lblMethod.Text = "叠加方式：";
            // 
            // cmbMethod
            // 
            this.cmbMethod.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbMethod.Items.AddRange(new object[] { "Intersect (相交)", "Union (联合)", "Clip (裁剪)" });
            this.cmbMethod.Location = new System.Drawing.Point(100, 102);
            this.cmbMethod.Size = new System.Drawing.Size(240, 20);
            this.cmbMethod.SelectedIndex = 0;
            // 
            // lblOutput
            // 
            this.lblOutput.AutoSize = true;
            this.lblOutput.Location = new System.Drawing.Point(20, 145);
            this.lblOutput.Text = "输出路径：";
            // 
            // txtOutput
            // 
            this.txtOutput.Location = new System.Drawing.Point(100, 142);
            this.txtOutput.Size = new System.Drawing.Size(190, 21);
            // 
            // btnBrowse
            // 
            this.btnBrowse.Location = new System.Drawing.Point(300, 140);
            this.btnBrowse.Size = new System.Drawing.Size(40, 23);
            this.btnBrowse.Text = "...";
            this.btnBrowse.Click += new System.EventHandler(this.btnBrowse_Click);
            // 
            // btnAnalyze
            // 
            this.btnAnalyze.Location = new System.Drawing.Point(100, 190);
            this.btnAnalyze.Text = "分析";
            this.btnAnalyze.Click += new System.EventHandler(this.btnAnalyze_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(220, 190);
            this.btnCancel.Text = "取消";
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // FormOverlay
            // 
            this.ClientSize = new System.Drawing.Size(384, 251);
            this.Controls.Add(this.lblInputLayer);
            this.Controls.Add(this.cmbInputLayer);
            this.Controls.Add(this.lblOverlayLayer);
            this.Controls.Add(this.cmbOverlayLayer);
            this.Controls.Add(this.lblMethod);
            this.Controls.Add(this.cmbMethod);
            this.Controls.Add(this.lblOutput);
            this.Controls.Add(this.txtOutput);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.btnAnalyze);
            this.Controls.Add(this.btnCancel);
            this.Text = "叠加分析";
            this.Load += new System.EventHandler(this.FormOverlay_Load);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void FormOverlay_Load(object sender, EventArgs e)
        {
            if (_mapControl == null) return;
            for (int i = 0; i < _mapControl.LayerCount; i++)
            {
                ILayer layer = _mapControl.get_Layer(i);
                if (layer is IFeatureLayer)
                {
                    cmbInputLayer.Items.Add(layer.Name);
                    cmbOverlayLayer.Items.Add(layer.Name);
                }
            }
            if (cmbInputLayer.Items.Count > 0) cmbInputLayer.SelectedIndex = 0;
            if (cmbOverlayLayer.Items.Count > 0) cmbOverlayLayer.SelectedIndex = 0;
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

        private void btnAnalyze_Click(object sender, EventArgs e)
        {
            if (cmbInputLayer.SelectedItem == null || cmbOverlayLayer.SelectedItem == null || string.IsNullOrEmpty(txtOutput.Text))
            {
                MessageBox.Show("请完善信息！");
                return;
            }

            try
            {
                string inputName = cmbInputLayer.SelectedItem.ToString();
                string overlayName = cmbOverlayLayer.SelectedItem.ToString();

                // 获取图层
                ILayer inputLayer = null;
                ILayer overlayLayer = null;

                for (int i = 0; i < _mapControl.LayerCount; i++)
                {
                    ILayer l = _mapControl.get_Layer(i);
                    if (l.Name == inputName) inputLayer = l;
                    if (l.Name == overlayName) overlayLayer = l;
                }

                if (inputLayer == null || overlayLayer == null) return;

                Geoprocessor gp = new Geoprocessor();
                gp.OverwriteOutput = true;

                string method = cmbMethod.SelectedItem.ToString();

                if (method.Contains("Intersect")) // 相交
                {
                    Intersect tool = new Intersect();
                    // 为了稳定起见，使用要素类的绝对路径进行输入，避免 GP 工具找不到图层对象
                    IDataset ds1 = (inputLayer as IFeatureLayer).FeatureClass as IDataset;
                    IDataset ds2 = (overlayLayer as IFeatureLayer).FeatureClass as IDataset;

                    // 假设是 Shapefile 工作空间 (简化处理)
                    string path1 = ds1.Workspace.PathName + "\\" + ds1.Name + ".shp";
                    string path2 = ds2.Workspace.PathName + "\\" + ds2.Name + ".shp";

                    // 相交工具通常需要分号分隔的输入列表
                    tool.in_features = path1 + ";" + path2;
                    tool.out_feature_class = txtOutput.Text;
                    gp.Execute(tool, null);
                }
                else if (method.Contains("Union")) // 联合
                {
                    Union tool = new Union();
                    IDataset ds1 = (inputLayer as IFeatureLayer).FeatureClass as IDataset;
                    IDataset ds2 = (overlayLayer as IFeatureLayer).FeatureClass as IDataset;
                    string path1 = ds1.Workspace.PathName + "\\" + ds1.Name + ".shp";
                    string path2 = ds2.Workspace.PathName + "\\" + ds2.Name + ".shp";

                    tool.in_features = path1 + ";" + path2;
                    tool.out_feature_class = txtOutput.Text;
                    gp.Execute(tool, null);
                }
                else if (method.Contains("Clip")) // 裁剪
                {
                    Clip tool = new Clip();
                    // 裁剪工具可以直接接受图层对象
                    tool.in_features = inputLayer;
                    tool.clip_features = overlayLayer;
                    tool.out_feature_class = txtOutput.Text;
                    gp.Execute(tool, null);
                }

                MessageBox.Show("叠置分析成功！");

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
