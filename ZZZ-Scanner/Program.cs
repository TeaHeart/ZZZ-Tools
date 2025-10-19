namespace ZZZScanner
{
    using System;
    using System.Diagnostics;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// dll调用相关
    /// </summary>
    internal partial class Program
    {
        [DllImport("shell32.dll")]
        static extern bool IsUserAnAdmin();

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll")]
        static extern int GetSystemMetrics(int nIndex);

        const int SW_RESTORE = 9;
        const int SM_CXSCREEN = 0;
        const int SM_CYSCREEN = 1;

        [DllImport("user32.dll")]
        static extern bool SetCursorPos(int X, int Y);

        [DllImport("user32.dll")]
        static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);

        const int MOUSEEVENTF_LEFTDOWN = 0x0002;
        const int MOUSEEVENTF_LEFTUP = 0x0004;
        const int MOUSEEVENTF_WHEEL = 0x0800;

        static void LeftClick(int x, int y, int duration = 0)
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, x, y, 0, 0);
            if (duration > 0)
            {
                Thread.Sleep(duration);
            }
            mouse_event(MOUSEEVENTF_LEFTUP, x, y, 0, 0);
        }

        static void MouseWheel(int delta)
        {
            mouse_event(MOUSEEVENTF_WHEEL, 0, 0, delta, 0);
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        static Color GetPixel(int x, int y)
        {
            var hdc = GetDC(IntPtr.Zero);
            var pixel = GetPixel(hdc, x, y);
            ReleaseDC(IntPtr.Zero, hdc);
            var color = Color.FromArgb((int)(pixel & 0x000000FF), (int)(pixel & 0x0000FF00) >> 8, (int)(pixel & 0x00FF0000) >> 16);
            return color;
        }
    }

    /// <summary>
    /// 游戏窗口相关
    /// </summary>
    internal partial class Program
    {
        // 主要支持的分辨率
        static Size StandardScreen = new Size(1920, 1080);
        // 窗口模式最大尺寸
        static Size MaxWindowSize = StandardScreen + new Size(20, 50);

        static void RequireWindow()
        {
            // 检查屏幕分辨率
            var screenWidth = GetSystemMetrics(SM_CXSCREEN);
            var screenHeight = GetSystemMetrics(SM_CYSCREEN);

            Console.WriteLine($"屏幕分辨率 {screenWidth} x {screenHeight}");

            if (screenWidth < StandardScreen.Width || screenHeight < StandardScreen.Height)
            {
                throw new Exception($"屏幕分辨率小于 {StandardScreen.Width} x {StandardScreen.Height}，如果启用了windows缩放请关闭");
            }

            // 检查游戏进程
            var process = Process.GetProcesses().Where(p => p.ProcessName == "ZenlessZoneZero").FirstOrDefault();
            if (process == null)
            {
                throw new Exception("未找到进程");
            }

            var handle = process.MainWindowHandle;
            if (handle == IntPtr.Zero)
            {
                throw new Exception("窗口句柄为空");
            }

            Console.WriteLine("找到窗口");

            // 检查游戏窗口状态
            if (IsIconic(handle))
            {
                Console.WriteLine("窗口为最小化，尝试最大化");
                if (!ShowWindow(handle, SW_RESTORE))
                {
                    throw new Exception("最大化失败");
                }
            }

            if (!SetForegroundWindow(handle))
            {
                throw new Exception("窗口前置失败");
            }

            // 确保窗口分辨率
            if (!GetWindowRect(handle, out var rect))
            {
                throw new Exception("窗口分辨率获取失败");
            }

            var width = rect.Right - rect.Left;
            var height = rect.Bottom - rect.Top;

            Console.WriteLine($"窗口分辨率 {width} x {height}");

            if (width < StandardScreen.Width || height < StandardScreen.Height)
            {
                throw new Exception($"窗口分辨率小于 {StandardScreen.Width} x {StandardScreen.Height}");
            }

            if (width > MaxWindowSize.Width || height > MaxWindowSize.Height)
            {
                throw new Exception($"窗口分辨率大于 {MaxWindowSize.Width} x {MaxWindowSize.Height}，请设置为1920x1080全屏或窗口");
            }

            var borderWidth = (StandardScreen.Width - width) / 2; // 左右边框宽度
            var titleHeight = (StandardScreen.Height - height) + -borderWidth; // 标题栏高度

            // 统一移动到屏幕左上角，方便从 0,0 => 1920,1080 截图
            if (!MoveWindow(handle, borderWidth, titleHeight, width, height, false))
            {
                throw new Exception("窗口移动失败");
            }

            Console.WriteLine($"窗口已移动 {borderWidth}, {titleHeight}");
        }
    }

    /// <summary>
    /// 驱动盘扫描相关
    /// </summary>
    internal partial class Program
    {
        // 背包驱动盘界面按钮
        static Point DriveDiscStorage = new Point(1530, 170);
        // 滚动条顶部
        static Point ScrollBarTop = new Point(1366, 242);
        // 滚动条底部
        static Point ScrollBarBottom = new Point(1366, 862);
        // 滚动条颜色
        static Color ScrollBarColor = Color.FromArgb(0xff, 0x80, 0x80, 0x80);
        // 驱动盘起始偏移，+50和+55是为了检测右下角的颜色（S级/A级）
        static Point DriveDiscOffset = new Point(30 + 50, 90 + 55);
        // 驱动盘之间间隔
        static Size DriveDiscSize = new Size(135, 175);
        // 左下角拆解位置，用于检测是否还在驱动盘页面，按ESC退出界面后就会停止扫描
        static Point Dismantle = new Point(222, 930);
        // 左下角拆解颜色
        static Color DismantleColor = Color.FromArgb(0xff, 0, 186, 255);
        // S级驱动盘颜色
        static Color DriveDiscSColor = Color.FromArgb(0xff, 255, 181, 0);
        // A级驱动盘颜色
        static Color DriveDiscAColor = Color.FromArgb(0xff, 233, 0, 255);
        // 驱动盘卡片
        static Rectangle DriveDiscCard = Rectangle.FromLTRB(1400, 258, 1850, 775);
        // 驱动盘信息位置
        static Rectangle[] DriveDiscInfo = new Rectangle[]
        {
            // 名称
            Rectangle.FromLTRB(1410,270,1635,310),
            // 等级
            Rectangle.FromLTRB(1465,405,1600,435),
            // 主属性
            Rectangle.FromLTRB(1418,489,1720,530),
            Rectangle.FromLTRB(1730,489,1830,530),
            // 副属性
            Rectangle.FromLTRB(1418,567,1720,608),
            Rectangle.FromLTRB(1730,567,1830,608),
            Rectangle.FromLTRB(1418,618,1720,659),
            Rectangle.FromLTRB(1730,618,1830,659),
            Rectangle.FromLTRB(1418,670,1720,711),
            Rectangle.FromLTRB(1730,670,1830,711),
            Rectangle.FromLTRB(1418,722,1720,763),
            Rectangle.FromLTRB(1730,722,1830,763),
        };
        // 属性背景偏移，通过检测背景颜色判断有没有更多的副属性
        static Point StatBackgroundOffset = new Point(10, 20);
        // 卡片信息背景颜色
        static Color CardBackGroundColor = Color.FromArgb(0xff, 0, 0, 0);
        // 属性条目背景色
        static Color StatBackGroundColor = Color.FromArgb(0xff, 22, 22, 22);

        enum TriBool
        {
            None = -1,
            False = 0,
            True = 1,
        }

        public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, TimeSpan interval, string timeoutMessage = "超时", CancellationToken token = default)
        {
            var startTime = DateTime.Now;
            while (DateTime.Now - startTime < timeout)
            {
                if (condition())
                {
                    return;
                }
                await Task.Delay(interval, token);
            }
            throw new TimeoutException(timeoutMessage);
        }

        // 监控拆解按钮颜色，即检测用户是否退出了背包
        static async Task MonitorUI(CancellationTokenSource cts)
        {
            while (!cts.IsCancellationRequested)
            {
                if (GetPixel(Dismantle.X, Dismantle.Y) != DismantleColor)
                {
                    cts.Cancel();
                    break;
                }
                await Task.Delay(100);
            }
        }

        // 遍历扫描逻辑
        static async Task Scan(DirectoryInfo dir, CancellationToken token)
        {
            var count = 0;
            // 遍历驱动盘，总共4行，1、2、4行正常扫描，第3行通过翻页扫描
            for (int row = 1; row <= 4; row++)
            {
                // 当前行
                var currY = DriveDiscOffset.Y + DriveDiscSize.Height * row;
                // 从左到右依次点击
                for (int col = 1; col <= 9; col++)
                {
                    // 当前列
                    var currX = DriveDiscOffset.X + DriveDiscSize.Width * col;
                    LeftClick(currX, currY);

                    // 检测驱动盘品质颜色，即当前点击是否为空格子
                    var color = GetPixel(currX, currY);
                    if (color != DriveDiscSColor && color != DriveDiscAColor)
                    {
                        // 扫描结束或用户退出操作
                        token.ThrowIfCancellationRequested();
                        return;
                    }

                    //检测驱动盘等级区域的背景色，即是否加载完成
                    await Task.Delay(100, token);
                    // 最慢的地方，100ms不能保证完全加载出来驱动盘信息，至少得200ms
                    await WaitUntilAsync(() => GetPixel(DriveDiscInfo[1].X, DriveDiscInfo[2].Y) == StatBackGroundColor, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.1), "驱动盘加载超时", token);

                    // 开始裁切屏幕
                    using (var card = new Bitmap(DriveDiscCard.Width, DriveDiscCard.Height))
                    {
                        using (var cardGraph = Graphics.FromImage(card))
                        {
                            // 填充黑色
                            cardGraph.Clear(Color.Black);
                            var k = 0;
                            foreach (var item in DriveDiscInfo)
                            {
                                // 名称、等级、主属性必有，或者有副属性
                                if (k <= 3 || GetPixel(item.X + StatBackgroundOffset.X, item.Y + StatBackgroundOffset.Y) != CardBackGroundColor)
                                {
                                    // 复制指定区域
                                    cardGraph.CopyFromScreen(item.X, item.Y, item.X - DriveDiscCard.X, item.Y - DriveDiscCard.Y, item.Size);
                                    k++;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }

                        // 二值化，128可保留词缀后橙色+x，192不保留
                        BinarizeImage(card, 192, false);
                        // 保存
                        var filename = Path.Combine(dir.Name, $"{count:0000}.png");
                        card.Save(filename, ImageFormat.Png);
                        Console.WriteLine($"保存 {filename}");
                        count++;
                    }
                }
                // 是第3行，且有下一行自动翻页
                if (row == 3 && GetPixel(ScrollBarBottom.X, ScrollBarBottom.Y) != ScrollBarColor)
                {
                    // 向下滚动
                    MouseWheel(-120);
                    // 保持在第3行
                    row--;
                    await Task.Delay(500, token);
                }
            }
        }

        static async Task StartScanner()
        {
            // 输出目录
            var dir = Directory.CreateDirectory($"{DateTime.Now:yyyy-MM-dd-HH-mm-ss}");
            // 等待背包
            Console.WriteLine("等待背包界面");
            await WaitUntilAsync(() => GetPixel(Dismantle.X, Dismantle.Y) == DismantleColor, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1), "等待背包超时");
            // 驱动盘页面
            LeftClick(DriveDiscStorage.X, DriveDiscStorage.Y);
            await Task.Delay(100);
            // 到第一行
            LeftClick(ScrollBarTop.X, ScrollBarTop.Y, 100);
            await Task.Delay(100);

            var cts = new CancellationTokenSource();
            Console.WriteLine("按ESC退出背包后停止扫描");
            var monitor = MonitorUI(cts);
            var scan = Scan(dir, cts.Token);

            try
            {
                await Task.WhenAny(monitor, scan);
                cts.Cancel();
                await Task.WhenAll(monitor, scan);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("用户取消操作");
            }
            Process.Start("explorer.exe", dir.FullName);
        }

        static unsafe void BinarizeImage(Bitmap image, int threshold = 128, bool invert = false)
        {
            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, image.PixelFormat);

            try
            {
                byte* ptr = (byte*)data.Scan0;
                int bits = Image.GetPixelFormatSize(image.PixelFormat) / 8;
                byte black = (byte)(!invert ? 0 : 0xff);
                byte white = (byte)(!invert ? 0xff : 0);

                for (int y = 0; y < data.Height; y++)
                {
                    byte* row = ptr + y * data.Stride;
                    for (int x = 0; x < data.Width; x++)
                    {
                        byte* pixel = row + x * bits;
                        byte gray = (byte)((pixel[2] + pixel[1] + pixel[0]) / 3);
                        byte newValue = gray < threshold ? black : white;
                        pixel[0] = newValue; // B
                        pixel[1] = newValue; // G  
                        pixel[2] = newValue; // R
                    }
                }
            }
            finally
            {
                image.UnlockBits(data);
            }
        }
    }

    /// <summary>
    /// 命令行相关
    /// </summary>
    internal partial class Program
    {
        static void Main(string[] args)
        {
            try
            {
                if (!IsUserAnAdmin())
                {
                    Console.WriteLine("不是管理员，尝试以管理员身份运行");
                    Process.Start(new ProcessStartInfo
                    {
                        UseShellExecute = true,
                        WorkingDirectory = Environment.CurrentDirectory,
                        FileName = Process.GetCurrentProcess().MainModule.FileName,
                        Arguments = string.Join(" ", args),
                        Verb = "runas"
                    });
                }
                else
                {
                    RequireWindow();
                    StartScanner().Wait();
                    Console.WriteLine("程序结束");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"程序终止：{ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ReadKey();
            }
        }
    }
}