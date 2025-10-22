namespace ZZZScanner;

using System.Collections;
using System.Drawing;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZZZScanner.Converters;
using ZZZScanner.Helpers;

public class Config
{
    // 基本配置
    public string ProcessName { get; set; } = "ZenlessZoneZero";
    public string ModelFile { get; set; } = "Resources/models/PP-OCRv5_mobile_rec_infer.onnx";
    public string CharacterDictFile { get; set; } = "Resources/models/characterDict.txt";
    public Point StandardScreen { get; set; } = new Point(1920, 1080);

    // 用于截图定位
    public Dictionary<string, Point> Points { get; set; } = new();
    public Dictionary<string, Color> Colors { get; set; } = new();
    public Dictionary<string, Rectangle> DriveDiscRectangles { get; set; } = new();

    // 用于OCR结果纠错
    public List<string> DriveDiscDict { get; set; } = new();
    public List<List<string>> MainStatDict { get; set; } = new();
    public List<Dictionary<string, StatValueRange>> MainStatValueDict { get; set; } = new();
    public List<string> SubStatDict { get; set; } = new();
    public List<Dictionary<string, StatValueRange>> SubStatValueDict { get; set; } = new();

    [JsonIgnore]
    public List<string> DriveDiscLevelDict { get; set; } = new();
    [JsonIgnore]
    public List<string> MainStatFlatDict { get; set; } = new();
    // 除以StandardScreen后得到的百分比位置
    [JsonIgnore]
    public Dictionary<string, PointF> PointFs { get; private set; } = new();
    [JsonIgnore]
    public Dictionary<string, RectangleF> DriveDiscRectangleFs { get; private set; } = new();

    public void SetProperties()
    {
        var (fw, fh) = ((float)StandardScreen.X, (float)StandardScreen.Y);
        foreach (var item in Points)
        {
            var x = item.Value.X / fw;
            var y = item.Value.Y / fh;
            var pf = new PointF(x, y);
            PointFs[item.Key] = pf;
        }
        foreach (var item in DriveDiscRectangles)
        {
            var x = item.Value.X / fw;
            var y = item.Value.Y / fh;
            var w = item.Value.Width / fw;
            var h = item.Value.Height / fh;
            var rf = new RectangleF(x, y, w, h);
            DriveDiscRectangleFs[item.Key] = rf;
        }
        // 生成 xx/09 xx/12 xx/15 字典
        for (int i = 9; i <= 15; i += 3)
        {
            for (int j = 0; j <= i; j++)
            {
                DriveDiscLevelDict.Add($"{j:D2}/{i:D2}");
            }
        }
        MainStatFlatDict.AddRange(MainStatDict.SelectMany(x => x));
    }

    public static readonly JsonSerializerOptions Option = new()
    {
        // 换行
        WriteIndented = true,
        // 尾随逗号
        AllowTrailingCommas = true,
        // 跳过注释
        ReadCommentHandling = JsonCommentHandling.Skip,
        // 允许的编码器
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        // 自定义转换器
        Converters = { new PointJsonConverter(), new RectangleJsonConverter(), new ColorJsonConverter(), new StatValueRangeJsonConverter() }
    };

    public static Config Load(string filename = "Resources/config.json")
    {
        var json = File.ReadAllText(filename);
        var config = JsonSerializer.Deserialize<Config>(json, Option) ?? throw new JsonException($"加载 {filename} 文件失败");
        config.SetProperties();
        return config;
    }

    public static void Save(Config config, string filename = "Resources/backup.json")
    {
        var json = JsonSerializer.Serialize(config, Option);
        File.WriteAllText(filename, json);
    }

    public struct StatValueRange : IEnumerable<string>
    {
        public float Start { get; }
        public float Step { get; }
        public float Stop { get; }

        [JsonIgnore]
        public bool IsPercent { get; }
        private string[] _all;
        [JsonIgnore]
        public string[] All => _all ??= this.ToArray(); // 缓存结果

        public StatValueRange(float start, float step, float stop)
        {
            Start = start;
            Step = step;
            Stop = stop;
            IsPercent = false;
            _all = null;
        }

        public StatValueRange(string start, string step, string stop)
        {
            Start = start.ToSingle();
            Step = step.ToSingle();
            Stop = stop.ToSingle();
            IsPercent = true;
            _all = null;
        }

        public override string ToString()
        {
            return All.SequenceToString(", ");
        }

        public IEnumerator<string> GetEnumerator()
        {
            for (var i = Start; i <= Stop; i += Step)
            {
                if (IsPercent)
                {
                    yield return $"{Math.Round(i, 1):F1}%"; // 百分比的都是1位小数
                }
                else
                {
                    yield return $"{(int)Math.Round(i)}"; // 都是整数显示
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
