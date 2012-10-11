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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace PeerCastStation.Core
{
  /// <summary>
  /// ログのレベルを表します
  /// </summary>
  public enum LogLevel
  {
    /// <summary>
    /// ログの記録なし
    /// </summary>
    None,
    /// <summary>
    /// 致命的なエラー
    /// </summary>
    Fatal,
    /// <summary>
    /// エラー
    /// </summary>
    Error,
    /// <summary>
    /// 警告
    /// </summary>
    Warn,
    /// <summary>
    /// 付加的な情報
    /// </summary>
    Info,
    /// <summary>
    /// デバッグ用の情報
    /// </summary>
    Debug,
  }

  public class Logger
  {
    private static TraceSwitch generalSwitch = new TraceSwitch("General", "Entire of PeerCastStation");

    /// <summary>
    /// ログ出力用の全体スイッチを取得します
    /// </summary>
    public static TraceSwitch GeneralSwitch { get { return generalSwitch; } }

    /// <summary>
    /// ログの出力先読み取り専用コレクションを取得します
    /// </summary>
    public static IList<System.IO.TextWriter> Writers {
      get {
        return Trace.Listeners.OfType<TraceListener>()
          .Select(listener => listener as TextWriterTraceListener)
          .Where(listener => listener!=null)
          .Select(listener => listener.Writer).ToList().AsReadOnly();
      }
    }

    /// <summary>
    /// 出力するログレベルを取得および設定します
    /// </summary>
    public static LogLevel Level {
      get {
        switch (generalSwitch.Level) {
        case TraceLevel.Off:     return LogLevel.None;
        case TraceLevel.Error:   return LogLevel.Error;
        case TraceLevel.Warning: return LogLevel.Warn;
        case TraceLevel.Info:    return LogLevel.Info;
        case TraceLevel.Verbose: return LogLevel.Debug;
        default: return LogLevel.Debug;
        }
      }
      set {
        TraceLevel level = generalSwitch.Level;
        switch (value) {
        case LogLevel.None:  level = TraceLevel.Off; break;
        case LogLevel.Error: level = TraceLevel.Error; break;
        case LogLevel.Warn:  level = TraceLevel.Warning; break;
        case LogLevel.Info:  level = TraceLevel.Info; break;
        case LogLevel.Debug: level = TraceLevel.Verbose; break;
        }
        generalSwitch.Level = level;
      }
    }

    /// <summary>
    /// ログの出力先を追加します
    /// </summary>
    /// <param name="writer">出力先として追加するTextWriter</param>
    public static void AddWriter(System.IO.TextWriter writer)
    {
      Trace.Listeners.Add(new TextWriterTraceListener(writer));
    }
 
    /// <summary>
    /// 指定したWriterをログ出力先から外します
    /// </summary>
    public static void RemoveWriter(System.IO.TextWriter writer)
    {
      var listeners = Trace.Listeners.OfType<TraceListener>()
        .Select(listener => listener as TextWriterTraceListener)
        .Where(listener => listener!=null && listener.Writer==writer).ToArray();
      foreach (var listener in listeners) {
        Trace.Listeners.Remove(listener);
      }
    }

    /// <summary>
    /// 全てのログ出力先を外します
    /// </summary>
    public static void ClearWriter()
    {
      Trace.Listeners.Clear();
    }

    static private void Output(LogLevel level, string source, string format, params object[] args)
    {
      string[] level_name = {
        "",
        "FATAL",
        "ERROR",
        "WARN",
        "INFO",
        "DEBUG",
      };
      try {
        var message =  
          String.Format("{0:s} [{1}] {2} {3} - {4}",
            DateTime.Now,
            System.Threading.Thread.CurrentThread.Name,
            level_name[(int)level],
            "",
            String.Format(format, args));
        switch (level) {
        case LogLevel.Debug:
          Trace.WriteLineIf(GeneralSwitch.TraceVerbose, message, source);
          break;
        case LogLevel.Info:
          Trace.WriteLineIf(GeneralSwitch.TraceInfo, message, source);
          break;
        case LogLevel.Warn:
          Trace.WriteLineIf(GeneralSwitch.TraceWarning, message, source);
          break;
        case LogLevel.Error:
        case LogLevel.Fatal:
          Trace.WriteLineIf(GeneralSwitch.TraceError, message, source);
          break;
        default:
          Trace.WriteLineIf(GeneralSwitch.TraceVerbose, message, source);
          break;
        }
      }
      catch (FormatException) {
      }
    }

    static private void Output(LogLevel level, string source, Exception e)
    {
      Output(level, source, "{0} {1}\n{2}", e.GetType().Name, e.Message, e.StackTrace);
      if (e.InnerException!=null) {
        Output(level, source, e.InnerException);
      }
    }

    /// <summary>
    /// 出力元を表す名前を取得します
    /// </summary>
    public string Source { get; private set; }

    /// <summary>
    /// 出力元の名前を指定してLoggerオブジェクトを初期化します
    /// </summary>
    /// <param name="source">出力元の名前</param>
    public Logger(string source)
    {
      this.Source = source;
    }

    /// <summary>
    /// 出力元のクラスを指定してLoggerオブジェクトを初期化します
    /// </summary>
    /// <param name="source">出力元の型</param>
    public Logger(Type type)
    {
      this.Source = type.Name;
    }

    /// <summary>
    /// Fatalレベルのログとして整形したメッセージを出力します
    /// </summary>
    /// <param name="format">出力するフォーマット文字列</param>
    /// <param name="args">フォーマットへの引数</param>
    [Conditional("TRACE")]
    public void Fatal(object format, params object[] args)
    {
      Output(LogLevel.Fatal, this.Source, format.ToString(), args);
    }

    /// <summary>
    /// Fatalレベルのログとして例外を整形して出力します
    /// </summary>
    /// <param name="exception">出力する例外</param>
    [Conditional("TRACE")]
    public void Fatal(Exception exception)
    {
      Output(LogLevel.Fatal, this.Source, exception);
    }

    /// <summary>
    /// Errorレベルのログとして整形したメッセージを出力します
    /// </summary>
    /// <param name="format">出力するフォーマット文字列</param>
    /// <param name="args">フォーマットへの引数</param>
    [Conditional("TRACE")]
    public void Error(object format, params object[] args)
    {
      Output(LogLevel.Error, this.Source, format.ToString(), args);
    }

    /// <summary>
    /// Errorレベルのログとして例外を整形して出力します
    /// </summary>
    /// <param name="exception">出力する例外</param>
    [Conditional("TRACE")]
    public void Error(Exception exception)
    {
      Output(LogLevel.Error, this.Source, exception);
    }

    /// <summary>
    /// Warnレベルのログとして整形したメッセージを出力します
    /// </summary>
    /// <param name="format">出力するフォーマット文字列</param>
    /// <param name="args">フォーマットへの引数</param>
    [Conditional("TRACE")]
    public void Warn(object format, params object[] args)
    {
      Output(LogLevel.Warn, this.Source, format.ToString(), args);
    }

    /// <summary>
    /// Warnレベルのログとして例外を整形して出力します
    /// </summary>
    /// <param name="exception">出力する例外</param>
    [Conditional("TRACE")]
    public void Warn(Exception exception)
    {
      Output(LogLevel.Warn, this.Source, exception);
    }

    /// <summary>
    /// Infoレベルのログとして整形したメッセージを出力します
    /// </summary>
    /// <param name="format">出力するフォーマット文字列</param>
    /// <param name="args">フォーマットへの引数</param>
    [Conditional("TRACE")]
    public void Info(object format, params object[] args)
    {
      Output(LogLevel.Info, this.Source, format.ToString(), args);
    }

    /// <summary>
    /// Infoレベルのログとして例外を整形して出力します
    /// </summary>
    /// <param name="exception">出力する例外</param>
    [Conditional("TRACE")]
    public void Info(Exception exception)
    {
      Output(LogLevel.Info, this.Source, exception);
    }

    /// <summary>
    /// Debugレベルのログとして整形したメッセージを出力します
    /// </summary>
    /// <param name="format">出力するフォーマット文字列</param>
    /// <param name="args">フォーマットへの引数</param>
    [Conditional("TRACE")]
    public void Debug(object format, params object[] args)
    {
      Output(LogLevel.Debug, this.Source, format.ToString(), args);
    }

    /// <summary>
    /// Debugレベルのログとして例外を整形して出力します
    /// </summary>
    /// <param name="exception">出力する例外</param>
    [Conditional("TRACE")]
    public void Debug(Exception exception)
    {
      Output(LogLevel.Debug, this.Source, exception);
    }
  }
}
