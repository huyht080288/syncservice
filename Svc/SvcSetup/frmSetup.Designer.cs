namespace SvcSetup
{
    partial class frmSetup
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.txtJsonPath = new System.Windows.Forms.TextBox();
            this.btnBrowseJson = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.txtLocalPath = new System.Windows.Forms.TextBox();
            this.btnBrowseFolder = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.txtDriveUrl = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.lblStatus = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(16, 32);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(158, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Google Cloud Credentials (JSON):";
            // 
            // txtJsonPath
            // 
            this.txtJsonPath.Location = new System.Drawing.Point(19, 48);
            this.txtJsonPath.Name = "txtJsonPath";
            this.txtJsonPath.ReadOnly = true;
            this.txtJsonPath.Size = new System.Drawing.Size(325, 20);
            this.txtJsonPath.TabIndex = 1;
            // 
            // btnBrowseJson
            // 
            this.btnBrowseJson.Location = new System.Drawing.Point(350, 46);
            this.btnBrowseJson.Name = "btnBrowseJson";
            this.btnBrowseJson.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseJson.TabIndex = 2;
            this.btnBrowseJson.Text = "Duyệt...";
            this.btnBrowseJson.UseVisualStyleBackColor = true;
            this.btnBrowseJson.Click += new System.EventHandler(this.btnBrowseJson_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(16, 85);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(127, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Thư mục cần sao lưu (Local):";
            // 
            // txtLocalPath
            // 
            this.txtLocalPath.Location = new System.Drawing.Point(19, 101);
            this.txtLocalPath.Name = "txtLocalPath";
            this.txtLocalPath.Size = new System.Drawing.Size(325, 20);
            this.txtLocalPath.TabIndex = 4;
            // 
            // btnBrowseFolder
            // 
            this.btnBrowseFolder.Location = new System.Drawing.Point(350, 99);
            this.btnBrowseFolder.Name = "btnBrowseFolder";
            this.btnBrowseFolder.Size = new System.Drawing.Size(75, 23);
            this.btnBrowseFolder.TabIndex = 5;
            this.btnBrowseFolder.Text = "Chọn...";
            this.btnBrowseFolder.UseVisualStyleBackColor = true;
            this.btnBrowseFolder.Click += new System.EventHandler(this.btnBrowseFolder_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(16, 139);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(127, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Link Thư mục Google Drive:";
            // 
            // txtDriveUrl
            // 
            this.txtDriveUrl.Location = new System.Drawing.Point(19, 155);
            this.txtDriveUrl.Name = "txtDriveUrl";
            this.txtDriveUrl.Size = new System.Drawing.Size(406, 20);
            this.txtDriveUrl.TabIndex = 7;
            // 
            // btnSave
            // 
            this.btnSave.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnSave.Location = new System.Drawing.Point(292, 231);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(161, 36);
            this.btnSave.TabIndex = 8;
            this.btnSave.Text = "ĐĂNG NHẬP && LƯU";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.ForeColor = System.Drawing.Color.Navy;
            this.lblStatus.Location = new System.Drawing.Point(22, 243);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(107, 13);
            this.lblStatus.TabIndex = 9;
            this.lblStatus.Text = "Trạng thái: Sẵn sàng.";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.txtJsonPath);
            this.groupBox1.Controls.Add(this.btnBrowseJson);
            this.groupBox1.Controls.Add(this.txtDriveUrl);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.txtLocalPath);
            this.groupBox1.Controls.Add(this.btnBrowseFolder);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(441, 203);
            this.groupBox1.TabIndex = 10;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Cấu hình Backup";
            // 
            // frmSetup
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(465, 283);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.btnSave);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "frmSetup";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Svc BK - Cấu hình Hệ thống";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox txtJsonPath;
        private System.Windows.Forms.Button btnBrowseJson;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtLocalPath;
        private System.Windows.Forms.Button btnBrowseFolder;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtDriveUrl;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.GroupBox groupBox1;
    }
}