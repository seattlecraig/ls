/*
 * program.cs 
 * 
 * replicates the unix LS command
 * 
 *  Date        Author          Description
 *  ====        ======          ===========
 *  06-26-25    Craig           initial implementation
 *
 */
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace LSReplica
{
    class Program
    {
        static bool showHidden = false;
        static bool recursive = false;
        static bool onePerLine = false;
        static bool reverseSort = false;
        static bool sortBySize = false;
        static bool sortByTime = false;
        static bool longFormat = false;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Enable ANSI support in Windows Terminal / VS Code Terminal
            if (OperatingSystem.IsWindows())
                EnableVirtualTerminal();

            var targets = new List<string>();
            ParseArgs(args, out targets);

            if (targets.Count == 0)
                targets.Add(Directory.GetCurrentDirectory());

            foreach (var target in targets)
            {
                if (!Directory.Exists(target))
                {
                    Console.Error.WriteLine($"ls: cannot access '{target}': No such directory");
                    continue;
                }

                if (targets.Count > 1)
                    Console.WriteLine($"\n{target}:");

                ListEntries(target, recursive);
            }
        }

        static void ParseArgs(string[] args, out List<string> targets)
        {
            targets = new List<string>();
            foreach (var arg in args)
            {
                if (arg.StartsWith("-"))
                {
                    foreach (var ch in arg.Skip(1))
                    {
                        switch (ch)
                        {
                            case '?': ShowHelp(); Environment.Exit(0); break;
                            case 'a': showHidden = true; break;
                            case 'R': recursive = true; break;
                            case 'r': reverseSort = true; break;
                            case 's': sortBySize = true; break;
                            case 't': sortByTime = true; break;
                            case '1': onePerLine = true; break;
                            case 'l': longFormat = true; break;
                            default:
                                Console.Error.WriteLine($"ls: invalid option -- '{ch}'");
                                Environment.Exit(1);
                                break;
                        }
                    }
                }
                else
                {
                    targets.Add(arg);
                }
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine(@"
Usage: ls [-?aRrst1l] [directories]
Options:
  -?       : display this message
  -a       : show hidden files
  -R       : recursive ls
  -r       : reverse sort
  -s       : sort by size (largest first)
  -t       : sort by modified time (most recent first)
  -1       : one entry per line
  -l       : long listing format (size, date, perms)
");
        }

        static void ListEntries(string path, bool recurse)
        {
            try
            {
                var entries = Directory.EnumerateFileSystemEntries(path).Where(entry =>
                {
                    if (!showHidden)
                    {
                        string name = Path.GetFileName(entry);
                        if (name.StartsWith(".") || (File.GetAttributes(entry) & FileAttributes.Hidden) != 0)
                            return false;
                    }
                    return true;
                });

                var sortedEntries = SortEntries(entries.ToList());

                if (longFormat)
                    PrintLongEntries(sortedEntries);
                else
                    PrintEntries(sortedEntries);

                if (recurse)
                {
                    var dirs = sortedEntries.Where(Directory.Exists);
                    foreach (var dir in dirs)
                    {
                        Console.WriteLine($"\n{dir}:");
                        ListEntries(dir, true);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"ls: cannot open directory '{path}': Permission denied");
            }
        }

        static List<string> SortEntries(List<string> entries)
        {
            IOrderedEnumerable<string> ordered;

            if (sortBySize)
            {
                ordered = entries.OrderByDescending(p =>
                {
                    try
                    {
                        return Directory.Exists(p) ? 0 : new FileInfo(p).Length;
                    }
                    catch { return -1; }
                });
            }
            else if (sortByTime)
            {
                ordered = entries.OrderByDescending(p =>
                {
                    try
                    {
                        return File.GetLastWriteTime(p);
                    }
                    catch { return DateTime.MinValue; }
                });
            }
            else
            {
                ordered = entries.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            }

            var list = ordered.ToList();
            if (reverseSort)
                list.Reverse();

            return list;
        }

        static void PrintEntries(List<string> entries)
        {
            if (entries.Count == 0) return;

            var names = entries.Select(Path.GetFileName).ToList();

            if (onePerLine)
            {
                foreach (var path in entries)
                {
                    WriteName(path);
                }
            }
            else
            {
                int maxLen = names.Max(n => n.Length) + 2;
                int cols = Math.Max(1, Console.WindowWidth / maxLen);
                int rows = (int)Math.Ceiling(names.Count / (double)cols);

                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        int i = c * rows + r;
                        if (i < names.Count)
                        {
                            WriteName(entries[i], maxLen);
                        }
                    }
                    Console.WriteLine();
                }
            }
        }

        static void PrintLongEntries(List<string> entries)
        {
            long totalSize = 0;
            int maxSizeLen = entries
                .Select(e => Directory.Exists(e) ? 0L : new FileInfo(e).Length)
                .Max()
                .ToString().Length;

            Console.WriteLine($"total {entries.Count}");

            foreach (var entry in entries)
            {
                var fi = new FileInfo(entry);
                var attrs = File.GetAttributes(entry);
                bool isDir = Directory.Exists(entry);

                string type = isDir ? "d" : "-";
                string h = attrs.HasFlag(FileAttributes.Hidden) ? "h" : "-";
                string s = attrs.HasFlag(FileAttributes.System) ? "s" : "-";
                string r = attrs.HasFlag(FileAttributes.ReadOnly) ? "r" : "w";
                string x = (entry.EndsWith(".exe") || entry.EndsWith(".bat") || entry.EndsWith(".cmd")) ? "x" : "-";

                string perms = $"{type}{h}{s}-{r}{x}-";

                long size = isDir ? 0 : fi.Length;
                totalSize += size;
                string sizeStr = size.ToString().PadLeft(maxSizeLen);

                string mod = File.GetLastWriteTime(entry).ToString("MMM dd yyyy  HH:mm");
                WriteColored($"{perms} {sizeStr}  {mod}  {Path.GetFileName(entry)}", entry);
                Console.WriteLine();
            }
        }

        static void WriteName(string path, int pad = 0)
        {
            WriteColored(Path.GetFileName(path).PadRight(pad), path);
        }

        static void WriteColored(string text, string path)
        {
            if (Directory.Exists(path))
                Console.Write("\x1b[34m"); // Blue
            else if (path.EndsWith(".exe") || path.EndsWith(".bat") || path.EndsWith(".cmd"))
                Console.Write("\x1b[32m"); // Green
            else if (File.GetAttributes(path).HasFlag(FileAttributes.Hidden))
                Console.Write("\x1b[2m");  // Dim
            else
                Console.Write("\x1b[0m");  // Reset

            Console.Write(text);
            Console.Write("\x1b[0m");
        }

        static void EnableVirtualTerminal()
        {
            const int STD_OUTPUT_HANDLE = -11;
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            GetConsoleMode(handle, out int mode);
            SetConsoleMode(handle, mode | 0x0004); // ENABLE_VIRTUAL_TERMINAL_PROCESSING
        }

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

        [System.Runtime.InteropServices.DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, int dwMode);
    }
}

