namespace test1
{
    partial class test11
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
            dataGridView1 = new DataGridView();
            btnUpload = new Button();
            btnLoad = new Button();
            button3 = new Button();
            txtSearch = new TextBox();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new Point(11, 165);
            dataGridView1.Margin = new Padding(2, 1, 2, 1);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersWidth = 82;
            dataGridView1.Size = new Size(885, 324);
            dataGridView1.TabIndex = 0;
            // 
            // btnUpload
            // 
            btnUpload.Location = new Point(62, 34);
            btnUpload.Name = "btnUpload";
            btnUpload.Size = new Size(162, 29);
            btnUpload.TabIndex = 1;
            btnUpload.Text = "CSV 업로드";
            btnUpload.UseVisualStyleBackColor = true;
            btnUpload.Click += btnUpload_Click;
            // 
            // btnLoad
            // 
            btnLoad.Location = new Point(310, 34);
            btnLoad.Name = "btnLoad";
            btnLoad.Size = new Size(178, 29);
            btnLoad.TabIndex = 2;
            btnLoad.Text = "시트 불러오기";
            btnLoad.UseVisualStyleBackColor = true;
            btnLoad.Click += btnLoad_Click;
            // 
            // button3
            // 
            button3.Location = new Point(62, 100);
            button3.Name = "button3";
            button3.Size = new Size(162, 26);
            button3.TabIndex = 3;
            button3.Text = "button3";
            button3.UseVisualStyleBackColor = true;
            // 
            // txtSearch
            // 
            txtSearch.Location = new Point(506, 100);
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new Size(378, 23);
            txtSearch.TabIndex = 4;
            //txtSearch.TextChanged += txtSearch_TextChanged_1;
            // 
            // test11
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(907, 499);
            Controls.Add(txtSearch);
            Controls.Add(button3);
            Controls.Add(btnLoad);
            Controls.Add(btnUpload);
            Controls.Add(dataGridView1);
            Margin = new Padding(2, 1, 2, 1);
            Name = "test11";
            Text = "test11";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private DataGridView dataGridView1;
        private Button btnUpload;
        private Button btnLoad;
        private Button button3;
        private TextBox txtSearch;
    }
}