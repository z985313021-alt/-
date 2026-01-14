// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
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
    /// 【制图布局助手】：负责将地理数据转化为符合地图出版标准的专业布局
    /// 包含：视图同步 (Synchronization)、整饰要素注入 (Cartographic Elements)
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
        /// 【双视图同步】：利用对象深拷贝技术，将数据编辑视图的图层与范围同步至排版视图
        /// 实现原理：通过 IObjectCopy 接口对整个 Map 对象进行克隆，确保符号系统与渲染状态完全一致
        /// </summary>
        public void SynchronizeMap()
        {
            if (_pageLayoutControl == null || _mapControl == null) return;

            IObjectCopy objectCopy = new ObjectCopyClass();
            object toCopyMap = _mapControl.Map;
            object copiedMap = objectCopy.Copy(toCopyMap); // 执行深度克隆
            object toOverwriteMap = _pageLayoutControl.ActiveView.FocusMap;

            // 覆盖排版视图的当前焦点地图
            objectCopy.Overwrite(copiedMap, ref toOverwriteMap);

            _pageLayoutControl.ActiveView.Refresh();
        }

        /// <summary>
        /// 【注入指北针】：基于 ArcGIS 预设符号库实例化并展示指北针
        /// 逻辑：通过 UID 识别“esriCarto.MarkerNorthArrow”，并自动定位在版面左下角
        /// </summary>
        public void AddNorthArrow()
        {
            if (_pageLayoutControl == null) return;

            IPageLayout pageLayout = _pageLayoutControl.PageLayout;
            IGraphicsContainer container = pageLayout as IGraphicsContainer;
            IActiveView activeView = pageLayout as IActiveView;

            // 获取焦点地图所在的框架对象
            IMapFrame mapFrame = (IMapFrame)container.FindFrame(((IActiveView)pageLayout).FocusMap);
            if (mapFrame == null) return;

            // 构造组件唯一标识符 (UID)
            UID uid = new UIDClass();
            uid.Value = "esriCarto.MarkerNorthArrow";

            // 实例化整饰框架
            IMapSurroundFrame mapSurroundFrame = mapFrame.CreateSurroundFrame(uid, null);
            if (mapSurroundFrame == null) return;

            // 坐标定位：固定在 (2, 2) 单元位置，尺寸为 2x2
            IElement element = (IElement)mapSurroundFrame;
            IEnvelope env = new EnvelopeClass();
            env.PutCoords(2, 2, 4, 4);
            element.Geometry = env;

            container.AddElement(element, 0);

            // 激活选择工具，方便用户移动或删除户直接拖拽调整
            _pageLayoutControl.CurrentTool = null;

            // 局部刷新图形容器层
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
        /// 【生成智能图例】：自动扫描焦点地图层级并构建动态图例
        /// 包含：图例项自动同步、几何位置适配以及动态刷新
        /// </summary>
        public void AddLegend()
        {
            if (_pageLayoutControl == null) return;

            IPageLayout pageLayout = _pageLayoutControl.PageLayout;
            IGraphicsContainer container = pageLayout as IGraphicsContainer;
            IActiveView activeView = pageLayout as IActiveView;

            IMapFrame mapFrame = (IMapFrame)container.FindFrame(((IActiveView)pageLayout).FocusMap);
            if (mapFrame == null) return;

            UID uid = new UIDClass { Value = "esriCarto.Legend" };
            IMapSurroundFrame mapSurroundFrame = mapFrame.CreateSurroundFrame(uid, null);
            if (mapSurroundFrame == null) return;


            // 图例通常会自动关联 MapFrame 的图层
            IElement element = (IElement)mapSurroundFrame;
            IEnvelope env = new EnvelopeClass();
            env.PutCoords(1, 4, 6, 11); // 定位于版面左侧区域
            element.Geometry = env;

            container.AddElement(element, 0);
            _pageLayoutControl.CurrentTool = null;

            activeView.PartialRefresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }
    }
}
