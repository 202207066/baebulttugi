namespace test1
{
    partial class Menu_Ins
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
            checkedListBox1 = new CheckedListBox();
            button1 = new Button();
            SuspendLayout();
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(81, 41);
            label1.Name = "label1";
            label1.Size = new Size(110, 32);
            label1.TabIndex = 0;
            label1.Text = "메뉴입력";
            // 
            // textBox1
            // 
            textBox1.Location = new Point(216, 34);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(389, 39);
            textBox1.TabIndex = 1;
            // 
            // checkedListBox1
            // 
            checkedListBox1.FormattingEnabled = true;
            checkedListBox1.Location = new Point(81, 104);
            checkedListBox1.Name = "checkedListBox1";
            checkedListBox1.Size = new Size(684, 328);
            checkedListBox1.TabIndex = 2;
            // 
            // button1
            // 
            button1.Location = new Point(615, 30);
            button1.Name = "button1";
            button1.Size = new Size(150, 46);
            button1.TabIndex = 3;
            button1.Text = "입력";
            button1.UseVisualStyleBackColor = true;
            // 
            // Menu_Ins
            // 
            AutoScaleDimensions = new SizeF(14F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(button1);
            Controls.Add(checkedListBox1);
            Controls.Add(textBox1);
            Controls.Add(label1);
            Name = "Menu_Ins";
            Text = "Menu_Ins";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label label1;
        private TextBox textBox1;
        private CheckedListBox checkedListBox1;
        private Button button1;
    }
}