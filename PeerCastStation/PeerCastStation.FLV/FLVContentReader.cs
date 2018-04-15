using System;
using System.IO;
using PeerCastStation.Core;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.FLV
{
  internal class BadDataException : ApplicationException
  {
  }

  public class FLVContentReader
    : IContentReader
  {
    private static readonly Logger Logger = new Logger(typeof(FLVContentReader));
    public FLVContentReader(Channel channel)
    {
      this.Channel = channel;
    }

    public string Name { get { return "Flash Video (FLV)"; } }
    public Channel Channel { get; private set; }
    private FLVFileParser fileParser = new FLVFileParser();

    public Task ReadAsync(IContentSink sink, Stream stream, CancellationToken cancel_token)
    {
      var buffered_sink = new FLVContentBuffer(this.Channel, sink);
      return fileParser.ReadAsync(stream, buffered_sink, cancel_token);
    }
  }

  public class FLVContentReaderFactory
    : IContentReaderFactory
  {
    public string Name { get { return "Flash Video (FLV)"; } }

    public IContentReader Create(Channel channel)
    {
      return new FLVContentReader(channel);
    }

    public bool TryParseContentType(byte[] header, out string content_type, out string mime_type)
    {
      if (header.Length>=13 && header[0]=='F' && header[1]=='L' && header[2]=='V') {
        content_type = "FLV";
        mime_type    = "video/x-flv";
        return true;
      }
      else {
        content_type = null;
        mime_type    = null;
        return false;
      }
    }

  }

  [Plugin]
  public class FLVContentReaderPlugin
    : PluginBase
  {
    override public string Name { get { return "FLV Content Reader"; } }

    private FLVContentReaderFactory factory;
    override protected void OnAttach()
    {
      if (factory==null) factory = new FLVContentReaderFactory();
      Application.PeerCast.ContentReaderFactories.Add(factory);
    }

    override protected void OnDetach()
    {
      Application.PeerCast.ContentReaderFactories.Remove(factory);
    }
  }
}
