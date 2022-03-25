using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.App
{
  public class ParsedOption
  {
    public string LongName { get; private set; }
    public IReadOnlyList<ParsedOption> Options { get; private set; }
    public IReadOnlyList<string> Arguments { get; private set; }
    public IReadOnlyList<string> RawArguments { get; private set; }

    public ParsedOption(string name, IReadOnlyList<ParsedOption> options, IReadOnlyList<string> arguments, IReadOnlyList<string> rawArguments)
    {
      LongName = name;
      Options = options;
      Arguments = arguments;
      RawArguments = rawArguments;
    }

    public ParsedOption(string name, IReadOnlyList<string> arguments)
      : this(name, Array.Empty<ParsedOption>(), arguments, arguments)
    {
    }

    public ParsedOption(string name, string argument)
      : this(name, new string[] { argument })
    {
    }

    public ParsedOption(string name)
      : this(name, Array.Empty<string>())
    {
    }

    public bool HasOption(string name)
    {
      return Options.Any(opt => opt.LongName==name);
    }

    public bool TryGetOption(string name, out ParsedOption option)
    {
      option = Options.FirstOrDefault(opt => opt.LongName==name);
      return option!=null;
    }

    public bool TryGetArgumentOf(string name, out string value)
    {
      var option = Options.FirstOrDefault(opt => opt.LongName==name);
      if (option!=null && option.Arguments.Count>0) {
        value = option.Arguments[0];
        return true;
      }
      else {
        value = null;
        return false;
      }
    }

    public string GetArgumentOf(string name)
    {
      if (TryGetArgumentOf(name, out var value)) {
        return value;
      }
      else {
        throw new KeyNotFoundException($"Argument of {name} is not specified.");
      }
    }

    public class Builder
    {
      public string LongName { get; }
      public List<ParsedOption.Builder> Options { get; private set; } = new List<ParsedOption.Builder>();
      public List<string> Arguments { get; private set; } = new List<string>();
      public List<string> RawArguments { get; private set; } = new List<string>();
      public Builder(string name)
      {
        LongName = name;
      }

      public ParsedOption ToParsedOption()
      {
        var options = Options.Select(opt => opt.ToParsedOption()).ToArray();
        var arguments = Arguments;
        var rawArguments = RawArguments;
        Options = new List<ParsedOption.Builder>();
        Arguments = new List<string>();
        RawArguments = new List<string>();
        return new ParsedOption(LongName, options, arguments, rawArguments);
      }

      public bool HasOption(string name)
      {
        return Options.Any(opt => opt.LongName==name);
      }

      public bool AddOption(string name)
      {
        if (!Options.Any(opt => opt.LongName==name)) {
          Options.Add(new ParsedOption.Builder(name));
        }
        return true;
      }

      public bool AddOption(string name, string arg, bool allowMultiple)
      {
        var opt = Options.FirstOrDefault(opt => opt.LongName==name);
        if (opt!=null) {
          if (allowMultiple || opt.Arguments.Count==0) {
            opt.Arguments.Add(arg);
          }
          else {
            opt.Arguments[0] = arg;
          }
        }
        else {
          opt = new ParsedOption.Builder(name);
          opt.Arguments.Add(arg);
          Options.Add(opt);
        }
        return true;
      }
    }

  }

  public class CommandHelpBuilder
  {
    public string CommandName { get; }
    private List<string> options = new List<string>();
    private List<string> arguments = new List<string>();
    private List<string> mainCommands = new List<string>();
    private List<string> subcommands = new List<string>();

    public CommandHelpBuilder(string commandName)
    {
      CommandName = commandName;
    }

    public void AddSubcommand(string name, Action<CommandHelpBuilder> subbuildAction)
    {
      var sub = new CommandHelpBuilder(name);
      subbuildAction(sub);
      if (String.IsNullOrEmpty(name)) {
        mainCommands.AddRange(sub.ToHelp());
      }
      else {
        subcommands.AddRange(sub.ToHelp());
      }
    }

    public void AddOption(string name)
    {
      options.Add(name);
    }

    public void AddArgument(string name)
    {
      arguments.Add(name);
    }

    public IEnumerable<string> ToHelp()
    {
      var prefix = new System.Text.StringBuilder();
      prefix.Append(CommandName);
      foreach (var opt in options) {
        prefix.Append(' ');
        prefix.Append(opt);
      }
      if (mainCommands.Count>0) {
        foreach (var cmd in mainCommands) {
          var line = new System.Text.StringBuilder();
          line.Append(prefix);
          line.Append(cmd);
          yield return line.ToString();
        }
      }
      else {
        var line = new System.Text.StringBuilder();
        line.Append(prefix);
        foreach (var arg in arguments) {
          prefix.Append(' ');
          prefix.Append(arg);
        }
        yield return line.ToString();
      }
      foreach (var cmd in subcommands) {
        var line = new System.Text.StringBuilder();
        line.Append(prefix);
        line.Append(' ');
        line.Append(cmd);
        yield return line.ToString();
      }
    }

  }

  [Flags]
  public enum OptionType
  {
    None = 0,
    Required = 1,
    Multiple = 2,
  }

  public interface IOptionParser
  {
    public string Name { get; }
    OptionType OptionType { get; }
    bool Parse(ParsedOption.Builder context, ref Span<string> args);
    void Help(CommandHelpBuilder helpBuilder);
  }

  public enum OptionArg
  {
    None,
    Optional,
    Required,
  }

  public class OptionDesc : IOptionParser
  {
    public OptionType OptionType { get; }
    public string Name { get { return LongName; } }
    public string LongName { get; }
    public string ShortName { get; }
    public OptionArg Argument { get; }
    public System.Text.RegularExpressions.Regex Regex { get; }
    public OptionDesc(string optLong, string optShort=null, OptionArg arg=OptionArg.None, OptionType type=OptionType.None)
    {
      OptionType = type;
      LongName = optLong;
      ShortName = optShort ?? optLong;
      Argument = arg;
      switch (Argument) {
      case OptionArg.None:
        Regex = new System.Text.RegularExpressions.Regex($"^(?:{ShortName}|{LongName})$");
        break;
      case OptionArg.Optional:
      case OptionArg.Required:
        Regex = new System.Text.RegularExpressions.Regex($"^(?:{ShortName}|{LongName})(?:=(.*))?$");
        break;
      }
    }

    public bool Parse(ParsedOption.Builder context, ref Span<string> args)
    {
      if (args.Length==0) {
        return false;
      }
      var md = Regex.Match(args[0]);
      if (md.Success) {
        args = args.Slice(1);
        switch (Argument) {
        case OptionArg.None:
          return context.AddOption(LongName);
        case OptionArg.Optional:
          if (md.Groups[1].Success) {
            return context.AddOption(LongName, md.Groups[1].Value, OptionType.HasFlag(OptionType.Multiple));
          }
          else if (1<=args.Length && !args[0].StartsWith("-")) {
            var value = args[0];
            args = args.Slice(1);
            return context.AddOption(LongName, value, OptionType.HasFlag(OptionType.Multiple));
          }
          else {
            return context.AddOption(LongName);
          }
        case OptionArg.Required:
          if (md.Groups[1].Success) {
            return context.AddOption(LongName, md.Groups[1].Value, OptionType.HasFlag(OptionType.Multiple));
          }
          else if (1<=args.Length && !args[0].StartsWith("-")) {
            var value = args[0];
            args = args.Slice(1);
            return context.AddOption(LongName, value, OptionType.HasFlag(OptionType.Multiple));
          }
          else {
            throw new OptionParseErrorException($"{LongName}:Argument required");
          }
        }
      }
      return false;
    }

    public void Help(CommandHelpBuilder helpBuilder)
    {
      switch (Argument) {
      case OptionArg.None:
        if (LongName==ShortName) {
          helpBuilder.AddOption($"[{LongName}]");
        }
        else {
          helpBuilder.AddOption($"[{LongName}|{ShortName}]");
        }
        break;
      case OptionArg.Optional:
        if (LongName==ShortName) {
          helpBuilder.AddOption($"[{LongName}[=ARG]]");
        }
        else {
          helpBuilder.AddOption($"[({LongName}[=ARG]|{ShortName} [ARG]]");
        }
        break;
      case OptionArg.Required:
        if (LongName==ShortName) {
          helpBuilder.AddOption($"[{LongName}=ARG]");
        }
        else {
          helpBuilder.AddOption($"[{LongName}=ARG|{ShortName} ARG]");
        }
        break;
      }
    }
  }

  public class NamedArgument : IOptionParser
  {
    public OptionType OptionType { get; }
    public string Name { get; }
    public NamedArgument(string name, OptionType type=OptionType.None)
    {
      Name = name;
      OptionType = type;
    }

    public bool Parse(ParsedOption.Builder context, ref Span<string> args)
    {
      if (args.Length==0 || args[0].StartsWith("-")) {
        return false;
      }
      if (OptionType.HasFlag(OptionType.Multiple)) {
        while (0<args.Length && !args[0].StartsWith("-")) {
          context.AddOption(Name, args[0], true);
          args = args.Slice(1);
        }
        return true;
      }
      else if (!context.HasOption(Name)) {
        context.AddOption(Name, args[0], false);
        args = args.Slice(1);
        return true;
      }
      else {
        return false;
      }
    }

    public void Help(CommandHelpBuilder helpBuilder)
    {
      var name = OptionType.HasFlag(OptionType.Multiple) ? $"{Name}..." : Name;
      if (OptionType.HasFlag(OptionType.Required)) {
        helpBuilder.AddArgument($"{name}");
      }
      else {
        helpBuilder.AddArgument($"[{name}]");
      }
    }
  }

  public class OptionParseErrorException
    : Exception
  {
    public OptionParseErrorException()
      : base()
    {
    }

    public OptionParseErrorException(string message)
      : base(message)
    {
    }
  }

  public abstract class Command : IOptionParser, IEnumerable
  {
    public OptionType OptionType { get; } = OptionType.None;
    public string Name { get; }
    private List<IOptionParser> options = new List<IOptionParser>();
    public IReadOnlyList<IOptionParser> Options { get; }

    public Command(string name)
    {
      Name = name;
      Options = options;
    }

    public void Add(string optLong, string optShort=null, OptionArg argc=OptionArg.None)
    {
      Add(new OptionDesc(optLong, optShort, argc));
    }

    public void Add(IOptionParser opt)
    {
      options.Add(opt);
    }

    public virtual bool Parse(ParsedOption.Builder context, ref Span<string> args)
    {
      var result = new ParsedOption.Builder(Name);
      result.RawArguments.AddRange(args.ToArray());
      while (args.Length>0) {
        var parsed = false;
        foreach (var opt in options) {
          parsed = opt.Parse(result, ref args) || parsed;
        }
        if (!parsed) {
          result.Arguments.Add(args[0]);
          args = args.Slice(1);
        }
      }
      foreach (var opt in options) {
        if (opt.OptionType.HasFlag(OptionType.Required) && !result.HasOption(opt.Name)) {
          throw new OptionParseErrorException($"{opt.Name} must be specified.");
        }
      }
      context.Options.Add(result);
      return true;
    }

    public IEnumerator GetEnumerator()
    {
      throw new NotImplementedException();
    }

    public abstract void Help(CommandHelpBuilder helpBuilder);

  }
  public class Subcommand : Command
  {
    public Subcommand(string name)
      : base(name)
    {
    }

    public override bool Parse(ParsedOption.Builder context, ref Span<string> args)
    {
      if (args.Length>0 && String.IsNullOrEmpty(Name)) {
        return base.Parse(context, ref args);
      }
      else {
        if (args.Length==0 || args[0]!=Name) {
          return false;
        }
        args = args.Slice(1);
        return base.Parse(context, ref args);
      }
    }

    public override void Help(CommandHelpBuilder helpBuilder)
    {
      helpBuilder.AddSubcommand(Name, builder => {
        foreach (var opt in Options) {
          opt.Help(builder);
        }
      });
    }

  }

  public class OptionParser : Command
  {
    public OptionParser()
      : base(System.IO.Path.GetFileName(System.Reflection.Assembly.GetEntryAssembly().Location))
    {
    }

    public OptionParser(string programName)
      : base(programName)
    {
    }

    public override void Help(CommandHelpBuilder helpBuilder)
    {
      foreach (var opt in Options) {
        opt.Help(helpBuilder);
      }
    }

    public ParsedOption Parse(string[] args)
    {
      var argsSpan = args.AsSpan();
      var result = new ParsedOption.Builder(Name);
      Parse(result, ref argsSpan);
      return result.ToParsedOption().Options.First();
    }

    public string Help()
    {
      var builder = new CommandHelpBuilder(Name);
      Help(builder);
      return String.Join("\n", builder.ToHelp());
    }

  }

}
