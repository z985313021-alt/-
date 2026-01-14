// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
using System;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geometry;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 【地图量算助手】：提供交互式的距离与面积测量功能
    /// 集成橡皮筋 (Rubber banding) 实时反馈与自动单位换算显示
    /// </summary>
    public class MeasureHelper
    {
        private AxMapControl _mapControl;
        private INewLineFeedback _newLineFeedback;
        private INewPolygonFeedback _newPolygonFeedback;
        private FormMeasureResult _formMeasureResult;
        
        // 距离测量状态
        private IPoint _startPoint;
        private double _totalLength;
        private double _segmentLength;

        // 当前模式
        public bool IsMeasuringDistance { get; private set; }
        public bool IsMeasuringArea { get; private set; }

        public MeasureHelper(AxMapControl mapControl)
        {
            _mapControl = mapControl;
        }

        // 【开启距离测量】：激活十字准星，记录分段长度与总长度
        public void StartMeasureDistance()
        {
            Stop(); 
            IsMeasuringDistance = true;
            _mapControl.MousePointer = esriControlsMousePointer.esriPointerCrosshair;
            ShowResultForm("交互测量模式：请在地图上点击确立起点...");
        }

        // 【开启面积测量】：基于封闭多边形的拓扑闭合计算
        public void StartMeasureArea()
        {
            Stop(); 
            IsMeasuringArea = true;
            _mapControl.MousePointer = esriControlsMousePointer.esriPointerCrosshair;
            ShowResultForm("交互测量模式：请在地图上拉框绘制多边形...");
        }

        public void Stop()
        {
            IsMeasuringDistance = false;
            IsMeasuringArea = false;

            if (_newLineFeedback != null)
            {
                _newLineFeedback.Stop();
                _newLineFeedback = null;
            }
            if (_newPolygonFeedback != null)
            {
                _newPolygonFeedback.Stop();
                _newPolygonFeedback = null;
            }

            _totalLength = 0;
            _segmentLength = 0;
            _startPoint = null;

            // 清除残影
            if (_mapControl.Map != null)
            {
                (_mapControl.Map as IActiveView).PartialRefresh(esriViewDrawPhase.esriViewForeground, null, null);
            }
        }

        public void OnMouseDown(int x, int y)
        {
            IPoint point = _mapControl.ActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(x, y);
 
            if (IsMeasuringDistance)
            {
                _startPoint = point; // 捕获当前段起点
                if (_newLineFeedback == null)
                {
                    _newLineFeedback = new NewLineFeedbackClass();
                    _newLineFeedback.Display = _mapControl.ActiveView.ScreenDisplay;
                    _newLineFeedback.Start(point);
                    _totalLength = 0;
                }
                else
                {
                    _newLineFeedback.AddPoint(point);
                }
                // 累加已完成的线段长度
                if (_segmentLength != 0) _totalLength += _segmentLength;
            }
            else if (IsMeasuringArea)
            {
                if (_newPolygonFeedback == null)
                {
                    _newPolygonFeedback = new NewPolygonFeedbackClass();
                    _newPolygonFeedback.Display = _mapControl.ActiveView.ScreenDisplay;
                    _newPolygonFeedback.Start(point);
                }
                else
                {
                    _newPolygonFeedback.AddPoint(point);
                }
            }
        }

        public void OnMouseMove(int x, int y)
        {
            IPoint movePt = _mapControl.ActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(x, y);

            if (IsMeasuringDistance)
            {
                if (_newLineFeedback != null)
                {
                    _newLineFeedback.MoveTo(movePt);
                }

                // 实时计算
                if (_startPoint != null && _newLineFeedback != null)
                {
                    double deltaX = movePt.X - _startPoint.X;
                    double deltaY = movePt.Y - _startPoint.Y;
                    _segmentLength = Math.Round(Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY)), 3);
                    double previewTotal = _totalLength + _segmentLength;

                    UpdateResultText(string.Format(
                        "当前线段长度：{0:###.##} {1}\r\n总长度为：{2:###.##} {1}",
                        _segmentLength, _mapControl.Map.MapUnits.ToString().Substring(4), previewTotal));
                }
            }
            else if (IsMeasuringArea)
            {
                if (_newPolygonFeedback != null)
                {
                    _newPolygonFeedback.MoveTo(movePt);
                }
            }
        }

        public void OnDoubleClick()
        {
            if (IsMeasuringDistance)
            {
                if (_newLineFeedback != null)
                {
                    _newLineFeedback.Stop();
                    _newLineFeedback = null;
                }
                Stop(); 
                UpdateResultText("测量已完成，量算逻辑已释放。");
            }
            else if (IsMeasuringArea)
            {
                if (_newPolygonFeedback == null) return;
                
                // 拓扑闭合：获取最终多边形
                IPolygon polygon = _newPolygonFeedback.Stop();
                _newPolygonFeedback = null;
                Stop(); 
 
                if (polygon != null && !polygon.IsEmpty)
                {
                    // 几何简化：处理多边形自相交问题并计算代数面积
                    (polygon as ITopologicalOperator).Simplify();
                    polygon.Close();
                    IArea area = polygon as IArea;
                    UpdateResultText(string.Format("测得闭合区域面积: {0:F2} 平方单位", Math.Abs(area.Area)));
                }
            }
             _mapControl.MousePointer = esriControlsMousePointer.esriPointerArrow;
        }

        private void ShowResultForm(string initialText)
        {
            if (_formMeasureResult == null || _formMeasureResult.IsDisposed)
            {
                _formMeasureResult = new FormMeasureResult();
                _formMeasureResult.Show();
            }
            else
            {
                _formMeasureResult.Activate();
            }
            _formMeasureResult.lblResultMeasure.Text = initialText;
        }

        private void UpdateResultText(string text)
        {
            if (_formMeasureResult != null && !_formMeasureResult.IsDisposed)
            {
                _formMeasureResult.lblResultMeasure.Text = text;
            }
        }
    }
}
