// [Agent (通用辅助)] Modified: 全量中文化注释深挖
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using System;
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

                // 转换像素容差到地图单位。
                // 5 像素的点击容差
                double dist = targetMapControl.ActiveView.ScreenDisplay.DisplayTransformation.FromPoints(5);
                pEnv.PutCoords(pPoint.X - dist, pPoint.Y - dist, pPoint.X + dist, pPoint.Y + dist);

                // 2. 遍历所有图层寻找匹配项
                IFeature pFoundFeature = null;

                // 从顶部图层开始向下遍历
                for (int i = 0; i < targetMapControl.LayerCount; i++)
                {
                    ILayer l = targetMapControl.get_Layer(i);
                    if (l is IFeatureLayer fl && fl.Visible)
                    {
                        // 目前仅针对点图层进行识别（非遗点位）
                        if (fl.FeatureClass.ShapeType == esriGeometryType.esriGeometryPoint)
                        {
                            ISpatialFilter pSpatialFilter = new SpatialFilterClass();
                            pSpatialFilter.Geometry = pEnv;
                            pSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;

                            IFeatureCursor pCursor = fl.Search(pSpatialFilter, false);
                            pFoundFeature = pCursor.NextFeature();

                            System.Runtime.InteropServices.Marshal.ReleaseComObject(pCursor);

                            // 如果找到要素，则停止寻找
                            if (pFoundFeature != null) break;
                        }
                    }
                }

                if (pFoundFeature != null)
                {
                    // 4. 显示非遗详情窗体
                    FormICHDetails form = new FormICHDetails(pFoundFeature);
                    form.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("要素识别错误: " + ex.Message);
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
