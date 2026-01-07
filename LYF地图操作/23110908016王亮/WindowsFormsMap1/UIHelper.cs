using System;
using System.Drawing;
using System.Windows.Forms;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.esriSystem;

namespace WindowsFormsMap1
{
    /// <summary>
    /// UI 辅助类
    /// 仅保留通用 UI 逻辑，如导出功能
    /// </summary>
    public class UIHelper
    {
        private Form1 _form;
        private AxMapControl _mapControl;
        private MenuStrip _menuStrip;

        public UIHelper(Form1 form, AxMapControl mapControl, MenuStrip menuStrip)
        {
            _form = form;
            _mapControl = mapControl;
            _menuStrip = menuStrip;
        }

        public void Initialize()
        {
            // 不再手动创建工具栏和菜单，回归 Designer 管理
        }

        /// <summary>
        /// [重构] 导出地图为图片
        /// </summary>
        public static void ExportMap(AxMapControl mapControl)
        {
            try
            {
                SaveFileDialog dlg = new SaveFileDialog
                {
                    Filter = "JPEG (*.jpg)|*.jpg|Bitmap (*.bmp)|*.bmp|PNG (*.png)|*.png",
                    Title = "导出地图",
                    FileName = "MapExport.jpg"
                };

                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    ESRI.ArcGIS.Carto.IActiveView activeView = mapControl.ActiveView;
                    ESRI.ArcGIS.Output.IExport export = dlg.FileName.EndsWith(".jpg") ? new ESRI.ArcGIS.Output.ExportJPEGClass() :
                                                       dlg.FileName.EndsWith(".bmp") ? (ESRI.ArcGIS.Output.IExport)new ESRI.ArcGIS.Output.ExportBMPClass() :
                                                       new ESRI.ArcGIS.Output.ExportPNGClass();

                    export.ExportFileName = dlg.FileName;
                    export.Resolution = 96;
                    ESRI.ArcGIS.esriSystem.tagRECT frame = activeView.ExportFrame;
                    int hdc = export.StartExporting();
                    activeView.Output(hdc, (int)export.Resolution, ref frame, null, null);
                    export.FinishExporting();
                    export.Cleanup();

                    MessageBox.Show("导出成功！路径：" + dlg.FileName);
                }
            }
            catch (Exception ex) { MessageBox.Show("导出失败：" + ex.Message); }
        }
    }
}
