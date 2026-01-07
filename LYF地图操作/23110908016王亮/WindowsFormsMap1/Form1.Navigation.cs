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
            if (e.button != 2) return; // Right Click

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
