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

  [Flags]
  public enum LoggerOutputTarget {
    None          = 0,
    Debug         = 1,
    Console       = 2,
    File          = 4,
    UserInterface = 8,
  };

  public class Logger
  {
    static Logger()
    {
      debugListener = Trace.Listeners.OfType<TraceListener>().FirstOrDefault(listener => listener is DefaultTraceListener);
      if (debugListener!=null) {
        outputTarget |= LoggerOutputTarget.Debug;
      }
    }

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
        return uiListeners.Select(listener => listener.Writer).ToArray();
      }
    }

    private static TextWriterTraceListener consoleListener;
    private static TextWriterTraceListener fileListener;
    private static List<TextWriterTraceListener> uiListeners = new List<TextWriterTraceListener>();
    private static TraceListener debugListener;

    private static string logFileName = null;
    public static string LogFileName {
      get { return logFileName; }
      set {
        if (logFileName==value) return;
        if (fileListener!=null) {
          Trace.Listeners.Remove(fileListener);
          fileListener.Close();
          fileListener = null;
        }
        logFileName = value;
        if (!String.IsNullOrEmpty(logFileName) && (outputTarget & LoggerOutputTarget.File)!=0) {
          fileListener = new TextWriterTraceListener(logFileName);
          Trace.Listeners.Add(fileListener);
        }
      }
    }

    private static LoggerOutputTarget outputTarget = LoggerOutputTarget.None;
    public static LoggerOutputTarget OutputTarget {
      get { return outputTarget; }
      set {
        SetOutputListeners(value, outputTarget);
        outputTarget = value;
      }
    }

    private static void SetOutputListeners(LoggerOutputTarget targets, LoggerOutputTarget old)
    {
      if ((targets & LoggerOutputTarget.Console)!=0 &&
          (old     & LoggerOutputTarget.Console)==0) {
        if (consoleListener==null) {
          consoleListener = new ConsoleTraceListener(true);
        }
        Trace.Listeners.Add(consoleListener);
      }
      if ((targets & LoggerOutputTarget.Console)==0 &&
          (old     & LoggerOutputTarget.Console)!=0) {
        Trace.Listeners.Remove(consoleListener);
      }
      if ((targets & LoggerOutputTarget.Debug)!=0 &&
          (old     & LoggerOutputTarget.Debug)==0) {
        if (debugListener==null) {
          debugListener = new DefaultTraceListener();
        }
        Trace.Listeners.Add(debugListener);
      }
      if ((targets & LoggerOutputTarget.Debug)==0 &&
          (old     & LoggerOutputTarget.Debug)!=0) {
        Trace.Listeners.Remove(debugListener);
      }
      if ((targets & LoggerOutputTarget.File)!=0 &&
          (old     & LoggerOutputTarget.File)==0) {
        if (fileListener==null && !String.IsNullOrEmpty(LogFileName)) {
          fileListener = new TextWriterTraceListener(LogFileName);
        }
        if (fileListener!=null) {
          Trace.Listeners.Add(fileListener);
        }
      }
      if ((targets & LoggerOutputTarget.File)==0 &&
          (old     & LoggerOutputTarget.File)!=0) {
        Trace.Listeners.Remove(fileListener);
      }
      if ((targets & LoggerOutputTarget.UserInterface)!=0 &&
          (old     & LoggerOutputTarget.UserInterface)==0) {
        foreach (var listener in uiListeners) {
          Trace.Listeners.Add(listener);
        }
      }
      if ((targets & LoggerOutputTarget.UserInterface)==0 &&
          (old     & LoggerOutputTarget.UserInterface)!=0) {
        foreach (var listener in uiListeners) {
          Trace.Listeners.Remove(listener);
        }
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
      var listener = new TextWriterTraceListener(writer);
      uiListeners.Add(listener);
      if ((outputTarget & LoggerOutputTarget.UserInterface)!=0) {
        Trace.Listeners.Add(listener);
      }
    }
 
    /// <summary>
    /// 指定したWriterをログ出力先から外します
    /// </summary>
    public static void RemoveWriter(System.IO.TextWriter writer)
    {
      foreach (var listener in uiListeners.Where(listener => listener.Writer==writer)) {
        Trace.Listeners.Remove(listener);
      }
      uiListeners.RemoveAll(listener => listener.Writer==writer);
    }

    /// <summary>
    /// 全てのログ出力先を外します
    /// </summary>
    public static void ClearWriter()
    {
      Trace.Listeners.Clear();
    }

    static public void Flush()
    {
      Trace.Flush();
    }

    static public void Close()
    {
      Trace.Close();
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
