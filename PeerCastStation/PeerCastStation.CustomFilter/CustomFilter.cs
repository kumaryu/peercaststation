using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;
using System.Xml.Linq;

namespace PeerCastStation.CustomFilter
{
  public class CustomFilterDescription
  {
    public string Name              { get; private set; }
    public string CommandLine       { get; private set; }
    public string InputContentType  { get; private set; }
    public string OutputContentType { get; private set; }
    public string OutputMIMEType    { get; private set; }
    public string OutputContentExt  { get; private set; }
    public string BasePath          { get; private set; }

    public static IEnumerable<CustomFilterDescription> Load(string filename)
    {
      filename = System.IO.Path.GetFullPath(filename);
      var doc = XDocument.Load(filename);
      return doc.Root.Elements(XName.Get("filter"))
        .Select(filter => {
          var desc = new CustomFilterDescription();
          desc.Name = filter.Attribute(XName.Get("name"))?.Value;
          desc.OutputMIMEType    = filter.Attribute(XName.Get("mimetype"))?.Value;
          desc.OutputContentType = filter.Attribute(XName.Get("contenttype"))?.Value;
          desc.OutputContentExt  = filter.Attribute(XName.Get("contentext"))?.Value;
          desc.BasePath = System.IO.Path.GetDirectoryName(filename);
          desc.CommandLine = filter.Value;
          return desc;
        }).ToArray();
    }
  }

  public class CustomFilterContentSink
    : IContentSink
  {
    public CustomFilterDescription Description { get; private set; }
    public IContentSink Sink { get; private set; }
    private System.Diagnostics.Process process = null;

    public CustomFilterContentSink(CustomFilterDescription desc, IContentSink sink)
    {
      this.Description = desc;
      this.Sink = sink;
    }

    public void OnChannelInfo(ChannelInfo channel_info)
    {
      if (String.IsNullOrEmpty(this.Description.OutputContentType)) {
        Sink.OnChannelInfo(channel_info);
      }
      else {
        var newinfo = new AtomCollection(channel_info.Extra);
        newinfo.SetChanInfoType(this.Description.OutputContentType);
        if (!String.IsNullOrEmpty(this.Description.OutputMIMEType)) {
          newinfo.SetChanInfoStreamType(this.Description.OutputMIMEType);
        }
        if (!String.IsNullOrEmpty(this.Description.OutputContentExt)) {
          newinfo.SetChanInfoStreamExt(this.Description.OutputContentExt);
        }
        Sink.OnChannelInfo(new ChannelInfo(newinfo));
      }
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
      Sink.OnChannelTrack(channel_track);
    }

    private CancellationTokenSource processCancellationToken;
    private Task stdErrorTask;
    private Task stdOutTask;
    private Content lastContent = null;
    public void OnContent(Content content)
    {
      lastContent = content;
      if (process==null) {
        var startinfo = new System.Diagnostics.ProcessStartInfo();
        var cmd = this.Description.CommandLine;
        var args =
          System.Text.RegularExpressions.Regex.Matches(cmd, @"(?:""[^""]+?"")|(?:\S+)")
          .Cast<System.Text.RegularExpressions.Match>()
          .Select(match => match.ToString())
          .ToArray();
        startinfo.WorkingDirectory = this.Description.BasePath;
        startinfo.FileName = args.First();
        startinfo.Arguments = String.Join(" ", args.Skip(1));
        startinfo.CreateNoWindow = false;
        startinfo.ErrorDialog = false;
        startinfo.RedirectStandardInput = true;
        startinfo.RedirectStandardOutput = true;
        startinfo.RedirectStandardError = true;
        startinfo.StandardOutputEncoding = System.Text.Encoding.ASCII;
        startinfo.StandardErrorEncoding = System.Text.Encoding.UTF8;
        processCancellationToken = new CancellationTokenSource();
        var cancel = processCancellationToken.Token;
        process = System.Diagnostics.Process.Start(startinfo);
        stdErrorTask = Task.Run(async () => {
          try {
            Logger logger = new Logger(typeof(CustomFilterContentSink), this.Description.Name);
            while (!cancel.IsCancellationRequested) {
              var line = await process.StandardError.ReadLineAsync();
              logger.Debug(line);
            }
          }
          catch (ObjectDisposedException) {
          }
        });
        stdOutTask = Task.Run(async () => {
          try {
            long pos = 0;
            var buffer = new byte[1024*15];
            while (!cancel.IsCancellationRequested) {
              var len = await process.StandardOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancel);
              if (len<=0) break;
              Sink.OnContent(new Content(lastContent.Stream, lastContent.Timestamp, pos, buffer, 0, len));
              pos += len;
            }
          }
          catch (ObjectDisposedException) {
          }
        });
      }
      process.StandardInput.BaseStream.Write(content.Data, 0, content.Data.Length);
    }

    public void OnContentHeader(Content content_header)
    {
      Sink.OnContentHeader(new Content(content_header.Stream, content_header.Timestamp, 0, new byte[0]));
      OnContent(content_header);
    }

    public void OnStop(StopReason reason)
    {
      process.Close();
      processCancellationToken.Cancel();
      process = null;
      processCancellationToken = null;
    }
  }

  public class CustomFilter
    : IContentFilter,
      IDisposable
  {
    public CustomFilterDescription Description { get; private set; }
    public string Name {
      get { return $"custom:{Description.Name}"; }
    }

    public CustomFilter(CustomFilterDescription desc)
    {
      this.Description = desc;
    }

    public IContentSink Activate(IContentSink sink)
    {
      return new CustomFilterContentSink(this.Description, sink);
    }

    public void Dispose()
    {
    }
  }

  [Plugin(PluginType.Content)]
  public class CustomFilterPlugin
    : PluginBase
  {
    public override string Name => "CustomFilter Plugin";
    public string CustomFilterPath { get; set; }
    public List<CustomFilterDescription> Descriptions { get; private set; }
    private IEnumerable<CustomFilter> filters = null;

    public CustomFilterPlugin()
    {
      this.CustomFilterPath =
        System.IO.Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
          "PeerCastStation",
          "Filters");
    }

    override protected void OnAttach()
    {
      Descriptions = LoadDescriptions().ToList();
    }

    override protected void OnDetach()
    {
      Descriptions.Clear();
    }

    override protected void OnStart()
    {
      if (filters!=null) return;
      filters = Descriptions.Select(desc => new CustomFilter(desc)).ToArray();
      foreach (var filter in filters) {
        Application.PeerCast.ContentFilters.Add(filter);
      }
    }

    override protected void OnStop()
    {
      if (filters==null) return;
      foreach (var filter in filters) {
        Application.PeerCast.ContentFilters.Remove(filter);
      }
      filters = null;
    }

    public IEnumerable<CustomFilterDescription> LoadDescriptions()
    {
      return
        System.IO.Directory.GetFiles(CustomFilterPath, "*.xml")
        .SelectMany(file => CustomFilterDescription.Load(file));
    }

    public void Restart()
    {
      OnStop();
      OnStart();
    }

    public void Reload()
    {
      OnDetach();
      OnAttach();
      Restart();
    }
  }

}
