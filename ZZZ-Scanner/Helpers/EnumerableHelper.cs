namespace ZZZScanner.Helpers;

using System.Collections;
using System.Text;

public static class EnumerableHelper
{
    public static string SequenceToString(this IEnumerable es, string separator = ", ", string prefix = "[", string suffix = "]")
    {
        return $"{prefix}{string.Join(separator, es)}{suffix}";
    }

    public static string SequenceToString<T>(this IEnumerable<T> es, string separator = ", ", string prefix = "[", string suffix = "]")
    {
        return $"{prefix}{string.Join(separator, es)}{suffix}";
    }

    public static string SequenceToString<TKey, TValue>(this IEnumerable<KeyValuePair<TKey, TValue>> es, string separator = ", ", string prefix = "{", string suffix = "}", string pairSeparator = "=")
    {
        var et = es.GetEnumerator();
        var sb = new StringBuilder();
        if (!et.MoveNext())
        {
            return sb.Append(prefix).Append(suffix).ToString();
        }

        sb.Append(prefix);
        while (true)
        {
            var curr = et.Current;
            sb.Append(curr.Key).Append(pairSeparator).Append(curr.Value);
            if (!et.MoveNext())
            {
                return sb.Append(suffix).ToString();
            }
            sb.Append(separator);
        }
    }

    public static void ForEach(this IEnumerable es, Action<object> action)
    {
        foreach (var item in es)
        {
            action(item);
        }
    }

    public static void ForEach<T>(this IEnumerable<T> es, Action<T> action)
    {
        foreach (var item in es)
        {
            action(item);
        }
    }
}
