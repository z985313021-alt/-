using System;
using System.Drawing;
using System.Windows.Forms;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Carto;
using System.Collections.Generic;
using System.Linq;

namespace WindowsFormsMap1
{
    public partial class FormSmartBuffer : Form
    {
        private Form1 _mainForm;
        private IGeometry _tempGeo; // Stores captured geometry

        // UI Controls
        private GroupBox grpType;
        private RadioButton radPoint;
        private RadioButton radLine;

        private GroupBox grpStep1; // Capture
        private Button btnPick;
        private Label lblStatus;

        private GroupBox grpStep2; // Params
        private CheckBox chkMultiRing;
        private Label lblDistance;
        private NumericUpDown numDistance;
        private TextBox txtMultiDistance; // For Multi-Ring input
        private ComboBox cmbUnit;
        private Label lblColor;
        private Panel pnlColor;
        private Button btnColor;

        private GroupBox grpStep3; // Execute
        private Button btnGenerate;
        private Button btnClear;
        
        public FormSmartBuffer(Form1 mainForm)
        {
            _mainForm = mainForm;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "智能缓冲区工具箱 (交互版)";
            this.Size = new Size(340, 450);
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.CenterScreen;

            // --- Type Selection ---
            grpType = new GroupBox { Text = "0. 缓冲区类型", Location = new System.Drawing.Point(10, 10), Size = new Size(300, 50) };
            radPoint = new RadioButton { Text = "点缓冲", Location = new System.Drawing.Point(20, 20), Checked = true, AutoSize = true };
            radLine = new RadioButton { Text = "线缓冲", Location = new System.Drawing.Point(120, 20), AutoSize = true };
            grpType.Controls.Add(radPoint);
            grpType.Controls.Add(radLine);
            radPoint.CheckedChanged += (s, e) => ResetCapture(); 
            radLine.CheckedChanged += (s, e) => ResetCapture();

            // --- Step 1: Capture ---
            grpStep1 = new GroupBox { Text = "1. 拾取要素", Location = new System.Drawing.Point(10, 70), Size = new Size(300, 70) };
            btnPick = new Button { Text = "开始拾取", Location = new System.Drawing.Point(20, 25), Size = new Size(80, 30) };
            lblStatus = new Label { Text = "未选择要素", Location = new System.Drawing.Point(110, 32), AutoSize = true, ForeColor = Color.Red };
            btnPick.Click += BtnPick_Click;
            grpStep1.Controls.Add(btnPick);
            grpStep1.Controls.Add(lblStatus);

            // --- Step 2: Parameters ---
            grpStep2 = new GroupBox { Text = "2. 参数设置", Location = new System.Drawing.Point(10, 150), Size = new Size(300, 160) };
            
            // Multi-Ring Checkbox
            chkMultiRing = new CheckBox { Text = "启用多环缓冲区", Location = new System.Drawing.Point(20, 25), AutoSize = true };
            chkMultiRing.CheckedChanged += ChkMultiRing_CheckedChanged;

            // Distance
            lblDistance = new Label { Text = "缓冲距离:", Location = new System.Drawing.Point(20, 60), AutoSize = true };
            
            // Single Distance Input
            numDistance = new NumericUpDown { Location = new System.Drawing.Point(90, 58), Size = new Size(100, 23), DecimalPlaces = 2, Value = 1 };
            
            // Multi Distance Input (Hidden by default)
            txtMultiDistance = new TextBox { Location = new System.Drawing.Point(90, 58), Size = new Size(100, 23), Visible = false };
            ToolTip tip = new ToolTip();
            tip.SetToolTip(txtMultiDistance, "输入多个距离，用分号隔开 (例如: 1.0; 2.5; 5.0)");

            // Unit
            cmbUnit = new ComboBox { Location = new System.Drawing.Point(200, 58), Size = new Size(80, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbUnit.Items.AddRange(new object[] { "千米", "米", "度" });
            cmbUnit.SelectedIndex = 0; // Default KM

            // Color
            lblColor = new Label { Text = "填充颜色:", Location = new System.Drawing.Point(20, 100), AutoSize = true };
            pnlColor = new Panel { Location = new System.Drawing.Point(90, 98), Size = new Size(100, 23), BackColor = Color.Blue, BorderStyle = BorderStyle.FixedSingle };
            btnColor = new Button { Text = "...", Location = new System.Drawing.Point(200, 98), Size = new Size(40, 23) };
            btnColor.Click += BtnColor_Click;

            grpStep2.Controls.Add(chkMultiRing);
            grpStep2.Controls.Add(lblDistance);
            grpStep2.Controls.Add(numDistance);
            grpStep2.Controls.Add(txtMultiDistance);
            grpStep2.Controls.Add(cmbUnit);
            grpStep2.Controls.Add(lblColor);
            grpStep2.Controls.Add(pnlColor);
            grpStep2.Controls.Add(btnColor);

            // --- Step 3: Execute ---
            grpStep3 = new GroupBox { Text = "3. 执行操作", Location = new System.Drawing.Point(10, 320), Size = new Size(300, 80) };
            btnGenerate = new Button { Text = "生成缓冲区", Location = new System.Drawing.Point(20, 25), Size = new Size(120, 35), Font = new Font(this.Font, FontStyle.Bold) };
            btnClear = new Button { Text = "清除所有", Location = new System.Drawing.Point(160, 25), Size = new Size(100, 35) };
            
            btnGenerate.Click += BtnGenerate_Click;
            btnClear.Click += BtnClear_Click;
            
            // Export Button
            Button btnExport = new Button { Text = "导出结果", Location = new System.Drawing.Point(20, 65), Size = new Size(120, 30) };
            btnExport.Click += BtnExport_Click;
            
            grpStep3.Controls.Add(btnGenerate);
            grpStep3.Controls.Add(btnClear);
            grpStep3.Controls.Add(btnExport);
            
            grpStep3.Size = new Size(300, 110); // Increase height

            // Add all groups
            this.Controls.Add(grpType);
            this.Controls.Add(grpStep1);
            this.Controls.Add(grpStep2);
            this.Controls.Add(grpStep3);

            this.FormClosing += FormSmartBuffer_FormClosing;
        }

        private void ResetCapture()
        {
            _tempGeo = null;
            lblStatus.Text = "未选择要素";
            lblStatus.ForeColor = Color.Red;
            if (_mainForm != null) _mainForm.SwitchTool(Form1.MapToolMode.None);
        }

        private void ChkMultiRing_CheckedChanged(object sender, EventArgs e)
        {
            if (chkMultiRing.Checked)
            {
                txtMultiDistance.Visible = true;
                numDistance.Visible = false;
                lblDistance.Text = "多级距离:";
            }
            else
            {
                txtMultiDistance.Visible = false;
                numDistance.Visible = true;
                lblDistance.Text = "缓冲距离:";
            }
        }

        private void BtnPick_Click(object sender, EventArgs e)
        {
            if (_mainForm == null) return;
            
            if (radPoint.Checked)
            {
                _mainForm.SwitchTool(Form1.MapToolMode.BufferPoint);
                MessageBox.Show("请在地图上点击以捕获【点】要素。", "提示");
            }
            else
            {
                _mainForm.SwitchTool(Form1.MapToolMode.BufferLine);
                MessageBox.Show("请在地图上绘制以捕获【线】要素 (双击结束)。", "提示");
            }
        }

        // Called by Form1.Navigation.cs
        public void OnGeometryCaptured(IGeometry geo)
        {
            if (geo == null || geo.IsEmpty) return;

            _tempGeo = geo;
            lblStatus.Text = "已捕获: " + (geo.GeometryType == esriGeometryType.esriGeometryPoint ? "Point" : "Line");
            lblStatus.ForeColor = Color.Green;
            
            // Auto-switch back to Arrow to prevent accidental multiple captures
            _mainForm.SwitchTool(Form1.MapToolMode.None);
            
            // Optional: User Feedback
            // MessageBox.Show("要素已捕获！请点击“生成缓冲区”。");
        }

        private void BtnGenerate_Click(object sender, EventArgs e)
        {
            if (_tempGeo == null)
            {
                MessageBox.Show("请先完成第1步：拾取要素。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                List<double> distances = new List<double>();
                string unit = cmbUnit.SelectedItem.ToString();

                // 1. Parse Distances
                if (chkMultiRing.Checked)
                {
                    string input = txtMultiDistance.Text;
                    if (string.IsNullOrWhiteSpace(input))
                    {
                        MessageBox.Show("请输入距离，用分号隔开。", "提示");
                        return;
                    }
                    
                    string[] parts = input.Split(new char[] { ';', '，', ',' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string p in parts)
                    {
                        if (double.TryParse(p.Trim(), out double d))
                            distances.Add(d);
                    }
                    
                    if (distances.Count == 0) throw new Exception("无效的距离输入！");
                    
                    // Sort Descending (Largest first) so smaller buffers are drawn ON TOP of larger ones (if filled)
                    // OR draw Largest first so it's at the bottom of the draw stack?
                    // GraphicsContainer.AddElement adds to top by default usually? Or depends on order.
                    // Actually, if we draw filled polygons, we must draw Largest first (bottom), then Smallest (top) 
                    // IF we want to see them all.
                    // Wait, AddElement(ele, 0) usually puts it at the TOP (Z-order 0).
                    // So we should draw SMALLEST first (so it ends up at bottom? No.)
                    // Correct Z-Order for Filled Polygons: Smallest on Top.
                    // If AddElement(0) puts at FRONT, then we add Largest first (Front), then Smallest (Front of Largest).
                    // So order: Largest -> Add -> Smallest -> Add.
                    // Result: Smallest covers Largest. Correct.
                    distances.Sort((a, b) => b.CompareTo(a)); // Descending: 5, 2, 1
                }
                else
                {
                    distances.Add((double)numDistance.Value);
                }

                _lastBufferGeometries.Clear(); // Clear previous
                // 2. Loop & Generate
                foreach (double dist in distances)
                {
                    double mapDist = ConvertToMapUnit(dist, unit);
                    ITopologicalOperator topo = _tempGeo as ITopologicalOperator;
                    IGeometry bufGeo = topo.Buffer(mapDist);
                    
                    _lastBufferGeometries.Add(bufGeo); // Store

                    // Color Logic: maybe vary transparency or shade?
                    // For now, use same color.
                    IRgbColor rgb = new RgbColorClass();
                    rgb.Red = pnlColor.BackColor.R;
                    rgb.Green = pnlColor.BackColor.G;
                    rgb.Blue = pnlColor.BackColor.B;
                    rgb.Transparency = 100; // Semi-transparent

                    _mainForm.DrawGeometry(bufGeo, rgb);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("生成失败: " + ex.Message);
            }
        }

        private void BtnClear_Click(object sender, EventArgs e)
        {
            if (_mainForm != null) _mainForm.RefreshMap();
            // Also reset capture status?
            // ResetCapture(); // Optional, maybe user wants to keep the geometry and clear graphics to retry with new params? 
            // Let's keep geometry.
        }

        private void BtnColor_Click(object sender, EventArgs e)
        {
            ColorDialog cd = new ColorDialog();
            cd.Color = pnlColor.BackColor;
            if (cd.ShowDialog() == DialogResult.OK)
            {
                pnlColor.BackColor = cd.Color;
            }
        }

        private void FormSmartBuffer_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_mainForm != null && !_mainForm.IsDisposed)
            {
                _mainForm.SwitchTool(Form1.MapToolMode.None);
            }
        }

        private List<IGeometry> _lastBufferGeometries = new List<IGeometry>(); // Store for export

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (_lastBufferGeometries == null || _lastBufferGeometries.Count == 0)
            {
                MessageBox.Show("没有可导出的缓冲区结果！请先生成缓冲区。", "提示");
                return;
            }

            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Filter = "Shapefile (*.shp)|*.shp";
            sfd.FileName = "Buffer_Result.shp";
            
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    DataHelper.ExportGeometryToShapefile(_lastBufferGeometries, sfd.FileName);
                    MessageBox.Show("导出成功！已保存至: " + sfd.FileName, "成功");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("导出失败: " + ex.Message, "错误");
                }
            }
        }

        private double ConvertToMapUnit(double val, string unit)
        {
            ISpatialReference mapSR = _mainForm.MapSpatialReference;
            return UnitHelper.Convert(val, unit, mapSR);
        }
    }

    // Simple Helper for Unit Conversion
    public static class UnitHelper
    {
        public static double Convert(double val, string unit, ISpatialReference mapSR)
        {
             if (mapSR is IProjectedCoordinateSystem pcs)
            {
                double meters = 0;
                switch (unit)
                {
                    case "千米": meters = val * 1000.0; break;
                    case "米": meters = val; break;
                    case "度": meters = val * 111000.0; break; // Approx
                }
                return meters / pcs.CoordinateUnit.MetersPerUnit;
            }
            else
            {
                // GCS
                switch (unit)
                {
                    case "千米": return val / 111.0; 
                    case "米": return val / 111000.0;
                    case "度": return val;
                }
                return val;
            }
        }
    }
}
