using System.Windows;
using SpaceCG.Extension;

namespace CaptureScreen
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 应用程序日志对象
        /// </summary>
        public static readonly log4net.ILog Log = log4net.LogManager.GetLogger(typeof(App));

        public App()
        {
            this.RunDefaultSetting();
        }

    }
}
