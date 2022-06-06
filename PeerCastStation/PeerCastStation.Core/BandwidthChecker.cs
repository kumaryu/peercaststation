using System;
using System.Net;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Net.Security;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace PeerCastStation.Core
{
  public struct BandwidthCheckResult
  {
    public bool     Succeeded;
    public long     DataSize;
    public TimeSpan ElapsedTime;
    public long Bitrate {
      get { return (long)(DataSize*8 / ElapsedTime.TotalSeconds); }
    }
  }

  public class BandwidthChecker
  {
    public Uri Target { get; private set; }
    public AddressFamily AddressFamily { get; private set; }
    public BandwidthChecker(Uri target_uri, NetworkType networkType)
    {
      this.Target = target_uri;
      switch (networkType) {
      case NetworkType.IPv4:
        this.AddressFamily = AddressFamily.InterNetwork;
          break;
      case NetworkType.IPv6:
        this.AddressFamily = AddressFamily.InterNetworkV6;
        break;
      }
    }

    private byte[] CreateChunk(byte[] data)
    {
      var prefix = System.Text.Encoding.ASCII.GetBytes($"{data.Length.ToString("X")}\r\n");
      var postfix = System.Text.Encoding.ASCII.GetBytes("\r\n");
      var chunked_data = new byte[prefix.Length+data.Length+postfix.Length];
      Buffer.BlockCopy(prefix, 0, chunked_data, 0, prefix.Length);
      Buffer.BlockCopy(data, 0, chunked_data, prefix.Length, data.Length);
      Buffer.BlockCopy(postfix, 0, chunked_data, prefix.Length+data.Length, postfix.Length);
      return chunked_data;
    }

    private async Task<byte[]> PostAsync(Func<Stream,CancellationToken,Task> post_body, CancellationToken cancellationToken)
    {
      IPAddress? targetAddress;
      try {
        targetAddress = (await Dns.GetHostAddressesAsync(Target.DnsSafeHost).ConfigureAwait(false))
          .Where(addr => addr.AddressFamily == AddressFamily)
          .FirstOrDefault();
      }
      catch (SocketException e) {
        new Logger(typeof(BandwidthChecker)).Error(e);
        targetAddress = null;
      }
      if (targetAddress==null) {
        throw new WebException();
      }
      using (var client=new TcpClient(AddressFamily)) {
        client.NoDelay = true;
        client.ReceiveBufferSize = 256*1024;
        client.SendBufferSize = 256*1024;
        await client.ConnectAsync(targetAddress, Target.Port).ConfigureAwait(false);
        Stream stream = client.GetStream();
        if (Target.Scheme=="https") {
          var ssl = new SslStream(stream);
          stream = ssl;
          await ssl.AuthenticateAsClientAsync(Target.DnsSafeHost).ConfigureAwait(false);
        }
        var req = System.Text.Encoding.ASCII.GetBytes(
          $"POST {Target.PathAndQuery} HTTP/1.1\r\n" +
          $"Host:{Target.DnsSafeHost}\r\n" +
          $"User-Agent:PeerCastStation\r\n" +
          $"Connection:close\r\n" +
          $"Accept:application/json\r\n" +
          $"Accept-Encoding:\r\n" +
          $"Content-Type:application/octet-stream\r\n" +
          $"Transfer-Encoding:chunked\r\n" +
          "\r\n");
        await stream.WriteBytesAsync(req, cancellationToken).ConfigureAwait(false);
        await post_body(stream, cancellationToken).ConfigureAwait(false);
        await stream.WriteBytesAsync(CreateChunk(new byte[0]), cancellationToken).ConfigureAwait(false);

        using (var bufStream=new BufferedStream(stream, 8192)) {
          string? line = null;
          var responses = new List<string>();
          var buf = new List<byte>(8192);
          while (line!="") {
            var value = await bufStream.ReadByteAsync(cancellationToken).ConfigureAwait(false);
            if (value<0) throw new WebException();
            buf.Add((byte)value);
            if (buf.Count>=2 && buf[buf.Count-2]=='\r' && buf[buf.Count-1]=='\n') {
              line = System.Text.Encoding.ASCII.GetString(buf.ToArray(), 0, buf.Count-2);
              if (line!="") responses.Add(line);
              buf.Clear();
            }
          }
          var statusLine = responses.FirstOrDefault();
          if (statusLine==null) throw new WebException();
          var match = Regex.Match(statusLine, @"^HTTP/1.\d (\d+) .*$", RegexOptions.IgnoreCase);
          if (!match.Success) throw new WebException();
          var status = Int32.Parse(match.Groups[1].Value);
          if (status!=200) throw new WebException();
          var headers =
            responses
            .Skip(1)
            .Select(ln => {
              var sep = ln.IndexOf(':');
              if (sep<0) {
                return new KeyValuePair<string,string>(ln, "");
              }
              else {
                return new KeyValuePair<string,string>(ln.Substring(0, sep), ln.Substring(sep+1));
              }
            })
            .ToDictionary(kv => kv.Key.ToUpperInvariant(), kv => kv.Value);
          //var content_length = Int32.Parse(headers["CONTENT-LENGTH"]);
          //return await bufStream.ReadBytesAsync(content_length, ct);
          return new byte[0];
        }
      }
    }

    public async Task<BandwidthCheckResult> RunAsync(CancellationToken cancellationToken)
    {
      var result = new BandwidthCheckResult {
        Succeeded = false,
        DataSize = 0,
        ElapsedTime = TimeSpan.MaxValue,
      };
      var rand = new Random();
      var data = new byte[250*1024];
      rand.NextBytes(data);
      data = CreateChunk(data);
      var stopwatch = new System.Diagnostics.Stopwatch();
      try {
        long sz = 0;
        stopwatch.Start();
        await PostAsync(async (s, ct) => {
          while (stopwatch.ElapsedMilliseconds<10000) {
            await s.WriteBytesAsync(data, ct).ConfigureAwait(false);
            sz += data.Length;
          }
        }, cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();
        result.DataSize = sz;
        result.Succeeded = true;
        result.ElapsedTime = stopwatch.Elapsed;
      }
      catch (WebException) {
        result.Succeeded = false;
      }
      return result;
    }

    public BandwidthCheckResult Run()
    {
      return RunAsync(CancellationToken.None).Result;
    }
  }

}

