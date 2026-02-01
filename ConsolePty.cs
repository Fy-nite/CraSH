using System;
using Microsoft.Xna.Framework.Input;
using StarChart.PTY;

namespace CraSH
{
    // Simple PTY implementation that wires to the host console for testing.
    public class ConsolePty : IPty
    {
        public event EventHandler<char> OnInput;

        public ConsolePty()
        {
            // start background reader
            var t = new System.Threading.Thread(ReadLoop) { IsBackground = true };
            t.Start();
        }

        void ReadLoop()
        {
            while (true)
            {
                var key = Console.ReadKey(intercept: true);
                char ch;
                // Map special console keys to the PTY expected values
                if (key.Key == ConsoleKey.Enter) ch = '\n';
                else if (key.Key == ConsoleKey.Backspace) ch = '\b';
                else if (key.Key == ConsoleKey.Tab) ch = '\t';
                else ch = key.KeyChar;

                // Local echo for interactive console so users see typed chars immediately.
                try
                {
                    if (ch == '\n') Console.WriteLine();
                    else if (ch == '\b') Console.Write("\b \b");
                    else Console.Write(ch);
                }
                catch { }

                OnInput?.Invoke(this, ch);
            }
        }

        public void WriteToPty(char c)
        {
            Console.Write(c);
        }

        // No-op for console PTY
        public void Resize(int cols, int rows) { }

        // Support runtime callers that forward key events
        public void HandleKey(Microsoft.Xna.Framework.Input.Keys key, bool shift)
        {
            // Map a subset of Keys to chars and raise OnInput
            // Keep mapping simple: Enter/Back/Tab and printable via ToString fallback
            if (key == Microsoft.Xna.Framework.Input.Keys.Enter) { OnInput?.Invoke(this, '\n'); return; }
            if (key == Microsoft.Xna.Framework.Input.Keys.Back) { OnInput?.Invoke(this, '\b'); return; }
            if (key == Microsoft.Xna.Framework.Input.Keys.Tab) { OnInput?.Invoke(this, '\t'); return; }
            // For letters and digits, map to chars
            if (key >= Microsoft.Xna.Framework.Input.Keys.A && key <= Microsoft.Xna.Framework.Input.Keys.Z)
            {
                var baseChar = (char)('a' + (key - Microsoft.Xna.Framework.Input.Keys.A));
                OnInput?.Invoke(this, shift ? char.ToUpperInvariant(baseChar) : baseChar);
                return;
            }
            if (key >= Microsoft.Xna.Framework.Input.Keys.D0 && key <= Microsoft.Xna.Framework.Input.Keys.D9)
            {
                var ch = (char)('0' + (key - Microsoft.Xna.Framework.Input.Keys.D0));
                OnInput?.Invoke(this, ch);
                return;
            }
        }
    
        
    }
}
