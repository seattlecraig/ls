/*
 * program.cs 
 * 
 * replicates the unix LS command
 * 
 *  Date        Author          Description
 *  ====        ======          ===========
 *  06-26-25    Craig           initial implementaton
 *
 */
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace LS
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
        static bool exactSize = false;

        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            if (OperatingSystem.IsWindows()) EnableVirtualTerminal();

            /*
             * Parse command line arguments and get the files to process.
             */
            var targets = new List<string>();
            ParseArgs(args, targets);
            var finalTargets = ExpandWildcards(targets);

            if (finalTargets.Count == 0)
                finalTargets.Add(Directory.GetCurrentDirectory());

            /*
             * process each target
             */
            foreach (var target in finalTargets)
            {
                if (File.Exists(target))
                {
                    var entries = new List<string> { target };
                    if (longFormat) PrintLongEntries(entries);
                    else PrintEntries(entries);
                    continue;
                }

                if (!Directory.Exists(target))
                {
                    Console.Error.WriteLine($"ls: cannot access '{target}': No such file or directory");
                    continue;
                }

                if (finalTargets.Count > 1)
                    Console.WriteLine($"\n{target}:");

                ListEntries(target);
            }
        } /* Main() */

        /*
         * ParseArgs
         * 
         * Parse command line arguments and collect targets.
         */
        static void ParseArgs(string[] args, List<string> targets)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("-") && arg.Length > 1)
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
                            case 'x': exactSize = true; break;
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
        } /* ParseArgs */

        /*
         * ExpandWildcards
         * 
         * Expand wildcards in the input list to actual file paths.
         * If a pattern contains '*' or '?', it will be expanded to match files in the current directory.
         */
        static List<string> ExpandWildcards(List<string> inputs)
        {
            var output = new List<string>();
            foreach (var pattern in inputs)
            {
                if (pattern.Contains("*") || pattern.Contains("?"))
                {
                    string dir = Path.GetDirectoryName(pattern);
                    if (string.IsNullOrEmpty(dir)) dir = Directory.GetCurrentDirectory();
                    string mask = Path.GetFileName(pattern);
                    var matches = Directory.GetFileSystemEntries(dir, mask, SearchOption.TopDirectoryOnly);
                    output.AddRange(matches);
                }
                else
                {
                    output.Add(pattern);
                }
            }
            return output;
        } /* ExpandWildcards */

        /*
         * ShowHelp
         * 
         * Display the help message for the ls command.
         */
        static void ShowHelp()
        {
            Console.WriteLine(@"
Usage: ls [-?aRrst1lx] [files/directories/wildcards]
Options:
  -?       : display this message
  -a       : show hidden files
  -R       : recursive
  -r       : reverse sort
  -s       : sort by size
  -t       : sort by modified time
  -1       : one item per line
  -l       : long listing format
  -x       : show exact byte sizes (default: human-readable with commas)
");
        } /* ShowHelp */

        /*
         * ListEntries
         * 
         * List the entries in the specified directory, applying filters and formatting as needed.
         */
        static void ListEntries(string path)
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

                var sorted = SortEntries(entries.ToList());

                if (longFormat) PrintLongEntries(sorted);
                else PrintEntries(sorted);

                if (recursive)
                {
                    var dirs = sorted.Where(Directory.Exists);
                    foreach (var dir in dirs)
                    {
                        Console.WriteLine($"\n{dir}:");
                        ListEntries(dir);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"ls: cannot open directory '{path}': Permission denied");
            }
        } /* ListEntries */

        /*
         * SortEntries
         * 
         * Sort the entries based on the specified criteria: size, time, or name.
         */
        static List<string> SortEntries(List<string> entries)
        {
            IOrderedEnumerable<string> ordered;

            if (sortBySize)
            {
                ordered = entries.OrderByDescending(p =>
                {
                    try { return Directory.Exists(p) ? 0 : new FileInfo(p).Length; }
                    catch { return -1; }
                });
            }
            else if (sortByTime)
            {
                ordered = entries.OrderByDescending(p =>
                {
                    try { return File.GetLastWriteTime(p); }
                    catch { return DateTime.MinValue; }
                });
            }
            else
            {
                ordered = entries.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);
            }

            var list = ordered.ToList();
            if (reverseSort) list.Reverse();
            return list;
        } /* SortEntries */

        /*
         * PrintEntries
         * 
         * print the entries in a formatted manner.
         */
        static void PrintEntries(List<string> entries)
        {
            if (entries.Count == 0) return;

            var names = entries.Select(Path.GetFileName).ToList();
            if (onePerLine)
            {
                foreach (var path in entries)
                    WriteName(path);
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
                            WriteName(entries[i], maxLen);
                    }
                    Console.WriteLine();
                }
            }
        } /* PrintEntries */

        /*
         * PrintLongEntries
         * 
         * Print the entries in long format, showing permissions, size, modification time, and name.
         */
        static void PrintLongEntries(List<string> entries)
        {
            if (entries.Count == 0) return;

            int sizeWidth = exactSize
                ? entries.Select(e => Directory.Exists(e) ? 0L : new FileInfo(e).Length).Max().ToString().Length
                : entries.Select(e => FormatSizeWithUnit(Directory.Exists(e) ? 0L : new FileInfo(e).Length).Length).Max();

            sizeWidth = Math.Max(sizeWidth, 9); // reasonable min width

            foreach (var entry in entries)
            {
                var fi = new FileInfo(entry);
                var attrs = File.GetAttributes(entry);
                bool isDir = Directory.Exists(entry);

                string type = isDir ? "d" : "-";
                string h = attrs.HasFlag(FileAttributes.Hidden) ? "h" : "-";
                string s = attrs.HasFlag(FileAttributes.System) ? "s" : "-";
                string r = attrs.HasFlag(FileAttributes.ReadOnly) ? "r" : "w";
                string x = (entry.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                            entry.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                            entry.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase)) ? "x" : "-";

                string perms = $"{type}{h}{s}-{r}{x}-";
                long size = isDir ? 0 : fi.Length;

                string sizeStr = exactSize
                    ? size.ToString().PadLeft(sizeWidth)
                    : FormatSizeWithUnit(size).PadLeft(sizeWidth);

                string mod = fi.LastWriteTime.ToString("MMM dd yyyy  HH:mm");
                string name = Path.GetFileName(entry);


                string sizeColor = GetSizeColorGranular(size);
                Console.Write($"{perms}  {sizeColor}{sizeStr}\x1b[0m  {mod}  ");
                //Console.Write($"{perms}  {sizeStr}  {mod}  ");
                WriteColored(name, entry);
                Console.WriteLine();
            }
        } /* PrintLongEntries */

        /*
         * FormatSizeWithUnit
         * 
         * Format the size in bytes into a human-readable string with appropriate units.
         */
        static string FormatSizeWithUnit(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:##,##0.##} {units[unit]}";

        } /* FormatSizeWithUnit */

        /*
         * WriteName
         * 
         * Write the name of the file or directory with appropriate padding and color.
         */
        static void WriteName(string path, int pad = 0)
        {
            WriteColored(Path.GetFileName(path).PadRight(pad), path);
        }

        /*
         * WriteColored
         * 
         * write the text to the console with color based on the type of file or directory.
         */
        static void WriteColored(string text, string path)
        {
            if (Directory.Exists(path))
            {
                Console.Write("\x1b[34m"); // Blue
            }
            else if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase) ||
                     path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
            {
                Console.Write("\x1b[32m"); // Green
            }
            else if (File.GetAttributes(path).HasFlag(FileAttributes.Hidden))
            {
                Console.Write("\x1b[2m");  // Dim
            }
            else
            {
                Console.Write("\x1b[0m");  // Reset
            }

            Console.Write($"{text}\x1b[0m");

        } /* WriteColored */

        /*
         * GetSizeColorGranular
         * 
         * get the ANSI color escape code based on the size of the file
         */
        static string GetSizeColorGranular(long size)
        {
            // ANSI 256-color escape codes for orange = 208
            if (size >= 1L << 40) return "\x1b[91m";       // ≥ 1 TB → Bright Red
            if (size >= 100L << 30) return "\x1b[31m";      // ≥ 100 GB → Red
            if (size >= 10L << 30) return "\x1b[35m";      // ≥ 10 GB → Magenta
            if (size >= 1L << 30) return "\x1b[38;5;208m"; // ≥ 1 GB → Orange
            if (size >= 100L << 20) return "\x1b[32m";      // ≥ 100 MB → Green
            if (size >= 10L << 20) return "\x1b[36m";      // ≥ 10 MB → Cyan
            if (size >= 1L << 20) return "\x1b[34m";      // ≥ 1 MB → Blue
            return "\x1b[2m";                               // < 1 MB → Dim
        } /* GetSizeColorGranular */

        /*
         * EnableVirtualTerminal
         * 
         * Enable virtual terminal processing on Windows to allow ANSI escape codes for colors.
         */
        static void EnableVirtualTerminal()
        {
            const int STD_OUTPUT_HANDLE = -11;
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            GetConsoleMode(handle, out int mode);
            SetConsoleMode(handle, mode | 0x0004);

        } /* EnableVirtualTerminal */

        [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int nStdHandle);
        [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);
        [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr hConsoleHandle, int dwMode);
    }
}

