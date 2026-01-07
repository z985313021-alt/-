using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using System;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    /// <summary>
    /// Form1 的编辑、测量与符号化逻辑
    /// </summary>
    public partial class Form1
    {
        // ================= 编辑功能触发 =================

        // [Member B] Added: Dashboard Module Initialization
        private FormChart _dashboardForm;
        public void InitDashboardModule()
        {
            if (_dashboardForm == null || _dashboardForm.IsDisposed)
            {
                _dashboardForm = new FormChart();
                _dashboardForm.SetMapControl(this.axMapControl2); // Pass Map Control
                _dashboardForm.Owner = this; // Optional: ensure it stays on top or linked
            }
            _dashboardForm.Show();
        }

        public void ItemStartEdit_Click(object sender, EventArgs e)
        {
            if (axMapControl2.LayerCount == 0) return;
            FormStartEdit form = new FormStartEdit(this.axMapControl2);
            if (form.ShowDialog() == DialogResult.OK && form.SelectedLayer != null)
            {
                _editorHelper.StartEditing(form.SelectedLayer);
            }
        }

        public void ItemUndo_Click(object sender, EventArgs e) => _editorHelper.Undo();
        public void ItemSaveEdit_Click(object sender, EventArgs e) => _editorHelper.SaveEdit();
        public void ItemStopEdit_Click(object sender, EventArgs e) { _editorHelper.StopEditing(); SwitchTool(MapToolMode.None); }
        public void ItemCreateFeature_Click(object sender, EventArgs e) => SwitchTool(MapToolMode.CreateFeature);

        // ================= 测量与搜索触发 =================

        private void tsmiAreaMeasure_Click(object sender, EventArgs e) => SwitchTool(MapToolMode.MeasureArea);
        private void 面积量测ToolStripMenuItem_Click(object sender, EventArgs e) => SwitchTool(MapToolMode.MeasureArea);
        private void 距离量测ToolStripMenuItem_Click(object sender, EventArgs e) => SwitchTool(MapToolMode.MeasureDistance);

        public void 数据查询ToolStripMenuItem_Click(object sender, EventArgs e) => ItemDataQuery_Click(sender, e);
        public void ItemDataQuery_Click(object sender, EventArgs e) => new DataoSourceForm { CurrentMap = axMapControl2.Map }.Show();
        public void 查询ToolStripMenuItem_Click(object sender, EventArgs e) => new FormSelectByLocation { CurrentMap = axMapControl2.Map }.Show();
        public void 空间查询ToolStripMenuItem_Click(object sender, EventArgs e) => new FormSelectByLocation { CurrentMap = axMapControl2.Map }.Show();

        // ================= 另存为逻辑 =================
        public void 另存为ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog pSaveFileDialog = new SaveFileDialog
                {
                    Title = "另存为",
                    OverwritePrompt = true,
                    Filter = "ArcMap 文档(*.mxd)|*.mxd|ArcMap 模板(*.mxt)|*.mxt",
                    RestoreDirectory = true
                };
                if (pSaveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    ESRI.ArcGIS.Carto.IMapDocument pMapDocument = new ESRI.ArcGIS.Carto.MapDocumentClass();
                    pMapDocument.New(pSaveFileDialog.FileName);
                    pMapDocument.ReplaceContents(axMapControl2.Map as ESRI.ArcGIS.Carto.IMxdContents);
                    pMapDocument.Save(true, true);
                    pMapDocument.Close();
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        // ================= 符号化与制图触发 =================

        public void ItemSymbolize_Click(object sender, EventArgs e) => new FormSymbolize(axMapControl2, axTOCControl2).Show();
        public void ItemExport_Click(object sender, EventArgs e) => UIHelper.ExportMap(axMapControl2); // 假设 UIHelper 有导出逻辑
        public void 动态标注ToolStripMenuItem_Click(object sender, EventArgs e) => new FormLabeling { CurrentMap = axMapControl2.Map }.ShowDialog();

        public void ItemAddNorthArrow_Click(object sender, EventArgs e) => _layoutHelper.AddNorthArrow();
        public void ItemAddScaleBar_Click(object sender, EventArgs e) => _layoutHelper.AddScaleBar();
        public void ItemAddLegend_Click(object sender, EventArgs e) => _layoutHelper.AddLegend();
    }
}
