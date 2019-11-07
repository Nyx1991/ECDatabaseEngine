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
            this.components = new System.ComponentModel.Container();
            this.personGrid = new System.Windows.Forms.DataGridView();
            this.txtStreet = new System.Windows.Forms.TextBox();
            this.txtCity = new System.Windows.Forms.TextBox();
            this.txtName = new System.Windows.Forms.TextBox();
            this.txtFirstname = new System.Windows.Forms.TextBox();
            this.txtRefAddr = new System.Windows.Forms.TextBox();
            this.txtRecIdAddress = new System.Windows.Forms.TextBox();
            this.addressGrid = new System.Windows.Forms.DataGridView();
            this.personBindingSource = new System.Windows.Forms.BindingSource(this.components);
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.personGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.addressGrid)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.personBindingSource)).BeginInit();
            this.SuspendLayout();
            // 
            // personGrid
            // 
            this.personGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.personGrid.Location = new System.Drawing.Point(12, 12);
            this.personGrid.Name = "personGrid";
            this.personGrid.Size = new System.Drawing.Size(613, 164);
            this.personGrid.TabIndex = 0;
            // 
            // txtStreet
            // 
            this.txtStreet.Location = new System.Drawing.Point(12, 445);
            this.txtStreet.Name = "txtStreet";
            this.txtStreet.Size = new System.Drawing.Size(216, 20);
            this.txtStreet.TabIndex = 2;
            // 
            // txtCity
            // 
            this.txtCity.Location = new System.Drawing.Point(12, 471);
            this.txtCity.Name = "txtCity";
            this.txtCity.Size = new System.Drawing.Size(216, 20);
            this.txtCity.TabIndex = 3;
            // 
            // txtName
            // 
            this.txtName.Location = new System.Drawing.Point(12, 208);
            this.txtName.Name = "txtName";
            this.txtName.Size = new System.Drawing.Size(216, 20);
            this.txtName.TabIndex = 5;
            // 
            // txtFirstname
            // 
            this.txtFirstname.Location = new System.Drawing.Point(12, 182);
            this.txtFirstname.Name = "txtFirstname";
            this.txtFirstname.Size = new System.Drawing.Size(216, 20);
            this.txtFirstname.TabIndex = 4;
            // 
            // txtRefAddr
            // 
            this.txtRefAddr.Location = new System.Drawing.Point(12, 234);
            this.txtRefAddr.Name = "txtRefAddr";
            this.txtRefAddr.Size = new System.Drawing.Size(216, 20);
            this.txtRefAddr.TabIndex = 6;
            // 
            // txtRecIdAddress
            // 
            this.txtRecIdAddress.Location = new System.Drawing.Point(12, 497);
            this.txtRecIdAddress.Name = "txtRecIdAddress";
            this.txtRecIdAddress.Size = new System.Drawing.Size(216, 20);
            this.txtRecIdAddress.TabIndex = 7;
            // 
            // addressGrid
            // 
            this.addressGrid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.addressGrid.Location = new System.Drawing.Point(12, 275);
            this.addressGrid.Name = "addressGrid";
            this.addressGrid.Size = new System.Drawing.Size(613, 164);
            this.addressGrid.TabIndex = 8;
            // 
            // personBindingSource
            // 
            this.personBindingSource.DataSource = typeof(WinFormsTest.Person);
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(315, 179);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 9;
            this.button1.Text = "Refresh";
            this.button1.UseVisualStyleBackColor = true;
            this.button1.Click += new System.EventHandler(this.button1_Click);
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(234, 179);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 10;
            this.button2.Text = "Save";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.button2_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(637, 526);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.button1);
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
            ((System.ComponentModel.ISupportInitialize)(this.personBindingSource)).EndInit();
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
        private System.Windows.Forms.BindingSource personBindingSource;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
    }
}

