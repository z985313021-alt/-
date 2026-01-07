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
    /// 编辑功能辅助类
    /// 负责要素的创建、撤销、保存等逻辑
    /// </summary>
    public class EditorHelper
    {
        private AxMapControl _mapControl;
        private IWorkspaceEdit _workspaceEdit;
        private IFeatureClass _targetFeatureClass;
        
        // 反馈对象
        private INewLineFeedback _editLineFeedback;
        private INewPolygonFeedback _editPolygonFeedback;

        // 状态
        public bool IsEditing { get; private set; }
        public bool IsCreatingFeature { get; private set; }

        public EditorHelper(AxMapControl mapControl)
        {
            _mapControl = mapControl;
            IsEditing = false;
        }

        // 开始编辑
        public void StartEditing(IFeatureLayer layer)
        {
            if (layer == null) return;

            IDataset dataset = layer.FeatureClass as IDataset;
            IWorkspace workspace = dataset.Workspace;

            if (workspace.Type == esriWorkspaceType.esriFileSystemWorkspace ||
                workspace.Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
            {
                _workspaceEdit = workspace as IWorkspaceEdit;
                if (_workspaceEdit.IsBeingEdited())
                {
                    MessageBox.Show("已经处于编辑状态。");
                    return;
                }

                _workspaceEdit.StartEditing(true);
                IsEditing = true;
                _targetFeatureClass = layer.FeatureClass;
                MessageBox.Show("开始编辑图层: " + layer.Name);
            }
            else
            {
                MessageBox.Show("不支持的工作空间类型 (仅支持 Shapefile/GDB)。");
            }
        }

        // 激活创建工具
        public void StartCreateFeature()
        {
            if (!IsEditing)
            {
                MessageBox.Show("请先开始编辑！");
                return;
            }
            IsCreatingFeature = true;
            _mapControl.MousePointer = esriControlsMousePointer.esriPointerCrosshair;
            MessageBox.Show("已激活创建工具。");
        }

        // 停止创建工具（只是取消工具激活，不停止编辑）
        public void StopCreateFeature()
        {
            IsCreatingFeature = false;
            // 清理反馈
            _editLineFeedback = null;
            _editPolygonFeedback = null;
            _mapControl.MousePointer = esriControlsMousePointer.esriPointerArrow;
             (_mapControl.Map as IActiveView).PartialRefresh(esriViewDrawPhase.esriViewForeground, null, null);
        }

        // 保存编辑
        public void SaveEdit()
        {
             if (!IsEditing || _workspaceEdit == null) return;
            try
            {
                _workspaceEdit.StopEditOperation();
                _workspaceEdit.StartEditOperation(); // 这个Start是为了防止Save后状态异常，但通常StopEditing(true)会自动保存
                // 实际上 SaveEdit通常是指 StopEditing(true)然后StartEditing. 
                // 但这里保持原来的逻辑：只为了中间保存，不退出
                 // 注意：WorkspaceEdit.StopEditing(true) 才是保存到磁盘。单纯Start/StopEditOperation只是内存事务。
                 // 如果要强制保存到磁盘但继续编辑，需要 StopEditing(true) 然后再 StartEditing。
                 bool hasEdits = _workspaceEdit.IsBeingEdited(); 
                 if(hasEdits)
                 {
                     _workspaceEdit.StopEditing(true);
                     _workspaceEdit.StartEditing(true);
                 }
                MessageBox.Show("保存成功。");
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存失败：" + ex.Message);
            }
        }

        // 停止编辑
        public void StopEditing()
        {
            if (!IsEditing) return;

            DialogResult res = MessageBox.Show("是否保存修改？", "停止编辑", MessageBoxButtons.YesNoCancel);
            if (res == DialogResult.Cancel) return;

            bool save = (res == DialogResult.Yes);
            try
            {
                if (_workspaceEdit != null)
                {
                    // 确保没有悬挂的操作
                    _workspaceEdit.StopEditOperation(); 
                    _workspaceEdit.StopEditing(save);
                }
                
                IsEditing = false;
                IsCreatingFeature = false;
                _targetFeatureClass = null;
                _mapControl.MousePointer = esriControlsMousePointer.esriPointerArrow;
                MessageBox.Show("已停止编辑。");
            }
            catch (Exception ex)
            {
                MessageBox.Show("停止编辑出错：" + ex.Message);
            }
        }

        // 撤销
        public void Undo()
        {
             if (!IsEditing || _workspaceEdit == null) return;
             try
             {
                 _workspaceEdit.UndoEditOperation();
                 _mapControl.ActiveView.Refresh();
                 MessageBox.Show("已撤销。");
             }
             catch (Exception ex)
             {
                 MessageBox.Show("撤销失败: " + ex.Message);
             }
        }

        public void OnMouseDown(int x, int y)
        {
            if (!IsCreatingFeature || !IsEditing || _targetFeatureClass == null) return;

            IPoint point = _mapControl.ActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(x, y);

            try
            {
                // 1. 点
                if (_targetFeatureClass.ShapeType == esriGeometryType.esriGeometryPoint)
                {
                    CreateFeature(point);
                }
                // 2. 线
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
                // 3. 面
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
                MessageBox.Show("操作失败: " + ex.Message);
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

        private void CreateFeature(IGeometry geometry)
        {
            try
            {
                _workspaceEdit.StartEditOperation();
                IFeature feature = _targetFeatureClass.CreateFeature();
                feature.Shape = geometry;
                feature.Store();
                _workspaceEdit.StopEditOperation();
                
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
