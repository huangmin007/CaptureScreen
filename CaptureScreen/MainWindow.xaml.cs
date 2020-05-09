using System;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using HPSocket.Tcp;
using HPSocket.Udp;
using SpaceCG.Extension;
using SpaceCG.Log4Net.Controls;

namespace CaptureScreen
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// MainWindow 日志对象
        /// </summary>
        public static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(MainWindow));

        // Config Arguments
        private string CaptureDeviceName; 
        private Rectangle FrameRectangle;
        private System.Drawing.Size FrameSize;
        private AForgeCaptureDevice CaptureDevice;

        //Video And Server
        private string VideoPath;
        private TcpServer TcpServer;
        private UdpServer UdpServer;

        public MainWindow()
        {
            InitializeComponent();
            this.SettingWindowState(ConfigurationManager.AppSettings["WindowState"]);

            if (ConfigurationManager.AppSettings["UseLoggerWindow"] != null && Convert.ToBoolean(ConfigurationManager.AppSettings["UseLoggerWindow"]))
                _ = new LoggerWindow();

            #region AForge Capture Device Arguments
            CaptureDeviceName = ConfigurationManager.AppSettings["CaptureDeviceName"];
            String resolution = ConfigurationManager.AppSettings["CaptureDeviceResolution"];
            if (!String.IsNullOrWhiteSpace(resolution))
            {
                string[] arr = resolution.Split(',');
                if (arr.Length == 2)
                {
                    int width, height;
                    int.TryParse(arr[0], out width);
                    int.TryParse(arr[1], out height);
                    FrameSize = new System.Drawing.Size(width, height);
                }
            }
            String rectangle = ConfigurationManager.AppSettings["CaptureDeviceRectangle"];
            if (!String.IsNullOrWhiteSpace(rectangle))
            {
                string[] arr = rectangle.Split(',');
                if (arr.Length == 4)
                {
                    int x, y, width, height;
                    int.TryParse(arr[0], out x);
                    int.TryParse(arr[1], out y);
                    int.TryParse(arr[2], out width);
                    int.TryParse(arr[3], out height);
                    FrameRectangle = new Rectangle(x, y, width, height);
                }
            }

            Stretch stretch;
            if (Enum.TryParse<Stretch>(ConfigurationManager.AppSettings["CaptureImageStretch"], true, out stretch))
                ImageCapture.Stretch = stretch;
            #endregion

            #region HPSocket Arguments
            if (ConfigurationManager.AppSettings["ListenPort"] != null)
            {
                ushort localPort;
                if (ushort.TryParse(ConfigurationManager.AppSettings["ListenPort"], out localPort))
                {
                    if (localPort >= 1024) OnSocketInitialized(localPort);
                }
                else
                {
                    Log.InfoFormat("启用网络通信服务失败，端口参数设置错误：{0}", ConfigurationManager.AppSettings["ListenPort"]);
                }
            }
            #endregion

            #region Video Arguments
            if (ConfigurationManager.AppSettings["VideoPath"] != null)
            {
                string path = ConfigurationManager.AppSettings["VideoPath"];
                if (!string.IsNullOrWhiteSpace(path) || path.ToLower().Trim() == "null")
                {
                    VideoPath = path;
                    MediaPlayer.Source = new Uri(Environment.CurrentDirectory + "/" + VideoPath, UriKind.RelativeOrAbsolute);
                }

                if (Enum.TryParse<Stretch>(ConfigurationManager.AppSettings["VideoStretch"], true, out stretch))
                    MediaPlayer.Stretch = stretch;
            }
            #endregion

            Log.InfoFormat("MainWindow");
        }

        /// <inheritdoc/>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (CaptureDevice != null) CaptureDevice.Stop();

            if (TcpServer != null) HPSocketExtension.DisposeServer(TcpServer);
            if (UdpServer != null) HPSocketExtension.DisposeServer(UdpServer);

            Log.InfoFormat("Window Closing");
        }

        /// <inheritdoc/>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (MediaPlayer.Source == null || UdpServer == null) return;

            switch(e.Key)
            {
                case Key.A:
                    ImageCapture.Visibility = Visibility.Visible;
                    break;

                case Key.B:
                    ImageCapture.Visibility = Visibility.Hidden;
                    break;
            }
        }

        /// <summary>
        /// On Socket Initialized
        /// </summary>
        /// <param name="localPort"></param>
        private void OnSocketInitialized(ushort localPort)
        {
            UdpServer = HPSocketExtension.CreateServer<UdpServer>(localPort, ClientDataHandler);
            TcpServer = HPSocketExtension.CreateServer<TcpServer>(localPort, ClientDataHandler);
        }
        private void ClientDataHandler(IntPtr client, byte[] data)
        {
            Console.WriteLine("Data:{0}", Encoding.Default.GetString(data));
        }

        /// <summary>
        /// Window Loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Log.InfoFormat("Window Loaded");

            CaptureDevice = new AForgeCaptureDevice(CaptureDeviceName, ImageCapture);
            CaptureDevice.SetFrameResolution(FrameSize, FrameRectangle);
            CaptureDevice.Start();
        }

        private void Controls_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ImageCapture.Visibility == Visibility.Visible)
            {
                if(CaptureDevice != null)   CaptureDevice.Start();
                if (MediaPlayer.Source != null) MediaPlayer.Pause();
            }
            else
            {
                //ImageCapture.Source = null;
                if (CaptureDevice != null) CaptureDevice.Stop();                
                if (MediaPlayer.Source != null) MediaPlayer.Play();
            }
        }

        private void MediaPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            MediaPlayer.Position = TimeSpan.Zero;
            MediaPlayer.Play();
        }
    }
}
