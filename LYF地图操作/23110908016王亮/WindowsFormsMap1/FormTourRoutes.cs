using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 【游览路线定制窗体】：提供基于地市选择的自动路线规划与方案管理
    /// 支持多策略路网图层筛选、城市序列解析以及规划方案的持久化展示
    /// </summary>
    public partial class FormTourRoutes : Form
    {
        private Form1 _mainForm;
        private List<AnalysisHelper.TourRoute> _routes; // 存储当前生成的多个路线方案
        private IFeatureLayer _cityLayer;
        private IFeatureLayer _ichLayer;
        private List<IFeatureLayer> _lineLayers;

        public FormTourRoutes(Form1 mainForm, IFeatureLayer cityLayer, List<IFeatureLayer> lineLayers, IFeatureLayer ichLayer)
        {
            InitializeComponent();
            _mainForm = mainForm;
            _cityLayer = cityLayer;
            _lineLayers = lineLayers;
            _ichLayer = ichLayer;
            _routes = new List<AnalysisHelper.TourRoute>();
            
            InitLayers();
            InitCityList();
        }

        // 【图层语义识别】：自动在所有图层中寻找最具“路网”特征的线图层
        private void InitLayers()
        {
            cmbRoadLayers.Items.Clear();
            if (_lineLayers != null)
            {
                foreach (var l in _lineLayers)
                {
                    cmbRoadLayers.Items.Add(new LayerItem(l));
                }
 
                if (cmbRoadLayers.Items.Count > 0)
                {
                    // 智能推荐逻辑：优先选择名称中包含“融合”、“路”、“高速”等关键字的图层
                    bool found = false;
                    for(int i=0; i<cmbRoadLayers.Items.Count; i++)
                    {
                        string name = (cmbRoadLayers.Items[i] as LayerItem).ToString();
                        if (name.Contains("融合") || name.Contains("路") || name.Contains("高速"))
                        {
                            cmbRoadLayers.SelectedIndex = i;
                            found = true;
                            break;
                        }
                    }
                    if (!found) cmbRoadLayers.SelectedIndex = 0;
                }
            }
        }

        private class LayerItem
        {
            public IFeatureLayer Layer;
            public LayerItem(IFeatureLayer l) { Layer = l; }
            public override string ToString() { return Layer.Name; }
        }

        // ... Existing InitCityList ...
        private void InitCityList()
        {
            checkedListBoxCities.Items.Clear();
            if (_cityLayer != null)
            {
                HashSet<string> cities = new HashSet<string>();
                try
                {
                    IFeatureCursor cursor = _cityLayer.FeatureClass.Search(null, false);
                    IFeature f;
                    int idxName = _cityLayer.FeatureClass.Fields.FindField("NAME");
                    if (idxName == -1) idxName = _cityLayer.FeatureClass.Fields.FindField("Name");
                    if (idxName == -1) idxName = _cityLayer.FeatureClass.Fields.FindField("名称");
                    if (idxName == -1) idxName = _cityLayer.FeatureClass.Fields.FindField("CITY_NAME");
                    if (idxName == -1) idxName = _cityLayer.FeatureClass.Fields.FindField("行政名");
                    
                    if (idxName != -1)
                    {
                        while ((f = cursor.NextFeature()) != null)
                        {
                             object val = f.get_Value(idxName);
                             if (val != DBNull.Value) cities.Add(val.ToString());
                        }
                    }
                    else
                    {
                        string[] defaults = { "济南市", "青岛市", "淄博市", "枣庄市", "东营市", "烟台市", "潍坊市", "济宁市", "泰安市", "威海市", "日照市", "临沂市", "德州市", "聊城市", "滨州市", "菏泽市" };
                        foreach (var s in defaults) cities.Add(s);
                    }
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);
                }
                catch { }

                if (cities.Count == 0) // Layer exists but no data found
                {
                     string[] defaults = { "济南市", "青岛市", "淄博市", "枣庄市", "东营市", "烟台市", "潍坊市", "济宁市", "泰安市", "威海市", "日照市", "临沂市", "德州市", "聊城市", "滨州市", "菏泽市" };
                     foreach (var s in defaults) cities.Add(s);
                }

                foreach (var c in cities) checkedListBoxCities.Items.Add(c);
            }
            else
            {
                string[] defaults = { "济南市", "青岛市", "淄博市", "枣庄市", "东营市", "烟台市", "潍坊市", "济宁市", "泰安市", "威海市", "日照市", "临沂市", "德州市", "聊城市", "滨州市", "菏泽市" };
                foreach (var s in defaults) checkedListBoxCities.Items.Add(s);
            }
        }

        // 【核心规划引擎】：调用后台算法进行跨地市路径规划
        private void btnGenerate_Click(object sender, EventArgs e)
        {
            // 1. 获取选中的路网数据源
            IFeatureLayer selectedRoadLayer = null;
            if (cmbRoadLayers.SelectedItem is LayerItem item)
            {
                selectedRoadLayer = item.Layer;
            }
 
            if (selectedRoadLayer == null)
            {
                MessageBox.Show("请先选择一个路网图层（线图层）。");
                return;
            }
 
            // 2. 获取目标城市序列
            List<string> selectedCities = new List<string>();
            foreach (var c in checkedListBoxCities.CheckedItems)
            {
                selectedCities.Add(c.ToString());
            }
 
            if (selectedCities.Count < 2)
            {
                MessageBox.Show("请至少选择两个城市以规划路线。");
                return;
            }
 
            // 3. 执行异步规划 (防止界面卡死)
            this.Cursor = Cursors.WaitCursor;
            lblStatus.Text = "正在解析路网并规划...";
            Application.DoEvents(); // 强制刷新 UI 渲染
 
            try
            {
                // 调用 AnalysisHelper 的复杂算法：涉及拓扑重建、Dijkstra 求解及非遗点位抽稀
                var route = _mainForm._analysisHelper.GenerateRouteByCities(selectedCities, _cityLayer, selectedRoadLayer, _ichLayer);
                if (route != null)
                {
                    _routes.Add(route);
                    UpdateRouteList();
                    lblStatus.Text = "规划成功！";
                    listBoxRoutes.SelectedIndex = listBoxRoutes.Items.Count - 1;
                }
                else
                {
                    lblStatus.Text = "规划失败";
                    // 提取算法内部日志进行故障诊断
                    string log = _mainForm._analysisHelper.LastLog;
                    if (log.Length > 500) log = log.Substring(log.Length - 500) + "..."; 
 
                    MessageBox.Show("规划失败。原因可能是：路网不连通、坐标系不一致或内存溢出。\n调试诊断结果：\n" + log, "诊断报告");
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "逻辑错误: " + ex.Message;
                MessageBox.Show("执行出错: " + ex.Message);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void UpdateRouteList()
        {
            listBoxRoutes.Items.Clear();
            foreach (var route in _routes)
            {
                listBoxRoutes.Items.Add(route);
            }
        }

        private void listBoxRoutes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxRoutes.SelectedItem is AnalysisHelper.TourRoute route)
            {
                txtDescription.Text = route.Description;
                btnShow.Enabled = true;
                
                // [Agent] Auto-display on map
                if (_mainForm != null)
                {
                    _mainForm.DisplayTourRoute(route);
                }
            }
        }

        private void btnShow_Click(object sender, EventArgs e)
        {
            if (listBoxRoutes.SelectedItem is AnalysisHelper.TourRoute route)
            {
                _mainForm.DisplayTourRoute(route);
            }
        }

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.listBoxRoutes = new System.Windows.Forms.ListBox();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.btnShow = new System.Windows.Forms.Button();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.checkedListBoxCities = new System.Windows.Forms.CheckedListBox();
            this.lblCity = new System.Windows.Forms.Label();
            this.btnGenerate = new System.Windows.Forms.Button();
            this.panelLeft = new System.Windows.Forms.Panel();
            this.lblRoad = new System.Windows.Forms.Label();
            this.cmbRoadLayers = new System.Windows.Forms.ComboBox();
            this.panelLeft.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelLeft
            // 
            this.panelLeft.BackColor = System.Drawing.Color.White;
            this.panelLeft.Controls.Add(this.btnGenerate);
            this.panelLeft.Controls.Add(this.checkedListBoxCities);
            this.panelLeft.Controls.Add(this.lblCity);
            this.panelLeft.Controls.Add(this.cmbRoadLayers);
            this.panelLeft.Controls.Add(this.lblRoad);
            this.panelLeft.Dock = System.Windows.Forms.DockStyle.Left;
            this.panelLeft.Location = new System.Drawing.Point(0, 0);
            this.panelLeft.Name = "panelLeft";
            this.panelLeft.Padding = new System.Windows.Forms.Padding(10);
            this.panelLeft.Size = new System.Drawing.Size(180, 381); // Increased Width
            this.panelLeft.TabIndex = 5;
            // 
            // lblRoad
            // 
            this.lblRoad.AutoSize = true;
            this.lblRoad.Font = new System.Drawing.Font("Microsoft YaHei", 9F, System.Drawing.FontStyle.Bold);
            this.lblRoad.ForeColor = System.Drawing.Color.DimGray;
            this.lblRoad.Location = new System.Drawing.Point(10, 15);
            this.lblRoad.Name = "lblRoad";
            this.lblRoad.Size = new System.Drawing.Size(110, 17);
            this.lblRoad.TabIndex = 5;
            this.lblRoad.Text = "1. 选择路网图层：";
            // 
            // cmbRoadLayers
            // 
            this.cmbRoadLayers.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbRoadLayers.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.cmbRoadLayers.FormattingEnabled = true;
            this.cmbRoadLayers.Location = new System.Drawing.Point(10, 35);
            this.cmbRoadLayers.Name = "cmbRoadLayers";
            this.cmbRoadLayers.Size = new System.Drawing.Size(160, 25);
            this.cmbRoadLayers.TabIndex = 6;
            // 
            // lblCity
            // 
            this.lblCity.AutoSize = true;
            this.lblCity.Font = new System.Drawing.Font("Microsoft YaHei", 9F, System.Drawing.FontStyle.Bold);
            this.lblCity.ForeColor = System.Drawing.Color.DimGray;
            this.lblCity.Location = new System.Drawing.Point(10, 75);
            this.lblCity.Name = "lblCity";
            this.lblCity.Size = new System.Drawing.Size(86, 17);
            this.lblCity.TabIndex = 0;
            this.lblCity.Text = "2. 选择城市：";
            // 
            // checkedListBoxCities
            // 
            this.checkedListBoxCities.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.checkedListBoxCities.CheckOnClick = true;
            this.checkedListBoxCities.Font = new System.Drawing.Font("Microsoft YaHei", 9F);
            this.checkedListBoxCities.FormattingEnabled = true;
            this.checkedListBoxCities.Location = new System.Drawing.Point(10, 100);
            this.checkedListBoxCities.Name = "checkedListBoxCities";
            this.checkedListBoxCities.Size = new System.Drawing.Size(160, 216); // Adjusted height
            this.checkedListBoxCities.TabIndex = 1;
            // 
            // btnGenerate
            // 
            this.btnGenerate.BackColor = System.Drawing.Color.MediumSeaGreen;
            this.btnGenerate.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnGenerate.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.btnGenerate.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnGenerate.Font = new System.Drawing.Font("Microsoft YaHei", 10F, System.Drawing.FontStyle.Bold);
            this.btnGenerate.ForeColor = System.Drawing.Color.White;
            this.btnGenerate.Location = new System.Drawing.Point(10, 331);
            this.btnGenerate.Name = "btnGenerate";
            this.btnGenerate.Size = new System.Drawing.Size(160, 40);
            this.btnGenerate.TabIndex = 2;
            this.btnGenerate.Text = "生成游览路线";
            this.btnGenerate.UseVisualStyleBackColor = false;
            this.btnGenerate.Click += new System.EventHandler(this.btnGenerate_Click);
            // 
            // listBoxRoutes
            // 
            this.listBoxRoutes.Font = new System.Drawing.Font("Microsoft YaHei", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.listBoxRoutes.FormattingEnabled = true;
            this.listBoxRoutes.ItemHeight = 20;
            this.listBoxRoutes.Location = new System.Drawing.Point(190, 50); // Shifted right
            this.listBoxRoutes.Name = "listBoxRoutes";
            this.listBoxRoutes.Size = new System.Drawing.Size(180, 260); // Taller
            this.listBoxRoutes.TabIndex = 0;
            this.listBoxRoutes.SelectedIndexChanged += new System.EventHandler(this.listBoxRoutes_SelectedIndexChanged);
            // 
            // txtDescription
            // 
            this.txtDescription.BackColor = System.Drawing.Color.White;
            this.txtDescription.Font = new System.Drawing.Font("Microsoft YaHei", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtDescription.Location = new System.Drawing.Point(380, 50); // Shifted right
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.ReadOnly = true;
            this.txtDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDescription.Size = new System.Drawing.Size(200, 260);
            this.txtDescription.TabIndex = 1;
            // 
            // btnShow
            // 
            this.btnShow.BackColor = System.Drawing.Color.SteelBlue;
            this.btnShow.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnShow.Enabled = false;
            this.btnShow.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnShow.Font = new System.Drawing.Font("Microsoft YaHei", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnShow.ForeColor = System.Drawing.Color.White;
            this.btnShow.Location = new System.Drawing.Point(380, 330);
            this.btnShow.Name = "btnShow";
            this.btnShow.Size = new System.Drawing.Size(200, 40);
            this.btnShow.TabIndex = 2;
            this.btnShow.Text = "在地图中展示";
            this.btnShow.UseVisualStyleBackColor = false;
            this.btnShow.Click += new System.EventHandler(this.btnShow_Click);
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTitle.ForeColor = System.Drawing.Color.DarkSlateBlue;
            this.lblTitle.Location = new System.Drawing.Point(190, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(128, 22);
            this.lblTitle.TabIndex = 3;
            this.lblTitle.Text = "3. 路线预览列表";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.ForeColor = System.Drawing.Color.Gray;
            this.lblStatus.Location = new System.Drawing.Point(190, 340);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(65, 12);
            this.lblStatus.TabIndex = 4;
            this.lblStatus.Text = "等待生成...";
            // 
            // FormTourRoutes
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.WhiteSmoke;
            this.ClientSize = new System.Drawing.Size(600, 381); // Resize
            this.Controls.Add(this.panelLeft);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.btnShow);
            this.Controls.Add(this.txtDescription);
            this.Controls.Add(this.listBoxRoutes);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.Name = "FormTourRoutes";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "智能路线推荐定制 - 演示版";
            this.panelLeft.ResumeLayout(false);
            this.panelLeft.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox listBoxRoutes;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.Button btnShow;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Panel panelLeft;
        private System.Windows.Forms.CheckedListBox checkedListBoxCities;
        private System.Windows.Forms.Label lblCity;
        private System.Windows.Forms.Button btnGenerate;
        private System.Windows.Forms.ComboBox cmbRoadLayers;
        private System.Windows.Forms.Label lblRoad;
    }
}
