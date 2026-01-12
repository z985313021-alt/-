
namespace WindowsFormsMap1
{
    partial class FormRoute
    {
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
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.lstPoints = new System.Windows.Forms.ListBox();
            this.btnAddPoint = new System.Windows.Forms.Button();
            this.btnRemovePoint = new System.Windows.Forms.Button();
            this.btnClear = new System.Windows.Forms.Button();
            this.btnSolve = new System.Windows.Forms.Button();
            this.lblInfo = new System.Windows.Forms.Label();
            this.grpRoute = new System.Windows.Forms.GroupBox();
            this.grpNetwork = new System.Windows.Forms.GroupBox();
            this.btnBuildNetwork = new System.Windows.Forms.Button();
            this.grpRoute.SuspendLayout();
            this.grpNetwork.SuspendLayout();
            this.SuspendLayout();
            // 
            // lstPoints
            // 
            this.lstPoints.FormattingEnabled = true;
            this.lstPoints.ItemHeight = 12;
            this.lstPoints.Location = new System.Drawing.Point(12, 20);
            this.lstPoints.Name = "lstPoints";
            this.lstPoints.Size = new System.Drawing.Size(260, 100);
            this.lstPoints.TabIndex = 0;
            // 
            // btnAddPoint
            // 
            this.btnAddPoint.Location = new System.Drawing.Point(12, 126);
            this.btnAddPoint.Name = "btnAddPoint";
            this.btnAddPoint.Size = new System.Drawing.Size(75, 23);
            this.btnAddPoint.TabIndex = 1;
            this.btnAddPoint.Text = "添加点";
            this.btnAddPoint.UseVisualStyleBackColor = true;
            this.btnAddPoint.Click += new System.EventHandler(this.btnAddPoint_Click);
            // 
            // btnRemovePoint
            // 
            this.btnRemovePoint.Location = new System.Drawing.Point(93, 126);
            this.btnRemovePoint.Name = "btnRemovePoint";
            this.btnRemovePoint.Size = new System.Drawing.Size(75, 23);
            this.btnRemovePoint.TabIndex = 2;
            this.btnRemovePoint.Text = "移除选定";
            this.btnRemovePoint.UseVisualStyleBackColor = true;
            this.btnRemovePoint.Click += new System.EventHandler(this.btnRemovePoint_Click);
            // 
            // btnClear
            // 
            this.btnClear.Location = new System.Drawing.Point(174, 126);
            this.btnClear.Name = "btnClear";
            this.btnClear.Size = new System.Drawing.Size(75, 23);
            this.btnClear.TabIndex = 3;
            this.btnClear.Text = "清空所有";
            this.btnClear.UseVisualStyleBackColor = true;
            this.btnClear.Click += new System.EventHandler(this.btnClear_Click);
            // 
            // btnSolve
            // 
            this.btnSolve.Font = new System.Drawing.Font("SimSun", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnSolve.ForeColor = System.Drawing.Color.DarkGreen;
            this.btnSolve.Location = new System.Drawing.Point(12, 160);
            this.btnSolve.Name = "btnSolve";
            this.btnSolve.Size = new System.Drawing.Size(260, 36);
            this.btnSolve.TabIndex = 4;
            this.btnSolve.Text = "开始计算路径";
            this.btnSolve.UseVisualStyleBackColor = true;
            this.btnSolve.Click += new System.EventHandler(this.btnSolve_Click);
            // 
            // lblInfo
            // 
            this.lblInfo.AutoSize = true;
            this.lblInfo.Location = new System.Drawing.Point(13, 335);
            this.lblInfo.Name = "lblInfo";
            this.lblInfo.Size = new System.Drawing.Size(89, 12);
            this.lblInfo.TabIndex = 5;
            this.lblInfo.Text = "等待计算...";
            // 
            // grpRoute
            // 
            this.grpRoute.Controls.Add(this.lstPoints);
            this.grpRoute.Controls.Add(this.btnAddPoint);
            this.grpRoute.Controls.Add(this.btnRemovePoint);
            this.grpRoute.Controls.Add(this.btnClear);
            this.grpRoute.Controls.Add(this.btnSolve);
            this.grpRoute.Location = new System.Drawing.Point(12, 90);
            this.grpRoute.Name = "grpRoute";
            this.grpRoute.Size = new System.Drawing.Size(284, 210);
            this.grpRoute.TabIndex = 6;
            this.grpRoute.TabStop = false;
            this.grpRoute.Text = "第2步：规划面板";
            // 
            // grpNetwork
            // 
            this.grpNetwork.Controls.Add(this.btnBuildNetwork);
            this.grpNetwork.Location = new System.Drawing.Point(12, 12);
            this.grpNetwork.Name = "grpNetwork";
            this.grpNetwork.Size = new System.Drawing.Size(284, 70);
            this.grpNetwork.TabIndex = 7;
            this.grpNetwork.TabStop = false;
            this.grpNetwork.Text = "第1步：准备数据";
            // 
            // btnBuildNetwork
            // 
            this.btnBuildNetwork.Font = new System.Drawing.Font("SimSun", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(134)));
            this.btnBuildNetwork.ForeColor = System.Drawing.Color.Blue;
            this.btnBuildNetwork.Location = new System.Drawing.Point(12, 20);
            this.btnBuildNetwork.Name = "btnBuildNetwork";
            this.btnBuildNetwork.Size = new System.Drawing.Size(260, 36);
            this.btnBuildNetwork.TabIndex = 5;
            this.btnBuildNetwork.Text = "构建/更新道路网络 (必需)";
            this.btnBuildNetwork.UseVisualStyleBackColor = true;
            this.btnBuildNetwork.Click += new System.EventHandler(this.btnBuildNetwork_Click);
            // 
            // FormRoute
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(309, 360);
            this.Controls.Add(this.grpNetwork);
            this.Controls.Add(this.lblInfo);
            this.Controls.Add(this.grpRoute);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.Name = "FormRoute";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "智能路径规划";
            this.TopMost = true;
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormRoute_FormClosing);
            this.grpRoute.ResumeLayout(false);
            this.grpNetwork.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListBox lstPoints;
        private System.Windows.Forms.Button btnAddPoint;
        private System.Windows.Forms.Button btnRemovePoint;
        private System.Windows.Forms.Button btnClear;
        private System.Windows.Forms.Button btnSolve;
        private System.Windows.Forms.Label lblInfo;
        private System.Windows.Forms.GroupBox grpRoute;
        private System.Windows.Forms.GroupBox grpNetwork;
        private System.Windows.Forms.Button btnBuildNetwork;
    }
}
