using System;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NetworkMonitor
{
    public partial class Form1 : Form
    {
        private NotifyIcon trayIcon;
        private System.Windows.Forms.Timer updateTimer;
        private NetworkInterface activeInterface;
        private long lastBytesReceived = 0;
        private long lastBytesSent = 0;
        private long[] bandwidthHistory = new long[60];
        private int historyIndex = 0;

        
        private Label statLabel;

        
        [DllImport("dwmapi")]
        private static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DwmBlurbehind pBlurBehind);

        public struct DwmBlurbehind
        {
            public int DwFlags;
            public bool FEnable;
            public IntPtr HRgnBlur;
            public bool FTransitionOnMaximized;
        }

        public Form1()
        {
            InitializeComponent();

           
            statLabel = new Label();
            statLabel.Dock = DockStyle.Fill;
            statLabel.TextAlign = ContentAlignment.TopLeft; 
            statLabel.Font = new Font("Inter", 8, FontStyle.Bold);
            statLabel.ForeColor = Color.White;
            statLabel.Margin = new Padding(0);
            statLabel.Padding = new Padding(5); 
            statLabel.AutoSize = false;
            statLabel.Height = this.ClientSize.Height;

            this.Controls.Add(statLabel);

            
            trayIcon = new NotifyIcon
            {
                Icon = new Icon("Resources/NetIcon.ico"),
                Visible = true
            };

            trayIcon.MouseClick += TrayIcon_MouseClick;

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Exit", null, (s, e) => ExitApplication());
            trayIcon.ContextMenuStrip = menu;

            updateTimer = new System.Windows.Forms.Timer();
            updateTimer.Interval = 1000;
            updateTimer.Tick += (s, e) => UpdateNetworkStats();
            updateTimer.Start();

            this.Load += Form1_Load;
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Color.Black;
            this.Opacity = 0.85;
            this.ClientSize = new Size(80, 80);

            this.ShowInTaskbar = false;
            this.TopMost = true;



            GetActiveNetworkInterface();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            
            var dbb = new DwmBlurbehind { FEnable = true, DwFlags = 1, HRgnBlur = IntPtr.Zero, FTransitionOnMaximized = false };
            DwmEnableBlurBehindWindow(Handle, ref dbb);
        }

        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Screen screen = Screen.PrimaryScreen;
                Rectangle workingArea = screen.WorkingArea;
                Rectangle screenBounds = screen.Bounds;

                int taskbarHeight = screenBounds.Height - workingArea.Height;
                int flyoutX = Cursor.Position.X - Width / 2;
                int flyoutY = workingArea.Bottom - Height; 

                this.Location = new Point(flyoutX, flyoutY);
                this.Visible = !this.Visible;
            }
        }


        private void UpdateNetworkStats()
        {
            if (activeInterface == null)
            {
                statLabel.Text = "No Active Network";
                return;
            }

            long bytesReceived = activeInterface.GetIPv4Statistics().BytesReceived;
            long bytesSent = activeInterface.GetIPv4Statistics().BytesSent;

           
            double downloadSpeedKbps = (bytesReceived - lastBytesReceived) * 8.0 / 1024.0;
            double uploadSpeedKbps = (bytesSent - lastBytesSent) * 8.0 / 1024.0;
            int pingMs = GetPing();
            long totalUsageLastHour = GetBandwidthUsageLastHour(bytesReceived, bytesSent);

            string downloadSpeedText = downloadSpeedKbps >= 1024
                ? $"D: {downloadSpeedKbps / 1024.0:F2} Mbps"
                : $"D: {downloadSpeedKbps:F2} Kbps";

            string uploadSpeedText = uploadSpeedKbps >= 1024
                ? $"U: {uploadSpeedKbps / 1024.0:F2} Mbps"
                : $"U: {uploadSpeedKbps:F2} Kbps";

            lastBytesReceived = bytesReceived;
            lastBytesSent = bytesSent;

            string stats = $"{downloadSpeedText}\n{uploadSpeedText}\nPing: {pingMs} ms\nUsage: {totalUsageLastHour / 1024.0 / 1024.0:F2} MB";
            statLabel.Text = stats;
            this.Refresh();
        }

        private void GetActiveNetworkInterface()
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                {
                    activeInterface = ni;
                    break;
                }
            }
        }

        private int GetPing()
        {
            try
            {
                Ping ping = new Ping();
                PingReply reply = ping.Send("8.8.8.8", 1000);
                return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : -1;
            }
            catch
            {
                return -1;
            }
        }

        private long GetBandwidthUsageLastHour(long currentReceived, long currentSent)
        {
            long totalUsed = currentReceived + currentSent;
            long diff = totalUsed - (bandwidthHistory[historyIndex] > 0 ? bandwidthHistory[historyIndex] : totalUsed);
            bandwidthHistory[historyIndex] = totalUsed;
            historyIndex = (historyIndex + 1) % 60;
            return diff;
        }

        private void ExitApplication()
        {
            updateTimer.Stop();
            trayIcon.Dispose();
            Application.Exit();
        }
    }
}
