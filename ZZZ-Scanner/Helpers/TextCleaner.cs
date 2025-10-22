namespace ZZZScanner.Helpers;

public class TextCleaner
{
    private readonly Config _config;

    public TextCleaner(Config config)
    {
        _config = config;
    }

    public GameHelper.DriveDiscExport Clean(List<Recognizer.OcrResult> result)
    {
        var export = new GameHelper.DriveDiscExport();

        for (int i = 0; i < result.Count; i++)
        {
            var (_, Text) = result[i];

            switch (i)
            {
                case 0: // 名称
                    (export.Name, export.Slot) = CleanName(Text);
                    break;
                case 1: // 等级
                    (export.Level, export.MaxLevel) = CleanLevel(Text);
                    break;
                case 2: // 主属性
                {
                    var key = CleanMainStat(Text, export.Slot);
                    var (_, Text2) = result[i + 1];
                    var value = CleanMainStatValue(Text2, key, export.MaxLevel);
                    export.MainStat[key] = value;
                    i++;
                    break;
                }
                case 4: // 副属性1
                case 6: // 副属性2
                case 8: // 副属性3
                case 10: // 副属性4
                {
                    var key = CleanSubStat(Text);
                    var (_, Text2) = result[i + 1];
                    var value = CleanSubStatValue(Text2, key, export.MaxLevel);
                    export.SubStats.Add(new() { { key, value } });
                    i++;
                    break;
                }
            }
        }

        return export;
    }

    private (string Name, int Slot) CleanName(string name)
    {
        var slot = name.FirstOrDefault(c => char.IsDigit(c) && "123456".ContainsChar(c));
        name = name.ToSimplifiedChinese(true);
        var match = StringHelper.BestMatch(_config.DriveDiscDict, name);
        return (match, slot == default ? 0 : slot - '0');
    }

    private (int Level, int MaxLevel) CleanLevel(string level)
    {
        var separator = '/';
        // 只保留数字和'/'
        level = string.Concat(level.Where(c => c == separator || char.IsDigit(c)));

        if (level.Length == 5 && level[2] == separator)
        {
            // 正确结果
        }
        else if (level.Length == 6 && level[3] == separator)
        {
            // 识别成 115/15 这种
            level = level.Substring(1);
        }
        else
        {
            // 其他可能直接根据字典相似度匹配
            level = StringHelper.BestMatch(_config.DriveDiscLevelDict, level);
        }

        var parts = level.Split(new[] { separator }, 2);
        return (int.Parse(parts[0]), int.Parse(parts[1]));
    }

    private string CleanMainStat(string stat, int slot)
    {
        stat = stat.ToSimplifiedChinese(true);
        var dict = _config.MainStatFlatDict;
        if (1 <= slot && slot <= 6)
        {
            dict = _config.MainStatDict[slot - 1];
        }
        return StringHelper.BestMatch(dict, stat);
    }

    private object CleanMainStatValue(string value, string stat, int maxLevel)
    {
        var rarity = maxLevel switch
        {
            15 => 0,
            12 => 1,
            9 => 2,
            _ => throw new ArgumentException($"错误的等级 {maxLevel}", nameof(maxLevel))
        };
        var isPercent = value.ContainsChar('%');
        if (isPercent)
        {
            stat = $"{stat}%";
        }
        var dict = _config.MainStatValueDict[rarity][stat].All;
        var match = StringHelper.BestMatch(dict, value);
        return isPercent ? match : float.Parse(match);
    }

    private string CleanSubStat(string name)
    {
        name = name.ToSimplifiedChinese(true);
        var match = StringHelper.BestMatch(_config.SubStatDict, name);
        return match;
    }

    private object CleanSubStatValue(string value, string stat, int maxLevel)
    {
        var rarity = maxLevel switch
        {
            15 => 0,
            12 => 1,
            9 => 2,
            _ => throw new ArgumentException($"错误的等级 {maxLevel}", nameof(maxLevel))
        };
        var isPercent = value.ContainsChar('%');
        if (isPercent)
        {
            stat = $"{stat}%";
        }
        var dict = _config.SubStatValueDict[rarity][stat].All;
        var match = StringHelper.BestMatch(dict, value);
        return isPercent ? match : float.Parse(match);
    }
}
