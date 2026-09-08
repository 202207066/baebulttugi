namespace test1
{
    partial class Food_Ins
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
            components = new System.ComponentModel.Container();
            contextMenuStrip1 = new ContextMenuStrip(components);
            comboBox1 = new ComboBox();
            label1 = new Label();
            label2 = new Label();
            label3 = new Label();
            label4 = new Label();
            label5 = new Label();
            label6 = new Label();
            textBox1 = new TextBox();
            textBox2 = new TextBox();
            textBox3 = new TextBox();
            textBox4 = new TextBox();
            textBox5 = new TextBox();
            button1 = new Button();
            SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            contextMenuStrip1.ImageScalingSize = new Size(32, 32);
            contextMenuStrip1.Name = "contextMenuStrip1";
            contextMenuStrip1.Size = new Size(61, 4);
            // 
            // comboBox1
            // 
            comboBox1.FormattingEnabled = true;
            comboBox1.Items.AddRange(new object[] { "가금류 ", "갑각류" });
            comboBox1.Location = new Point(253, 389);
            comboBox1.Name = "comboBox1";
            comboBox1.Size = new Size(242, 40);
            comboBox1.TabIndex = 1;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(142, 32);
            label1.TabIndex = 2;
            label1.Text = "식재료 이름";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(12, 74);
            label2.Name = "label2";
            label2.Size = new Size(62, 32);
            label2.TabIndex = 3;
            label2.Text = "단가";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(12, 152);
            label3.Name = "label3";
            label3.Size = new Size(110, 32);
            label3.TabIndex = 4;
            label3.Text = "탄수화물";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(12, 230);
            label4.Name = "label4";
            label4.Size = new Size(86, 32);
            label4.TabIndex = 5;
            label4.Text = "단백질";
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(12, 314);
            label5.Name = "label5";
            label5.Size = new Size(62, 32);
            label5.TabIndex = 6;
            label5.Text = "지방";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(12, 389);
            label6.Name = "label6";
            label6.Size = new Size(142, 32);
            label6.TabIndex = 7;
            label6.Text = "알러지 여부";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(253, 9);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(242, 39);
            textBox1.TabIndex = 8;
            // 
            // textBox2
            // 
            textBox2.Location = new Point(253, 74);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(242, 39);
            textBox2.TabIndex = 9;
            // 
            // textBox3
            // 
            textBox3.Location = new Point(253, 145);
            textBox3.Name = "textBox3";
            textBox3.Size = new Size(242, 39);
            textBox3.TabIndex = 10;
            // 
            // textBox4
            // 
            textBox4.Location = new Point(253, 223);
            textBox4.Name = "textBox4";
            textBox4.Size = new Size(242, 39);
            textBox4.TabIndex = 11;
            // 
            // textBox5
            // 
            textBox5.Location = new Point(253, 314);
            textBox5.Name = "textBox5";
            textBox5.Size = new Size(242, 39);
            textBox5.TabIndex = 12;
            // 
            // button1
            // 
            button1.Location = new Point(599, 216);
            button1.Name = "button1";
            button1.Size = new Size(150, 46);
            button1.TabIndex = 13;
            button1.Text = "보내기";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // Food_Ins
            // 
            AutoScaleDimensions = new SizeF(14F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1416, 960);
            Controls.Add(button1);
            Controls.Add(textBox5);
            Controls.Add(textBox4);
            Controls.Add(textBox3);
            Controls.Add(textBox2);
            Controls.Add(textBox1);
            Controls.Add(label6);
            Controls.Add(label5);
            Controls.Add(label4);
            Controls.Add(label3);
            Controls.Add(label2);
            Controls.Add(label1);
            Controls.Add(comboBox1);
            Name = "Food_Ins";
            Text = "Food_Ins";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private ContextMenuStrip contextMenuStrip1;
        private ComboBox comboBox1;
        private Label label1;
        private Label label2;
        private Label label3;
        private Label label4;
        private Label label5;
        private Label label6;
        private TextBox textBox1;
        private TextBox textBox2;
        private TextBox textBox3;
        private TextBox textBox4;
        private TextBox textBox5;
        private Button button1;
    }
}