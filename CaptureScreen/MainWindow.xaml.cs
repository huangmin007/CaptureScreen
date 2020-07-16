using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private int VideoIndex = 0;
        private List<string> VideoPaths;

        private TcpServer TcpServer;
        private UdpServer UdpServer;
        private SocketDataAnalyse<String> DataAnalyse;


        public MainWindow()
        {
            InitializeComponent();
#if !DEBUG
            this.SettingWindowState(ConfigurationManager.AppSettings["WindowState"]);
#endif
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

            #region Video Arguments
            if (ConfigurationManager.AppSettings["VideoPath"] != null)
            {
                string paths = ConfigurationManager.AppSettings["VideoPath"];
                if (!string.IsNullOrWhiteSpace(paths) || paths.ToLower().Trim() == "null")
                {
                    VideoIndex = 0;
                    VideoPaths = new List<string>(paths.Split(','));

                    MediaPlayer.Source = GetVideoSource();
                }

                if (Enum.TryParse<Stretch>(ConfigurationManager.AppSettings["VideoStretch"], true, out stretch))
                    MediaPlayer.Stretch = stretch;
            }
            #endregion

            #region HPSocket Arguments
            if (MediaPlayer.Source != null)
            {
                if (ConfigurationManager.AppSettings["ListenPort"] != null)
                {
                    ushort localPort;
                    if (ushort.TryParse(ConfigurationManager.AppSettings["ListenPort"], out localPort))
                    {
                        if (localPort >= 1024) 
                            OnSocketInitialized(localPort);
                        else
                            Log.WarnFormat("禁用网络通信服务接口，端口参数设置不在范围：{0}", localPort);
                    }
                    else
                    {
                        Log.WarnFormat("启用网络通信服务接口失败，端口参数设置错误：{0}", ConfigurationManager.AppSettings["ListenPort"]);
                    }
                }
            }
            else
            {
                Log.Warn("无视频源，将不启动网络通信服务接口");
            }
            #endregion

            Log.Info("MainWindow Read Config Complete.");
        }

        /// <inheritdoc/>
        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);

            if (CaptureDevice != null)
            {
                CaptureDevice.Dispose();
                CaptureDevice = null;
            }

            if (TcpServer != null)
            {
                HPSocketExtension.DisposeServer(TcpServer);
                TcpServer = null;
            }
            if (UdpServer != null)
            {
                HPSocketExtension.DisposeServer(UdpServer);
                UdpServer = null;
            }

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

            DataAnalyse = new SocketDataAnalyse<string>();
            DataAnalyse.AddChannel("socket.client", 128);
        }
        private void ClientDataHandler(IntPtr client, byte[] data)
        {
            DataAnalyse.AnalyseChannel("socket.client", data, (s, v) =>
            {
                Log.InfoFormat("客户端 {0} 数据分析结果：{1}", s, v);
                ImageCapture.Dispatcher.BeginInvoke((Action)delegate()
                {
                    ImageCapture.Visibility = v ? Visibility.Visible : Visibility.Hidden;
                });
                return true;
            });
        }

        /// <summary>
        /// Window Loaded
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Log.InfoFormat("Window Loaded");

            int rotation = 0;

            if(ConfigurationManager.AppSettings["CaptureDeviceRotation"] != null)
                rotation = Convert.ToInt32(ConfigurationManager.AppSettings["CaptureDeviceRotation"]);

            CaptureDevice = new AForgeCaptureDevice(CaptureDeviceName, ImageCapture, (Rotation)rotation);
            CaptureDevice.SetFrameResolution(FrameSize, FrameRectangle);
            CaptureDevice.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Controls_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            /**
             *  在同屏画面与视频画面切换过程中，不显示的对象需要暂停，以防止出现背景声音在播放 
             */
            if (ImageCapture.Visibility == Visibility.Visible)
            {
                if (CaptureDevice != null)   CaptureDevice.Start();
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
            /**
             * 视频播放完成后，要检查是否存在多个视频循环，还是一个视循频环
             */ 
            if (VideoPaths.Count > 1)
            {
                VideoIndex = VideoIndex == VideoPaths.Count - 1 ? 0 : VideoIndex + 1;
                MediaPlayer.Source = GetVideoSource();
            }
            else
            {
                MediaPlayer.Position = TimeSpan.Zero;
            }

            MediaPlayer.Play();
        }

        /// <summary>
        /// 获取视频源
        /// </summary>
        /// <returns></returns>
        protected Uri GetVideoSource()
        {
            if (VideoPaths.Count == 0) return null;

            String path = Path.Combine(Environment.CurrentDirectory, VideoPaths[VideoIndex]);
            if(!File.Exists(path))
            {
                Log.WarnFormat("视频文件不存在:{0}", path);

                VideoPaths.RemoveAt(VideoIndex);
                if (VideoIndex >= VideoPaths.Count) VideoIndex--;

                return GetVideoSource();
            }

            Log.InfoFormat("播放视频文件:{0}", path);
            return new Uri(path, UriKind.RelativeOrAbsolute);
        }
    }

}
