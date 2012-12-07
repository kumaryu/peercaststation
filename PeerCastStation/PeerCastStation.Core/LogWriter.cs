using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace PeerCastStation.Core
{
  public class LogWriter
    : TextWriter
  {
    private Ringbuffer<string> lines;
    private System.Text.StringBuilder line = new System.Text.StringBuilder();

    public LogWriter(int capacity)
    {
      lines = new Ringbuffer<string>(capacity);
    }

    public override System.Text.Encoding Encoding
    {
      get { return System.Text.Encoding.Unicode; }
    }

    public IEnumerable<string> Lines {
      get { return lines.Concat(Enumerable.Repeat(line.ToString(), 1)); }
    }

    public override void Write(char value)
    {
      line.Append(value);
      var nl = NewLine;
      if (line.Length>=nl.Length && line.ToString(line.Length-nl.Length, nl.Length)==nl) {
        lines.Add(line.ToString(0, line.Length-nl.Length));
        line.Remove(0, line.Length);
      }
    }

    public override void WriteLine()
    {
      lines.Add(line.ToString());
      line.Remove(0, line.Length);
    }

    public override string ToString()
    {
      return String.Join(NewLine, Lines.ToArray());
    }

    public void Clear()
    {
      lines.Clear();
      line.Remove(0, line.Length);
    }
  }
}
