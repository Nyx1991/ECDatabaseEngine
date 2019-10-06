namespace WinFormsTest
{
    partial class Form1
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.personGrid = new System.Windows.Forms.DataGridView();
            this.txtStreet = new System.Windows.Forms.TextBox();
            this.txtCity = new System.Windows.Forms.TextBox();
            this.txtName = new System.Windows.Forms.TextBox();
            this.txtFirstname = new System.Windows.Forms.TextBox();
            this.txtRefAddr = new System.Windows.Forms.TextBox();
            this.txtRecIdAddress = new System.Windows.Forms.TextBox();
            this.addressGrid = new System.Windows.Forms.DataGridView();
            ((System.ComponentModel.ISupportInitialize)(this.personGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.addressGrid)).BeginInit();
            this.SuspendLayout();
            // 
            // personGrid
            // 
            this.personGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.personGrid.Location = new System.Drawing.Point(12, 12);
            this.personGrid.Name = "personGrid";
            this.personGrid.Size = new System.Drawing.Size(292, 164);
            this.personGrid.TabIndex = 0;
            // 
            // txtStreet
            // 
            this.txtStreet.Location = new System.Drawing.Point(407, 202);
            this.txtStreet.Name = "txtStreet";
            this.txtStreet.Size = new System.Drawing.Size(216, 20);
            this.txtStreet.TabIndex = 2;
            // 
            // txtCity
            // 
            this.txtCity.Location = new System.Drawing.Point(407, 228);
            this.txtCity.Name = "txtCity";
            this.txtCity.Size = new System.Drawing.Size(216, 20);
            this.txtCity.TabIndex = 3;
            // 
            // txtName
            // 
            this.txtName.Location = new System.Drawing.Point(12, 228);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(216, 20);
            this.txtName.TabIndex = 5;
            // 
            // txtFirstname
            // 
            this.txtFirstname.Location = new System.Drawing.Point(12, 202);
            this.txtFirstname.Name = "txtFirstname";
            this.txtFirstname.Size = new System.Drawing.Size(216, 20);
            this.txtFirstname.TabIndex = 4;
            // 
            // txtRefAddr
            // 
            this.txtRefAddr.Location = new System.Drawing.Point(12, 254);
            this.txtRefAddr.Name = "txtRefAddr";
            this.txtRefAddr.Size = new System.Drawing.Size(216, 20);
            this.txtRefAddr.TabIndex = 6;
            // 
            // txtRecIdAddress
            // 
            this.txtRecIdAddress.Location = new System.Drawing.Point(407, 254);
            this.txtRecIdAddress.Name = "txtRecIdAddress";
            this.txtRecIdAddress.Size = new System.Drawing.Size(216, 20);
            this.txtRecIdAddress.TabIndex = 7;
            // 
            // addressGrid
            // 
            this.addressGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.addressGrid.Location = new System.Drawing.Point(331, 12);
            this.addressGrid.Name = "addressGrid";
            this.addressGrid.Size = new System.Drawing.Size(292, 164);
            this.addressGrid.TabIndex = 8;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(637, 289);
            this.Controls.Add(this.addressGrid);
            this.Controls.Add(this.txtRecIdAddress);
            this.Controls.Add(this.txtRefAddr);
            this.Controls.Add(this.txtName);
            this.Controls.Add(this.txtFirstname);
            this.Controls.Add(this.txtCity);
            this.Controls.Add(this.txtStreet);
            this.Controls.Add(this.personGrid);
            this.Name = "Form1";
            this.Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)(this.personGrid)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.addressGrid)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView personGrid;
        private System.Windows.Forms.TextBox txtStreet;
        private System.Windows.Forms.TextBox txtCity;
        private System.Windows.Forms.TextBox txtName;
        private System.Windows.Forms.TextBox txtFirstname;
        private System.Windows.Forms.TextBox txtRefAddr;
        private System.Windows.Forms.TextBox txtRecIdAddress;
        private System.Windows.Forms.DataGridView addressGrid;
    }
}

