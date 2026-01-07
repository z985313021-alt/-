// 位于 Program.cs 文件中

using ESRI.ArcGIS.esriSystem; // 必须有这个 using
using System;
using System.Windows.Forms;

namespace WindowsFormsMap1
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 步骤 1: 绑定产品代码 (必须)
            ESRI.ArcGIS.RuntimeManager.Bind(ESRI.ArcGIS.ProductCode.EngineOrDesktop);

           

            // 步骤 4: 运行窗体
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());

        
        }
    }
}