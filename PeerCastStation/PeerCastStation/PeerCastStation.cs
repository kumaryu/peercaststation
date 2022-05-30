using PeerCastStation.App;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PeerCastStation.Main
{
  public class PeerCastStation
  {
    static int ProcessMain(string basepath, string[] args)
    {
      return StandaloneApp.Run(basepath, args);
    }

    static int ProcessUpdate(
      string basepath,
      string sourcepath,
      string targetpath,
      bool cleanup,
      bool start,
      string[] args)
    {
      UI.Updater.Update(sourcepath, targetpath);
      if (cleanup) {
        if (start) {
          args = Enumerable.Concat(new string[] { "--start" }, args).ToArray();
        }
        UI.Updater.StartCleanup(targetpath, sourcepath, args);
      }
      return 0;
    }

    static int ProcessCleanup(string basepath, string tmppath, bool start, string[] args)
    {
      UI.Updater.Cleanup(tmppath);
      if (start) {
        return ProcessMain(basepath, args);
      }
      else {
        return 0;
      }
    }

    static readonly OptionParser s_optionParser = new OptionParser {
      new Subcommand("install") {
        { "--cleanup" },
        { "--start" },
        new NamedArgument("TARGETPATH", OptionType.Required),
      },
      new Subcommand("update") {
        { "--no-cleanup" },
        { "--no-start" },
        new NamedArgument("SOURCEPATH", OptionType.Required),
        new NamedArgument("TARGETPATH", OptionType.Required),
      },
      new Subcommand("cleanup") {
        { "--start" },
        new NamedArgument("TARGETPATH", OptionType.Required),
      },
      new Subcommand("") {
        {"--settings", "-s", OptionArg.Required },
        {"--kill", "-kill" },
        {"--multi", "-multi" },
      },
    };

    [STAThread]
    static int Main(string[] args)
    {
      var basepath = StandaloneApp.GetDefaultBasePath();
      try {
        var opts = s_optionParser.Parse(args);
        if (opts.TryGetOption("", out var mainCmd)) {
          return ProcessMain(basepath, mainCmd.RawArguments.ToArray());
        }
        if (opts.TryGetOption("install", out var installCmd)) {
          return ProcessUpdate(
            basepath,
            basepath,
            installCmd.GetArgumentOf("TARGETPATH"),
            installCmd.HasOption("--cleanup"),
            installCmd.HasOption("--start"),
            installCmd.Arguments.ToArray());
        }
        if (opts.TryGetOption("update", out var updateCmd)) {
          return ProcessUpdate(
            basepath,
            updateCmd.GetArgumentOf("SOURCEPATH"),
            updateCmd.GetArgumentOf("TARGETPATH"),
            !updateCmd.HasOption("--no-cleanup"),
            !updateCmd.HasOption("--no-start"),
            updateCmd.Arguments.ToArray());
        }
        if (opts.TryGetOption("cleanup", out var cleanupCmd)) {
          return ProcessCleanup(
            basepath,
            cleanupCmd.GetArgumentOf("TARGETPATH"),
            cleanupCmd.HasOption("--start"),
            cleanupCmd.Arguments.ToArray());
        }
        return ProcessMain(basepath, opts.Arguments.ToArray());
      }
      catch (OptionParseErrorException ex) {
        Console.WriteLine("USAGE:");
        Console.WriteLine(s_optionParser.Help());
        Console.WriteLine($"ERROR: {ex.Message}");
        return 1;
      }
    }

  }
}
