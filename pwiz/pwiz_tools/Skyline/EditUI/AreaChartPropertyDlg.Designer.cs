﻿namespace pwiz.Skyline.EditUI
{
    partial class AreaChartPropertyDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AreaChartPropertyDlg));
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnOk = new System.Windows.Forms.Button();
            this.textMaxArea = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.cbDecimalCvs = new System.Windows.Forms.CheckBox();
            this.labelCvPercent = new System.Windows.Forms.Label();
            this.textMaxCv = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // textMaxArea
            // 
            resources.ApplyResources(this.textMaxArea, "textMaxArea");
            this.textMaxArea.Name = "textMaxArea";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.cbDecimalCvs);
            this.groupBox1.Controls.Add(this.labelCvPercent);
            this.groupBox1.Controls.Add(this.textMaxCv);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Controls.Add(this.textMaxArea);
            this.groupBox1.Controls.Add(this.label2);
            resources.ApplyResources(this.groupBox1, "groupBox1");
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.TabStop = false;
            // 
            // cbDecimalCvs
            // 
            resources.ApplyResources(this.cbDecimalCvs, "cbDecimalCvs");
            this.cbDecimalCvs.Name = "cbDecimalCvs";
            this.cbDecimalCvs.UseVisualStyleBackColor = true;
            this.cbDecimalCvs.CheckedChanged += new System.EventHandler(this.cbDecimalCvs_CheckedChanged);
            // 
            // labelCvPercent
            // 
            resources.ApplyResources(this.labelCvPercent, "labelCvPercent");
            this.labelCvPercent.Name = "labelCvPercent";
            // 
            // textMaxCv
            // 
            resources.ApplyResources(this.textMaxCv, "textMaxCv");
            this.textMaxCv.Name = "textMaxCv";
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // AreaChartPropertyDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "AreaChartPropertyDlg";
            this.ShowInTaskbar = false;
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.TextBox textMaxArea;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox textMaxCv;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label labelCvPercent;
        private System.Windows.Forms.CheckBox cbDecimalCvs;
    }
}