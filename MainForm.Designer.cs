using System.Windows.Forms;

namespace WinFormsCounter
{
    partial class MainForm : Form
    {
        private System.ComponentModel.IContainer components = null;
        private Button button;
        private Label labelCount;

        private void InitializeComponent()
        {
            this.button = new System.Windows.Forms.Button();
            this.labelCount = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // button
            // 
            this.button.Location = new System.Drawing.Point(12, 12);
            this.button.Name = "button";
            this.button.Size = new System.Drawing.Size(160, 40);
            this.button.TabIndex = 0;
            this.button.Text = "Press me";
            this.button.UseVisualStyleBackColor = true;
            this.button.Click += new System.EventHandler(this.button_Click);
            // 
            // labelCount
            // 
            this.labelCount.AutoSize = true;
            this.labelCount.Location = new System.Drawing.Point(12, 60);
            this.labelCount.Name = "labelCount";
            this.labelCount.Size = new System.Drawing.Size(172, 20);
            this.labelCount.TabIndex = 1;
            this.labelCount.Text = "Button pressed 0 times";
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(300, 100);
            this.Controls.Add(this.labelCount);
            this.Controls.Add(this.button);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "Counter";
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void button_Click(object? sender, EventArgs e)
        {
            // Event handler stub (designer referenced). No-op.
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}