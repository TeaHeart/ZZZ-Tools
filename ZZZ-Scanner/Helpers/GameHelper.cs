namespace ZZZScanner.Helpers;

using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

public class GameHelper : IDisposable
{
    private readonly Config _config;
    private readonly WindowHelper _windowHelper;
    private readonly Recognizer _recognizer;
    private readonly TextCleaner _textCleaner;
    private readonly BlockingCollection<(Mat src, Rect[] rois)> _ocrTaskQueue = new();
    private readonly List<DriveDiscExport> _ocrResults = new();

    public GameHelper()
    {
        _config = Config.Load();
        _windowHelper = new WindowHelper(_config.ProcessName);
        _recognizer = new Recognizer(_config.ModelFile, _config.CharacterDictFile);
        _textCleaner = new TextCleaner(_config);
    }

    public void Dispose()
    {
        _recognizer.Dispose();
        Config.Save(_config);
    }

    public async Task StartScanner()
    {
        var dismantleColor = _config.Colors["拆解按钮"];
        var dismantle = _config.PointFs["拆解按钮"];
        var driveDiscStorage = _config.PointFs["驱动盘按钮"];
        var scrollBarTop = _config.PointFs["滚动条顶部"];

        _windowHelper.SetVisible(true);
        await Task.Delay(100);
        // 等待背包
        Console.WriteLine("等待背包界面");
        await WaitUntilAsync(() => _windowHelper.GetPixel(dismantle.X, dismantle.Y) == dismantleColor, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1), "等待背包超时");
        // 驱动盘页面
        _windowHelper.LeftClick(driveDiscStorage.X, driveDiscStorage.Y, 100);
        await Task.Delay(100);
        // 到第一行
        _windowHelper.LeftClick(scrollBarTop.X, scrollBarTop.Y, 100);
        await Task.Delay(100);

        Console.WriteLine("按ESC退出背包后停止扫描");
        var cts = new CancellationTokenSource();

        var monitor = MonitorUI(cts);
        var ocrTask = Task.Run(OcrTaskConsumer);
        var scan = Scan(cts.Token);

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

        // 结束添加任务
        _ocrTaskQueue.CompleteAdding();
        await Task.WhenAll(ocrTask, MonitorOcrTask());

        // 保存文件
        var json = JsonSerializer.Serialize(_ocrResults, Config.Option);
        var filename = $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.json";
        File.WriteAllText(filename, json);
        Console.WriteLine($"保存 {_ocrResults.Count} 个结果到 {filename}");
        Process.Start(filename);
    }

    public struct DriveDiscExport
    {
        [JsonPropertyName("名称")]
        public string Name { get; set; }
        [JsonPropertyName("槽位")]
        public int Slot { get; set; }
        [JsonPropertyName("等级")]
        public int Level { get; set; }
        [JsonPropertyName("最大等级")]
        public int MaxLevel { get; set; }
        [JsonPropertyName("主属性")]
        public Dictionary<string, object> MainStat { get; set; } = new();
        [JsonPropertyName("副属性")]
        public List<Dictionary<string, object>> SubStats { get; set; } = new();

        public DriveDiscExport()
        {
            Name = default;
            Slot = default;
            Level = default;
            MaxLevel = default;
        }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
    }

    private async Task MonitorOcrTask()
    {
        while (_ocrTaskQueue.Count > 0)
        {
            Console.WriteLine($"等待 {_ocrTaskQueue.Count} 个OCR任务完成");
            await Task.Delay(500);
        }
    }

    // 识别任务消费者
    private void OcrTaskConsumer()
    {
        foreach (var (src, rois) in _ocrTaskQueue.GetConsumingEnumerable())
        {
            // using管理资源
            using var _ = src;
            var result = _recognizer.Recognize(src, rois);
            var id = _ocrResults.Count;
            Console.WriteLine($"识别结果 {id}: {result.SequenceToString()}");
            try
            {
                var export = _textCleaner.Clean(result);
                Console.WriteLine($"纠错结果 {id}: {export}");
                _ocrResults.Add(export);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"纠错失败 {id}: {result.SequenceToString()}");
                Console.Error.WriteLine(ex.Message);
                Console.Error.WriteLine(ex.StackTrace);
            }
        }
    }

    // 监控拆解按钮颜色，即检测用户是否退出了背包
    private async Task MonitorUI(CancellationTokenSource cts)
    {
        var dismantle = _config.PointFs["拆解按钮"];
        var dismantleColor = _config.Colors["拆解按钮"];

        while (!cts.IsCancellationRequested)
        {
            if (_windowHelper.GetPixel(dismantle.X, dismantle.Y) != dismantleColor)
            {
                cts.Cancel();
                break;
            }
            await Task.Delay(100);
        }
    }

    // 遍历驱动盘逻辑
    private async Task Scan(CancellationToken token)
    {
        var driveDiscOffset = _config.PointFs["驱动盘偏移"];
        var driveDiscSize = _config.PointFs["驱动盘间隔"];
        var scrollBarBottom = _config.PointFs["滚动条底部"];
        var statBackgroundOffset = _config.PointFs["属性背景偏移"];
        var driveDiscSColor = _config.Colors["S级"];
        var driveDiscAColor = _config.Colors["A级"];
        var driveDiscBColor = _config.Colors["B级"];
        var scrollBarColor = _config.Colors["滚动条"];
        var cardBackGroundColor = _config.Colors["驱动盘信息背景"];
        var statBackGroundColor = _config.Colors["属性条目背景"];
        var driveDiscCard = _config.DriveDiscRectangleFs["位置"];
        var (bx, by) = _windowHelper.ToAbsolute(driveDiscCard.X, driveDiscCard.Y);
        // 感兴趣区域，相对驱动盘信息位置
        var rois = _config.DriveDiscRectangleFs.Values.Skip(1).Select(item =>
        {
            var (x, y, w, h) = _windowHelper.ToAbsolute(item.X, item.Y, item.Width, item.Height);
            return new Rect(x - bx, y - by, w, h);
        }).ToArray();
        // 词条属性背景偏移
        var (statBackgroundOffsetX, statBackgroundOffsetY) = _windowHelper.ToAbsolute(statBackgroundOffset.X, statBackgroundOffset.Y, false);

        // 遍历驱动盘，总共4行，1、2、4行正常扫描，第3行通过翻页扫描
        for (int row = 1; row <= 4; row++)
        {
            // 当前行
            var currY = driveDiscOffset.Y + driveDiscSize.Y * row;
            // 从左到右依次点击
            for (int col = 1; col <= 9; col++)
            {
                var sw = Stopwatch.StartNew();
                // 当前列
                var currX = driveDiscOffset.X + driveDiscSize.X * col;
                _windowHelper.LeftClick(currX, currY);

                // 检测驱动盘品质颜色，即当前点击是否为空格子
                var color = _windowHelper.GetPixel(currX, currY);
                if (color != driveDiscSColor && color != driveDiscAColor && color != driveDiscBColor)
                {
                    // 扫描结束或用户退出操作
                    token.ThrowIfCancellationRequested();
                    return;
                }

                //检测驱动盘等级区域的背景色，即是否加载完成
                await Task.Delay(100, token);
                // 最慢的地方，100ms不能保证完全加载出来驱动盘信息，至少得200ms
                // await WaitUntilAsync(() => _windowHelper.GetPixel(driveDiscRois[1].X, driveDiscRois[1].Y) == statBackGroundColor, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(0.1), "驱动盘加载超时", token);

                // 截取信息
                using var image = _windowHelper.GetImage(driveDiscCard.X, driveDiscCard.Y, driveDiscCard.Width, driveDiscCard.Height);

                // 确定副词条数量
                int k = 0;
                foreach (var item in rois)
                {
                    // 名称、等级、主属性必有，或者有副属性
                    if (k <= 3 || image.GetPixel(item.X + statBackgroundOffsetX, item.Y + statBackgroundOffsetY) != cardBackGroundColor)
                    {
                        k++;
                    }
                    else
                    {
                        break;
                    }
                }

                // 加入处理队列不等待完成
                _ocrTaskQueue.Add((image.ToMat(), rois.Take(k).ToArray()));
                Console.WriteLine($"本次耗时: {sw.ElapsedMilliseconds,4}ms, 任务队列: {_ocrTaskQueue.Count,4}");
            }
            // 是第3行，且有下一行自动翻页
            if (row == 3 && _windowHelper.GetPixel(scrollBarBottom.X, scrollBarBottom.Y) != scrollBarColor)
            {
                // 向下滚动
                _windowHelper.MouseWheel(-120);
                // 保持在第3行
                row--;
                await Task.Delay(500, token);
            }
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, TimeSpan interval, string message = "超时", CancellationToken token = default)
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
        throw new TimeoutException(message);
    }
}
