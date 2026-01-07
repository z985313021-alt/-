namespace WindowsFormsMap1
{
    partial class FormSymbolize
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
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormSymbolize));
            this.label1 = new System.Windows.Forms.Label();
            this.cmbLayer = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabSimple = new System.Windows.Forms.TabPage();
            this.btnSimpleStyle = new System.Windows.Forms.Button();
            this.picPreview = new System.Windows.Forms.PictureBox();
            this.label2 = new System.Windows.Forms.Label();
            this.tabUnique = new System.Windows.Forms.TabPage();
            this.lvUnique = new System.Windows.Forms.ListView();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.btnUniqueAddAll = new System.Windows.Forms.Button();
            this.cmbUniqueField = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tabClassBreaks = new System.Windows.Forms.TabPage();
            this.lvClassBreaks = new System.Windows.Forms.ListView();
            this.columnHeader3 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader4 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.numClassCount = new System.Windows.Forms.NumericUpDown();
            this.label5 = new System.Windows.Forms.Label();
            this.cmbClassField = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.btnApply = new System.Windows.Forms.Button();
            this.btnClose = new System.Windows.Forms.Button();
            this.axSymbologyControl1 = new ESRI.ArcGIS.Controls.AxSymbologyControl();
            this.groupBox1.SuspendLayout();
            this.tabControl1.SuspendLayout();
            this.tabSimple.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).BeginInit();
            this.tabUnique.SuspendLayout();
            this.tabClassBreaks.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numClassCount)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.axSymbologyControl1)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(13, 13);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(65, 12);
            this.label1.TabIndex = 0;
            this.label1.Text = "选择图层：";
            // 
            // cmbLayer
            // 
            this.cmbLayer.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbLayer.FormattingEnabled = true;
            this.cmbLayer.Location = new System.Drawing.Point(84, 10);
            this.cmbLayer.Name = "cmbLayer";
            this.cmbLayer.Size = new System.Drawing.Size(262, 20);
            this.cmbLayer.TabIndex = 1;
            this.cmbLayer.SelectedIndexChanged += new System.EventHandler(this.cmbLayer_SelectedIndexChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.tabControl1);
            this.groupBox1.Location = new System.Drawing.Point(12, 40);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(460, 350);
            this.groupBox1.TabIndex = 2;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "渲染方式";
            // 
            // tabControl1
            // 
            this.tabControl1.Controls.Add(this.tabSimple);
            this.tabControl1.Controls.Add(this.tabUnique);
            this.tabControl1.Controls.Add(this.tabClassBreaks);
            this.tabControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabControl1.Location = new System.Drawing.Point(3, 17);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(454, 330);
            this.tabControl1.TabIndex = 0;
            // 
            // tabSimple
            // 
            this.tabSimple.Controls.Add(this.btnSimpleStyle);
            this.tabSimple.Controls.Add(this.picPreview);
            this.tabSimple.Controls.Add(this.label2);
            this.tabSimple.Location = new System.Drawing.Point(4, 22);
            this.tabSimple.Name = "tabSimple";
            this.tabSimple.Padding = new System.Windows.Forms.Padding(3);
            this.tabSimple.Size = new System.Drawing.Size(446, 304);
            this.tabSimple.TabIndex = 0;
            this.tabSimple.Text = "单一符号";
            this.tabSimple.UseVisualStyleBackColor = true;
            // 
            // btnSimpleStyle
            // 
            this.btnSimpleStyle.Location = new System.Drawing.Point(180, 150);
            this.btnSimpleStyle.Name = "btnSimpleStyle";
            this.btnSimpleStyle.Size = new System.Drawing.Size(75, 23);
            this.btnSimpleStyle.TabIndex = 2;
            this.btnSimpleStyle.Text = "更改符号...";
            this.btnSimpleStyle.UseVisualStyleBackColor = true;
            this.btnSimpleStyle.Click += new System.EventHandler(this.btnSimpleStyle_Click);
            // 
            // picPreview
            // 
            this.picPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picPreview.Location = new System.Drawing.Point(167, 44);
            this.picPreview.Name = "picPreview";
            this.picPreview.Size = new System.Drawing.Size(100, 100);
            this.picPreview.TabIndex = 1;
            this.picPreview.TabStop = false;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(20, 20);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(161, 12);
            this.label2.TabIndex = 0;
            this.label2.Text = "所有要素使用相同的符号绘制";
            // 
            // tabUnique
            // 
            this.tabUnique.Controls.Add(this.lvUnique);
            this.tabUnique.Controls.Add(this.btnUniqueAddAll);
            this.tabUnique.Controls.Add(this.cmbUniqueField);
            this.tabUnique.Controls.Add(this.label3);
            this.tabUnique.Location = new System.Drawing.Point(4, 22);
            this.tabUnique.Name = "tabUnique";
            this.tabUnique.Padding = new System.Windows.Forms.Padding(3);
            this.tabUnique.Size = new System.Drawing.Size(446, 304);
            this.tabUnique.TabIndex = 1;
            this.tabUnique.Text = "唯一值渲染";
            this.tabUnique.UseVisualStyleBackColor = true;
            // 
            // lvUnique
            // 
            this.lvUnique.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
            this.lvUnique.FullRowSelect = true;
            this.lvUnique.GridLines = true;
            this.lvUnique.HideSelection = false;
            this.lvUnique.Location = new System.Drawing.Point(21, 57);
            this.lvUnique.Name = "lvUnique";
            this.lvUnique.Size = new System.Drawing.Size(404, 227);
            this.lvUnique.TabIndex = 3;
            this.lvUnique.UseCompatibleStateImageBehavior = false;
            this.lvUnique.View = System.Windows.Forms.View.Details;
            this.lvUnique.DoubleClick += new System.EventHandler(this.lvUnique_DoubleClick);
            // 
            // columnHeader1
            // 
            this.columnHeader1.Text = "值";
            this.columnHeader1.Width = 120;
            // 
            // columnHeader2
            // 
            this.columnHeader2.Text = "标签";
            this.columnHeader2.Width = 120;
            // 
            // btnUniqueAddAll
            // 
            this.btnUniqueAddAll.Location = new System.Drawing.Point(250, 15);
            this.btnUniqueAddAll.Name = "btnUniqueAddAll";
            this.btnUniqueAddAll.Size = new System.Drawing.Size(120, 23);
            this.btnUniqueAddAll.TabIndex = 2;
            this.btnUniqueAddAll.Text = "添加所有值";
            this.btnUniqueAddAll.UseVisualStyleBackColor = true;
            this.btnUniqueAddAll.Click += new System.EventHandler(this.btnUniqueAddAll_Click);
            // 
            // cmbUniqueField
            // 
            this.cmbUniqueField.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbUniqueField.FormattingEnabled = true;
            this.cmbUniqueField.Location = new System.Drawing.Point(85, 17);
            this.cmbUniqueField.Name = "cmbUniqueField";
            this.cmbUniqueField.Size = new System.Drawing.Size(150, 20);
            this.cmbUniqueField.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(19, 20);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 12);
            this.label3.TabIndex = 0;
            this.label3.Text = "值字段：";
            // 
            // tabClassBreaks
            // 
            this.tabClassBreaks.Controls.Add(this.lvClassBreaks);
            this.tabClassBreaks.Controls.Add(this.numClassCount);
            this.tabClassBreaks.Controls.Add(this.label5);
            this.tabClassBreaks.Controls.Add(this.cmbClassField);
            this.tabClassBreaks.Controls.Add(this.label4);
            this.tabClassBreaks.Location = new System.Drawing.Point(4, 22);
            this.tabClassBreaks.Name = "tabClassBreaks";
            this.tabClassBreaks.Padding = new System.Windows.Forms.Padding(3);
            this.tabClassBreaks.Size = new System.Drawing.Size(446, 304);
            this.tabClassBreaks.TabIndex = 2;
            this.tabClassBreaks.Text = "分级色彩";
            this.tabClassBreaks.UseVisualStyleBackColor = true;
            // 
            // lvClassBreaks
            // 
            this.lvClassBreaks.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader3,
            this.columnHeader4});
            this.lvClassBreaks.FullRowSelect = true;
            this.lvClassBreaks.GridLines = true;
            this.lvClassBreaks.HideSelection = false;
            this.lvClassBreaks.Location = new System.Drawing.Point(21, 57);
            this.lvClassBreaks.Name = "lvClassBreaks";
            this.lvClassBreaks.Size = new System.Drawing.Size(404, 227);
            this.lvClassBreaks.TabIndex = 7;
            this.lvClassBreaks.UseCompatibleStateImageBehavior = false;
            this.lvClassBreaks.View = System.Windows.Forms.View.Details;
            this.lvClassBreaks.DoubleClick += new System.EventHandler(this.lvClassBreaks_DoubleClick);
            // 
            // columnHeader3
            // 
            this.columnHeader3.Text = "范围";
            this.columnHeader3.Width = 150;
            // 
            // columnHeader4
            // 
            this.columnHeader4.Text = "标签";
            this.columnHeader4.Width = 120;
            // 
            // numClassCount
            // 
            this.numClassCount.Location = new System.Drawing.Point(300, 17);
            this.numClassCount.Maximum = new decimal(new int[] {
            32,
            0,
            0,
            0});
            this.numClassCount.Minimum = new decimal(new int[] {
            2,
            0,
            0,
            0});
            this.numClassCount.Name = "numClassCount";
            this.numClassCount.Size = new System.Drawing.Size(60, 21);
            this.numClassCount.TabIndex = 6;
            this.numClassCount.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.numClassCount.ValueChanged += new System.EventHandler(this.numClassCount_ValueChanged);
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(253, 20);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(41, 12);
            this.label5.TabIndex = 5;
            this.label5.Text = "分类：";
            // 
            // cmbClassField
            // 
            this.cmbClassField.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbClassField.FormattingEnabled = true;
            this.cmbClassField.Location = new System.Drawing.Point(85, 17);
            this.cmbClassField.Name = "cmbClassField";
            this.cmbClassField.Size = new System.Drawing.Size(150, 20);
            this.cmbClassField.TabIndex = 3;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(19, 20);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(53, 12);
            this.label4.TabIndex = 2;
            this.label4.Text = "值字段：";
            // 
            // btnApply
            // 
            this.btnApply.Location = new System.Drawing.Point(312, 400);
            this.btnApply.Name = "btnApply";
            this.btnApply.Size = new System.Drawing.Size(75, 23);
            this.btnApply.TabIndex = 3;
            this.btnApply.Text = "应用";
            this.btnApply.UseVisualStyleBackColor = true;
            this.btnApply.Click += new System.EventHandler(this.btnApply_Click);
            // 
            // btnClose
            // 
            this.btnClose.Location = new System.Drawing.Point(397, 400);
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size(75, 23);
            this.btnClose.TabIndex = 4;
            this.btnClose.Text = "关闭";
            this.btnClose.UseVisualStyleBackColor = true;
            this.btnClose.Click += new System.EventHandler(this.btnClose_Click);
            // 
            // axSymbologyControl1
            // 
            this.axSymbologyControl1.Location = new System.Drawing.Point(12, 400);
            this.axSymbologyControl1.Name = "axSymbologyControl1";
            this.axSymbologyControl1.Size = new System.Drawing.Size(265, 265);
            this.axSymbologyControl1.TabIndex = 5;
            this.axSymbologyControl1.Visible = false;
            // 
            // FormSymbolize
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 436);
            this.Controls.Add(this.axSymbologyControl1);
            this.Controls.Add(this.btnClose);
            this.Controls.Add(this.btnApply);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.cmbLayer);
            this.Controls.Add(this.label1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormSymbolize";
            this.Text = "符号化";
            this.Load += new System.EventHandler(this.FormSymbolize_Load);
            this.groupBox1.ResumeLayout(false);
            this.tabControl1.ResumeLayout(false);
            this.tabSimple.ResumeLayout(false);
            this.tabSimple.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.picPreview)).EndInit();
            this.tabUnique.ResumeLayout(false);
            this.tabUnique.PerformLayout();
            this.tabClassBreaks.ResumeLayout(false);
            this.tabClassBreaks.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numClassCount)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.axSymbologyControl1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.ComboBox cmbLayer;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabSimple;
        private System.Windows.Forms.TabPage tabUnique;
        private System.Windows.Forms.TabPage tabClassBreaks;
        private System.Windows.Forms.Button btnApply;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.PictureBox picPreview;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnSimpleStyle;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox cmbUniqueField;
        private System.Windows.Forms.Button btnUniqueAddAll;
        private System.Windows.Forms.ListView lvUnique;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox cmbClassField;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.NumericUpDown numClassCount;
        private System.Windows.Forms.ListView lvClassBreaks;
        private System.Windows.Forms.ColumnHeader columnHeader3;
        private System.Windows.Forms.ColumnHeader columnHeader4;
        public ESRI.ArcGIS.Controls.AxSymbologyControl axSymbologyControl1;
    }
}
