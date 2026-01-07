using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    /// <summary>
    /// Form1 的可视化/演示模式相关逻辑
    /// </summary>
    public partial class Form1
    {
        // [Member B] Modified: Integrated interactive dashboard layout trigger
        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            TabPage selectedTab = tabControl1.SelectedTab;
            bool isVisual = selectedTab.Text.Contains("可视化") || tabControl1.SelectedIndex == 2;
            bool isLayout = selectedTab.Text.Contains("布局") || tabControl1.SelectedIndex == 1;

            // 联动显隐 UI
            // 演示模式下隐藏左侧 TOC 和分割条，使地图充满主体
            this.axTOCControl2.Visible = !isVisual;
            this.splitter1.Visible = !isVisual;
            this.splitter2.Visible = !isVisual;

            // 菜单栏和状态栏始终保持可见以便操作
            this.menuStrip1.Visible = true;
            this.statusStrip1.Visible = true;

            if (isLayout) _layoutHelper.SynchronizeMap();
            if (isVisual) 
            {
                SyncToVisualMode();
                if (!_isVisualLayoutInitialized) InitVisualLayout(); // Ensure layout is set
            }
        }

        private bool _isVisualLayoutInitialized = false;
        private SplitContainer _splitContainerVisual;

        // [Member B] Added: Method to embed Dashboard into Visual Tab
        public void InitVisualLayout()
        {
            if (_isVisualLayoutInitialized) return;

            // 1. Create SplitContainer
            _splitContainerVisual = new SplitContainer();
            _splitContainerVisual.Dock = DockStyle.Fill;
            _splitContainerVisual.Orientation = Orientation.Horizontal; // Map on Top, Chart on Bottom? Or Vertical?
            // User screenshot shows a wide chart. Horizontal split makes sense for a "Bottom Dashboard".
            // Let's go with Horizontal: Panel1 (Top) = Map, Panel2 (Bottom) = Chart.
            _splitContainerVisual.Orientation = Orientation.Horizontal;
            _splitContainerVisual.SplitterDistance = (int)(tabPageVisual.Height * 0.7); // Map takes 70%

            // 2. Adjust Parent of axMapControlVisual
            // Currently axMapControlVisual is directly in tabPageVisual.
            // We need to move it to splitContainer.Panel1
            tabPageVisual.Controls.Remove(axMapControlVisual);
            _splitContainerVisual.Panel1.Controls.Add(axMapControlVisual);

            // 3. Embed Dashboard (FormChart) into SplitContainer.Panel2
            if (_dashboardForm == null || _dashboardForm.IsDisposed)
            {
                _dashboardForm = new FormChart();
            }
            _dashboardForm.SetMapControl(this.axMapControlVisual); // Link to Visual Map
            _dashboardForm.SetMainForm(this); // [Integration] Link Data
            _dashboardForm.TopLevel = false;
            _dashboardForm.FormBorderStyle = FormBorderStyle.None;
            _dashboardForm.Dock = DockStyle.Fill;
            _dashboardForm.Visible = true;
            
            _splitContainerVisual.Panel2.Controls.Add(_dashboardForm);

            // 4. Add SplitContainer to tabPageVisual (under Header)
            tabPageVisual.Controls.Add(_splitContainerVisual);
            _splitContainerVisual.BringToFront(); // Ensure it's visible
            
            // Ensure Header stays at top? Header is Dock=Top, SplitContainer is Dock=Fill.
            // If we Add SplitContainer *after* Header is already there, WinForms Dock logic works by Z-order.
            // We usually need the Fill control to be added *first* in code or use SendToBack/BringToFront carefully.
            // Actually, Dock=Top controls must be Z-Order LAST (added first to collection usually) to stay at top.
            // Safe bet: Add SplitContainer, then ensure PanelVisualHeader is BroughtToFront if it overlaps,
            // or just rely on Dock=Top of Header and Dock=Fill of SplitContainer working together.
            // Let's explicitly re-dock header to be safe or just add SplitContainer.
            
            _isVisualLayoutInitialized = true;
        }

        private void SyncToVisualMode()
        {
            if (axMapControlVisual == null || axMapControl2 == null) return;
            try
            {
                axMapControlVisual.ClearLayers();
                // [修复] 必须正向遍历添加图层，否则底图会覆盖在上层
                for (int i = 0; i < axMapControl2.LayerCount; i++)
                {
                    axMapControlVisual.AddLayer(axMapControl2.get_Layer(i));
                }

                EnableLabelsForAllLayers();

                // 同步当前视图范围
                if (axMapControl2.LayerCount > 0)
                {
                    axMapControlVisual.Extent = axMapControl2.Extent;
                }

                axMapControlVisual.ActiveView.Refresh();
                axMapControlVisual.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show("同步演示视图失败: " + ex.Message);
            }
        }

        private void EnableLabelsForAllLayers()
        {
            for (int i = 0; i < axMapControlVisual.LayerCount; i++)
            {
                IGeoFeatureLayer gfl = axMapControlVisual.get_Layer(i) as IGeoFeatureLayer;
                if (gfl == null) continue;

                string[] labelFields = { "名称", "项目名称", "Name", "TITLE" };
                string targetField = "";
                if (gfl.FeatureClass == null) continue;
                for (int j = 0; j < gfl.FeatureClass.Fields.FieldCount; j++)
                {
                    string fName = gfl.FeatureClass.Fields.get_Field(j).Name;
                    foreach (var lf in labelFields) if (fName.Equals(lf, StringComparison.OrdinalIgnoreCase)) { targetField = fName; break; }
                    if (!string.IsNullOrEmpty(targetField)) break;
                }

                if (!string.IsNullOrEmpty(targetField))
                {
                    gfl.DisplayAnnotation = true;
                    ILabelEngineLayerProperties engineProps = new LabelEngineLayerPropertiesClass { Expression = "[" + targetField + "]" };
                    ITextSymbol textSym = new TextSymbolClass { Size = 8 };
                    stdole.IFontDisp font = (stdole.IFontDisp)new stdole.StdFontClass { Name = "微软雅黑" };
                    textSym.Font = font;
                    engineProps.Symbol = textSym;
                    gfl.AnnotationProperties.Clear();
                    gfl.AnnotationProperties.Add(engineProps as IAnnotateLayerProperties);
                }
            }
        }

        // --- 演示模式交互 ---

        private void BtnBackToPro_Click(object sender, EventArgs e) => tabControl1.SelectedIndex = 0;

        private void BtnVisualPan_Click(object sender, EventArgs e)
        {
            // 使用内置漫游工具，操作手感与主界面完全一致
            axMapControlVisual.CurrentTool = new ESRI.ArcGIS.Controls.ControlsMapPanToolClass();
        }

        private void BtnVisualZoomIn_Click(object sender, EventArgs e)
        {
            IEnvelope env = axMapControlVisual.Extent;
            env.Expand(0.5, 0.5, true);
            axMapControlVisual.Extent = env;
            axMapControlVisual.ActiveView.Refresh();
        }

        private void BtnVisualZoomOut_Click(object sender, EventArgs e)
        {
            IEnvelope env = axMapControlVisual.Extent;
            env.Expand(2.0, 2.0, true);
            axMapControlVisual.Extent = env;
            axMapControlVisual.ActiveView.Refresh();
        }

        private void BtnVisualFull_Click(object sender, EventArgs e)
        {
            axMapControlVisual.Extent = axMapControlVisual.FullExtent;
            axMapControlVisual.ActiveView.Refresh();
        }

        private void BtnVisualSync_Click(object sender, EventArgs e)
        {
            SyncToVisualMode();
            MessageBox.Show("图层同步完成！");
        }

        private void BtnVisualSearch_Click(object sender, EventArgs e)
        {
            string keyword = txtVisualSearch.Text.Trim();
            if (string.IsNullOrEmpty(keyword)) return;
            axMapControlVisual.Map.ClearSelection();

            bool found = false;
            for (int i = 0; i < axMapControlVisual.LayerCount; i++)
            {
                IFeatureLayer fl = axMapControlVisual.get_Layer(i) as IFeatureLayer;
                if (fl == null) continue;

                string targetField = "";
                string[] pFields = { "名称", "项目名称", "Name", "TITLE" };
                foreach (var f in pFields) if (fl.FeatureClass.Fields.FindField(f) != -1) { targetField = f; break; }
                if (string.IsNullOrEmpty(targetField)) continue;

                IQueryFilter qf = new QueryFilterClass { WhereClause = $"{targetField} LIKE '%{keyword}%'" };
                IFeatureCursor cursor = fl.Search(qf, true);
                IFeature feature = cursor.NextFeature();

                if (feature != null)
                {
                    if (feature.Shape.GeometryType == esriGeometryType.esriGeometryPoint)
                    {
                        IPoint pt = feature.Shape as IPoint;
                        IEnvelope env = new EnvelopeClass { SpatialReference = axMapControlVisual.Map.SpatialReference };
                        env.XMin = pt.X - 0.075; env.XMax = pt.X + 0.075;
                        env.YMin = pt.Y - 0.075; env.YMax = pt.Y + 0.075;
                        axMapControlVisual.Extent = env;
                    }
                    else
                    {
                        IEnvelope env = feature.Shape.Envelope; env.Expand(1.5, 1.5, true);
                        axMapControlVisual.Extent = env;
                    }

                    axMapControlVisual.Map.SelectFeature(fl, feature);
                    axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                    axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
                    found = true;
                    System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);
                    break;
                }
                System.Runtime.InteropServices.Marshal.ReleaseComObject(cursor);
            }
            if (!found) MessageBox.Show("未找到匹配项", "提示");
        }

        private void AxMapControlVisual_OnMouseDown(object sender, IMapControlEvents2_OnMouseDownEvent e)
        {
            // 仅在左键点击 且 没有启用任何特殊工具时触发
            if (e.button != 1 || axMapControlVisual.CurrentTool != null) return;
            
            IActiveView av = axMapControlVisual.ActiveView;
            IPoint pt = av.ScreenDisplay.DisplayTransformation.ToMapPoint(e.x, e.y);
            double tol = 5 * av.ScreenDisplay.DisplayTransformation.Resolution;
            IEnvelope env = pt.Envelope; env.Expand(tol, tol, false);

            // [修复] 1. 先清除之前的所有选择，避免全选错觉
            axMapControlVisual.Map.ClearSelection();

            // [修复] 2. 智能锁定非遗图层
            IFeatureLayer targetLayer = null;
            for (int i = 0; i < axMapControlVisual.LayerCount; i++)
            {
                ILayer layer = axMapControlVisual.get_Layer(i);
                if (layer is IFeatureLayer && layer.Visible)
                {
                    string ln = layer.Name;
                    if (ln.Contains("非遗") || ln.Contains("名录") || ln.Contains("项目") || ln.Contains("ICH"))
                    {
                        targetLayer = layer as IFeatureLayer;
                        break;
                    }
                }
            }

            // [修复] 3. 仅从目标图层选择，防止点到背景多边形
            if (targetLayer != null)
            {
                IFeatureSelection featSel = targetLayer as IFeatureSelection;
                ISpatialFilter spatialFilter = new SpatialFilterClass();
                spatialFilter.Geometry = env;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                featSel.SelectFeatures(spatialFilter, esriSelectionResultEnum.esriSelectionResultNew, false);
            }
            else
            {
                // 如果没找到特定图层，才允许全图查询
                axMapControlVisual.Map.SelectByShape(env, null, false);
            }

            // 刷新选择集显示
            axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);

            IEnumFeature enumFeat = axMapControlVisual.Map.FeatureSelection as IEnumFeature;
            enumFeat.Reset();
            IFeature feat = enumFeat.Next();
            
            // 只有当选中的要素字段较多（即非简单边界）时才弹出详情
            if (feat != null && feat.Fields.FieldCount > 5)
            {
                new FormICHDetails(feat).ShowDialog();
            }
        }

        /// <summary>
        /// [Member B] 根据年份过滤地图上的非遗要素
        /// </summary>
        public void FilterMapByYear(int year)
        {
            try
            {
                // 1. 查找非遗图层
                IFeatureLayer heritageLayer = null;
                for (int i = 0; i < axMapControlVisual.LayerCount; i++)
                {
                    ILayer layer = axMapControlVisual.get_Layer(i);
                    if (layer is IFeatureLayer)
                    {
                        string ln = layer.Name;
                        if (ln.Contains("非遗") || ln.Contains("名录") || ln.Contains("项目") || ln.Contains("ICH"))
                        {
                            heritageLayer = layer as IFeatureLayer;
                            break;
                        }
                    }
                }

                if (heritageLayer == null) return;

                // 2. 检查是否有"公布时间"字段
                IFeatureClass fc = heritageLayer.FeatureClass;
                int timeFieldIndex = fc.FindField("公布时间");
                if (timeFieldIndex == -1) return;

                // 3. 构建双模式SQL过滤条件（复用Form1.Data.cs的逻辑）
                int maxBatch = 1;
                if (year >= 2006) maxBatch = 1;
                if (year >= 2008) maxBatch = 2;
                if (year >= 2011) maxBatch = 3;
                if (year >= 2014) maxBatch = 4;
                if (year >= 2021) maxBatch = 5;

                string sqlFilter = $"(公布时间 >= 1900 AND 公布时间 <= {year}) OR (公布时间 >= 1 AND 公布时间 <= {maxBatch})";

                // 4. 应用Definition Expression
                IFeatureLayerDefinition layerDef = heritageLayer as IFeatureLayerDefinition;
                if (layerDef != null)
                {
                    layerDef.DefinitionExpression = sqlFilter;
                }

                // 5. 刷新地图视图
                axMapControlVisual.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
            }
            catch (Exception)
            {
                // 静默处理，不影响主流程
            }
        }
    }
}
