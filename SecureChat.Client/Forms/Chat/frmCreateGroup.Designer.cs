namespace SecureChat.Client.Forms.Chat
{
    partial class frmCreateGroup
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // _pbAvatar?.Image?.Dispose();
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Toàn bộ UI được xây dựng bằng code trong frmCreateGroup.cs.
        /// Designer này chỉ thiết lập thuộc tính cơ bản của Form.
        /// </summary>
        private void InitializeComponent()
        {
            this.SuspendLayout();
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(432, 540);
            this.Name = "frmCreateGroup";
            this.Text = "New Group";
            this.ResumeLayout(false);
        }
    }
}