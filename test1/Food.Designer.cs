namespace test1
{
    partial class Food
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
            Food_Button_Upd = new Button();
            Food_Button_Ins = new Button();
            label1 = new Label();
            SuspendLayout();
            // 
            // Food_Button_Upd
            // 
            Food_Button_Upd.Location = new Point(467, 158);
            Food_Button_Upd.Name = "Food_Button_Upd";
            Food_Button_Upd.Size = new Size(231, 135);
            Food_Button_Upd.TabIndex = 5;
            Food_Button_Upd.Text = "식재료 수정";
            Food_Button_Upd.UseVisualStyleBackColor = true;
            Food_Button_Upd.Click += Food_Button_Upd_Click;
            // 
            // Food_Button_Ins
            // 
            Food_Button_Ins.Location = new Point(103, 158);
            Food_Button_Ins.Name = "Food_Button_Ins";
            Food_Button_Ins.Size = new Size(231, 135);
            Food_Button_Ins.TabIndex = 4;
            Food_Button_Ins.Text = "식재료 입력";
            Food_Button_Ins.UseVisualStyleBackColor = true;
            Food_Button_Ins.Click += Food_Button_Ins_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(348, 76);
            label1.Name = "label1";
            label1.Size = new Size(110, 32);
            label1.TabIndex = 6;
            label1.Text = "식재료창";
            // 
            // Food
            // 
            AutoScaleDimensions = new SizeF(14F, 32F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(label1);
            Controls.Add(Food_Button_Upd);
            Controls.Add(Food_Button_Ins);
            Name = "Food";
            Text = "Food";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Button Food_Button_Upd;
        private Button Food_Button_Ins;
        private Label label1;
    }
}