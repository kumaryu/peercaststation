using System;
using System.Linq;
using System.IO;
using PeerCastStation.Core;
using System.Collections.Generic;
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
			this.contentBuffer = new FLVContentBuffer(channel);
		}

		public string Name { get { return "Flash Video (FLV)"; } }
		public Channel Channel { get; private set; }
		private FLVContentBuffer contentBuffer;
		private FLVFileParser fileParser = new FLVFileParser();

		public ParsedContent Read(Stream stream)
		{
			if (fileParser.Read(stream, contentBuffer)) {
				return contentBuffer.GetContents();
			}
			else {
				throw new EndOfStreamException();
			}
		}

    public Task<ParsedContent> ReadAsync(Stream stream, CancellationToken cancel_token)
    {
      throw new NotImplementedException();
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
