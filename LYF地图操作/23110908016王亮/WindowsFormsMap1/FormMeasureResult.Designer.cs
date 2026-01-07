
namespace WindowsFormsMap1
{
    partial class FormMeasureResult
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
            this.lblResultMeasure = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lblResultMeasure
            // 
            this.lblResultMeasure.AutoSize = true;
            this.lblResultMeasure.Location = new System.Drawing.Point(44, 130);
            this.lblResultMeasure.Name = "lblResultMeasure";
            this.lblResultMeasure.Size = new System.Drawing.Size(0, 18);
            this.lblResultMeasure.TabIndex = 0;
            this.lblResultMeasure.Click += new System.EventHandler(this.label1_Click);
            // 
            // FormMeasureResult
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.lblResultMeasure);
            this.Name = "FormMeasureResult";
            this.Text = "FormMeasureResult";
            this.Load += new System.EventHandler(this.FormMeasureResult_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        public System.Windows.Forms.Label lblResultMeasure;
    }
}