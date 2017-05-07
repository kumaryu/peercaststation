using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PeerCastStation.Core;
using System.Xml.Linq;
using System.Collections.Concurrent;

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
    public bool   Logging           { get; private set; }

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
          desc.Logging           = ToBool(filter.Attribute(XName.Get("logging"))?.Value);
          desc.BasePath = System.IO.Path.GetDirectoryName(filename);
          desc.CommandLine = filter.Value;
          return desc;
        }).ToArray();
    }

    private static bool ToBool(string value)
    {
      if (value==null) return false;
      switch (value.ToLowerInvariant()) {
      case "1":
      case "true":
      case "on":
        return true;
      default:
        return false;
      }
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
        else {
          newinfo.RemoveByName(Atom.PCP_CHAN_INFO_STREAMTYPE);
        }
        if (!String.IsNullOrEmpty(this.Description.OutputContentExt)) {
          newinfo.SetChanInfoStreamExt(this.Description.OutputContentExt);
        }
        else {
          newinfo.RemoveByName(Atom.PCP_CHAN_INFO_STREAMEXT);
        }
        Sink.OnChannelInfo(new ChannelInfo(newinfo));
      }
    }

    public void OnChannelTrack(ChannelTrack channel_track)
    {
      Sink.OnChannelTrack(channel_track);
    }

    class WaitableQueue<T>
    {
      private SemaphoreSlim locker = new SemaphoreSlim(0);
      private ConcurrentQueue<T> queue = new ConcurrentQueue<T>();

      public bool IsEmpty {
        get { return locker.CurrentCount==0; }
        }

      public void Enqueue(T value)
      {
        queue.Enqueue(value);
        locker.Release();
      }

      public async Task<T> DequeueAsync(CancellationToken cancellationToken)
      {
        await locker.WaitAsync(cancellationToken);
        T result;
        while (!queue.TryDequeue(out result)) {
          await locker.WaitAsync(cancellationToken);
        }
        return result;
      }
    }

    private CancellationTokenSource processCancellationToken;
    private Task stdErrorTask;
    private Task stdOutTask;
    private Task stdInTask;
    private bool writable = false;
    private WaitableQueue<byte[]> pipePackets = new WaitableQueue<byte[]>();

    private Content lastContent = null;
    private void StartProcess()
    {
      var startinfo = new System.Diagnostics.ProcessStartInfo();
      var cmd = this.Description.CommandLine;
      var args =
        System.Text.RegularExpressions.Regex.Matches(cmd, @"(?:""[^""]+?"")|(?:\S+)")
        .Cast<System.Text.RegularExpressions.Match>()
        .Select(match => match.ToString())
        .ToArray();
      startinfo.WorkingDirectory = this.Description.BasePath;
      startinfo.UseShellExecute = false;
      startinfo.FileName = args.First();
      startinfo.Arguments = String.Join(" ", args.Skip(1));
      startinfo.CreateNoWindow = true;
      startinfo.ErrorDialog = false;
      startinfo.RedirectStandardInput = true;
      startinfo.RedirectStandardOutput = true;
      startinfo.RedirectStandardError = true;
      startinfo.StandardOutputEncoding = System.Text.Encoding.ASCII;
      startinfo.StandardErrorEncoding = System.Text.Encoding.Default;
      processCancellationToken = new CancellationTokenSource();
      var cancel = processCancellationToken.Token;
      process = System.Diagnostics.Process.Start(startinfo);
      pipePackets = new WaitableQueue<byte[]>();
      var stdout = process.StandardOutput.BaseStream;
      var stdin  = process.StandardInput.BaseStream;
      var stderr = process.StandardError;
      stdErrorTask = Task.Run(async () => {
        try {
          Logger logger = new Logger(typeof(CustomFilterContentSink), this.Description.Name);
          while (!cancel.IsCancellationRequested) {
            var line = await stderr.ReadLineAsync();
            if (line==null) break;
            if (Description.Logging) {
              logger.Debug(line);
            }
          }
          stderr.Close();
        }
        catch (System.IO.IOException) {
        }
        catch (ObjectDisposedException) {
        }
      });
      stdOutTask = Task.Run(async () => {
        try {
          long pos = 0;
          var buffer = new byte[1024*15];
          while (!cancel.IsCancellationRequested) {
            var len = await stdout.ReadAsync(buffer, 0, buffer.Length, cancel);
            System.Console.WriteLine("stdout {0}", len);
            if (len<=0) break;
            Sink.OnContent(new Content(lastContent.Stream, lastContent.Timestamp, pos, buffer, 0, len));
            pos += len;
          }
          stdout.Close();
        }
        catch (System.IO.IOException) {
        }
        catch (ObjectDisposedException) {
        }
        finally {
          System.Console.WriteLine("stdoutclosed");
        }
      });
      writable = true;
      stdInTask = Task.Run(async () => {
        try {
          while (!cancel.IsCancellationRequested) {
            var packet = await pipePackets.DequeueAsync(cancel);
            if (packet!=null) {
              await stdin.WriteAsync(packet, 0, packet.Length, cancel);
              if (pipePackets.IsEmpty) {
                await stdin.FlushAsync();
              }
            }
            else {
              stdin.Close();
              break;
            }
          }
        }
        catch (System.IO.IOException) {
        }
        catch (ObjectDisposedException) {
        }
        finally {
          writable = false;
          System.Console.WriteLine("stdinclosed");
        }
        //TODO:子プロセスが死んだ時に上流か下流かに伝える必要がある
      });
    }

    public void OnContent(Content content)
    {
      lastContent = content;
      if (process==null) {
        StartProcess();
      }
      if (writable) {
        pipePackets.Enqueue(content.Data);
      }
    }

    public void OnContentHeader(Content content_header)
    {
      Sink.OnContentHeader(new Content(content_header.Stream, content_header.Timestamp, 0, new byte[0]));
      OnContent(content_header);
    }

    public void OnStop(StopReason reason)
    {
      if (process!=null) {
        pipePackets.Enqueue(null);
        try {
          if (!Task.WaitAll(new [] { stdInTask, stdOutTask, stdErrorTask }, 333)) {
            processCancellationToken.Cancel();
            Task.WaitAll(new [] { stdInTask, stdOutTask, stdErrorTask }, 333);
          }
        }
        catch (AggregateException) {
        }
        try {
          if (!process.HasExited) {
            if (!process.CloseMainWindow() ||
                !process.WaitForExit(1000)) {
              process.Kill();
            }
          }
        }
        catch (System.ComponentModel.Win32Exception) {
        }
        catch (InvalidOperationException) {
        }
        process.Close();
        process = null;
        processCancellationToken = null;
      }
      Sink.OnStop(reason);
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
      string path = null;
      try {
        path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
      }
      catch (PlatformNotSupportedException) {
      }
      if (string.IsNullOrEmpty(path)) {
        try {
          path = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }
        catch (PlatformNotSupportedException) {
        }
      }
      if (string.IsNullOrEmpty(path)) {
        this.CustomFilterPath =
          System.IO.Path.Combine(
            path,
            "PeerCastStation",
            "Filters");
      }
      else {
        this.CustomFilterPath = null;
      }
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
      try {
        return
          System.IO.Directory.GetFiles(CustomFilterPath, "*.xml")
          .SelectMany(file => CustomFilterDescription.Load(file));
      }
      catch (Exception) {
        return Enumerable.Empty<CustomFilterDescription>();
      }
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
