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
    public partial class FormWebVisual : Form
    {
        private WebView2 webView;

        public FormWebVisual()
        {
            InitializeComponent();
            // Initialization moved to Form_Load
        }

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
            this.Text = "Web Visualization Mode";
            this.WindowState = System.Windows.Forms.FormWindowState.Maximized;
            this.Load += new System.EventHandler(this.FormWebVisual_Load);

            // [Agent] Inject data when page finishes loading
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

        private async void FormWebVisual_Load(object sender, EventArgs e)
        {
            try
            {
                await webView.EnsureCoreWebView2Async(null);

                // [Agent] Register the bridge for SQL interaction
                // JS can access via: window.chrome.webview.hostObjects.bridge
                webView.CoreWebView2.AddHostObjectToScript("bridge", new WebBridge());

                // [Agent] Navigate AFTER bridge is registered to avoid race conditions
                string htmlPath = FindHtmlAsset("index.html");

                if (File.Exists(htmlPath))
                {
                    webView.Source = new Uri(htmlPath);
                }
                else
                {
                    MessageBox.Show($"VisualWeb assets not found at: {htmlPath}", "Assets Missing", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("WebView2 Runtime initialization failed: " + ex.Message + "\nMake sure WebView2 Runtime is installed.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // [Agent] Core Logic: Independent Data Injection (SQL Version)
        private void InjectData()
        {
            if (webView.CoreWebView2 == null) return;

            try
            {
                // First inject map data
                InjectMapData();

                // First inject map data
                InjectMapData();

                // Then inject ICH data
                WebBridge bridge = new WebBridge();
                string json = bridge.GetAllData();

                // Send to JS
                webView.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Injection Error: " + ex.Message);
                MessageBox.Show("数据注入错误: " + ex.Message, "Error");
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
