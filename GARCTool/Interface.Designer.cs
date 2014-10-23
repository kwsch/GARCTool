namespace GARCTool
{
    partial class Interface
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
            this.B_OpenFolder = new System.Windows.Forms.Button();
            this.B_Process = new System.Windows.Forms.Button();
            this.TB_Path = new System.Windows.Forms.TextBox();
            this.B_OpenFile = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.label1 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // B_OpenFolder
            // 
            this.B_OpenFolder.Location = new System.Drawing.Point(12, 12);
            this.B_OpenFolder.Name = "B_OpenFolder";
            this.B_OpenFolder.Size = new System.Drawing.Size(75, 23);
            this.B_OpenFolder.TabIndex = 0;
            this.B_OpenFolder.Text = "Open Folder";
            this.B_OpenFolder.UseVisualStyleBackColor = true;
            this.B_OpenFolder.Click += new System.EventHandler(this.B_Open_Click);
            // 
            // B_Process
            // 
            this.B_Process.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.B_Process.Location = new System.Drawing.Point(196, 12);
            this.B_Process.Name = "B_Process";
            this.B_Process.Size = new System.Drawing.Size(75, 23);
            this.B_Process.TabIndex = 1;
            this.B_Process.Text = "Process";
            this.B_Process.UseVisualStyleBackColor = true;
            this.B_Process.Click += new System.EventHandler(this.B_Process_Click);
            // 
            // TB_Path
            // 
            this.TB_Path.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.TB_Path.Location = new System.Drawing.Point(12, 41);
            this.TB_Path.Name = "TB_Path";
            this.TB_Path.Size = new System.Drawing.Size(259, 20);
            this.TB_Path.TabIndex = 2;
            // 
            // B_OpenFile
            // 
            this.B_OpenFile.Location = new System.Drawing.Point(105, 12);
            this.B_OpenFile.Name = "B_OpenFile";
            this.B_OpenFile.Size = new System.Drawing.Size(75, 23);
            this.B_OpenFile.TabIndex = 3;
            this.B_OpenFile.Text = "Open File";
            this.B_OpenFile.UseVisualStyleBackColor = true;
            this.B_OpenFile.Click += new System.EventHandler(this.B_OpenFile_Click);
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(12, 67);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(259, 13);
            this.progressBar.TabIndex = 4;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(11, 79);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(21, 13);
            this.label1.TabIndex = 5;
            this.label1.Text = "0%";
            this.label1.Visible = false;
            // 
            // Interface
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 92);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.B_OpenFile);
            this.Controls.Add(this.TB_Path);
            this.Controls.Add(this.B_Process);
            this.Controls.Add(this.B_OpenFolder);
            this.Controls.Add(this.label1);
            this.MaximizeBox = false;
            this.MaximumSize = new System.Drawing.Size(600, 130);
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(300, 130);
            this.Name = "Interface";
            this.Text = "GARCTool";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button B_OpenFolder;
        private System.Windows.Forms.Button B_Process;
        private System.Windows.Forms.TextBox TB_Path;
        private System.Windows.Forms.Button B_OpenFile;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label label1;
    }
}

