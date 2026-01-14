using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using ESRI.ArcGIS.Carto; // Added for IFeatureLayer
using ESRI.ArcGIS.Geodatabase; // Added for IQueryFilter, IDataStatistics, etc.
using System.Collections.Generic; // Added for List<T>
using System.Web.Script.Serialization; // Added for JavaScriptSerializer
using System.Runtime.InteropServices; // Added for Marshal.ReleaseComObject

namespace WindowsFormsMap1
{
    /// <summary>
    /// 【Web 演示模式窗体】：利用 WebView2 容器承载由 HTML5/JS 驱动的高级可视化效果
    /// 实现了 C# WinForms 与现代 Web 技术栈的深度融合
    /// </summary>
    public partial class FormWebVisual : Form
    {
        private WebView2 webView; // 核心浏览器内核容器指针

        public FormWebVisual()
        {
            InitializeComponent();
            // Initialization moved to Form_Load
        }

        // 【动态容器配置】：初始化 WebView2 实例并绑定导航监听
        private void InitializeComponent()
        {
            this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            ((System.ComponentModel.ISupportInitialize)(this.webView)).BeginInit();
            this.SuspendLayout();
            // 
            // webView
            // 
            this.webView.AllowExternalDrop = true;
            this.webView.CreationProperties = null;
            this.webView.DefaultBackgroundColor = System.Drawing.Color.White;
            this.webView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.webView.Location = new System.Drawing.Point(0, 0);
            this.webView.Name = "webView";
            this.webView.Size = new System.Drawing.Size(800, 450);
            this.webView.TabIndex = 0;
            this.webView.ZoomFactor = 1D;
            // 
            // FormWebVisual
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.webView);
            this.Name = "FormWebVisual";
            this.Text = "游客演示模式 - 现代 Web 可视化体验";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.FormWebVisual_Load);

            // [业务逻辑绑定]：在页面导航完成后自动执行数据注入
            this.webView.NavigationCompleted += WebView_NavigationCompleted;

            ((System.ComponentModel.ISupportInitialize)(this.webView)).EndInit();
            this.ResumeLayout(false);
        }

        private void WebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess)
            {
                InjectData();
            }
        }

        // 【环境预热】：异步初始化 WebView2 运行时并注册 C# 通信协议桥
        private async void FormWebVisual_Load(object sender, EventArgs e)
        {
            try
            {
                // 等待内核就绪
                await webView.EnsureCoreWebView2Async(null);

                // [异构系统桥接]：将 C# 的 WebBridge 对象暴露给 JavaScript
                // JS 端可以通过 `window.chrome.webview.hostObjects.bridge` 进行无缝调用
                webView.CoreWebView2.AddHostObjectToScript("bridge", new WebBridge());

                // [资源定位]：自适应搜索 HTML 资产路径 (适配开发环境与发布环境)
                string htmlPath = FindHtmlAsset("index.html");

                if (File.Exists(htmlPath))
                {
                    webView.Source = new Uri(htmlPath); // 激活导航
                }
                else
                {
                    MessageBox.Show($"可视化前端资源丢失，请检查路径：{htmlPath}", "部署异常", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 内核启动失败，请确保宿主环境已安装 WebView2 Runtime。\n错误明细：" + ex.Message);
            }
        }

        // [Agent] Core Logic: Independent Data Injection (SQL Version)
        // 【全量数据压入】：将后台 SQL 数据库内容以 JSON 流形式推送到前端 ECharts 中
        private void InjectData()
        {
            if (webView.CoreWebView2 == null) return;

            try
            {
                // 首先注入静态地理底图配置 (GeoJSON)
                InjectMapData();

                // 调用数据分析引擎获取非遗明细 (JSON)
                WebBridge bridge = new WebBridge();
                string json = bridge.GetAllData();

                // [推模式下发]：通过 WebMessage 通信机制将数据推送到前端总线
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("数据总线故障: " + ex.Message);
            }
        }


        private void InjectMapData()
        {
            try
            {
                string mapJsonPath = FindHtmlAsset(@"data\shandong.json");
                if (File.Exists(mapJsonPath))
                {
                    string mapJson = File.ReadAllText(mapJsonPath);
                    // Inject map data into JS global variable
                    string script = $"window.SHANDONG_MAP_DATA = {mapJson};";
                    webView.CoreWebView2.ExecuteScriptAsync(script);
                }
                else
                {
                    MessageBox.Show($"Map data not found: {mapJsonPath}", "Warning");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Map injection error: " + ex.Message, "Error");
            }
        }

        // [Deleted] CalculateStatsInternal (Dead Code)

        private string FindField(ESRI.ArcGIS.Geodatabase.IFeatureClass fc, string[] candidates)
        {
            foreach (var f in candidates)
            {
                if (fc.Fields.FindField(f) != -1) return f;
            }
            return null;
        }

        private string FindDataAsset(string relativePath)
        {
            // Try relative to bin/Debug
            string path = Path.Combine(Application.StartupPath, relativePath);
            if (Directory.Exists(path) || File.Exists(path)) return path; // GDB is directory-like

            // Try dev path
            string devPath = Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\" + relativePath));
            if (Directory.Exists(devPath) || File.Exists(devPath)) return devPath;

            return null;
        }

        private string FindHtmlAsset(string fileName)
        {
            // 1. Try relative to executable (Output dir)
            string path = Path.Combine(Application.StartupPath, "VisualWeb", fileName);
            if (File.Exists(path)) return path;

            // 2. Try dev path (source dir)
            // StartupPath is typically .../bin/Debug
            string devPath = Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\VisualWeb", fileName));
            if (File.Exists(devPath)) return devPath;

            return path; // Return original check path for error message
        }
    }
}
