namespace ZZZScanner;

using System.Diagnostics;
using System.Security.Principal;
using ZZZScanner.Helpers;

/// <summary>
/// 命令行相关
/// </summary>
internal class Program
{
    static async Task Main(string[] args)
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
                using var gameHelper = new GameHelper();
                await gameHelper.StartScanner();
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

    private static bool IsUserAnAdmin()
    {
        var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }
}
