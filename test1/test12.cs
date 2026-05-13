using System;
using System.Data;
using System.IO;
using System.Text; // Encoding 사용을 위해 필요
using System.Windows.Forms;

namespace test1
{
    public partial class test12 : Form
    {
        public test12()
        {
            InitializeComponent();

            // [중요!] 최신 .NET에서 한글 인코딩(ANSI/EUC-KR)을 사용 가능하게 등록합니다.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            this.Load += (s, e) => LoadCsvData();
        }

        private void LoadCsvData()
        {
            string filePath = @"C:\Users\sol\Desktop\ttt\food.csv";

            if (!File.Exists(filePath))
            {
                MessageBox.Show("파일을 찾을 수 없습니다.");
                return;
            }

            try
            {
                DataTable dt = new DataTable();

                // 파일을 읽을 때 euc-kr 방식을 사용합니다.
                using (StreamReader sr = new StreamReader(filePath, Encoding.GetEncoding("euc-kr")))
                {
                    string firstLine = sr.ReadLine();
                    if (firstLine != null)
                    {
                        string[] headers = firstLine.Split(',');
                        for (int i = 0; i < headers.Length; i++)
                        {
                            string colName = headers[i].Trim();
                            if (string.IsNullOrEmpty(colName) || dt.Columns.Contains(colName))
                                colName = $"열_{i + 1}";
                            dt.Columns.Add(colName);
                        }

                        while (!sr.EndOfStream)
                        {
                            string line = sr.ReadLine();
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            string[] rows = line.Split(',');

                            if (rows.Length != dt.Columns.Count)
                                Array.Resize(ref rows, dt.Columns.Count);

                            dt.Rows.Add(rows);
                        }
                    }
                }

                dataGridView1.DataSource = dt;
                dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            }
            catch (Exception ex)
            {
                // 여기에서 에러가 발생하면 메시지를 띄웁니다.
                MessageBox.Show("오류가 발생했습니다: " + ex.Message);
            }
        }
    }
}