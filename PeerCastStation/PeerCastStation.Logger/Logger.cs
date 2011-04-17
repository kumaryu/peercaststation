using System;
using System.Collections.Generic;
using System.Linq;

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
  /// <summary>
  /// ログの出力先を表します
  /// </summary>
  [Flags]
  public enum LogOutputTarget
  {
    /// <summary>
    /// 出力しない
    /// </summary>
    None    = 0,
    /// <summary>
    /// メモリに記録する
    /// </summary>
    Memory  = 1,
    /// <summary>
    /// コンソールへ出力する
    /// </summary>
    Console = 2,
    /// <summary>
    /// ファイルに記録する
    /// </summary>
    File    = 4,
    /// <summary>
    /// 全てに記録する
    /// </summary>
    All     = 0xFFFF,
  }

  public class Logger
  {
    /// <summary>
    /// ログの出力先読み取り専用コレクションを取得します
    /// </summary>
    public static IList<System.IO.TextWriter> Writers { get { return writers.AsReadOnly(); } }
    /// <summary>
    /// 出力するログレベルを取得および設定します
    /// </summary>
    public static LogLevel Level { get; set; }

    /// <summary>
    /// ログの出力先を追加します
    /// </summary>
    /// <param name="writer">出力先として追加するTextWriter</param>
    public static void AddWriter(System.IO.TextWriter writer)
    {
      lock (writeLock) {
        writers.Add(writer);
      }
    }

    /// <summary>
    /// 指定したWriterをログ出力先から外します
    /// </summary>
    public static void RemoveWriter(System.IO.TextWriter writer)
    {
      lock (writeLock) {
        writers.Remove(writer);
      }
    }

    /// <summary>
    /// 全てのログ出力先を外します
    /// </summary>
    public static void ClearWriter()
    {
      lock (writeLock) {
        writers.Clear();
      }
    }

    private static List<System.IO.TextWriter> writers = new List<System.IO.TextWriter>();
    private static object writeLock = new Object();
    static Logger()
    {
      Level = LogLevel.Warn;
    }

    static private void Output(LogLevel level, string source, string format, params object[] args)
    {
      lock (writeLock) {
        if (level<=Level) {
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
              String.Format("{0:s} [{1}] {2} {3} {4} - {5}",
                DateTime.Now,
                System.Threading.Thread.CurrentThread.Name,
                level_name[(int)level],
                source,
                "",
                String.Format(format, args));
            foreach (var writer in writers) {
              writer.WriteLine(message);
            }
          }
          catch (FormatException e) {
            Output(level, source, e);
          }
        }
      }
    }

    static private void Output(LogLevel level, string source, Exception e)
    {
      lock (writeLock) {
        Output(level, source, "{0} {1}\n{2}", e.GetType().Name, e.Message, e.StackTrace);
        if (e.InnerException!=null) {
          Output(level, source, e.InnerException);
        }
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
    public void Fatal(object format, params object[] args)
    {
      Output(LogLevel.Fatal, this.Source, format.ToString(), args);
    }

    /// <summary>
    /// Fatalレベルのログとして例外を整形して出力します
    /// </summary>
    /// <param name="exception">出力する例外</param>
    public void Fatal(Exception exception)
    {
      Output(LogLevel.Fatal, this.Source, exception);
    }

    /// <summary>
    /// Errorレベルのログとして整形したメッセージを出力します
    /// </summary>
    /// <param name="format">出力するフォーマット文字列</param>
    /// <param name="args">フォーマットへの引数</param>
    public void Error(object format, params object[] args)
    {
      Output(LogLevel.Error, this.Source, format.ToString(), args);
    }

    /// <summary>
    /// Errorレベルのログとして例外を整形して出力します
    /// </summary>
    /// <param name="exception">出力する例外</param>
    public void Error(Exception exception)
    {
      Output(LogLevel.Error, this.Source, exception);
    }

    /// <summary>
    /// Warnレベルのログとして整形したメッセージを出力します
    /// </summary>
    /// <param name="format">出力するフォーマット文字列</param>
    /// <param name="args">フォーマットへの引数</param>
    public void Warn(object format, params object[] args)
    {
      Output(LogLevel.Warn, this.Source, format.ToString(), args);
    }

    /// <summary>
    /// Warnレベルのログとして例外を整形して出力します
    /// </summary>
    /// <param name="exception">出力する例外</param>
    public void Warn(Exception exception)
    {
      Output(LogLevel.Warn, this.Source, exception);
    }

    /// <summary>
    /// Infoレベルのログとして整形したメッセージを出力します
    /// </summary>
    /// <param name="format">出力するフォーマット文字列</param>
    /// <param name="args">フォーマットへの引数</param>
    public void Info(object format, params object[] args)
    {
      Output(LogLevel.Info, this.Source, format.ToString(), args);
    }

    /// <summary>
    /// Infoレベルのログとして例外を整形して出力します
    /// </summary>
    /// <param name="exception">出力する例外</param>
    public void Info(Exception exception)
    {
      Output(LogLevel.Info, this.Source, exception);
    }

    /// <summary>
    /// Debugレベルのログとして整形したメッセージを出力します
    /// </summary>
    /// <param name="format">出力するフォーマット文字列</param>
    /// <param name="args">フォーマットへの引数</param>
    public void Debug(object format, params object[] args)
    {
      Output(LogLevel.Debug, this.Source, format.ToString(), args);
    }

    /// <summary>
    /// Debugレベルのログとして例外を整形して出力します
    /// </summary>
    /// <param name="exception">出力する例外</param>
    public void Debug(Exception exception)
    {
      Output(LogLevel.Debug, this.Source, exception);
    }
  }
}
