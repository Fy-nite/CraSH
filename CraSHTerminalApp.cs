using System;
using System.Collections.Generic;
using System.Text;
using StarChart.Plugins;
using StarChart.PTY;
using Adamantite.VFS;

namespace CraSH
{
    // Custom external shell/terminal app for CraSH
    public class CraSHTerminalApp : IStarChartApp
    {
        private readonly IPty _pty;
        private readonly VfsManager? _vfs;
        private PluginContext? _ctx;
        private bool _running = false;
        private List<string> _history = new List<string>();
        private int _historyIndex = -1;
        private const int _historyMax = 1000;
        private const string CrashHistoryPath = "/home/.crash_history";
        private const string CrashRcPath = "/home/.crashrc";

        public CraSHTerminalApp(IPty pty, VfsManager? vfs = null)
        {
            _pty = pty ?? throw new ArgumentNullException(nameof(pty));
            _vfs = vfs;
        }

        public StarChart.stdlib.W11.Window? MainWindow => null;

        public void Initialize(PluginContext context)
        {
            _ctx = context;
        }

        public void Start()
        {
            _running = true;
            LoadHistory();
            LoadRc();
            PrintWelcome();
            RunTerminalLoop();
        }

        public void Stop()
        {
            SaveHistory();
            _running = false;
        }

        private void LoadHistory()
        {
            try
            {
                var vfs = _vfs ?? _ctx?.VFS;
                if (vfs != null && vfs.Exists(CrashHistoryPath))
                {
                    var bytes = vfs.ReadAllBytes(CrashHistoryPath);
                    var text = Encoding.UTF8.GetString(bytes);
                    var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    _history = new List<string>(lines);
                }
            }
            catch { }
        }

        private void SaveHistory()
        {
            try
            {
                var vfs = _vfs ?? _ctx?.VFS;
                if (vfs != null)
                {
                    // keep only last _historyMax entries
                    var lines = _history.Count > _historyMax ? _history.GetRange(_history.Count - _historyMax, _historyMax) : new List<string>(_history);
                    var text = string.Join('\n', lines) + '\n';
                    vfs.WriteAllBytes(CrashHistoryPath, Encoding.UTF8.GetBytes(text));
                }
            }
            catch { }
        }

        private void LoadRc()
        {
            try
            {
                var vfs = _vfs ?? _ctx?.VFS;
                if (vfs != null && vfs.Exists(CrashRcPath))
                {
                    var bytes = vfs.ReadAllBytes(CrashRcPath);
                    var text = Encoding.UTF8.GetString(bytes);
                    var lines = text.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var raw in lines)
                    {
                        var line = raw.Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                        if (line.StartsWith("#")) continue;
                        // execute rc command silently
                        HandleCommand(line);
                    }
                }
            }
            catch { }
        }

        private void PrintWelcome()
        {
            _pty.WriteToPty('\n');
            _pty.WriteToPty('C'); _pty.WriteToPty('r'); _pty.WriteToPty('a'); _pty.WriteToPty('S'); _pty.WriteToPty('H');
            _pty.WriteToPty(' '); _pty.WriteToPty('T'); _pty.WriteToPty('e'); _pty.WriteToPty('r'); _pty.WriteToPty('m'); _pty.WriteToPty('i'); _pty.WriteToPty('n'); _pty.WriteToPty('a'); _pty.WriteToPty('l');
            _pty.WriteToPty('\n');
            _pty.WriteToPty('>');
        }

        private void RunTerminalLoop()
        {
            string buffer = "";
            _pty.OnInput += (s, c) =>
            {
                if (!_running) return;
                if (c == '\n')
                {
                    HandleCommand(buffer);
                    buffer = "";
                    _pty.WriteToPty('>');
                }
                else if (c == '\b')
                {
                    if (buffer.Length > 0) buffer = buffer.Substring(0, buffer.Length - 1);
                }
                else
                {
                    buffer += c;
                }
            };
            // Block until stopped
            while (_running) System.Threading.Thread.Sleep(50);
        }

        private void HandleCommand(string cmd)
        {
            var CMD = (cmd ?? "").Trim();
            if (string.IsNullOrEmpty(CMD)) return;

            // history recall: !! or !N
            if (CMD == "!!")
            {
                if (_history.Count > 0) CMD = _history[_history.Count - 1];
                else { PrintToPty("No history\n"); return; }
            }
            else if (CMD.StartsWith("!"))
            {
                if (int.TryParse(CMD.Substring(1), out var idx))
                {
                    if (idx >= 1 && idx <= _history.Count)
                        CMD = _history[idx - 1];
                    else { PrintToPty("History index out of range\n"); return; }
                }
            }

            // add to history
            _history.Add(CMD);
            if (_history.Count > _historyMax) _history.RemoveAt(0);
            _historyIndex = -1;

            var args = CMD.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (args.Length == 0) return;
            var command = args[0].ToLowerInvariant();
            switch (command)
            {
                case "help":
                    PrintToPty("Available commands:\n");
                    PrintToPty("  help                Show this help message\n");
                    PrintToPty("  echo [msg]          Print a message\n");
                    PrintToPty("  cat [file]          Print file contents\n");
                    PrintToPty("  exit                Exit terminal\n");
                    PrintToPty("  add [a] [b]         Add two numbers\n");
                    PrintToPty("  history             Show command history\n");
                    PrintToPty("  !N                  Run Nth history entry\n");
                    break;
                case "echo":
                    if (args.Length > 1)
                        PrintToPty(string.Join(' ', args, 1, args.Length - 1) + "\n");
                    else
                        PrintToPty("Usage: echo [message]\n");
                    break;
                case "cat":
                    if (args.Length > 1)
                    {
                        var file = args[1];
                        try
                        {
                            var vfs = _vfs ?? _ctx?.VFS;
                            if (vfs != null && vfs.Exists(file))
                            {
                                var bytes = vfs.ReadAllBytes(file);
                                PrintToPty("\n");
                                foreach (var bs in bytes) PrintToPty(((char)bs).ToString());
                                PrintToPty("\n");
                            }
                            else
                                PrintToPty("File not found\n");
                        }
                        catch { PrintToPty("Error reading file\n"); }
                    }
                    else
                        PrintToPty("Usage: cat [file]\n");
                    break;
                case "exit":
                    PrintToPty("Bye!\n");
                    Stop();
                    break;
                case "add":
                    if (args.Length == 3 && int.TryParse(args[1], out var a) && int.TryParse(args[2], out var b))
                        PrintToPty($"{a} + {b} = {a + b}\n");
                    else
                        PrintToPty("Usage: add [a] [b]\n");
                    break;
                case "history":
                    for (int i = 0; i < _history.Count; i++)
                        PrintToPty($"{i + 1}: {_history[i]}\n");
                    break;
                default:
                    PrintToPty("Unknown command. Type 'help' for a list.\n");
                    break;
            }
        }
        private void PrintToPty(string msg)
        {
            foreach (var c in msg)
                _pty.WriteToPty(c);
        }
    }
}