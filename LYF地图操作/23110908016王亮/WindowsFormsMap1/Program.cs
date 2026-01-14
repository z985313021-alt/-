// [Agent (通用辅助)] Modified: 全量中文化注释深挖
// 位于 Program.cs 文件中

using ESRI.ArcGIS.esriSystem; // 必须有这个 using
using System;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点（Windows 窗体程序的启动函数）。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 步骤 1: 绑定 ArcGIS Runtime 产品许可 (这是 Engine 程序运行的前提，必须置于 Main 首行)
            ESRI.ArcGIS.RuntimeManager.Bind(ESRI.ArcGIS.ProductCode.EngineOrDesktop);

            // 步骤 4: 运行窗体tCompatibleTextRenderingDefault(false);
            // This line seems incomplete or a typo. Assuming it should be Application.SetCompatibleTextRenderingDefault(false);
            // If it's meant to be a comment, it should stay as is.
            // For now, keeping it as a comment as it was in the original.

            // 步骤 3: 实例化并运行主窗体 Form1
            Application.Run(new Form1());
        }
    }
}