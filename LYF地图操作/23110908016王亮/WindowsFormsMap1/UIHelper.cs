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
            ms.ImageScalingSize = new Size(32, 32);
            ms.Renderer = new ModernMenuRenderer(); // 使用自定义渲染器

            foreach (ToolStripMenuItem item in ms.Items)
            {
                item.TextImageRelation = TextImageRelation.ImageAboveText;
                item.Padding = new Padding(15, 10, 15, 10); // 增加间距使其像按钮

                // 递归设置子项，子项保持横向图标
                SetSubItemStyle(item);
            }
        }

        private static void SetSubItemStyle(ToolStripMenuItem item)
        {
            foreach (ToolStripItem sub in item.DropDownItems)
            {
                if (sub is ToolStripMenuItem tsmi)
                {
                    tsmi.TextImageRelation = TextImageRelation.ImageBeforeText;
                    SetSubItemStyle(tsmi);
                }
            }
        }

        public static void ApplyTOCTheme(Control toc)
        {
            toc.BackColor = Color.White;
            toc.ForeColor = ColorText;
            if (toc.Parent != null) toc.Parent.BackColor = Color.White;
        }

        public static void ApplyStatusStripTheme(StatusStrip ss)
        {
            ss.BackColor = ColorNeutral;
            ss.ForeColor = Color.FromArgb(100, 116, 139);
            ss.Font = new Font(FontDefault, 8.5F);
        }

        public static void ApplyTabControlTheme(TabControl tc)
        {
            tc.Font = new Font(FontDefault, 9F);
        }

        /// <summary>
        /// [Agent Add] 获取增强型图标集 (提升辨识度)
        /// </summary>
        public static Image GetIcon(string type, Color? color = null)
        {
            int size = 32;
            Bitmap bmp = new Bitmap(size, size);
            Color c = color ?? ColorPrimary;
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.Clear(Color.Transparent);

                using (Pen pen = new Pen(c, 2.5f))
                using (SolidBrush brush = new SolidBrush(c))
                using (SolidBrush lightBrush = new SolidBrush(Color.FromArgb(100, c)))
                {
                    float offset = size * 0.15f;
                    float innerSize = size * 0.7f;

                    switch (type)
                    {
                        case "Data": // 数据库 - 更加立体的圆柱体
                            g.FillEllipse(lightBrush, offset, offset, innerSize, innerSize * 0.3f);
                            g.DrawEllipse(pen, offset, offset, innerSize, innerSize * 0.3f);
                            g.DrawLine(pen, offset, offset + innerSize * 0.15f, offset, offset + innerSize * 0.85f);
                            g.DrawLine(pen, offset + innerSize, offset + innerSize * 0.15f, offset + innerSize, offset + innerSize * 0.85f);
                            g.DrawArc(pen, offset, offset + innerSize * 0.7f, innerSize, innerSize * 0.3f, 0, 180);
                            g.FillPath(lightBrush, GetCylinderPath(offset, offset + innerSize * 0.15f, innerSize, innerSize * 0.7f));
                            break;

                        case "Toolbox": // 工具箱 - 实心手提箱感
                            g.FillRectangle(lightBrush, offset, offset + innerSize * 0.3f, innerSize, innerSize * 0.6f);
                            g.DrawRectangle(pen, offset, offset + innerSize * 0.3f, innerSize, innerSize * 0.6f);
                            g.DrawArc(pen, offset + innerSize * 0.3f, offset + innerSize * 0.1f, innerSize * 0.4f, innerSize * 0.4f, 180, 180);
                            g.DrawLine(pen, offset + innerSize * 0.5f, offset + innerSize * 0.5f, offset + innerSize * 0.5f, offset + innerSize * 0.7f);
                            break;

                        case "Measure": // 尺子 - 带填充
                            g.FillRectangle(lightBrush, offset, offset + innerSize * 0.35f, innerSize, innerSize * 0.3f);
                            g.DrawRectangle(pen, offset, offset + innerSize * 0.35f, innerSize, innerSize * 0.3f);
                            for (int i = 1; i < 6; i++) g.DrawLine(pen, offset + i * (innerSize / 6), offset + innerSize * 0.35f, offset + i * (innerSize / 6), offset + innerSize * 0.5f);
                            break;

                        case "Refresh": // 循环 - 加租箭头
                            g.DrawArc(pen, offset, offset, innerSize, innerSize, 30, 300);
                            PointF tip = new PointF((float)(size / 2 + Math.Cos(30 * Math.PI / 180) * innerSize / 2), (float)(size / 2 + Math.Sin(30 * Math.PI / 180) * innerSize / 2));
                            g.FillPolygon(brush, new PointF[] { new PointF(tip.X, tip.Y - 6), new PointF(tip.X + 8, tip.Y), new PointF(tip.X, tip.Y + 6) });
                            break;

                        case "Clear": // 清除 - 像是一个刷子或X
                            g.FillEllipse(lightBrush, offset + 2, offset + 2, innerSize - 4, innerSize - 4);
                            g.DrawEllipse(pen, offset + 2, offset + 2, innerSize - 4, innerSize - 4);
                            g.DrawLine(pen, offset + 8, offset + 8, offset + innerSize - 8, offset + innerSize - 8);
                            g.DrawLine(pen, offset + innerSize - 8, offset + 8, offset + 8, offset + innerSize - 8);
                            break;

                        case "Pan": // 漫游 - 填充圆
                            g.FillEllipse(brush, size / 2 - 6, size / 2 - 6, 12, 12);
                            g.DrawLine(pen, size / 2, size / 2, size / 2, size / 2 - 10);
                            break;

                        case "Mapping": // 制图 - 画板带颜色块
                            g.FillRectangle(lightBrush, offset, offset, innerSize, innerSize);
                            g.DrawRectangle(pen, offset, offset, innerSize, innerSize);
                            g.FillRectangle(brush, offset + 4, offset + 4, innerSize * 0.3f, innerSize * 0.3f);
                            g.DrawLine(pen, offset + 4, offset + innerSize - 8, offset + innerSize - 4, offset + innerSize - 8);
                            break;

                        case "Query": // 查询 - 放大镜带高光
                            g.FillEllipse(lightBrush, offset, offset, innerSize * 0.7f, innerSize * 0.7f);
                            g.DrawEllipse(pen, offset, offset, innerSize * 0.7f, innerSize * 0.7f);
                            g.DrawLine(new Pen(c, 4f), offset + innerSize * 0.55f, offset + innerSize * 0.55f, offset + innerSize, offset + innerSize);
                            break;

                        case "Analysis": // 分析 - 实体齿轮
                            g.FillEllipse(lightBrush, offset + innerSize * 0.2f, offset + innerSize * 0.2f, innerSize * 0.6f, innerSize * 0.6f);
                            g.DrawEllipse(pen, offset + innerSize * 0.2f, offset + innerSize * 0.2f, innerSize * 0.6f, innerSize * 0.6f);
                            for (int i = 0; i < 8; i++)
                            {
                                double angle = i * Math.PI / 4;
                                g.DrawLine(new Pen(c, 3f), (float)(size / 2 + Math.Cos(angle) * innerSize * 0.3), (float)(size / 2 + Math.Sin(angle) * innerSize * 0.3),
                                                           (float)(size / 2 + Math.Cos(angle) * innerSize * 0.5), (float)(size / 2 + Math.Sin(angle) * innerSize * 0.5));
                            }
                            break;

                        case "Layout": // 布局 - 多层纸张
                            g.FillRectangle(lightBrush, offset + 4, offset + 4, innerSize * 0.8f, innerSize * 0.8f);
                            g.DrawRectangle(pen, offset + 4, offset + 4, innerSize * 0.8f, innerSize * 0.8f);
                            g.DrawRectangle(pen, offset, offset, innerSize * 0.6f, innerSize * 0.6f);
                            break;

                        case "Edit": // 编辑 - 实心铅笔
                            g.FillPolygon(lightBrush, new PointF[] { new PointF(offset, offset + innerSize), new PointF(offset + innerSize, offset), new PointF(offset + innerSize - 5, offset - 5), new PointF(offset - 5, offset + innerSize - 5) });
                            g.DrawLine(new Pen(c, 4f), offset, offset + innerSize, offset + innerSize, offset);
                            break;

                        case "ZoomIn":
                            g.DrawEllipse(pen, offset, offset, innerSize * 0.8f, innerSize * 0.8f);
                            g.DrawLine(new Pen(c, 3f), offset + innerSize * 0.4f, offset + innerSize * 0.15f, offset + innerSize * 0.4f, offset + innerSize * 0.65f);
                            g.DrawLine(new Pen(c, 3f), offset + innerSize * 0.15f, offset + innerSize * 0.4f, offset + innerSize * 0.65f, offset + innerSize * 0.4f);
                            break;

                        case "ZoomOut":
                            g.DrawEllipse(pen, offset, offset, innerSize * 0.8f, innerSize * 0.8f);
                            g.DrawLine(new Pen(c, 3f), offset + innerSize * 0.15f, offset + innerSize * 0.4f, offset + innerSize * 0.65f, offset + innerSize * 0.4f);
                            break;

                        case "Full":
                            g.FillRectangle(lightBrush, offset, offset, innerSize, innerSize);
                            g.DrawRectangle(pen, offset, offset, innerSize, innerSize);
                            g.DrawLine(pen, offset, offset, offset + 8, offset + 8);
                            g.DrawLine(pen, offset + innerSize, offset + innerSize, offset + innerSize - 8, offset + innerSize - 8);
                            break;

                        case "Sync":
                            g.DrawArc(pen, offset, offset, innerSize, innerSize, 0, 160);
                            g.DrawArc(pen, offset, offset, innerSize, innerSize, 180, 160);
                            g.FillPolygon(brush, new PointF[] { new PointF(offset + innerSize, size / 2 - 4), new PointF(offset + innerSize + 6, size / 2), new PointF(offset + innerSize, size / 2 + 4) });
                            break;

                        case "Heatmap": // 热力图 - 多个重叠的渐变圆感
                            g.FillEllipse(lightBrush, offset, offset + 5, innerSize * 0.6f, innerSize * 0.6f);
                            g.FillEllipse(lightBrush, offset + 10, offset, innerSize * 0.6f, innerSize * 0.6f);
                            g.DrawEllipse(pen, offset, offset + 5, innerSize * 0.6f, innerSize * 0.6f);
                            g.DrawEllipse(pen, offset + 10, offset, innerSize * 0.6f, innerSize * 0.6f);
                            break;

                        case "Back": // 返回 - 大箭头
                            g.DrawLine(new Pen(c, 4f), offset + innerSize, size / 2, offset, size / 2);
                            g.DrawLine(new Pen(c, 4f), offset, size / 2, offset + innerSize * 0.3f, size / 2 - innerSize * 0.3f);
                            g.DrawLine(new Pen(c, 4f), offset, size / 2, offset + innerSize * 0.3f, size / 2 + innerSize * 0.3f);
                            break;

                        case "Pointer":
                            PointF[] pts = { new PointF(offset, offset), new PointF(offset + innerSize * 0.7f, offset + innerSize * 0.5f), new PointF(offset + innerSize * 0.4f, offset + innerSize * 0.6f), new PointF(offset + innerSize * 0.5f, offset + innerSize * 0.9f) };
                            g.FillPolygon(lightBrush, pts);
                            g.DrawPolygon(pen, pts);
                            break;

                        case "Identify":
                            g.FillEllipse(lightBrush, offset, offset, innerSize, innerSize);
                            g.DrawEllipse(pen, offset, offset, innerSize, innerSize);
                            g.DrawString("i", new Font("Georgia", 14f, FontStyle.Bold), brush, offset + innerSize * 0.25f, offset + innerSize * 0.05f);
                            break;

                        case "Web":
                            g.FillEllipse(lightBrush, offset, offset, innerSize, innerSize);
                            g.DrawEllipse(pen, offset, offset, innerSize, innerSize);
                            g.DrawLine(pen, offset, size / 2, offset + innerSize, size / 2);
                            g.DrawEllipse(pen, offset + innerSize * 0.25f, offset, innerSize * 0.5f, innerSize);
                            break;

                        default:
                            g.DrawRectangle(pen, offset, offset, innerSize, innerSize);
                            break;
                    }
                }
            }
            return bmp;
        }

        private static System.Drawing.Drawing2D.GraphicsPath GetCylinderPath(float x, float y, float w, float h)
        {
            System.Drawing.Drawing2D.GraphicsPath path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(x, y + h - h * 0.3f, w, h * 0.3f, 0, 180);
            path.AddLine(x, y + h - h * 0.15f, x, y);
            path.AddArc(x, y - h * 0.15f, w, h * 0.3f, 180, -180);
            path.AddLine(x + w, y, x + w, y + h - h * 0.15f);
            return path;
        }

        private class ModernMenuRenderer : ToolStripProfessionalRenderer
        {
            public ModernMenuRenderer() : base(new ModernColorTable()) { }
            protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
            {
                if (e.Item.Selected)
                {
                    Rectangle rect = new Rectangle(System.Drawing.Point.Empty, e.Item.Size);
                    using (SolidBrush brush = new SolidBrush(ColorSecondary))
                        e.Graphics.FillRectangle(brush, rect);
                }
            }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { /* 去边框 */ }
        }

        private class ModernColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => ColorSecondary;
            public override Color MenuItemSelectedGradientBegin => ColorSecondary;
            public override Color MenuItemSelectedGradientEnd => ColorSecondary;
            public override Color MenuItemPressedGradientBegin => ColorSecondary;
            public override Color MenuItemPressedGradientEnd => ColorSecondary;
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuBorder => Color.FromArgb(226, 232, 240);
            public override Color ToolStripDropDownBackground => Color.White;
        }
    }
}
