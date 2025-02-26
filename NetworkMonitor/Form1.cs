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
        private long[] bandwidthHistory = new long[3600]; 
        private int historyIndex = 0;
        private Label statLabel;

        
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

       
        [DllImport("dwmapi")]
        private static extern int DwmEnableBlurBehindWindow(IntPtr hWnd, ref DwmBlurbehind pBlurBehind);

        private const int HOTKEY_ID = 1;
        private const uint MOD_ALT = 0x0001;
        private const uint VK_N = 0x4E;

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
                Icon = new Icon("NetIcon.ico"),
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

            
            RegisterHotKey(this.Handle, HOTKEY_ID, MOD_ALT, VK_N);
        }

        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                this.Location = new Point(0); 
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

            string usageText;
            double usageMB = totalUsageLastHour / 1024.0 / 1024.0;

            if (usageMB >= 1024)
            {
                usageText = $"{usageMB / 1024.0:F2} GB"; 
            }
            else
            {
                usageText = $"{usageMB:F2} MB"; 
            }

            string stats = $"{downloadSpeedText}\n{uploadSpeedText}\nPing: {pingMs} ms\nUsage: {usageText}";
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

            
            long currentUsage = totalUsed - lastBytesReceived - lastBytesSent;
            if (currentUsage < 0) currentUsage = 0; 

            
            bandwidthHistory[historyIndex] = currentUsage;

            
            historyIndex = (historyIndex + 1) % 3600;

            
            long totalUsageLastHour = 0;
            foreach (long usage in bandwidthHistory)
            {
                totalUsageLastHour += usage;
            }

            lastBytesReceived = currentReceived;
            lastBytesSent = currentSent;

            return totalUsageLastHour;
        }


        protected override void WndProc(ref Message m)
        {
            const int WM_HOTKEY = 0x0312;
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                this.Visible = !this.Visible;
            }
            base.WndProc(ref m);
        }

        private void ExitApplication()
        {
            updateTimer.Stop();
            trayIcon.Dispose();
            UnregisterHotKey(this.Handle, HOTKEY_ID);
            Application.Exit();
        }
    }
}
