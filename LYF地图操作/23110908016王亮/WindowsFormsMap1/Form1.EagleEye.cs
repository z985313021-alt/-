// [Member E] Added: Eagle Eye functionality for both Pro and Visual modes
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geometry;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    public partial class Form1
    {
        private AxMapControl _eagleEyePro;
        private AxMapControl _eagleEyeVisual;
        private Panel _panelEaglePro;
        private Panel _panelEagleVisual;

        /// <summary>
        /// 初始化鹰眼图逻辑
        /// </summary>
        public void InitEagleEye()
        {
            // 1. 专业模式鹰眼
            InitEagleEyePro();

            // 2. 演示模式鹰眼
            InitEagleEyeVisual();

            // 3. 初始同步
            SyncEagleEyeLayers();
        }

        private void InitEagleEyePro()
        {
            if (this.tabPage1 == null) return;

            _panelEaglePro = CreateEaglePanel();
            _eagleEyePro = CreateEagleMapControl();
            _panelEaglePro.Controls.Add(_eagleEyePro);

            // 叠放在专业版 TabPage 上
            this.tabPage1.Controls.Add(_panelEaglePro);
            _panelEaglePro.BringToFront();

            // 监听句柄创建，确保同步成功
            _eagleEyePro.HandleCreated += (s, e) => this.SyncEagleEyeLayers();

            // 定位到右上角
            _panelEaglePro.Location = new System.Drawing.Point(this.tabPage1.Width - _panelEaglePro.Width - 25, 10);
            _panelEaglePro.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            // 事件绑定
            axMapControl2.OnExtentUpdated += (s, e) =>
            {
                if (this.tabControl1.SelectedIndex == 0) // 仅专业模式可见时更新
                    UpdateEagleEnvelope(_eagleEyePro, axMapControl2.Extent);
            };
            _eagleEyePro.OnMouseDown += (s, e) => MoveMainMapCenter(axMapControl2, e.mapX, e.mapY);
        }

        private void InitEagleEyeVisual()
        {
            if (axMapControlVisual == null) return;

            _panelEagleVisual = CreateEaglePanel();
            _eagleEyeVisual = CreateEagleMapControl();
            _panelEagleVisual.Controls.Add(_eagleEyeVisual);

            // 叠放在演示页 TabPage 上
            this.tabPageVisual.Controls.Add(_panelEagleVisual);
            _panelEagleVisual.BringToFront();

            // 监听句柄创建
            _eagleEyeVisual.HandleCreated += (s, e) => this.SyncEagleEyeLayers();

            _panelEagleVisual.Location = new System.Drawing.Point(this.tabPageVisual.Width - _panelEagleVisual.Width - 25, 55);
            _panelEagleVisual.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            // 事件绑定
            axMapControlVisual.OnExtentUpdated += (s, e) =>
            {
                if (this.tabControl1.SelectedIndex == 2) // 仅演示模式可见时更新 (索引为2)
                    UpdateEagleEnvelope(_eagleEyeVisual, axMapControlVisual.Extent);
            };
            _eagleEyeVisual.OnMouseDown += (s, e) => MoveMainMapCenter(axMapControlVisual, e.mapX, e.mapY);

            // 当切换到演示页时强制刷新一次位置和同步
            this.tabControl1.SelectedIndexChanged += (s, e) =>
            {
                if (this.tabControl1.SelectedIndex == 2)
                {
                    SyncEagleEyeLayers();
                    UpdateEagleEnvelope(_eagleEyeVisual, axMapControlVisual.Extent);
                }
            };
        }

        private Panel CreateEaglePanel()
        {
            return new Panel
            {
                Size = new Size(160, 120), // 略微缩小，节省空间
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.White,
                Padding = new Padding(2)
            };
        }

        private AxMapControl CreateEagleMapControl()
        {
            AxMapControl map = new AxMapControl();
            ((System.ComponentModel.ISupportInitialize)(map)).BeginInit();
            map.Dock = DockStyle.Fill;
            ((System.ComponentModel.ISupportInitialize)(map)).EndInit();
            return map;
        }

        private void UpdateEagleEnvelope(AxMapControl eagleMap, IEnvelope mainExtent)
        {
            if (eagleMap == null || mainExtent == null) return;

            // 核心：如果控件尚未初始化完成或已销毁，直接跳过，防止 InvalidActiveXStateException
            if (!eagleMap.IsHandleCreated || eagleMap.IsDisposed) return;

            try
            {
                IGraphicsContainer container = eagleMap.Map as IGraphicsContainer;
                container.DeleteAllElements();

                IElement element = new RectangleElementClass();
                element.Geometry = mainExtent;

                // 设置红框样式
                IRgbColor color = new RgbColorClass();
                color.Red = 255;
                color.Green = 0;
                color.Blue = 0;

                ISimpleLineSymbol lineSym = new SimpleLineSymbolClass();
                lineSym.Color = color;
                lineSym.Width = 2;

                ISimpleFillSymbol fillSym = new SimpleFillSymbolClass();
                fillSym.Outline = lineSym;
                fillSym.Style = esriSimpleFillStyle.esriSFSHollow;

                IFillShapeElement fillElement = element as IFillShapeElement;
                fillElement.Symbol = fillSym;

                container.AddElement(element, 0);
                eagleMap.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
            }
            catch (Exception ex)
            {
                // 静默处理可能的 COM 异常，保证主界面不崩溃
                System.Diagnostics.Debug.WriteLine("鹰眼更新异常: " + ex.Message);
            }
        }

        private void MoveMainMapCenter(AxMapControl mainMap, double x, double y)
        {
            IPoint pt = new PointClass();
            pt.PutCoords(x, y);
            mainMap.CenterAt(pt);
            mainMap.ActiveView.Refresh();
        }

        /// <summary>
        /// 同步底图图层到鹰眼图
        /// </summary>
        public void SyncEagleEyeLayers()
        {
            // 使用 BeginInvoke 确保在 COM 控件完全准备好加载图层
            if (this.IsHandleCreated)
            {
                this.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        // 1. 同步专业版鹰眼
                        if (axMapControl2 != null && axMapControl2.LayerCount > 0)
                            CopyBaseLayers(axMapControl2, _eagleEyePro);

                        // 2. 同步演示版鹰眼
                        // 优先从演示地图控件同步，如果没图层则从专业版同步
                        if (axMapControlVisual != null && axMapControlVisual.LayerCount > 0)
                            CopyBaseLayers(axMapControlVisual, _eagleEyeVisual);
                        else if (axMapControl2 != null && axMapControl2.LayerCount > 0)
                            CopyBaseLayers(axMapControl2, _eagleEyeVisual);
                    }
                    catch { }
                }));
            }
        }

        private void CopyBaseLayers(AxMapControl source, AxMapControl target)
        {
            if (source == null || target == null) return;
            if (!target.IsHandleCreated || target.IsDisposed) return;

            try
            {
                target.ClearLayers();
                for (int i = source.LayerCount - 1; i >= 0; i--)
                {
                    ILayer layer = source.get_Layer(i);
                    target.AddLayer(layer);
                }
                target.Extent = target.FullExtent;
                target.ActiveView.Refresh();
            }
            catch { }
        }
    }
}
