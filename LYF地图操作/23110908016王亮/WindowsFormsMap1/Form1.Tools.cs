using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    /// <summary>
    /// Form1 的业务工具逻辑 (加载、分析、数据管理)
    /// </summary>
    public partial class Form1
    {
        // ================= 数据加载逻辑 =================

        private void 加载地图文档ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "MXD文档(*.mxd)|*.mxd" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                if (axMapControl2.CheckMxFile(dlg.FileName))
                {
                    axMapControl2.LoadMxFile(dlg.FileName);
                    axMapControl2.ActiveView.Refresh();
                    axTOCControl2.Update();
                    CheckBrokenLayers(axMapControl2.Map);
                }
            }
        }

        private void CheckBrokenLayers(IMap map)
        {
            if (map == null) return;
            List<string> broken = new List<string>();
            for (int i = 0; i < map.LayerCount; i++) if (!map.get_Layer(i).Valid) broken.Add(map.get_Layer(i).Name);
            if (broken.Count > 0) MessageBox.Show("以下图层数据丢失：\n" + string.Join("\n", broken), "警告");
        }

        private void 加载shp数据ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog { Filter = "SHP文件(*.shp)|*.shp" };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                axMapControl2.AddShapeFile(System.IO.Path.GetDirectoryName(dlg.FileName), System.IO.Path.GetFileNameWithoutExtension(dlg.FileName));
                axMapControl2.ActiveView.Refresh();
            }
        }

        public void ItemAddXYData_Click(object sender, EventArgs e) => new FormAddXYData(axMapControl2).ShowDialog();

        // ================= 空间分析触发 =================

        public void ItemBuffer_Click(object sender, EventArgs e) => new FormBuffer(axMapControl2).ShowDialog();
        public void ItemOverlay_Click(object sender, EventArgs e) => new FormOverlay(axMapControl2).ShowDialog();

        // ================= 非遗专项业务 =================

        private void ItemInitData_Click(object sender, EventArgs e)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                string targetDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\", "Data");
                if (!System.IO.Directory.Exists(targetDir)) System.IO.Directory.CreateDirectory(targetDir);

                string srcProj = @"c:\Users\Administrator\Desktop\LYF地图操作\【小黄鸭】非物质文化遗产、传承人空间点位数据\国家级非物质文化遗产代表性项目名录.shp";
                string res = DataHelper.SlimDataToGDB(srcProj, targetDir, "ShandongICH.gdb");

                if (res.Contains("成功"))
                {
                    IWorkspaceFactory wf = new ESRI.ArcGIS.DataSourcesGDB.FileGDBWorkspaceFactoryClass();
                    IFeatureWorkspace fw = (IFeatureWorkspace)wf.OpenFromFile(System.IO.Path.Combine(targetDir, "ShandongICH.gdb"), 0);
                    IFeatureClass fc = fw.OpenFeatureClass(System.IO.Path.GetFileNameWithoutExtension(srcProj) + "_SD");
                    DataHelper.DisplaceDuplicatePoints(fc);

                    IFeatureLayer fl = new FeatureLayerClass { FeatureClass = fc, Name = "山东国家级非遗项目 (已散开)" };
                    axMapControl2.AddLayer(fl);
                    axMapControl2.ActiveView.Refresh();
                    axTOCControl2.Update();
                }
                this.Cursor = Cursors.Default;
                MessageBox.Show(res, "初始化结果");
            }
            catch (Exception ex) { this.Cursor = Cursors.Default; MessageBox.Show("初始化失败: " + ex.Message); }
        }

        private void ItemCheckDataQuality_Click(object sender, EventArgs e)
        {
            ILayer selLayer = GetSelectedLayer();
            if (selLayer is IFeatureLayer fl)
            {
                string rpt = global::WindowsFormsMap1.DataAnalyzer.CheckDuplicateLocations(fl);
                ShowReport(rpt, selLayer.Name);
            }
            else MessageBox.Show("请先在TOC选中要素图层");
        }

        private void ItemDisplaceCoordinates_Click(object sender, EventArgs e)
        {
            ILayer selLayer = GetSelectedLayer();
            if (selLayer is IFeatureLayer fl)
            {
                string res = DataHelper.DisplaceDuplicatePoints(fl.FeatureClass);
                axMapControl2.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                MessageBox.Show(res);
            }
        }

        private ILayer GetSelectedLayer()
        {
            esriTOCControlItem item = esriTOCControlItem.esriTOCControlItemNone;
            IBasicMap map = null; ILayer layer = null; object other = null; object index = null;
            axTOCControl2.GetSelectedItem(ref item, ref map, ref layer, ref other, ref index);
            return (item == esriTOCControlItem.esriTOCControlItemLayer) ? layer : null;
        }

        private void ShowReport(string content, string title)
        {
            Form f = new Form { Text = "报告 - " + title, Size = new System.Drawing.Size(600, 400), StartPosition = FormStartPosition.CenterParent };
            f.Controls.Add(new TextBox { Multiline = true, Dock = DockStyle.Fill, Text = content, ReadOnly = true, ScrollBars = ScrollBars.Both });
            f.ShowDialog();
        }
    }
}
