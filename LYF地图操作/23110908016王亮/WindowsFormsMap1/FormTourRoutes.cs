using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geometry;

namespace WindowsFormsMap1
{
    public partial class FormTourRoutes : Form
    {
        private Form1 _mainForm;
        private List<AnalysisHelper.TourRoute> _routes;

        public FormTourRoutes(Form1 mainForm, List<AnalysisHelper.TourRoute> routes)
        {
            InitializeComponent();
            _mainForm = mainForm;
            _routes = routes;
            InitData();
        }

        private void InitData()
        {
            if (_routes == null || _routes.Count == 0)
            {
                lblStatus.Text = "暂无推荐路线";
                return;
            }

            foreach (var route in _routes)
            {
                listBoxRoutes.Items.Add(route); // Tostrring will be used
            }
            lblStatus.Text = $"共发现 {_routes.Count} 条推荐路线";
        }

        private void listBoxRoutes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxRoutes.SelectedItem is AnalysisHelper.TourRoute route)
            {
                txtDescription.Text = route.Description;
                btnShow.Enabled = true;
            }
        }

        private void btnShow_Click(object sender, EventArgs e)
        {
            if (listBoxRoutes.SelectedItem is AnalysisHelper.TourRoute route)
            {
                _mainForm.DisplayTourRoute(route);
                
                // Keep the window open or close depending on preference, sticking to open for now
                // this.Close(); 
            }
        }

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.listBoxRoutes = new System.Windows.Forms.ListBox();
            this.txtDescription = new System.Windows.Forms.TextBox();
            this.btnShow = new System.Windows.Forms.Button();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblStatus = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // listBoxRoutes
            // 
            this.listBoxRoutes.Font = new System.Drawing.Font("Microsoft YaHei", 10.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.listBoxRoutes.FormattingEnabled = true;
            this.listBoxRoutes.ItemHeight = 20;
            this.listBoxRoutes.Location = new System.Drawing.Point(20, 50);
            this.listBoxRoutes.Name = "listBoxRoutes";
            this.listBoxRoutes.Size = new System.Drawing.Size(200, 240);
            this.listBoxRoutes.TabIndex = 0;
            this.listBoxRoutes.SelectedIndexChanged += new System.EventHandler(this.listBoxRoutes_SelectedIndexChanged);
            // 
            // txtDescription
            // 
            this.txtDescription.BackColor = System.Drawing.Color.White;
            this.txtDescription.Font = new System.Drawing.Font("Microsoft YaHei", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.txtDescription.Location = new System.Drawing.Point(240, 50);
            this.txtDescription.Multiline = true;
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.ReadOnly = true;
            this.txtDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtDescription.Size = new System.Drawing.Size(200, 180);
            this.txtDescription.TabIndex = 1;
            // 
            // btnShow
            // 
            this.btnShow.BackColor = System.Drawing.Color.SteelBlue;
            this.btnShow.Cursor = System.Windows.Forms.Cursors.Hand;
            this.btnShow.Enabled = false;
            this.btnShow.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnShow.Font = new System.Drawing.Font("Microsoft YaHei", 10.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnShow.ForeColor = System.Drawing.Color.White;
            this.btnShow.Location = new System.Drawing.Point(240, 250);
            this.btnShow.Name = "btnShow";
            this.btnShow.Size = new System.Drawing.Size(200, 40);
            this.btnShow.TabIndex = 2;
            this.btnShow.Text = "在地图中展示";
            this.btnShow.UseVisualStyleBackColor = false;
            this.btnShow.Click += new System.EventHandler(this.btnShow_Click);
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft YaHei UI", 14F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.lblTitle.ForeColor = System.Drawing.Color.DarkSlateBlue;
            this.lblTitle.Location = new System.Drawing.Point(15, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(164, 26);
            this.lblTitle.TabIndex = 3;
            this.lblTitle.Text = "推荐游览路线列表";
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.ForeColor = System.Drawing.Color.Gray;
            this.lblStatus.Location = new System.Drawing.Point(20, 300);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(65, 12);
            this.lblStatus.TabIndex = 4;
            this.lblStatus.Text = "等待选择...";
            // 
            // FormTourRoutes
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.WhiteSmoke;
            this.ClientSize = new System.Drawing.Size(460, 320);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.lblTitle);
            this.Controls.Add(this.btnShow);
            this.Controls.Add(this.txtDescription);
            this.Controls.Add(this.listBoxRoutes);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.Name = "FormTourRoutes";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "智能路线推荐";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox listBoxRoutes;
        private System.Windows.Forms.TextBox txtDescription;
        private System.Windows.Forms.Button btnShow;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblStatus;
    }
}
