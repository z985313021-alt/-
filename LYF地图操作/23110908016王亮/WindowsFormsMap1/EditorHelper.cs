// [Member ROLE] Modified/Added: 说明内容
// [Agent (通用辅助)] Modified: 全量重建与中文化业务标注
using System;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;

namespace WindowsFormsMap1
{
    /// <summary>
    /// 【地图编辑助手】：封装 ArcGIS 空间要素的在线编辑逻辑
    /// 包含：工作空间事务管理、图形交互式采集以及编辑状态机维护
    /// </summary>
    public class EditorHelper
    {
        private AxMapControl _mapControl;
        private IWorkspaceEdit _workspaceEdit;
        private IFeatureClass _targetFeatureClass;

        // 【图形反馈系统】：用于在鼠标移动时实时绘制“橡皮筋”辅助线
        private INewLineFeedback _editLineFeedback;
        private INewPolygonFeedback _editPolygonFeedback;

        // 【状态机逻辑】：控制编辑器的生命周期
        public bool IsEditing { get; private set; }
        public bool IsCreatingFeature { get; private set; }

        public EditorHelper(AxMapControl mapControl)
        {
            _mapControl = mapControl;
            IsEditing = false;
        }

        // 【开启编辑会话】：锁定目标工作空间并启动版本化/非版本化编辑事务
        public void StartEditing(IFeatureLayer layer)
        {
            if (layer == null) return;

            IDataset dataset = layer.FeatureClass as IDataset;
            IWorkspace workspace = dataset.Workspace;

            // 编辑权限校验：目前仅支持物理文件型工作空间 (Shapefile/File GDB)
            if (workspace.Type == esriWorkspaceType.esriFileSystemWorkspace ||
                workspace.Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
            {
                _workspaceEdit = workspace as IWorkspaceEdit;
                if (_workspaceEdit.IsBeingEdited())
                {
                    MessageBox.Show("当前已处于编辑状态，请先结束上一进程。");
                    return;
                }

                // 启动编辑流程：不开启版本撤销（即时生效模式）
                _workspaceEdit.StartEditing(true);
                IsEditing = true;
                _targetFeatureClass = layer.FeatureClass;
                MessageBox.Show("编辑模式已激活，目标图层: " + layer.Name);
            }
            else
            {
                MessageBox.Show("架构限制：当前数据存储类型不支持在线交互式编辑。");
            }
        }

        // 【激活采集工具】：进入要素实时钩绘模式
        public void StartCreateFeature()
        {
            if (!IsEditing)
            {
                MessageBox.Show("模式错误：请先点击“开始编辑”按钮激活工作空间！");
                return;
            }
            IsCreatingFeature = true;
            _mapControl.MousePointer = esriControlsMousePointer.esriPointerCrosshair;
        }

        // 【挂起采集工具】：暂时退出钩绘模式，但不关闭编辑事务
        public void StopCreateFeature()
        {
            IsCreatingFeature = false;
            _editLineFeedback = null;
            _editPolygonFeedback = null;
            _mapControl.MousePointer = esriControlsMousePointer.esriPointerArrow;
            // 刷新前景，清除残留的反馈图形
            (_mapControl.Map as IActiveView).PartialRefresh(esriViewDrawPhase.esriViewForeground, null, null);
        }

        // 【强制持久化】：将内存中的编辑事务直接写入物理磁盘
        public void SaveEdit()
        {
            if (!IsEditing || _workspaceEdit == null) return;
            try
            {
                // 实现原理：ArcGIS Engine 中物理保存通常需要通过 StopEditing(true) 并立即重新开启来模拟
                _workspaceEdit.StopEditOperation();

                if (_workspaceEdit.IsBeingEdited())
                {
                    _workspaceEdit.StopEditing(true); // 物理保存到磁盘
                    _workspaceEdit.StartEditing(true); // 恢复编辑环境以便后续操作
                }
                MessageBox.Show("修改已成功固化到原始数据文件中。");
            }
            catch (Exception ex)
            {
                MessageBox.Show("磁盘写入操作异常：" + ex.Message);
            }
        }

        // 【安全退出编辑器】：提供保存提醒并清理所有 COM 占用
        public void StopEditing()
        {
            if (!IsEditing) return;

            DialogResult res = MessageBox.Show("监测到尚未保存的修改，是否立即写入磁碟？", "退出编辑器", MessageBoxButtons.YesNoCancel);
            if (res == DialogResult.Cancel) return;

            bool save = (res == DialogResult.Yes);
            try
            {
                if (_workspaceEdit != null)
                {
                    _workspaceEdit.StopEditOperation();
                    _workspaceEdit.StopEditing(save);
                }

                IsEditing = false;
                IsCreatingFeature = false;
                _targetFeatureClass = null;
                _mapControl.MousePointer = esriControlsMousePointer.esriPointerArrow;
                MessageBox.Show("编辑会话已正常关闭。");
            }
            catch (Exception ex)
            {
                MessageBox.Show("退出过程中发生系统级错误：" + ex.Message);
            }
        }

        // 【撤销最近操作】：基于工作空间事务栈进行回滚
        public void Undo()
        {
            if (!IsEditing || _workspaceEdit == null) return;
            try
            {
                _workspaceEdit.UndoEditOperation();
                _mapControl.ActiveView.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show("回滚失败，事务栈可能已损坏: " + ex.Message);
            }
        }

        // 【键鼠联动分发】：根据图层几何类型（PT/PL/PG）驱动特定的反馈逻辑
        public void OnMouseDown(int x, int y)
        {
            if (!IsCreatingFeature || !IsEditing || _targetFeatureClass == null) return;

            // 坐标投影：将屏幕像素点映射为地图真实地理坐标
            IPoint point = _mapControl.ActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(x, y);

            try
            {
                if (_targetFeatureClass.ShapeType == esriGeometryType.esriGeometryPoint)
                {
                    CreateFeature(point); // 点要素一键创建
                }
                else if (_targetFeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
                {
                    if (_editLineFeedback == null)
                    {
                        _editLineFeedback = new NewLineFeedbackClass();
                        _editLineFeedback.Display = _mapControl.ActiveView.ScreenDisplay;
                        _editLineFeedback.Start(point);
                    }
                    else
                    {
                        _editLineFeedback.AddPoint(point);
                    }
                }
                else if (_targetFeatureClass.ShapeType == esriGeometryType.esriGeometryPolygon)
                {
                    if (_editPolygonFeedback == null)
                    {
                        _editPolygonFeedback = new NewPolygonFeedbackClass();
                        _editPolygonFeedback.Display = _mapControl.ActiveView.ScreenDisplay;
                        _editPolygonFeedback.Start(point);
                    }
                    else
                    {
                        _editPolygonFeedback.AddPoint(point);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("图形采集引擎异常: " + ex.Message);
                _editLineFeedback = null;
                _editPolygonFeedback = null;
            }
        }

        public void OnMouseMove(int x, int y)
        {
            if (!IsCreatingFeature || !IsEditing) return;

            IPoint point = _mapControl.ActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(x, y);
            if (_editLineFeedback != null) _editLineFeedback.MoveTo(point);
            if (_editPolygonFeedback != null) _editPolygonFeedback.MoveTo(point);
        }

        // 【完成图形勾绘】：双击确立终点，并将几何对象持久化到数据库
        public void OnDoubleClick()
        {
            if (!IsCreatingFeature || !IsEditing) return;

            IGeometry geometry = null;

            if (_editPolygonFeedback != null)
            {
                geometry = _editPolygonFeedback.Stop();
                _editPolygonFeedback = null;
            }
            else if (_editLineFeedback != null)
            {
                geometry = _editLineFeedback.Stop();
                _editLineFeedback = null;
            }

            if (geometry != null)
            {
                CreateFeature(geometry);
            }
        }

        // 【核心落库逻辑】：执行 CreateFeature 事务
        private void CreateFeature(IGeometry geometry)
        {
            try
            {
                _workspaceEdit.StartEditOperation();
                IFeature feature = _targetFeatureClass.CreateFeature();
                feature.Shape = geometry;
                feature.Store();
                _workspaceEdit.StopEditOperation();

                // 触发多级刷新：同步更新地图背景与上层渲染
                _mapControl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                _mapControl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewForeground, null, null);
            }
            catch (Exception)
            {
                _workspaceEdit.AbortEditOperation();
                throw;
            }
        }
    }
}
