namespace ZZZScanner.Helpers;

using Microsoft.VisualBasic;

public static class StringHelper
{
    public static float ToSingle(this string str, bool ignorePercent = true, bool toPercent = false)
    {
        if (ignorePercent)
        {
            str = str.TrimEnd().TrimEnd('%');
        }
        var value = float.Parse(str);
        return toPercent ? value / 100 : value;
    }

    // rename Contains
    public static bool ContainsChar(this string str, char ch)
    {
        return str.IndexOf(ch) >= 0;
    }

    public static string ToSimplifiedChinese(this string text, bool filterChar = false)
    {
        text = Strings.StrConv(text, VbStrConv.SimplifiedChinese);
        if (filterChar)
        {
            return string.Concat(text.Where(IsSimplifiedChinese));
        }
        return text;
    }

    public static bool IsSimplifiedChinese(char c)
    {
        // 基本汉字
        if (c >= '\u4E00' && c <= '\u9FFF')
        {
            return true;
        }

        // CJK扩展A区
        if (c >= '\u3400' && c <= '\u4DBF')
        {
            return true;
        }

        return false;
    }

    public static string BestMatch(IEnumerable<string> dict, string text, float factor = 0.5f)
    {
        var bestMatch = text;
        var minDis = text.Length;

        foreach (var item in dict)
        {
            var dis = LevenshteinDistance(text, item);
            if (dis < minDis)
            {
                minDis = dis;
                bestMatch = item;
            }
        }

        var maxLength = Math.Max(bestMatch.Length, text.Length);
        var f = 1.0f - (float)minDis / maxLength;
        return f >= factor ? bestMatch : text;
    }

    // 计算Levenshtein距离
    public static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;
        var d = new int[n + 1, m + 1];

        if (n == 0)
        {
            return m;
        }

        if (m == 0)
        {
            return n;
        }

        for (int i = 0; i <= n; d[i, 0] = i++)
        { }
        for (int j = 0; j <= m; d[0, j] = j++)
        { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                var cost = t[j - 1] == s[i - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[n, m];
    }
}
