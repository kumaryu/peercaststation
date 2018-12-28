using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.App
{
  public enum OptionArg
  {
    None,
    Optional,
    Required,
  }

  public class OptionDesc
  {
    public string LongName { get; private set; }
    public string ShortName { get; private set; }
    public OptionArg Argument { get; private set; }
    public System.Text.RegularExpressions.Regex Regex { get; private set; }
    public OptionDesc(string optLong, string optShort, OptionArg arg)
    {
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

  }

  public class ParsedOption
  {
    public string LongName { get; private set; }
    public string[] Arguments { get; private set; }

    public ParsedOption(string name, string[] arguments)
    {
      LongName = name;
      Arguments = arguments;
    }

    public ParsedOption(string name, string argument)
      : this(name, new string[] { argument })
    {
    }

    public ParsedOption(string name)
      : this(name, new string[0])
    {
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

  public class OptionParser
    : IEnumerable<OptionDesc>
  {
    public static readonly string OtherArguments = "";

    private List<OptionDesc> options = new List<OptionDesc>();
    public IReadOnlyList<OptionDesc> Options { get; private set; }

    public OptionParser()
    {
      Options = options;
    }

    public void Add(string optLong, string optShort, OptionArg argc)
    {
      options.Add(new OptionDesc(optLong, optShort, argc));
    }

    public IDictionary<string,ParsedOption> Parse(string[] args)
    {
      var results = new List<ParsedOption>();
      var others = new List<string>();
      int pos = 0;
      while (pos<args.Length) {
        var opt = options.FirstOrDefault(o => o.Regex.IsMatch(args[pos]));
        if (opt!=null) {
          var md = opt.Regex.Match(args[pos]);
          switch (opt.Argument) {
          case OptionArg.None:
            results.Add(new ParsedOption(opt.LongName));
            break;
          case OptionArg.Optional:
            if (md.Groups[1].Success) {
              results.Add(new ParsedOption(opt.LongName, md.Groups[1].Value));
            }
            else if (pos+1<args.Length && !args[pos+1].StartsWith("-")) {
              results.Add(new ParsedOption(opt.LongName, args[pos+1]));
              pos += 1;
            }
            else {
              results.Add(new ParsedOption(opt.LongName));
            }
            break;
          case OptionArg.Required:
            if (md.Groups[1].Success) {
              results.Add(new ParsedOption(opt.LongName, md.Groups[1].Value));
            }
            else if (pos+1<args.Length && !args[pos+1].StartsWith("-")) {
              results.Add(new ParsedOption(opt.LongName, args[pos+1]));
              pos += 1;
            }
            else {
              throw new OptionParseErrorException($"{opt.LongName}:Argument required");
            }
            break;
          }
        }
        else if (args[pos]=="--") {
          others.AddRange(args.Skip(pos+1));
          pos = args.Length;
        }
        else if (args[pos].StartsWith("-")) {
          throw new OptionParseErrorException($"Unknown option:{args[pos]}");
        }
        else {
          others.Add(args[pos]);
        }
        pos += 1;
      }
      results.Add(new ParsedOption(OtherArguments, others.ToArray()));
      return results.ToDictionary(opt => opt.LongName);
    }

    public IEnumerator<OptionDesc> GetEnumerator()
    {
      return options.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
      return options.GetEnumerator();
    }

  }

}
