using System;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.esriSystem;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 布局视图辅助类 (第8章)
    /// 负责版面元素的添加（指北针、比例尺、图例）以及数据视图与布局视图的同步
    /// </summary>
    public class LayoutHelper
    {
        private AxPageLayoutControl _pageLayoutControl;
        private AxMapControl _mapControl;

        public LayoutHelper(AxPageLayoutControl pageLayoutControl, AxMapControl mapControl)
        {
            _pageLayoutControl = pageLayoutControl;
            _mapControl = mapControl;
        }

        /// <summary>
        /// 同步数据：将 MapControl 的内容复制到 PageLayoutControl
        /// </summary>
        public void SynchronizeMap()
        {
            if (_pageLayoutControl == null || _mapControl == null) return;

            IObjectCopy objectCopy = new ObjectCopyClass();
            object toCopyMap = _mapControl.Map;
            object copiedMap = objectCopy.Copy(toCopyMap);
            object toOverwriteMap = _pageLayoutControl.ActiveView.FocusMap;

            objectCopy.Overwrite(copiedMap, ref toOverwriteMap);

            _pageLayoutControl.ActiveView.Refresh();
        }

        /// <summary>
        /// 添加指北针
        /// </summary>
        /// <summary>
        /// 添加指北针
        /// </summary>
        public void AddNorthArrow()
        {
            if (_pageLayoutControl == null) return;

            IPageLayout pageLayout = _pageLayoutControl.PageLayout;
            IGraphicsContainer container = pageLayout as IGraphicsContainer;
            IActiveView activeView = pageLayout as IActiveView;

            IMapFrame mapFrame = (IMapFrame)container.FindFrame(((IActiveView)pageLayout).FocusMap);
            if (mapFrame == null) return;

            // 使用 UID 创建 SurroundFrame
            UID uid = new UIDClass();
            uid.Value = "esriCarto.MarkerNorthArrow";

            // 创建 SurroundFrame
            IMapSurroundFrame mapSurroundFrame = mapFrame.CreateSurroundFrame(uid, null);
            if (mapSurroundFrame == null) return;

            // 设置位置 (左下角)
            IElement element = (IElement)mapSurroundFrame;
            IEnvelope env = new EnvelopeClass();
            env.PutCoords(2, 2, 4, 4); // 避免放在中间 (10, 10)
            element.Geometry = env;

            // 添加到页面
            container.AddElement(element, 0);
            
            // 激活选择工具，方便用户移动或删除
            _pageLayoutControl.CurrentTool = null; 
            
            activeView.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }

        /// <summary>
        /// 添加比例尺
        /// </summary>
        public void AddScaleBar()
        {
            if (_pageLayoutControl == null) return;

            IPageLayout pageLayout = _pageLayoutControl.PageLayout;
            IGraphicsContainer container = pageLayout as IGraphicsContainer;
            IActiveView activeView = pageLayout as IActiveView;

            IMapFrame mapFrame = (IMapFrame)container.FindFrame(((IActiveView)pageLayout).FocusMap);
            if (mapFrame == null) return;

            // 使用 UID 创建 SurroundFrame
            UID uid = new UIDClass();
            uid.Value = "esriCarto.ScaleLine";

            IMapSurroundFrame mapSurroundFrame = mapFrame.CreateSurroundFrame(uid, null);
            if (mapSurroundFrame == null) return;

            // 设置单位 (如果支持)
            IScaleBar scaleBar = mapSurroundFrame.MapSurround as IScaleBar;
            if (scaleBar != null)
            {
                scaleBar.Units = esriUnits.esriKilometers;
            }

            IElement element = (IElement)mapSurroundFrame;
            IEnvelope env = new EnvelopeClass();
            env.PutCoords(2, 0.5, 8, 1.5); // 底部
            element.Geometry = env;

            container.AddElement(element, 0);

            // 激活选择工具
            _pageLayoutControl.CurrentTool = null;

            activeView.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }

        /// <summary>
        /// 添加图例
        /// </summary>
        public void AddLegend()
        {
            if (_pageLayoutControl == null) return;

            IPageLayout pageLayout = _pageLayoutControl.PageLayout;
            IGraphicsContainer container = pageLayout as IGraphicsContainer;
            IActiveView activeView = pageLayout as IActiveView;

            IMapFrame mapFrame = (IMapFrame)container.FindFrame(((IActiveView)pageLayout).FocusMap);
            if (mapFrame == null) return;

            // 使用 UID 创建，避免类型转换错误
            UID uid = new UIDClass();
            uid.Value = "esriCarto.Legend";

            IMapSurroundFrame mapSurroundFrame = mapFrame.CreateSurroundFrame(uid, null);
            if (mapSurroundFrame == null) return;
            
            // 图例通常会自动关联 MapFrame 的图层
            
            IElement element = (IElement)mapSurroundFrame;
            IEnvelope env = new EnvelopeClass();
            env.PutCoords(1, 5, 5, 10); // 左侧位置
            element.Geometry = env;

            container.AddElement(element, 0);

            // 激活选择工具
            _pageLayoutControl.CurrentTool = null;

            activeView.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }
    }
}
