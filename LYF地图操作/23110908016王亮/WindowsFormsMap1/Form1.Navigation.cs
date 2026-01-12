// [Agent (通用辅助)] Modified: 全量中文化注释深挖
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.esriSystem;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    /// <summary>
    /// Form1 的导航与基础 GIS 交互逻辑
    /// </summary>
    public partial class Form1
    {
        // ================= 标准地图导航事件 =================

        public void 漫游ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SwitchTool(MapToolMode.Pan);
        }

        public void 刷新ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (axMapControl2 == null) return;
            axMapControl2.ActiveView.Refresh();
        }

        public void 刷新至初始视图ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (axMapControl2 == null) return;
            axMapControl2.Extent = axMapControl2.FullExtent;
        }

        // 鼠标按下路由
        private void axMapControl2_OnMouseDown(object sender, IMapControlEvents2_OnMouseDownEvent e)
        {
            if (e.button != 1) return; // 只处理左键

            switch (_currentToolMode)
            {
                case MapToolMode.Pan:
                    axMapControl2.Pan();
                    break;
                case MapToolMode.CreateFeature:
                    _editorHelper.OnMouseDown(e.x, e.y);
                    break;
                case MapToolMode.MeasureDistance:
                case MapToolMode.MeasureArea:
                    _measureHelper.OnMouseDown(e.x, e.y);
                    break;
                // [Member C] 智能工具箱交互
                case MapToolMode.BufferPoint:
                    {
                        // [Modified] New Interactive Workflow
                        IPoint bufferPt = axMapControl2.ActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(e.x, e.y);
                        if (SmartBufferForm != null && !SmartBufferForm.IsDisposed) SmartBufferForm.OnGeometryCaptured(bufferPt);
                        else ExecutePointBuffer(e.x, e.y); // Fallback
                    }
                    break;
                case MapToolMode.BufferLine:
                    IGeometry lineGeo = axMapControl2.TrackLine();
                    if (SmartBufferForm != null && !SmartBufferForm.IsDisposed) SmartBufferForm.OnGeometryCaptured(lineGeo);
                    else ExecuteLineBuffer(lineGeo); // Fallback
                    break;
                case MapToolMode.PickRoutePoint:
                    IPoint pt = axMapControl2.ActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(e.x, e.y);
                    // [修复] 显式指定坐标系为当前地图坐标系，防止投影计算偏差
                    pt.SpatialReference = axMapControl2.SpatialReference;
                    if (_routeForm != null && !_routeForm.IsDisposed)
                    {
                        _routeForm.AddPoint(pt); // 回传给弹窗
                    }
                    break;
                case MapToolMode.QueryBox:
                    IEnvelope env = axMapControl2.TrackRectangle();
                    PerformSpatialQuery(env);
                    break;
                case MapToolMode.QueryPolygon:
                    IGeometry poly = axMapControl2.TrackPolygon();
                    PerformSpatialQuery(poly);
                    break;
                default:
                    // [Member B] 要素识别 (Identify) 逻辑
                    // 仅在默认工具模式（箭头）下触发
                    DoIdentify(e.x, e.y, axMapControl2);
                    break;
            }
        }

        // [Member B] 要素识别 (Identify) 共享逻辑
        private void DoIdentify(int x, int y, ESRI.ArcGIS.Controls.AxMapControl targetMapControl = null)
        {
            try
            {
                if (targetMapControl == null) targetMapControl = this.axMapControl2;

                // 1. 创建用于空间查询的缓冲区 (Envelope)
                IEnvelope pEnv = new EnvelopeClass();
                IPoint pPoint = targetMapControl.ActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(x, y);

                // [Optimization] 增加容差到 20 像素，方便点中细小图标
                double dist = targetMapControl.ActiveView.ScreenDisplay.DisplayTransformation.FromPoints(20);
                pEnv.PutCoords(pPoint.X - dist, pPoint.Y - dist, pPoint.X + dist, pPoint.Y + dist);

                // [Agent Modified] 判断是否处于自定义识别或联网搜索模式 (增加 Identify 指针支持)
                if (targetMapControl.CurrentTool == null &&
                    (targetMapControl.MousePointer == esriControlsMousePointer.esriPointerCrosshair ||
                     targetMapControl.MousePointer == esriControlsMousePointer.esriPointerIdentify))
                {
                    DoWebSearch(pEnv, targetMapControl);
                    return;
                }

                // 2. 遍历所有图层寻找匹配项 (原来的简单逻辑用于漫游等情况的备用，虽然设了null tool其实很少走到这)
                // ... (保留原有的简单逻辑作为 fallback 或者不做任何事) ...
                // 实际上如果只是漫游，根本不会进到这个事件处理里（漫游工具有自己的逻辑）。
                // 只有当 CurrentTool 为 null 时才会进到这里。

                // 为了兼容之前逻辑，这里也可以保留一个简单的识别，但既然有了独立按钮，
                // 我们可以让默认点击不做任何事，或者仅通过 DoWebSearch 触发。
                // 鉴于用户要求“原有识别按钮”是原生的，那个按钮走的是 ArcGIS 自带逻辑，不走这里。
                // 这里只处理我们自定义的工具。
            }
            catch (Exception ex)
            {
                Console.WriteLine("交互错误: " + ex.Message);
            }
        }

        // [Agent Modified] 独立的联网搜索逻辑，支持跨坐标系、穿透 Group Layer
        private void DoWebSearch(IEnvelope pEnv, ESRI.ArcGIS.Controls.AxMapControl targetMapControl)
        {
            try
            {
                IMap targetMap = targetMapControl.Map;
                // 1. 确保搜索框具有地图的空间参考
                pEnv.SpatialReference = targetMap.SpatialReference;

                IFeature pFoundFeature = null;

                // 2. 递归遍历所有图层
                IEnumLayer pEnumLayer = targetMap.get_Layers(null, true);
                pEnumLayer.Reset();
                ILayer pLayer = pEnumLayer.Next();

                while (pLayer != null)
                {
                    if (pLayer.Visible && pLayer is IFeatureLayer fl)
                    {
                        // 仅关注点图层
                        if (fl.FeatureClass != null && fl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPoint)
                        {
                            try
                            {
                                // 获取图层的空间参考
                                ISpatialReference layerSR = (fl as IGeoDataset)?.SpatialReference;

                                // 克隆搜索框，以免修改原始对象影响后续图层
                                IClone envClone = pEnv as IClone;
                                IEnvelope queryEnv = envClone.Clone() as IEnvelope;

                                // [关键修复] 如果图层坐标系与地图不同，必须进行投影变换！
                                if (layerSR != null && pEnv.SpatialReference != null)
                                {
                                    queryEnv.Project(layerSR);
                                }

                                ISpatialFilter pSpatialFilter = new SpatialFilterClass();
                                pSpatialFilter.Geometry = queryEnv;
                                pSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                                pSpatialFilter.GeometryField = fl.FeatureClass.ShapeFieldName;

                                IFeatureCursor pCursor = fl.Search(pSpatialFilter, false);
                                pFoundFeature = pCursor.NextFeature();
                                System.Runtime.InteropServices.Marshal.ReleaseComObject(pCursor);

                                if (pFoundFeature != null) break;
                            }
                            catch { }
                        }
                    }
                    pLayer = pEnumLayer.Next();
                }

                if (pFoundFeature != null)
                {
                    // [Agent Modified] 保持窗口单例显示，并自动定位到右侧
                    if (_activeDetailsForm != null && !_activeDetailsForm.IsDisposed)
                    {
                        _activeDetailsForm.Close();
                    }

                    _activeDetailsForm = new FormICHDetails(pFoundFeature);

                    // 获取鹰眼面板引用 (如果是演示模式则用演示面板)
                    Panel eaglePanel = (tabControl1.SelectedIndex == 2) ? _panelEagleVisual : _panelEaglePro;

                    if (eaglePanel != null)
                    {
                        _activeDetailsForm.AlignToSidebar(this, eaglePanel);
                    }

                    _activeDetailsForm.Show();
                }
                else
                {
                    MessageBox.Show("未找到非遗项目。\n建议：\n1. 请确保点击的是绿色的小点图标。\n2. 尝试放大地图后再点击。", "搜索提示");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("搜索出错: " + ex.Message);
            }
        }

        // 鼠标移动路由
        private void axMapControl1_OnMouseMove(object sender, IMapControlEvents2_OnMouseMoveEvent e)
        {
            switch (_currentToolMode)
            {
                case MapToolMode.CreateFeature:
                    _editorHelper.OnMouseMove(e.x, e.y);
                    break;
                case MapToolMode.MeasureDistance:
                case MapToolMode.MeasureArea:
                    _measureHelper.OnMouseMove(e.x, e.y);
                    break;
            }
        }

        // 双击结束逻辑
        public void axMapControl1_OnDoubleClick(object sender, IMapControlEvents2_OnDoubleClickEvent e)
        {
            switch (_currentToolMode)
            {
                case MapToolMode.CreateFeature:
                    _editorHelper.OnDoubleClick();
                    break;
                case MapToolMode.MeasureArea:
                    _measureHelper.OnDoubleClick();
                    SwitchTool(MapToolMode.None);
                    break;
                case MapToolMode.MeasureDistance:
                    _measureHelper.OnDoubleClick();
                    SwitchTool(MapToolMode.None);
                    break;
            }
        }

        // 清除选择集
        private void 清除选择集ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (axMapControl2.Map == null) return;
            axMapControl2.Map.ClearSelection();
            axMapControl2.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
            axMapControl2.MousePointer = esriControlsMousePointer.esriPointerArrow;
            SwitchTool(MapToolMode.None);
            MessageBox.Show("已清除选择！");
        }

        // ================= TOC 交互逻辑 =================

        private void AxTOCControl2_OnMouseDown(object sender, ITOCControlEvents_OnMouseDownEvent e)
        {
            if (e.button != 2) return; // 右键点击

            esriTOCControlItem item = esriTOCControlItem.esriTOCControlItemNone;
            IBasicMap map = null;
            ILayer layer = null;
            object other = null;
            object index = null;

            axTOCControl2.HitTest(e.x, e.y, ref item, ref map, ref layer, ref other, ref index);

            if (item == esriTOCControlItem.esriTOCControlItemLayer && layer != null)
            {
                ContextMenuStrip contextMenu = new ContextMenuStrip();

                ToolStripMenuItem propItem = new ToolStripMenuItem("属性 (符号化)");
                propItem.Click += (s, ev) => { new FormSymbolize(this.axMapControl2, this.axTOCControl2).Show(); };
                contextMenu.Items.Add(propItem);

                ToolStripMenuItem removeItem = new ToolStripMenuItem("移除图层");
                removeItem.Click += (s, ev) =>
                {
                    axMapControl2.Map.DeleteLayer(layer);
                    axMapControl2.ActiveView.Refresh();
                    axTOCControl2.Update();
                };
                contextMenu.Items.Add(removeItem);

                ToolStripMenuItem zoomItem = new ToolStripMenuItem("缩放到图层");
                zoomItem.Click += (s, ev) =>
                {
                    if (layer is IGeoDataset geo)
                    {
                        axMapControl2.Extent = geo.Extent;
                        axMapControl2.ActiveView.Refresh();
                    }
                };
                contextMenu.Items.Add(zoomItem);

                contextMenu.Show(axTOCControl2, e.x, e.y);
            }
        }
    }
}
