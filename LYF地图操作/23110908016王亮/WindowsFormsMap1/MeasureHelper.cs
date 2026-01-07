using System;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geometry;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 测量功能辅助类
    /// 负责距离和面积的测量逻辑
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

        public void StartMeasureDistance()
        {
            Stop(); // 先停止之前的
            IsMeasuringDistance = true;
            _mapControl.MousePointer = esriControlsMousePointer.esriPointerCrosshair;
            ShowResultForm("请在地图上点击开始量算距离...");
        }

        public void StartMeasureArea()
        {
            Stop(); // 先停止之前的
            IsMeasuringArea = true;
            _mapControl.MousePointer = esriControlsMousePointer.esriPointerCrosshair;
            ShowResultForm("请在地图上点击绘制多边形...");
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
                _startPoint = point; // 记录段起点
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
                if (_segmentLength != 0)
                {
                    _totalLength += _segmentLength;
                }
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
                Stop(); // 结束测量状态
                UpdateResultText("测量结束");
            }
            else if (IsMeasuringArea)
            {
                if (_newPolygonFeedback == null) return;
                
                IPolygon polygon = _newPolygonFeedback.Stop();
                _newPolygonFeedback = null;
                Stop(); // 结束测量状态

                if (polygon != null && !polygon.IsEmpty)
                {
                    (polygon as ITopologicalOperator).Simplify();
                    polygon.Close();
                    IArea area = polygon as IArea;
                    UpdateResultText(string.Format("面积为: {0:F2} 平方单位", Math.Abs(area.Area)));
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
