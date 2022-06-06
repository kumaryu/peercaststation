// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System.IO;
using System.Collections.Generic;
using System.Threading;
using PeerCastStation.Core;
using System.Threading.Tasks;

namespace PeerCastStation.HTTP
{
  /// <summary>
  /// ストリームからHTTPリクエストを読み取るクラスです
  /// </summary>
  public static class HTTPRequestReader
  {
    /// <summary>
    /// ストリームからHTTPリクエストを読み取り解析します
    /// </summary>
    /// <param name="stream">読み取り元のストリーム</param>
    /// <returns>解析済みHTTPRequest</returns>
    public static HTTPRequest? Read(Stream stream)
    {
      string? line = null;
      var requests = new List<string>();
      var buf = new List<byte>();
      while (line!="") {
        var value = stream.ReadByte();
        if (value<0) return null;
        buf.Add((byte)value);
        if (buf.Count >= 2 && buf[buf.Count - 2] == '\r' && buf[buf.Count - 1] == '\n') {
          line = System.Text.Encoding.UTF8.GetString(buf.ToArray(), 0, buf.Count - 2);
          if (line!="") requests.Add(line);
          buf.Clear();
        }
      }
      if (HTTPRequest.TryParse(requests, out var req)) {
        return req;
      }
      else {
        return null;
      }
    }

    public static async Task<HTTPRequest?> ReadAsync(Stream stream, CancellationToken cancel_token)
    {
      string? line = null;
      var requests = new List<string>();
      var buf = new List<byte>();
      while (line!="") {
        var value = await stream.ReadByteAsync(cancel_token).ConfigureAwait(false);
        if (value<0) return null;
        buf.Add((byte)value);
        if (buf.Count >= 2 && buf[buf.Count - 2] == '\r' && buf[buf.Count - 1] == '\n') {
          line = System.Text.Encoding.UTF8.GetString(buf.ToArray(), 0, buf.Count - 2);
          if (line!="") requests.Add(line);
          buf.Clear();
        }
      }
      if (HTTPRequest.TryParse(requests, out var req)) {
        return req;
      }
      else {
        return null;
      }
    }

  }

}

