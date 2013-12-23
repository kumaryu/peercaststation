using System.Collections.Generic;
using PeerCastStation.Core;

namespace PeerCastStation.PCP
{
  internal static class StreamConnectionExtension
  {
    public static IEnumerable<Atom> RecvAtoms(this StreamConnection connection)
    {
      var res = new Queue<Atom>();
      connection.Recv(s => {
        while (s.Position<s.Length) {
          var pos = s.Position;
          try {
            var atom = AtomReader.Read(s);
            res.Enqueue(atom);
          }
          catch (System.IO.EndOfStreamException) {
            s.Position = pos;
            break;
          }
        }
      });
      return res;
    }
  }
}
