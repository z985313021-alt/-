// [Agent (通用辅助)] Modified: 中文化注释与架构梳理
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

        /// <summary>
        /// [Agent (通用辅助)] Added: 深度克隆地图对象
        /// 使用 IObjectCopy 接口实现地图对象的无干扰同步
        /// </summary>
        public static ESRI.ArcGIS.Carto.IMap CloneMap(ESRI.ArcGIS.Carto.IMap sourceMap)
        {
            try
            {
                ESRI.ArcGIS.esriSystem.IObjectCopy objectCopy = new ESRI.ArcGIS.esriSystem.ObjectCopyClass();
                object clonedObject = objectCopy.Copy(sourceMap);
                return clonedObject as ESRI.ArcGIS.Carto.IMap;
            }
            catch { return null; }
        }
    }

    /// <summary>
    /// [Agent Add] 全局样式引擎 (蓝白现代风格)
    /// </summary>
    public static class ThemeEngine
    {
        public static readonly Color ColorPrimary = Color.FromArgb(37, 99, 235);     // 深蓝
        public static readonly Color ColorSecondary = Color.FromArgb(239, 246, 255); // 浅蓝
        public static readonly Color ColorNeutral = Color.FromArgb(248, 250, 252);   // 偏白灰
        public static readonly Color ColorText = Color.FromArgb(30, 41, 59);        // 深灰蓝
        public static readonly string FontDefault = "微软雅黑";

        public static void ApplyTheme(Form form)
        {
            form.Font = new Font(FontDefault, 9F);
            form.BackColor = Color.White;
        }

        public static void ApplyButtonTheme(Button btn, bool isPrimary = false)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.Font = new Font(FontDefault, 9F, isPrimary ? FontStyle.Bold : FontStyle.Regular);
            btn.Height = 32;
            btn.Cursor = Cursors.Hand;

            if (isPrimary)
            {
                btn.BackColor = ColorPrimary;
                btn.ForeColor = Color.White;
                btn.FlatAppearance.BorderSize = 0;
            }
            else
            {
                btn.BackColor = Color.White;
                btn.ForeColor = ColorText;
                btn.FlatAppearance.BorderColor = Color.FromArgb(226, 232, 240);
                btn.FlatAppearance.MouseOverBackColor = ColorSecondary;
            }
        }

        public static void ApplyMenuStripTheme(MenuStrip ms)
        {
            ms.BackColor = Color.White;
            ms.ForeColor = ColorText;
            ms.Font = new Font(FontDefault, 9F);
            ms.RenderMode = ToolStripRenderMode.Professional; // 改为专业模式以支持自定义 Renderer
            ms.Padding = new Padding(6, 4, 6, 4);
        }

        public static void ApplyTOCTheme(Control toc)
        {
            // TOCControl 本身是 Com 接口，其背景色通常随系统或宿主容器
            // 我们通过美化外层容器来提升视觉感
            toc.BackColor = Color.White;
            toc.ForeColor = ColorText;
            if (toc.Parent != null)
            {
                toc.Parent.BackColor = Color.White;
            }
        }

        public static void ApplyStatusStripTheme(StatusStrip ss)
        {
            ss.BackColor = ColorNeutral;
            ss.ForeColor = Color.FromArgb(100, 116, 139);
            ss.Font = new Font(FontDefault, 8.5F);
        }

        public static void ApplyTabControlTheme(TabControl tc)
        {
            // Windows Forms 原生 TabControl 较难深入美化，此处仅优化基础字体与背景
            tc.Font = new Font(FontDefault, 9F);
        }
    }
}
