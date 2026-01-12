
using ESRI.ArcGIS.Geometry;
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using ESRI.ArcGIS.Display;

namespace WindowsFormsMap1
{
    public partial class FormRoute : Form
    {
        private Form1 _mainForm;
        private List<IPoint> _routePoints;
        private AnalysisHelper _analyzer;

        public FormRoute(Form1 mainForm, AnalysisHelper analyzer)
        {
            InitializeComponent();
            _mainForm = mainForm;
            _analyzer = analyzer;
            _routePoints = new List<IPoint>();

            // [Agent (通用辅助)] Added: 为构建路网按钮添加右键菜单
            ContextMenuStrip buildMenu = new ContextMenuStrip();
            buildMenu.Items.Add("智能加载(优先使用缓存)", null, (s, e) =>
            {
                _mainForm.BuildRoadNetwork();
                lblInfo.Text = "尝试加载路网缓存...";
            });
            buildMenu.Items.Add("强制重新构建", null, (s, e) =>
            {
                _mainForm.ForceBuildRoadNetwork();
                lblInfo.Text = "正在重新构建路网...";
            });
            btnBuildNetwork.ContextMenuStrip = buildMenu;
        }

        public void AddPoint(IPoint pt)
        {
            if (pt == null) return;
            _routePoints.Add(pt);

            // 简单显示坐标
            lstPoints.Items.Add($"点 {_routePoints.Count}: ({pt.X:F3}, {pt.Y:F3})");

            // 在地图上绘制临时的标记
            _mainForm.DrawTempPoint(pt, _routePoints.Count);
        }

        private void btnAddPoint_Click(object sender, EventArgs e)
        {
            // 切换主窗体工具为选点模式
            _mainForm.SwitchTool(Form1.MapToolMode.PickRoutePoint);
            this.lblInfo.Text = "请在地图上点击添加途经点...";
        }

        private void btnRemovePoint_Click(object sender, EventArgs e)
        {
            int idx = lstPoints.SelectedIndex;
            if (idx >= 0)
            {
                lstPoints.Items.RemoveAt(idx);
                _routePoints.RemoveAt(idx);
                _mainForm.RefreshMap(); // 清除旧的绘制
                lblInfo.Text = "已移除选定点。";
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            lstPoints.Items.Clear();
            _routePoints.Clear();
            _mainForm.RefreshMap();
            lblInfo.Text = "已清空。";
        }

        // [Agent (通用辅助)] Modified: 左键默认智能加载,右键菜单提供强制重建选项
        private void btnBuildNetwork_Click(object sender, EventArgs e)
        {
            // 左键点击:默认智能加载(优先使用缓存)
            _mainForm.BuildRoadNetwork();
            lblInfo.Text = "尝试加载路网缓存...";
        }

        private void btnSolve_Click(object sender, EventArgs e)
        {
            if (_routePoints.Count < 2)
            {
                MessageBox.Show("至少需要 2 个点才能规划路径！");
                return;
            }

            try
            {
                this.Cursor = Cursors.WaitCursor;
                lblInfo.Text = "正在计算...";

                IPolyline result = _analyzer.FindShortestPath(_routePoints);

                this.Cursor = Cursors.Default;

                if (result != null && !result.IsEmpty)
                {
                    // [Agent Fix] 单位换算逻辑
                    double lenKm = 0;

                    // 1. 尝试投影到地图坐标系 (通常是投影坐标系/米)
                    ISpatialReference mapSR = _mainForm.MapSpatialReference;
                    if (mapSR != null && result.SpatialReference != null)
                    {
                        // 如果当前路网是地理坐标系(Results in Degrees) 而地图是投影坐标系(Meters) -> 需要投影
                        if (mapSR is IProjectedCoordinateSystem && !(result.SpatialReference is IProjectedCoordinateSystem))
                        {
                            try { result.Project(mapSR); } catch { }
                        }
                    }

                    // 2. 计算长度 (假设投影后单位为米)
                    // 如果地图本身就是地理坐标系，那没办法，只能显示度，或者强行用 GeodeticLength
                    // 但根据截图坐标(362890...)，地图肯定是投影坐标系(米)
                    double lenMeters = result.Length;

                    // 3. 换算为公里
                    lenKm = lenMeters / 1000.0;

                    // 绘制结果 (加粗红线)
                    _mainForm.DrawGeometry(result, new RgbColorClass { Red = 255 });

                    string info = $"规划成功！总长度: {lenKm:F2} km";
                    lblInfo.Text = info;
                    MessageBox.Show($"{info}\n已在地图显示红色路径。\n若看不见，请检查图层是否开启。\n代码位于: AnalysisHelper.cs", "结果");
                }
                else
                {
                    lblInfo.Text = "路径规划失败。";
                    string tip = "未能找到路径。请检查：\n1. 是否点击了【构建路网】菜单？\n2. 选中的道路图层是否有内容？\n\n核心逻辑文件：AnalysisHelper.cs";
                    MessageBox.Show(tip, "提示");
                }
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Default;
                lblInfo.Text = "运行错: " + ex.Message;
                MessageBox.Show("计算出错，请重试或重构路网。\n错误详情: " + ex.Message);
            }
        }

        private void FormRoute_FormClosing(object sender, FormClosingEventArgs e)
        {
            // 窗体关闭时，重置地图工具
            _mainForm.SwitchTool(Form1.MapToolMode.None);
            _mainForm.RefreshMap(); // 清理临时点
        }
    }
}
