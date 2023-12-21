namespace IpCam
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pictureBox1 = new PictureBox();
            button_ON = new Button();
            button_OFF = new Button();
            panel1 = new Panel();
            panel2 = new Panel();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            panel1.SuspendLayout();
            panel2.SuspendLayout();
            SuspendLayout();
            // 
            // pictureBox1
            // 
            pictureBox1.BorderStyle = BorderStyle.FixedSingle;
            pictureBox1.Dock = DockStyle.Fill;
            pictureBox1.Location = new Point(0, 0);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(600, 450);
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // button_ON
            // 
            button_ON.Location = new Point(64, 97);
            button_ON.Name = "button_ON";
            button_ON.Size = new Size(75, 23);
            button_ON.TabIndex = 1;
            button_ON.Text = "ON";
            button_ON.UseVisualStyleBackColor = true;
            button_ON.Click += button1_Click;
            // 
            // button_OFF
            // 
            button_OFF.Location = new Point(73, 243);
            button_OFF.Name = "button_OFF";
            button_OFF.Size = new Size(75, 23);
            button_OFF.TabIndex = 2;
            button_OFF.Text = "OFF";
            button_OFF.UseVisualStyleBackColor = true;
            button_OFF.Click += button2_Click;
            // 
            // panel1
            // 
            panel1.Controls.Add(button_ON);
            panel1.Controls.Add(button_OFF);
            panel1.Dock = DockStyle.Right;
            panel1.Location = new Point(600, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(200, 450);
            panel1.TabIndex = 3;
            // 
            // panel2
            // 
            panel2.Controls.Add(pictureBox1);
            panel2.Dock = DockStyle.Fill;
            panel2.Location = new Point(0, 0);
            panel2.Name = "panel2";
            panel2.Size = new Size(600, 450);
            panel2.TabIndex = 4;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(panel2);
            Controls.Add(panel1);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            panel1.ResumeLayout(false);
            panel2.ResumeLayout(false);
            ResumeLayout(false);
        }

        #endregion

        private PictureBox pictureBox1;
        private Button button_ON;
        private Button button_OFF;
        private Panel panel1;
        private Panel panel2;
    }
}