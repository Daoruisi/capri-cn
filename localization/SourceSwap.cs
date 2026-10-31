using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Replaces exact C# string literals in the app's source with their Chinese versions.
internal static class SourceSwap
{
    private static string Escape(string text)
    {
        // The dump already carries \n and \t as escape sequences; only quotes need escaping for C# source.
        return text.Replace("\"", "\\\"");
    }

    private static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("usage: swap.exe <appDir> <map.tsv>"); return 2; }
        string dir = Path.GetFullPath(args[0]);
        Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(args[1], Encoding.UTF8))
        {
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int tab = line.IndexOf('\t');
            if (tab <= 0) continue;
            map[line.Substring(0, tab)] = line.Substring(tab + 1);
        }

        Dictionary<string, int> hits = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (string key in map.Keys) hits[key] = 0;

        foreach (string file in Directory.GetFiles(dir, "*.cs"))
        {
            string text = File.ReadAllText(file, Encoding.UTF8);
            string original = text;
            foreach (KeyValuePair<string, string> entry in map)
            {
                string from = "\"" + Escape(entry.Key) + "\"";
                int at = text.IndexOf(from, StringComparison.Ordinal);
                while (at >= 0)
                {
                    hits[entry.Key]++;
                    at = text.IndexOf(from, at + 1, StringComparison.Ordinal);
                }
                if (hits[entry.Key] > 0) text = text.Replace(from, "\"" + Escape(entry.Value) + "\"");
            }
            if (!ReferenceEquals(text, original) && text != original)
            {
                File.WriteAllText(file, text, new UTF8Encoding(true));
                Console.WriteLine("changed " + Path.GetFileName(file));
            }
        }

        int missing = 0;
        foreach (KeyValuePair<string, string> entry in map)
        {
            if (hits[entry.Key] == 0)
            {
                missing++;
                Console.WriteLine("NOT FOUND: " + entry.Key.Substring(0, Math.Min(70, entry.Key.Length)));
            }
        }
        Console.WriteLine("done. strings in map: " + map.Count + ", not found: " + missing);
        return 0;
    }
}
