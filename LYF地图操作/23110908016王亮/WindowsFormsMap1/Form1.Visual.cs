// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
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
        // [Member B] 修改：集成了交互式看板布局触发逻辑
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

        // [Member B] 新增：将统计看板（FormChart）嵌入可视化选项卡的方法
        public void InitVisualLayout()
        {
            if (_isVisualLayoutInitialized) return;

            // 1. Create SplitContainer
            _splitContainerVisual = new SplitContainer();
            _splitContainerVisual.Dock = DockStyle.Fill;
            _splitContainerVisual.Orientation = Orientation.Horizontal; // 上图下表布局
            // 用户截图显示图表较宽，水平拆分（上下结构）最符合“底端面板”设计
            _splitContainerVisual.Orientation = Orientation.Horizontal;
            _splitContainerVisual.SplitterDistance = (int)(tabPageVisual.Height * 0.7); // 地图占据
            // 2. 调整 axMapControlVisual 的父容器
            // 目前 axMapControlVisual 直接位于 tabPageVisual 中。
            axMapControlVisual.Parent = null;
            _splitContainerVisual.Panel1.Controls.Add(axMapControlVisual);

            // 3. 将仪表板 (FormChart) 嵌入 SplitContainer.Panel2
            if (_dashboardForm == null || _dashboardForm.IsDisposed)
            {
                _dashboardForm = new FormChart();
            }
            _dashboardForm.SetMapControl(this.axMapControlVisual); // 关联到可视化地图
            _dashboardForm.SetMainForm(this); // [集成] 关联数据源
            _dashboardForm.TopLevel = false;
            _dashboardForm.FormBorderStyle = FormBorderStyle.None;
            _dashboardForm.Dock = DockStyle.Fill;
            _dashboardForm.Visible = true;

            _splitContainerVisual.Panel2.Controls.Add(_dashboardForm);

            // 4. 将 SplitContainer 添加到 tabPageVisual (在 Header 面板下方)
            tabPageVisual.Controls.Add(_splitContainerVisual);

            // [Member B] Fix: Layout Order
            // 我们希望 Header 首先停靠在顶部（保留空间），然后 SplitContainer 填充剩余空间。
            // 在 WinForms 中，较低的 Z 轴顺序（Back）会先停靠。
            panelVisualHeader.SendToBack();
            _splitContainerVisual.BringToFront();

            // 确保 Header 保持在顶部？Header 是 Dock=Top，SplitContainer 是 Dock=Fill。
            // 如果我们在 Header 已经在那里之后添加 SplitContainer，WinForms 的 Dock 逻辑会受到 Z-order 影响。
            // 此时 Header 是 Top，SplitContainer 是 Fill，逻辑正确。
            // 实际上，Dock=Top 的控件必须是 Z-Order LAST（通常是首先添加到集合中）才能保持在顶部。
            // 安全的做法是：添加 SplitContainer，然后确保 PanelVisualHeader 在重叠时被 BringToFront，
            // 或者仅仅依赖 Header 的 Dock=Top 和 SplitContainer 的 Dock=Fill 协同工作。
            // 让我们明确地重新停靠 Header 以确保安全，或者仅仅添加 SplitContainer。

            _isVisualLayoutInitialized = true;
        }

        private void SyncToVisualMode()
        {
            if (axMapControlVisual == null || axMapControl2 == null) return;
            try
            {
                axMapControlVisual.ClearLayers();

                // [Member E] 修改：对图层进行排序，确保点要素（非遗项目）显示在最上方
                List<ILayer> pointLayers = new List<ILayer>();
                List<ILayer> otherLayers = new List<ILayer>();

                for (int i = 0; i < axMapControl2.LayerCount; i++)
                {
                    ILayer layer = axMapControl2.get_Layer(i);
                    IFeatureLayer fl = layer as IFeatureLayer;
                    if (fl != null && fl.FeatureClass != null && fl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPoint)
                    {
                        pointLayers.Add(layer);
                    }
                    else
                    {
                        otherLayers.Add(layer);
                    }
                }

                // Add background layers first
                foreach (var layer in otherLayers) axMapControlVisual.AddLayer(layer);
                // Add point layers last (on top)
                foreach (var layer in pointLayers) axMapControlVisual.AddLayer(layer);

                EnableLabelsForAllLayers();

                // [Member E] 修改：切换到演示模式时默认显示全图范围
                axMapControlVisual.Extent = axMapControlVisual.FullExtent;

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

        private void BtnVisualArrow_Click(object sender, EventArgs e)
        {
            // Reset to pointer mode (Identify)
            axMapControlVisual.CurrentTool = null;
            axMapControlVisual.MousePointer = esriControlsMousePointer.esriPointerArrow;
        }

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
            if (e.button != 1) return;
            // [Member B] 在演示模式下启用“要素识别 (Identify)”
            // 由于演示模式下没有激活其他复杂的测量/编辑工具，
            // 默认鼠标左键点击即为识别要素属性。
            DoIdentify(e.x, e.y, axMapControlVisual);
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
