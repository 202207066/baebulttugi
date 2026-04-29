namespace test1
{
    partial class Menu_Upd
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
            label1 = new Label();
            textBox1 = new TextBox();
            button1 = new Button();
            checkedListBox1 = new CheckedListBox();
            button2 = new Button();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(66, 38);
            label1.Name = "label1";
            label1.Size = new Size(118, 32);
            label1.TabIndex = 0;
            label1.Text = "메뉴 수정";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(199, 31);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(411, 39);
            textBox1.TabIndex = 1;
            textBox1.TextChanged += textBox1_TextChanged;
            // 
            // button1
            // 
            button1.Location = new Point(638, 27);
            button1.Name = "button1";
            button1.Size = new Size(150, 46);
            button1.TabIndex = 2;
            button1.Text = "검색";
            button1.UseVisualStyleBackColor = true;
            // 
            // checkedListBox1
            // 
            checkedListBox1.FormattingEnabled = true;
            checkedListBox1.Location = new Point(66, 93);
            checkedListBox1.Name = "checkedListBox1";
            checkedListBox1.Size = new Size(722, 292);
            checkedListBox1.TabIndex = 3;
            // 
            // button2
            // 
            button2.Location = new Point(323, 392);
            button2.Name = "button2";
            button2.Size = new Size(150, 46);
            button2.TabIndex = 4;
            button2.Text = "수정";
            button2.UseVisualStyleBackColor = true;
            // 
            // Menu_Upd
            // 
            AutoScaleDimensions = new SizeF(14F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(button2);
            Controls.Add(checkedListBox1);
            Controls.Add(button1);
            Controls.Add(textBox1);
            Controls.Add(label1);
            Name = "Menu_Upd";
            Text = "Menu_Upd";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox textBox1;
        private Button button1;
        private CheckedListBox checkedListBox1;
        private Button button2;
    }
}