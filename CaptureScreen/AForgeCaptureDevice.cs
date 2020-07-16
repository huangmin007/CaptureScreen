using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media.Imaging;
using AForge.Video;
using AForge.Video.DirectShow;

namespace CaptureScreen
{
    /// <summary>
    /// Custom AForge Capture Device
    /// </summary>
    public class AForgeCaptureDevice:IDisposable
    {
        private String _DeviceName;
        private Rectangle _FrameRectangle;
        private System.Drawing.Size _FrameSize;

        private Rotation _Rotate = Rotation.Rotate0;

        private VideoCaptureDevice _CaptureDevice;
        private System.Windows.Controls.Image _ImageControl;
        public static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(AForgeCaptureDevice));


        /// <summary>
        /// AForge Capture Device
        /// </summary>
        /// <param name="deviceName">设备名称或是设备索引</param>
        /// <param name="imageControl">输出显示的 Image 控件</param>
        /// <exception cref="ArgumentNullException"></exception>
        public AForgeCaptureDevice(String deviceName, System.Windows.Controls.Image imageControl, Rotation rotate = Rotation.Rotate0)
        {
            OutputCaptureDeviceInfo();

            if (String.IsNullOrWhiteSpace(deviceName)) throw new ArgumentNullException(nameof(deviceName), "参数不能为空");
            if (imageControl == null) throw new ArgumentNullException(nameof(imageControl), "参数不能为空");

            this._Rotate = rotate;
            this._DeviceName = deviceName;
            this._ImageControl = imageControl;

            string deviceMoniker;
            if (TryGetDeviceMoniker(_DeviceName, out deviceMoniker))
            {
                _CaptureDevice = new VideoCaptureDevice(deviceMoniker);
            }
            else
            {
                Log.ErrorFormat("无法获取到捕捉设备 {0} ", _DeviceName);
                MessageBox.Show("无法获取到捕捉设备", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 设置帧输出分辨率大小，及帧显示范围
        /// <para>注意：在运行状态设备分辨率参数无效，需暂停-设置-启动，参数生效</para>
        /// </summary>
        /// <param name="frameSize">输出帧的分辨率大小，默认为设备支持的第一个分辨率</param>
        /// <param name="frameRectangle">输出帧的显示范围，默认为完整范围，不进行裁剪</param>
        public void SetFrameResolution(System.Drawing.Size frameSize = default, Rectangle frameRectangle = default)
        {
            this._FrameSize = frameSize;
            this._FrameRectangle = frameRectangle;

            if (_CaptureDevice == null) return;

            VideoCapabilities vCapabilities;
            if (TryGetDeviceVideoResolution(_CaptureDevice, _FrameSize, out vCapabilities))
            {
                _CaptureDevice.VideoResolution = vCapabilities;
                Log.InfoFormat("Capture Device [{0}] Setting Resolution {1} Success. ", _DeviceName, vCapabilities.FrameSize);
            }
            else
            {
                _CaptureDevice.VideoResolution = _CaptureDevice.VideoCapabilities[0];
                Log.WarnFormat("未找到设置的可用分辨率 {0} ，使用默认分辨率 {1} ", _FrameSize, _CaptureDevice.VideoCapabilities[0].FrameSize);
            }
        }

        /// <summary>
        /// 启动设备
        /// </summary>
        public void Start()
        {
            /**
             * 如果启动停止相达到快速图像切换，不可调用 VideoCaptureDevice.Start() , 该函数启动是异步的，需等待
             * 如果想达到快速不等待效果，让 VideoCaptureDevice 在后台一直启动状态，只是添加或移除 NewFrame 事件
             */
            if (_CaptureDevice != null)
            {
                _CaptureDevice.NewFrame += CaptureDevice_NewFrame;
                if (!_CaptureDevice.IsRunning)  _CaptureDevice.Start();

                Log.InfoFormat("Capture Device [{0}] Started Success. ", _DeviceName);
            }
        }

        /// <summary>
        /// 启动设备
        /// </summary>
        /// <param name="onlyRemoveListen">是否只是移除事件监听，默认为 true; 如果为 false 则在次启动会异步处理。</param>
        public void Stop(bool onlyRemoveListen = true)
        {
            if (_CaptureDevice != null)
            {
                _CaptureDevice.NewFrame -= CaptureDevice_NewFrame;
                if(_CaptureDevice.IsRunning && !onlyRemoveListen) _CaptureDevice.SignalToStop();

                Log.InfoFormat("Capture Device [{0}] Stopped Success. ", _DeviceName);
            }
        }

        /// <summary>
        /// 停止并清理设备
        /// </summary>
        public void Dispose()
        {
            if (_CaptureDevice != null)
            {
                _CaptureDevice.NewFrame -= CaptureDevice_NewFrame;
                _CaptureDevice.SignalToStop();

                Log.InfoFormat("Capture Device [{0}] Dispose Success. ", _DeviceName);
            }

            _CaptureDevice = null;
        }

        /// <summary>
        /// Captrue Device New Frame Event Handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CaptureDevice_NewFrame(object sender, NewFrameEventArgs e)
        {
            try
            {
                Bitmap bmp;
                if (_FrameRectangle.IsEmpty)
                {
                    bmp = e.Frame;//.Clone();
                }
                else
                {
                    bmp = new Bitmap(_FrameRectangle.Width, _FrameRectangle.Height);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.DrawImage(e.Frame, new Rectangle(0, 0, bmp.Width, bmp.Height), _FrameRectangle, GraphicsUnit.Pixel);
                    }
                }

                MemoryStream ms = new MemoryStream();
                bmp.Save(ms, ImageFormat.Bmp);
                ms.Seek(0, SeekOrigin.Begin);

                //旋转：https://docs.microsoft.com/zh-cn/dotnet/framework/wpf/controls/how-to-rotate-an-image
                //裁剪：https://docs.microsoft.com/zh-cn/dotnet/framework/wpf/controls/how-to-crop-an-image
                BitmapImage bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.Rotation = _Rotate;
                bitmap.EndInit();
                bitmap.Freeze();

                _ImageControl.Dispatcher.BeginInvoke((Action)delegate ()
                {
                    _ImageControl.Source = bitmap;

                    //CroppedBitmap cropped = new CroppedBitmap(bitmap, new Int32Rect(0, 0, 200, 300));
                    //_ImageControl.Source = cropped;
                });
            }
            catch (Exception ex)
            {
                Log.ErrorFormat("Capture Device New Frame Event Handler Error: {0}", ex.Message);
            }
        }


        /// <summary>
        /// 输出所有设备信息
        /// </summary>
        public static void OutputCaptureDeviceInfo()
        {
            foreach (FilterInfo info in new FilterInfoCollection(FilterCategory.VideoInputDevice))
            {
                String format = $"CaptureDevice Name:[{info.Name}]  MonikerString:[{info.MonikerString}]";

                VideoCaptureDevice device = new VideoCaptureDevice(info.MonikerString);

                format += $"\nVideoCapabilities FrameSize: ";
                foreach (VideoCapabilities cap in device.VideoCapabilities) format += $"{cap.FrameSize}  ";

                format += $"\nSnapshotCapabilities FrameSize: ";
                foreach (VideoCapabilities cap in device.SnapshotCapabilities)  format += $"{cap.FrameSize}  ";

                format += $"\nCamera Control Properties:";
                for(CameraControlProperty pro = CameraControlProperty.Pan; pro <= CameraControlProperty.Focus; pro ++)
                {
                    if (device.GetCameraProperty(pro, out int value, out CameraControlFlags flags))
                    {
                        if (device.GetCameraPropertyRange(pro, out int minValue, out int maxValue, out int stepSize, out int defaultValue, out CameraControlFlags rFlags))
                            format += $"(CameraControlFlags.{pro}({(int)pro}) value:{value} minValue:{minValue} maxValue:{maxValue} stepSize:{stepSize} defaultValue:{defaultValue} flags:[{flags}] cFlags:[{rFlags}])  ";
                        else
                            format += $"(CameraControlFlags.{pro}({(int)pro}) value:{value} flags:[{flags}])  ";
                    }
                }

                //device.DisplayPropertyPage(IntPtr.Zero);
                //device.DisplayCrossbarPropertyPage(IntPtr.Zero);

                device.SignalToStop();
                Log.Info(format);
            }
        }

        /// <summary>
        /// 跟据名称或者索引，试图获取捕捉设备标识符
        /// </summary>
        /// <param name="nameORIndex">设备名称或设备索引</param>
        /// <param name="monikerString">如果存在则返回设备标识符</param>
        /// <returns></returns>
        public static bool TryGetDeviceMoniker(String nameORIndex, out string monikerString)
        {
            monikerString = null;
            if (String.IsNullOrWhiteSpace(nameORIndex)) return false;

            FilterInfoCollection CaptureDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);

            try
            {
                //检查 nameOrIndex 是否匹配数字
                if (Regex.IsMatch(nameORIndex, @"^\d*$"))   //@"^\d+$"
                {
                    monikerString = CaptureDevices[int.Parse(nameORIndex)].MonikerString;
                    return true;
                }
                else
                {
                    foreach (FilterInfo info in CaptureDevices)
                    {
                        if (info.Name.Equals(nameORIndex, StringComparison.CurrentCultureIgnoreCase))
                        {
                            monikerString = info.MonikerString;
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.ErrorFormat($"Get Capture Device Error [{nameORIndex}]", e);
            }

            monikerString = null;
            return false;
        }

        /// <summary>
        /// 获取设备 Video 分辨率参数
        /// </summary>
        /// <param name="device"></param>
        /// <param name="frameSize"></param>
        /// <param name="capabilities"></param>
        /// <returns></returns>
        public static bool TryGetDeviceVideoResolution(VideoCaptureDevice device, System.Drawing.Size frameSize, out VideoCapabilities capabilities)
        {
            capabilities = null;
            if (device == null) return false;

            if(frameSize.IsEmpty)
            {
                capabilities = device.VideoCapabilities[0];
                return true;
            }

            foreach (VideoCapabilities cap in device.VideoCapabilities)
            {
                if (cap.FrameSize == frameSize)
                {
                    capabilities = cap;
                    return true;
                }
            }

            capabilities = null;
            return false;
        }

        /// <summary>
        /// 获取设备 Snapshot 分辨率参数
        /// </summary>
        /// <param name="device"></param>
        /// <param name="frameSize"></param>
        /// <param name="capabilities"></param>
        /// <returns></returns>
        public static bool TryGetDeviceSnapshotResolution(VideoCaptureDevice device, System.Drawing.Size frameSize, out VideoCapabilities capabilities)
        {
            capabilities = null;
            if (device == null) return false;

            foreach (VideoCapabilities cap in device.SnapshotCapabilities)
            {
                if (cap.FrameSize == frameSize)
                {
                    capabilities = cap;
                    return true;
                }
            }

            capabilities = null;
            return false;
        }

        /// <summary>
        /// 计算矩形绕中心任意角度旋转后所占区域矩形宽高
        /// </summary>
        /// <param name="width">原矩形的宽</param>
        /// <param name="height">原矩形高</param>
        /// <param name="angle">顺时针旋转角度</param>
        /// <returns></returns>
        public static Rectangle GetRotateRectangle(int width, int height, float angle)
        {
            double radian = angle * Math.PI / 180; ;
            double cos = Math.Cos(radian);
            double sin = Math.Sin(radian);
            //只需要考虑到第四象限和第三象限的情况取大值(中间用绝对值就可以包括第一和第二象限)
            int newWidth = (int)(Math.Max(Math.Abs(width * cos - height * sin), Math.Abs(width * cos + height * sin)));
            int newHeight = (int)(Math.Max(Math.Abs(width * sin - height * cos), Math.Abs(width * sin + height * cos)));
            
            return new Rectangle(0, 0, newWidth, newHeight);
        }

        /// <summary>
        /// 获取原图像绕中心任意角度旋转后的图像
        /// </summary>
        /// <param name="rawImg"></param>
        /// <param name="angle"></param>
        /// <returns></returns>
        public Image GetRotateImage(Image srcImage, int angle)
        {
            angle = angle % 360;
            //原图的宽和高
            int srcWidth = srcImage.Width;
            int srcHeight = srcImage.Height;
            //图像旋转之后所占区域宽和高
            Rectangle rotateRec = GetRotateRectangle(srcWidth, srcHeight, angle);
            int rotateWidth = rotateRec.Width;
            int rotateHeight = rotateRec.Height;
            //目标位图
            Bitmap destImage = null;
            Graphics graphics = null;
            try
            {
                //定义画布，宽高为图像旋转后的宽高
                destImage = new Bitmap(rotateWidth, rotateHeight);
                //graphics根据destImage创建，因此其原点此时在destImage左上角
                graphics = Graphics.FromImage(destImage);
                //要让graphics围绕某矩形中心点旋转N度，分三步
                //第一步，将graphics坐标原点移到矩形中心点,假设其中点坐标（x,y）
                //第二步，graphics旋转相应的角度(沿当前原点)
                //第三步，移回（-x,-y）
                //获取画布中心点
                System.Drawing.Point centerPoint = new System.Drawing.Point(rotateWidth / 2, rotateHeight / 2);
                //将graphics坐标原点移到中心点
                graphics.TranslateTransform(centerPoint.X, centerPoint.Y);
                //graphics旋转相应的角度(绕当前原点)
                graphics.RotateTransform(angle);
                //恢复graphics在水平和垂直方向的平移(沿当前原点)
                graphics.TranslateTransform(-centerPoint.X, -centerPoint.Y);
                //此时已经完成了graphics的旋转

                //计算:如果要将源图像画到画布上且中心与画布中心重合，需要的偏移量
                System.Drawing.Point Offset = new System.Drawing.Point((rotateWidth - srcWidth) / 2, (rotateHeight - srcHeight) / 2);
                //将源图片画到rect里（rotateRec的中心）
                graphics.DrawImage(srcImage, new Rectangle(Offset.X, Offset.Y, srcWidth, srcHeight));
                //重至绘图的所有变换
                graphics.ResetTransform();
                graphics.Save();
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                if (graphics != null)
                    graphics.Dispose();
            }
            return destImage;
        }
    }
}
