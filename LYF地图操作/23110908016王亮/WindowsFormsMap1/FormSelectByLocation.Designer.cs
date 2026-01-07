
namespace WindowsFormsMap1
{
    partial class FormSelectByLocation
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
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.checkedListBoxTargetLayers = new System.Windows.Forms.CheckedListBox();
            this.chkOnlyShowSelectable = new System.Windows.Forms.CheckBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.comboBoxSourceLayer = new System.Windows.Forms.ComboBox();
            this.chkUseSelectedFeatures = new System.Windows.Forms.CheckBox();
            this.lblSelectedCount = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.comboBoxMethods = new System.Windows.Forms.ComboBox();
            this.chkUseBuffer = new System.Windows.Forms.CheckBox();
            this.txtBufferSize = new System.Windows.Forms.TextBox();
            this.lblBufferUnits = new System.Windows.Forms.Label();
            this.buttonOK = new System.Windows.Forms.Button();
            this.buttonApply = new System.Windows.Forms.Button();
            this.buttonClose = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.Dock = System.Windows.Forms.DockStyle.Top;
            this.label1.Font = new System.Drawing.Font("宋体", 11F);
            this.label1.Location = new System.Drawing.Point(0, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(665, 58);
            this.label1.TabIndex = 0;
            this.label1.Text = "根据空间位置选择是使用源图层中的要素与目标图层的空间关系（如覆盖、相交等），在目标图层中选择要素的操作";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.checkedListBoxTargetLayers);
            this.groupBox1.Location = new System.Drawing.Point(3, 61);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(658, 390);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "目标图层";
            this.groupBox1.Enter += new System.EventHandler(this.groupBox1_Enter);
            // 
            // checkedListBoxTargetLayers
            // 
            this.checkedListBoxTargetLayers.Dock = System.Windows.Forms.DockStyle.Fill;
            this.checkedListBoxTargetLayers.FormattingEnabled = true;
            this.checkedListBoxTargetLayers.Location = new System.Drawing.Point(3, 24);
            this.checkedListBoxTargetLayers.Name = "checkedListBoxTargetLayers";
            this.checkedListBoxTargetLayers.Size = new System.Drawing.Size(652, 363);
            this.checkedListBoxTargetLayers.TabIndex = 0;
            this.checkedListBoxTargetLayers.SelectedIndexChanged += new System.EventHandler(this.checkedListBox1_SelectedIndexChanged);
            // 
            // chkOnlyShowSelectable
            // 
            this.chkOnlyShowSelectable.AutoSize = true;
            this.chkOnlyShowSelectable.Location = new System.Drawing.Point(12, 457);
            this.chkOnlyShowSelectable.Name = "chkOnlyShowSelectable";
            this.chkOnlyShowSelectable.Size = new System.Drawing.Size(160, 22);
            this.chkOnlyShowSelectable.TabIndex = 2;
            this.chkOnlyShowSelectable.Text = "只列出可选图层";
            this.chkOnlyShowSelectable.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.comboBoxSourceLayer);
            this.groupBox2.Location = new System.Drawing.Point(6, 485);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(652, 59);
            this.groupBox2.TabIndex = 3;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "源图层";
            // 
            // comboBoxSourceLayer
            // 
            this.comboBoxSourceLayer.Dock = System.Windows.Forms.DockStyle.Top;
            this.comboBoxSourceLayer.FormattingEnabled = true;
            this.comboBoxSourceLayer.Location = new System.Drawing.Point(3, 24);
            this.comboBoxSourceLayer.Name = "comboBoxSourceLayer";
            this.comboBoxSourceLayer.Size = new System.Drawing.Size(646, 26);
            this.comboBoxSourceLayer.TabIndex = 0;
            // 
            // chkUseSelectedFeatures
            // 
            this.chkUseSelectedFeatures.AutoSize = true;
            this.chkUseSelectedFeatures.Location = new System.Drawing.Point(9, 550);
            this.chkUseSelectedFeatures.Name = "chkUseSelectedFeatures";
            this.chkUseSelectedFeatures.Size = new System.Drawing.Size(178, 22);
            this.chkUseSelectedFeatures.TabIndex = 4;
            this.chkUseSelectedFeatures.Text = "使用被选择的要素";
            this.chkUseSelectedFeatures.UseVisualStyleBackColor = true;
            // 
            // lblSelectedCount
            // 
            this.lblSelectedCount.AutoSize = true;
            this.lblSelectedCount.Location = new System.Drawing.Point(244, 554);
            this.lblSelectedCount.Name = "lblSelectedCount";
            this.lblSelectedCount.Size = new System.Drawing.Size(215, 18);
            this.lblSelectedCount.TabIndex = 5;
            this.lblSelectedCount.Text = "(当前有 0 个要素被选择)";
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.comboBoxMethods);
            this.groupBox3.Location = new System.Drawing.Point(12, 578);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(646, 59);
            this.groupBox3.TabIndex = 6;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "空间选择方法:";
            // 
            // comboBoxMethods
            // 
            this.comboBoxMethods.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboBoxMethods.FormattingEnabled = true;
            this.comboBoxMethods.Items.AddRange(new object[] {
            "目标图层的要素与源图层的要素相交(intersect)",
            "目标图层的要素位于源图层要素的一定距离范围内(within a distance)",
            "目标图层的要素包含源图层的要素(contain)",
            "目标图层的要素在源图层的要素内(within)",
            "目标图层的要素与源图层要素的边界相接(touch)",
            "目标图层的要素被源图层要素的轮廓穿过(cross)"});
            this.comboBoxMethods.Location = new System.Drawing.Point(3, 24);
            this.comboBoxMethods.Name = "comboBoxMethods";
            this.comboBoxMethods.Size = new System.Drawing.Size(640, 26);
            this.comboBoxMethods.TabIndex = 0;
            // 
            // chkUseBuffer
            // 
            this.chkUseBuffer.AutoSize = true;
            this.chkUseBuffer.Location = new System.Drawing.Point(15, 661);
            this.chkUseBuffer.Name = "chkUseBuffer";
            this.chkUseBuffer.Size = new System.Drawing.Size(385, 22);
            this.chkUseBuffer.TabIndex = 7;
            this.chkUseBuffer.Text = "对源图层使用缓冲区进行查询。缓冲区大小:";
            this.chkUseBuffer.UseVisualStyleBackColor = true;
            // 
            // txtBufferSize
            // 
            this.txtBufferSize.Location = new System.Drawing.Point(406, 655);
            this.txtBufferSize.Name = "txtBufferSize";
            this.txtBufferSize.Size = new System.Drawing.Size(249, 28);
            this.txtBufferSize.TabIndex = 8;
            // 
            // lblBufferUnits
            // 
            this.lblBufferUnits.AutoSize = true;
            this.lblBufferUnits.Location = new System.Drawing.Point(444, 700);
            this.lblBufferUnits.Name = "lblBufferUnits";
            this.lblBufferUnits.Size = new System.Drawing.Size(188, 18);
            this.lblBufferUnits.TabIndex = 9;
            this.lblBufferUnits.Text = "(单位与地图单位相同)";
            // 
            // buttonOK
            // 
            this.buttonOK.Location = new System.Drawing.Point(330, 744);
            this.buttonOK.Name = "buttonOK";
            this.buttonOK.Size = new System.Drawing.Size(94, 32);
            this.buttonOK.TabIndex = 10;
            this.buttonOK.Text = "确定";
            this.buttonOK.UseVisualStyleBackColor = true;
            this.buttonOK.Click += new System.EventHandler(this.buttonOK_Click);
            // 
            // buttonApply
            // 
            this.buttonApply.Location = new System.Drawing.Point(445, 744);
            this.buttonApply.Name = "buttonApply";
            this.buttonApply.Size = new System.Drawing.Size(101, 31);
            this.buttonApply.TabIndex = 11;
            this.buttonApply.Text = "应用";
            this.buttonApply.UseVisualStyleBackColor = true;
            this.buttonApply.Click += new System.EventHandler(this.buttonApply_Click);
            // 
            // buttonClose
            // 
            this.buttonClose.Location = new System.Drawing.Point(556, 744);
            this.buttonClose.Name = "buttonClose";
            this.buttonClose.Size = new System.Drawing.Size(97, 29);
            this.buttonClose.TabIndex = 12;
            this.buttonClose.Text = "关闭";
            this.buttonClose.UseVisualStyleBackColor = true;
            this.buttonClose.Click += new System.EventHandler(this.buttonClose_Click);
            // 
            // FormSelectByLocation
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(665, 781);
            this.Controls.Add(this.buttonClose);
            this.Controls.Add(this.buttonApply);
            this.Controls.Add(this.buttonOK);
            this.Controls.Add(this.lblBufferUnits);
            this.Controls.Add(this.txtBufferSize);
            this.Controls.Add(this.chkUseBuffer);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.lblSelectedCount);
            this.Controls.Add(this.chkUseSelectedFeatures);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.chkOnlyShowSelectable);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label1);
            this.Name = "FormSelectByLocation";
            this.Text = "FormSelectByLocation";
            this.Load += new System.EventHandler(this.FormSelectByLocation_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.CheckedListBox checkedListBoxTargetLayers;
        private System.Windows.Forms.CheckBox chkOnlyShowSelectable;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.ComboBox comboBoxSourceLayer;
        private System.Windows.Forms.CheckBox chkUseSelectedFeatures;
        private System.Windows.Forms.Label lblSelectedCount;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.ComboBox comboBoxMethods;
        private System.Windows.Forms.CheckBox chkUseBuffer;
        private System.Windows.Forms.TextBox txtBufferSize;
        private System.Windows.Forms.Label lblBufferUnits;
        private System.Windows.Forms.Button buttonOK;
        private System.Windows.Forms.Button buttonApply;
        private System.Windows.Forms.Button buttonClose;
    }
}