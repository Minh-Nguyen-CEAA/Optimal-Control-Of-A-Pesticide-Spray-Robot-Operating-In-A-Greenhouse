using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.IO;
namespace Agricultural_Robot
{
    public partial class Form1 : Form
    {
        // CHỈ KHAI BÁO CÁC BIẾN LOGIC - KHÔNG KHAI BÁO LẠI CÁC CONTROL TỪ DESIGN
        private List<float[]> dataLog = new List<float[]>();
        private List<byte> rxBuffer = new List<byte>();
        private string _lineBuffer = "";
        private bool isRunning = false;
        private bool isCalibDone = false;
        private int frameCount = 0;

        private const int N_FLOAT = 31;
        private const int FRAME_SZ = 2 + N_FLOAT * 4;
        private const byte MAGIC1 = 0xAB, MAGIC2 = 0xCD;
        private const byte CMD_START = 0x01, CMD_STOP = 0x02, CMD_CALIB = 0x03;
        private const int MAX_PTS = 300;

        // Index signals
        private const int IDX_X = 0, IDX_Y = 1, IDX_THETA = 2, IDX_VC = 3, IDX_WC = 4;
        private const int IDX_V = 5, IDX_W = 6, IDX_XD = 7, IDX_YD = 8, IDX_THETAD = 9;
        private const int IDX_VD = 10, IDX_WD = 11, IDX_TAUC_R = 12, IDX_TAUC_L = 13;
        private const int IDX_TAU_R = 14, IDX_TAU_L = 15, IDX_UHAT_R = 16, IDX_UHAT_L = 17;


        private void rtbLog_TextChanged(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void chart_yd_Click(object sender, EventArgs e)
        {

        }

        private void Graph1_Click(object sender, EventArgs e)
        {

        }

        private void btnConn_Click(object sender, EventArgs e)
        {
            if (serialPort.IsOpen)
            {
                SendCmd(CMD_STOP);
                serialPort.Close();
                Status.Text = "DISCONNECTED";
                Status.ForeColor = Color.Gray;
                btnCalib.Enabled = false;   // Giữ disabled vĩnh viễn
                btnStart.Enabled = false;
                isCalibDone = false;
                return;
            }
            try
            {
                serialPort.PortName = cbPort.Text;
                serialPort.BaudRate = 115200;
                serialPort.Open();
                btnCalib.Enabled = false;   // Không bao giờ enable nữa
                btnStart.Enabled = false;
                isCalibDone = false;
                Status.Text = "CONNECTED - Doi STM32 calib Gyro...";
                Status.ForeColor = Color.Cyan;
            }
            catch (Exception ex) { MessageBox.Show("Loi COM: " + ex.Message); }
        }
        private void btnCalib_Click(object sender, EventArgs e)
        {
            //SendCmd(CMD_CALIB);
            //btnCalib.Enabled = false;   /* Khong cho nhan 2 lan */
            //Status.Text = "XOAY ROBOT 360 DO TRONG 30s...";
            //Status.ForeColor = Color.Orange;
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            if (!isCalibDone) { MessageBox.Show("Chua nhan CALIB_DONE tu STM32!"); return; }

            dataLog.Clear(); // <-- THÊM DÒNG NÀY ĐỂ XÓA DATA CŨ
            frameCount = 0;  // Reset luôn frame đếm

            SendCmd(CMD_START);
            isRunning = true;
            Status.Text = "RUNNING";
            Status.ForeColor = Color.LimeGreen;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            SendCmd(CMD_STOP);
            isRunning = false;
            Status.Text = "STOPPED";
            Status.ForeColor = Color.Tomato;
        }

        private void btnSaveLog_Click(object sender, EventArgs e)
        {
            if (isRunning)
            {
                MessageBox.Show("STOP robot trước khi lưu log!");
                return;
            }
            SaveLogToCSV();
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            frameCount = 0;
            // Gom tất cả chart vào mảng để xóa
            Chart[] allCharts = { chart_xy, chart_xd, chart_yd, chart_v_vd, chart_w_wd, chart_v_vc, chart_w_wc, chart_vc_vd, chart_wc_wd, chart_errors123, chart_errors45, chart_tauc };
            foreach (var c in allCharts)
                if (c != null) foreach (var s in c.Series) s.Points.Clear();
        }
        private const int IDX_E1 = 18, IDX_E2 = 19, IDX_E3 = 20, IDX_E4 = 21, IDX_E5 = 22;
        private const int IDX_EVD_V = 23, IDX_EWD_W = 24, IDX_EVC_VD = 25, IDX_EWC_WD = 26;
        private const int IDX_B_OMEGA = 27, IDX_OMEGA_RAW = 28;
        private const int IDX_P_THETA = 29, IDX_P_B_OMEGA = 30;

        public Form1()
        {
            // Lấy kích thước bạn muốn (Ví dụ 1200x750, hãy thay bằng số bạn thích)
            Size fixSize = new Size(1519, 946);

            this.Size = fixSize;
            this.MinimumSize = fixSize; // Ép không cho nhỏ hơn
                                        // this.MaximumSize = fixSize; // Nếu không muốn người dùng phóng to thì mở dòng này

            this.StartPosition = FormStartPosition.CenterScreen;
            InitializeComponent();
            string[] ports = SerialPort.GetPortNames();
            cbPort.Items.AddRange(ports);
            if (cbPort.Items.Count > 0) cbPort.SelectedIndex = 0;
            if (serialPort != null)
                serialPort.DataReceived += OnDataReceived;
            SetupChartProperties();

            /* THEM 4 DONG NAY VAO DAY — trong constructor */
            rtbLog.WordWrap = false;
            rtbLog.Font = new Font("Consolas", 9);
            rtbLog.BackColor = Color.Black;
            rtbLog.ForeColor = Color.LimeGreen;
        }

        private void SetupChartProperties()
        {
            // Danh sách đầy đủ 13 chart
            Chart[] allCharts = {
        chart_xy, chart_xd, chart_yd, chart_thetad,
        chart_errors123, chart_errors45, chart_tauc,chart_tau,chart_uhat,
        chart_v_vd, chart_w_wd, chart_vc_vd, chart_wc_wd,
        chart_v_vc, chart_w_wc
        };

            foreach (var chart in allCharts)
            {
                if (chart != null)
                {
                    // 1. Cấu hình trục OY tự động scale âm/dương cực mượt
                    var area = chart.ChartAreas[0];
                    area.AxisY.IsStartedFromZero = false;
                    area.AxisY.Minimum = double.NaN;
                    area.AxisY.Maximum = double.NaN;
                    area.AxisY.Crossing = 0; // Đường kẻ đậm ở mốc số 0
                    area.AxisY.MajorGrid.LineColor = Color.LightGray;

                    // 2. Gán sự kiện Click để lưu hình ảnh khổ lớn (chỉ khi Robot Stop)
                    chart.Cursor = Cursors.Hand;
                    chart.Click += (sender, e) => SaveChartImage((Chart)sender);
                }
            }
        }


        private void SaveChartImage(Chart targetChart)
        {
            if (isRunning) { MessageBox.Show("Stop robot truoc!"); return; }

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "PNG Image|*.png";
                sfd.FileName = targetChart.Name + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                if (sfd.ShowDialog() != DialogResult.OK) return;

                Size origSize = targetChart.Size;

                /* Tinh ty le phong to */
                float scaleX = 1920f / origSize.Width;
                float scaleY = 1080f / origSize.Height;
                float scale = Math.Min(scaleX, scaleY);  /* Lay ty le nho hon */

                /* Scale font truoc */
                foreach (var area in targetChart.ChartAreas)
                {
                    area.AxisX.LabelStyle.Font = new Font("Arial", 10 * scale);
                    area.AxisY.LabelStyle.Font = new Font("Arial", 10 * scale);
                    area.AxisX.TitleFont = new Font("Arial", 11 * scale, FontStyle.Bold);
                    area.AxisY.TitleFont = new Font("Arial", 11 * scale, FontStyle.Bold);
                }
                if (targetChart.Titles.Count > 0)
                    targetChart.Titles[0].Font = new Font("Arial", 12 * scale, FontStyle.Bold);
                if (targetChart.Legends.Count > 0)
                    targetChart.Legends[0].Font = new Font("Arial", 9 * scale);

                targetChart.Size = new Size(1920, 1080);
                targetChart.Update();

                using (Bitmap bmp = new Bitmap(1920, 1080))
                {
                    targetChart.DrawToBitmap(bmp, new Rectangle(0, 0, 1920, 1080));
                    bmp.Save(sfd.FileName, System.Drawing.Imaging.ImageFormat.Png);
                }

                /* Khoi phuc tat ca ve goc */
                targetChart.Size = origSize;
                foreach (var area in targetChart.ChartAreas)
                {
                    area.AxisX.LabelStyle.Font = new Font("Arial", 8);
                    area.AxisY.LabelStyle.Font = new Font("Arial", 8);
                    area.AxisX.TitleFont = new Font("Arial", 9, FontStyle.Bold);
                    area.AxisY.TitleFont = new Font("Arial", 9, FontStyle.Bold);
                }
                if (targetChart.Titles.Count > 0)
                    targetChart.Titles[0].Font = new Font("Arial", 10, FontStyle.Bold);
                if (targetChart.Legends.Count > 0)
                    targetChart.Legends[0].Font = new Font("Arial", 8);
                targetChart.Update();

                MessageBox.Show("Da luu: " + sfd.FileName);
            }
        }

        // Các hàm sự kiện Click (Đảm bảo tên hàm khớp với tên trong Properties > Events)

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (serialPort.BytesToRead <= 0) return;
            byte[] b = new byte[serialPort.BytesToRead];
            serialPort.Read(b, 0, b.Length);
            lock (rxBuffer) { rxBuffer.AddRange(b); }

            /* Gop tung byte vao line buffer, chi hien khi gap \n */
            foreach (byte byt in b)
            {
                if (byt == '\n')
                {
                    string line = _lineBuffer.TrimEnd('\r');
                    int clean = 0;
                    foreach (char c in line)
                        if (c >= 32 && c <= 126) clean++;
                    float ratio = line.Length > 0 ? (float)clean / line.Length : 0f;

                    if (ratio > 0.85f && line.Length > 2)
                    {
                        string fl = line + "\n";
                        BeginInvoke((MethodInvoker)(() => {
                            rtbLog.AppendText(fl);
                            rtbLog.ScrollToCaret();
                        }));

                        /* DETECT TRANG THAI TRONG LINE HOAN CHINH */
                        if (line.Contains("ICM OK"))
                            BeginInvoke((MethodInvoker)(() => {
                                Status.Text = "ICM OK - Dang calib Gyro (2s)...";
                                Status.ForeColor = Color.Cyan;
                            }));
                        //if (line.Contains("[3] Nhan CALIB"))
                        //    BeginInvoke((MethodInvoker)(() => {
                        //        btnCalib.Enabled = true;
                        //        Status.Text = "Nhan CALIB MAG!";
                        //        Status.ForeColor = Color.Yellow;
                        //    }));
                        //if (line.Contains("GO!"))
                        //    BeginInvoke((MethodInvoker)(() => {
                        //        Status.Text = "XOAY 360 DO...";
                        //        Status.ForeColor = Color.Orange;
                        //    }));
                             if (line.Contains("CALIB_DONE"))
                            BeginInvoke((MethodInvoker)(() => {
                                isCalibDone = true;
                                btnStart.Enabled = true;
                                Status.Text = "Gyro calib xong → Nhan START";
                                Status.ForeColor = Color.LimeGreen;
                            }));
                        if (line.Contains("START:"))
                            BeginInvoke((MethodInvoker)(() => {
                                Status.Text = "RUNNING - " + line;
                                Status.ForeColor = Color.LimeGreen;
                            }));
                    }
                    _lineBuffer = "";
                }
                else if (byt >= 32 && byt <= 126)
                {
                    _lineBuffer += (char)byt;
                    if (_lineBuffer.Length > 200) _lineBuffer = "";
                }
                else if (byt != '\r')
                {
                    _lineBuffer = "";
                }
            }

            /* XOA TOAN BO DOAN detect raw ben duoi — khong can nua */
            /* if (raw.Contains("ICM OK")) ... XOA HET */

            ParseFrames();
        }

        private void ParseFrames()
        {
            lock (rxBuffer)
            {
                while (rxBuffer.Count >= FRAME_SZ)
                {
                    int idx = -1;
                    for (int i = 0; i <= rxBuffer.Count - FRAME_SZ; i++)
                        if (rxBuffer[i] == MAGIC1 && rxBuffer[i + 1] == MAGIC2) { idx = i; break; }

                    if (idx < 0)
                    {
                        if (rxBuffer.Count > 1) rxBuffer.RemoveRange(0, rxBuffer.Count - 1);
                        break;
                    }
                    if (idx > 0) { rxBuffer.RemoveRange(0, idx); continue; }

                    byte[] f = rxBuffer.GetRange(0, FRAME_SZ).ToArray();
                    rxBuffer.RemoveRange(0, FRAME_SZ);

                    float[] v = new float[N_FLOAT];
                    for (int i = 0; i < N_FLOAT; i++)
                        v[i] = BitConverter.ToSingle(f, 2 + i * 4);

                    if (!IsDisposed && IsHandleCreated)
                        this.BeginInvoke((MethodInvoker)(() => UpdateUI(v)));
                }
            }
        }
        private bool IsValidFrame(float[] v)
        {
            foreach (float f in v)
            {
                if (float.IsNaN(f) || float.IsInfinity(f)) return false;
            }
            /* Kiem tra cac gia tri co ly: */
            if (Math.Abs(v[IDX_XD]) > 4f) return false;  /* xd khong qua 4m */
            if (Math.Abs(v[IDX_YD]) > 4f) return false;  /* yd khong qua 4m */
            if (Math.Abs(v[IDX_VD]) > 5f) return false;  /* vd khong qua 5m/s */
            if (Math.Abs(v[IDX_WD]) > 5f) return false;  /* wd khong qua 5 rad/s */
            if (Math.Abs(v[IDX_VC]) > 5f) return false;  /* vc khong qua 5m/s */
            if (Math.Abs(v[IDX_WC]) > 5f) return false;  /* wc khong qua 5 rad/s */
            if (Math.Abs(v[IDX_X]) > 4f) return false;  /* x khong qua 4m */
            if (Math.Abs(v[IDX_Y]) > 4f) return false;  /* y khong qua 4m */
            if (Math.Abs(v[IDX_V]) > 5f) return false;  /* v khong qua 5m/s */
            if (Math.Abs(v[IDX_W]) > 5f) return false;  /* w khong qua 5 rad/s */
            if (Math.Abs(v[IDX_THETA]) > 7f) return false; /* theta -pi..pi */
            if (Math.Abs(v[IDX_TAU_R]) > 50f) return false;  /* tauR khong qua 50 */
            if (Math.Abs(v[IDX_TAU_L]) > 50f) return false;  /* tauL khong qua 50 */
            if (Math.Abs(v[IDX_TAUC_R]) > 50f) return false;  /* tauC_R khong qua 50 */
            if (Math.Abs(v[IDX_TAUC_L]) > 50f) return false;  /* tauC_L khong qua 5m/s */
            if (Math.Abs(v[IDX_UHAT_R]) > 50f) return false;  /* uhat_R khong qua 50 */
            if (Math.Abs(v[IDX_UHAT_L]) > 50f) return false;  /* uhat_L khong qua 5m/s */
            return true;
        }
        private void UpdateUI(float[] v)
        {
            try
            {
                if (!IsValidFrame(v)) return;
                // ===== TEXTBOX (REALTIME OK) =====
                xd_textbox.Text = v[IDX_XD].ToString("F3");
                yd_textbox.Text = v[IDX_YD].ToString("F3");
                x_textbox.Text = v[IDX_X].ToString("F3");
                y_textbox.Text = v[IDX_Y].ToString("F3");
                thetad_textbox.Text = v[IDX_THETAD].ToString("F3");
                theta_textbox.Text = v[IDX_THETA].ToString("F3");
                vd_textbox.Text = v[IDX_VD].ToString("F3");
                wd_textbox.Text = v[IDX_WD].ToString("F3");
                v_textbox.Text = v[IDX_V].ToString("F3");
                w_textbox.Text = v[IDX_W].ToString("F3");

                // ===== LƯU DATA =====
                dataLog.Add((float[])v.Clone());

                // tránh tràn RAM
                if (dataLog.Count > 50000)
                    dataLog.RemoveAt(0);
            }
            catch { }
        }

        private void TrimCharts(int max)
        {
            Chart[] allCharts = { chart_xy, chart_xd, chart_yd, chart_v_vd, chart_w_wd, chart_v_vc, chart_w_wc, chart_vc_vd, chart_wc_wd, chart_errors123, chart_errors45, chart_tauc };
            foreach (var c in allCharts)
            {
                if (c == null) continue;
                foreach (var s in c.Series)
                    while (s.Points.Count > max) s.Points.RemoveAt(0);
            }
        }

        private void SendCmd(byte cmd)
        {
            if (serialPort != null && serialPort.IsOpen)
                serialPort.Write(new byte[] { 0xAA, 0x55, cmd, (byte)(~cmd) }, 0, 4);
        }
        private void btnDraw_Click(object sender, EventArgs e)
        {
            if (isRunning)
            {
                MessageBox.Show("Phải STOP trước khi vẽ!");
                return;
            }

            if (dataLog.Count == 0)
            {
                MessageBox.Show("Không có dữ liệu!");
                return;
            }

            DrawAllCharts();
        }
        private void DrawAllCharts()
        {
            btnClear_Click(null, null);

            Chart[] allCharts = {
            chart_xy, chart_xd, chart_yd, chart_thetad,
            chart_v_vd, chart_w_wd, chart_v_vc, chart_w_wc,
            chart_vc_vd, chart_wc_wd, chart_errors123,
            chart_errors45, chart_tauc};

            // 🚀 Freeze toàn bộ Chart
            foreach (var c in allCharts)
            {
                if (c == null) continue;

                c.SuspendLayout();
                foreach (var s in c.Series)
                {
                    s.Points.SuspendUpdates();
                    s.Points.Clear(); // 🔥 clear tại đây luôn (nhanh hơn btnClear loop)
                }
            }

            // 🚀 Giới hạn số điểm để tránh lag (rất quan trọng)
            int step = Math.Max(1, dataLog.Count / 4000); // max ~4000 điểm

            for (int i = 0; i < dataLog.Count; i += step)
            {
                var v = dataLog[i];
                if (!IsValidFrame(v)) continue;  /* Bo qua diem loi */

                chart_xy.Series["Robot"].Points.AddXY(v[IDX_X], v[IDX_Y]);
                chart_xy.Series["Ref"].Points.AddXY(v[IDX_XD], v[IDX_YD]);

                chart_xd.Series["Robot"].Points.AddY(v[IDX_X]);
                chart_xd.Series["Ref"].Points.AddY(v[IDX_XD]);

                chart_yd.Series["Robot"].Points.AddY(v[IDX_Y]);
                chart_yd.Series["Ref"].Points.AddY(v[IDX_YD]);

                chart_thetad.Series["Robot"].Points.AddY(v[IDX_THETA]);
                chart_thetad.Series["Ref"].Points.AddY(v[IDX_THETAD]);

                chart_vc_vd.Series["vc"].Points.AddY(v[IDX_VC]);
                chart_vc_vd.Series["vd"].Points.AddY(v[IDX_VD]);

                chart_wc_wd.Series["wc"].Points.AddY(v[IDX_WC]);
                chart_wc_wd.Series["wd"].Points.AddY(v[IDX_WD]);

                chart_v_vc.Series["v"].Points.AddY(v[IDX_V]);
                chart_v_vc.Series["vc"].Points.AddY(v[IDX_VC]);

                chart_w_wc.Series["w"].Points.AddY(v[IDX_W]);
                chart_w_wc.Series["wc"].Points.AddY(v[IDX_WC]);

                chart_v_vd.Series["v"].Points.AddY(v[IDX_V]);
                chart_v_vd.Series["vd"].Points.AddY(v[IDX_VD]);

                chart_w_wd.Series["w"].Points.AddY(v[IDX_W]);
                chart_w_wd.Series["wd"].Points.AddY(v[IDX_WD]);

                chart_errors123.Series["e1"].Points.AddY(v[IDX_E1]);
                chart_errors123.Series["e2"].Points.AddY(v[IDX_E2]);
                chart_errors123.Series["e3"].Points.AddY(v[IDX_E3]);

                chart_errors45.Series["e4"].Points.AddY(v[IDX_E4]);
                chart_errors45.Series["e5"].Points.AddY(v[IDX_E5]);

                chart_tau.Series["tau_R"].Points.AddY(v[IDX_TAU_R]);
                chart_tau.Series["tau_L"].Points.AddY(v[IDX_TAU_L]);
                chart_tauc.Series["tauC_R"].Points.AddY(v[IDX_TAUC_R]);
                chart_tauc.Series["tauC_L"].Points.AddY(v[IDX_TAUC_L]);
                chart_uhat.Series["uhat_R"].Points.AddY(v[IDX_UHAT_R]);
                chart_uhat.Series["uhat_L"].Points.AddY(v[IDX_UHAT_L]);
            }

            // 🚀 Resume + redraw 1 lần duy nhất
            foreach (var c in allCharts)
            {
                if (c == null) continue;

                foreach (var s in c.Series)
                    s.Points.ResumeUpdates();

                c.ResumeLayout();
                c.Invalidate();
            }
        }
        private void SaveLogToCSV()
        {
            if (dataLog.Count == 0)
            {
                MessageBox.Show("Chưa có data để lưu!");
                return;
            }

            /* === Mở dialog cho user chọn chỗ lưu === */
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "CSV File|*.csv";
                sfd.FileName = "log_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                sfd.Title = "Lưu log EKF";
                sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                if (sfd.ShowDialog() != DialogResult.OK) return;

                try
                {
                    using (StreamWriter sw = new StreamWriter(sfd.FileName))
                    {
                        /* Header */
                        sw.WriteLine("idx,x,y,theta,vc,wc,v,w," +
                                     "xd,yd,thetad,vd,wd," +
                                     "tauc_R,tauc_L,tau_R,tau_L,uhat_R,uhat_L," +
                                     "e1,e2,e3,e4,e5," +
                                     "evd_v,ewd_w,evc_vd,ewc_wd," +
                                     "b_omega,omega_raw,P_theta,P_b_omega");

                        /* Data */
                        for (int i = 0; i < dataLog.Count; i++)
                        {
                            float[] v = dataLog[i];
                            sw.Write(i);
                            for (int j = 0; j < v.Length; j++)
                            {
                                sw.Write(",");
                                sw.Write(v[j].ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
                            }
                            sw.WriteLine();
                        }
                    }

                    MessageBox.Show($"Đã lưu {dataLog.Count} samples\nFile: {sfd.FileName}",
                                    "Save Log", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi lưu file: " + ex.Message);
                }
            }
        }
    }
}
