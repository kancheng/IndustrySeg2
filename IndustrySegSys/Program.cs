using System;
using System.Windows.Forms;

namespace IndustrySegSys
{
    internal static class Program
    {
        /// <summary>
        /// 應用程序的主入口點。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 啟用應用程序樣式
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            // 設置高 DPI 支持
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            
            // 運行主窗體
            Application.Run(new MainForm());
        }
    }
}
