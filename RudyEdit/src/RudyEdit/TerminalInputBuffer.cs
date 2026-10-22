using System.Text;
namespace RudyEdit;

// Terminal input is a stream, even when a Windows host delivers it one
// character per INPUT_RECORD. Never expose a CSI/SS3 payload as document text.
public sealed class TerminalInputBuffer(Func<long>? clock = null)
{
    readonly Func<long> now = clock ?? (() => Environment.TickCount64);
    readonly StringBuilder pending = new();
    long since;
    bool pasting;
    public bool HasPending => pending.Length != 0 || pasting;
    public void Append(string text)
    {
        if (pending.Length == 0) since = now();
        pending.Append(text);
    }
    public KeyEvent? Read(bool flush = false)
    {
        if (pending.Length == 0) return null;
        string data = pending.ToString();
        if (pasting)
        {
            int end = data.IndexOf("\x1b[201~", StringComparison.Ordinal);
            if (end < 0) return null;
            pending.Remove(0, end + 6); pasting = false;
            return new("paste-text", data[..end], true);
        }
        if (data.StartsWith("\x1b[200~", StringComparison.Ordinal))
        {
            pending.Remove(0, 6); pasting = true; return Read(flush);
        }
        if (data[0] != '\x1b')
        {
            if (char.IsHighSurrogate(data[0]) && data.Length == 1 && !flush && now() - since < 100) return null;
            int len = char.IsHighSurrogate(data[0]) && data.Length > 1 && char.IsLowSurrogate(data[1]) ? 2 : 1;
            string value = data[..len]; pending.Remove(0, len);
            return len == 2 ? new(value, value) : char.IsSurrogate(value[0]) ? new("unknown") : InputParser.Character(value[0]);
        }
        if (data.Length == 1)
        {
            if (!flush && now() - since < 60) return null;
            pending.Remove(0, 1); return new("esc");
        }
        if (data[1] is '[' or 'O')
        {
            for (int i = 2; i < data.Length; i++)
            {
                if (data[i] == '\x1b') { pending.Remove(0, i); return new("unknown"); }
                if (data[i] is >= '@' and <= '~')
                {
                    string seq = data[1..(i + 1)]; pending.Remove(0, i + 1);
                    return InputParser.EscapeSequence(seq);
                }
            }
            if (!flush && now() - since < 100 && data.Length < 4096) return null;
            pending.Clear(); return new("unknown");
        }
        if (data[1] == '\x1b') { pending.Remove(0, 1); return new("esc"); }
        pending.Remove(0, 2); return InputParser.EscapeSequence(data[1].ToString());
    }
}
