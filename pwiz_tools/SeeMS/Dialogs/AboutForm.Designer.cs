namespace seems
{
    partial class AboutForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose( bool disposing )
        {
            if( disposing && ( components != null ) )
            {
                components.Dispose();
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.componentListView = new System.Windows.Forms.ListView();
            this.Component = new System.Windows.Forms.ColumnHeader();
            this.Version = new System.Windows.Forms.ColumnHeader();
            this.aboutTextBox = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // componentListView
            // 
            this.componentListView.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.componentListView.Columns.AddRange( new System.Windows.Forms.ColumnHeader[] {
            this.Component,
            this.Version} );
            this.componentListView.FullRowSelect = true;
            this.componentListView.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.componentListView.LabelWrap = false;
            this.componentListView.Location = new System.Drawing.Point( 12, 115 );
            this.componentListView.Name = "componentListView";
            this.componentListView.Scrollable = false;
            this.componentListView.Size = new System.Drawing.Size( 262, 89 );
            this.componentListView.TabIndex = 0;
            this.componentListView.UseCompatibleStateImageBehavior = false;
            this.componentListView.View = System.Windows.Forms.View.Details;
            // 
            // Component
            // 
            this.Component.Text = "Component";
            this.Component.Width = 121;
            // 
            // Version
            // 
            this.Version.Text = "Version";
            this.Version.Width = 136;
            // 
            // aboutTextBox
            // 
            this.aboutTextBox.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.aboutTextBox.BackColor = System.Drawing.SystemColors.Control;
            this.aboutTextBox.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.aboutTextBox.Location = new System.Drawing.Point( 12, 14 );
            this.aboutTextBox.Multiline = true;
            this.aboutTextBox.Name = "aboutTextBox";
            this.aboutTextBox.Size = new System.Drawing.Size( 261, 97 );
            this.aboutTextBox.TabIndex = 1;
            this.aboutTextBox.Text = "SeeMS <<version>>\r\n� 2008 Vanderbilt University\r\n\r\nAuthor: Matt Chambers\r\n\r\nThank" +
                "s to: David Tabb, the ProteoWizard Team,\r\n                  Digital Rune, Joe Wo" +
                "odbury";
            // 
            // AboutForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 284, 214 );
            this.Controls.Add( this.aboutTextBox );
            this.Controls.Add( this.componentListView );
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size( 292, 248 );
            this.Name = "AboutForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "About SeeMS";
            this.ResumeLayout( false );
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView componentListView;
        private System.Windows.Forms.ColumnHeader Component;
        private System.Windows.Forms.ColumnHeader Version;
        private System.Windows.Forms.TextBox aboutTextBox;


    }
}