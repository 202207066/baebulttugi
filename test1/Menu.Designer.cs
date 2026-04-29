namespace test1
{
    partial class Menu
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
            Menu_Button_Upd = new Button();
            Menu_Button_Ins = new Button();
            label1 = new Label();
            SuspendLayout();
            // 
            // Menu_Button_Upd
            // 
            Menu_Button_Upd.Location = new Point(467, 158);
            Menu_Button_Upd.Name = "Menu_Button_Upd";
            Menu_Button_Upd.Size = new Size(231, 135);
            Menu_Button_Upd.TabIndex = 3;
            Menu_Button_Upd.Text = "메뉴 수정";
            Menu_Button_Upd.UseVisualStyleBackColor = true;
            Menu_Button_Upd.Click += Menu_Button_Upd_Click;
            // 
            // Menu_Button_Ins
            // 
            Menu_Button_Ins.Location = new Point(103, 158);
            Menu_Button_Ins.Name = "Menu_Button_Ins";
            Menu_Button_Ins.Size = new Size(231, 135);
            Menu_Button_Ins.TabIndex = 2;
            Menu_Button_Ins.Text = "메뉴 입력";
            Menu_Button_Ins.UseVisualStyleBackColor = true;
            Menu_Button_Ins.Click += Menu_Button_Ins_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(347, 69);
            label1.Name = "label1";
            label1.Size = new Size(86, 32);
            label1.TabIndex = 4;
            label1.Text = "메뉴창";
            // 
            // Menu
            // 
            AutoScaleDimensions = new SizeF(14F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(label1);
            Controls.Add(Menu_Button_Upd);
            Controls.Add(Menu_Button_Ins);
            Name = "Menu";
            Text = "Form2";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button Menu_Button_Upd;
        private Button Menu_Button_Ins;
        private Label label1;
    }
}