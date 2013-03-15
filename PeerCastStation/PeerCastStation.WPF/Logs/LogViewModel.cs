// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2013 PROGRE (djyayutto@gmail.com)
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
using System.IO;
using System.Windows.Input;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;
using System.Collections.Generic;

namespace PeerCastStation.WPF.Logs
{
  class LogViewModel : ViewModelBase, IDisposable
  {
    private readonly LogWriter guiWriter = new LogWriter(1000);
    private TextWriter logFileWriter;

    private static readonly KeyValuePair<string,LogLevel>[] logLevelItems = new KeyValuePair<string,LogLevel>[] {
      new KeyValuePair<string,LogLevel>("なし", LogLevel.None),
      new KeyValuePair<string,LogLevel>("エラー全般", LogLevel.Error),
      new KeyValuePair<string,LogLevel>("エラーと警告", LogLevel.Warn),
      new KeyValuePair<string,LogLevel>("通知メッセージも含む", LogLevel.Info),
      new KeyValuePair<string,LogLevel>("デバッグメッセージも含む", LogLevel.Debug),
    };
    public IEnumerable<KeyValuePair<string,LogLevel>> LogLevelItems {
      get { return logLevelItems; }
    }

    public LogLevel LogLevel
    {
      get { return Logger.Level; }
      set
      {
        if (Logger.Level==value) return;
        Logger.Level = value;
        OnPropertyChanged("LogLevel");
      }
    }

    private bool GetLoggerOutputTarget(LoggerOutputTarget target)
    {
      return (Logger.OutputTarget & target)!=0;
    }

    private void SetLoggerOutputTarget(LoggerOutputTarget target, bool value)
    {
      if (value) {
        Logger.OutputTarget |= target;
      }
      else {
        Logger.OutputTarget &= ~target;
      }
    }

    public bool IsOutputToGui
    {
      get { return GetLoggerOutputTarget(LoggerOutputTarget.UserInterface); }
      set
      {
        if (GetLoggerOutputTarget(LoggerOutputTarget.UserInterface)==value) return;
        SetLoggerOutputTarget(LoggerOutputTarget.UserInterface, value);
        OnPropertyChanged("IsOutputToGui");
      }
    }

    public bool IsOutputToConsole
    {
      get { return GetLoggerOutputTarget(LoggerOutputTarget.Console); }
      set
      {
        if (GetLoggerOutputTarget(LoggerOutputTarget.Console)==value) return;
        SetLoggerOutputTarget(LoggerOutputTarget.Console, value);
        OnPropertyChanged("IsOutputToConsole");
      }
    }

    public bool IsOutputToFile
    {
      get { return GetLoggerOutputTarget(LoggerOutputTarget.File); }
      set
      {
        if (GetLoggerOutputTarget(LoggerOutputTarget.File)==value) return;
        SetLoggerOutputTarget(LoggerOutputTarget.File, value);
        OnPropertyChanged("IsOutputToFile");
      }
    }

    public string OutputFileName
    {
      get { return Logger.LogFileName; }
      set
      {
        if (Logger.LogFileName==value) return;
        Logger.LogFileName = value;
        OnPropertyChanged("OutputFileName");
      }
    }

    public string Log { get { return guiWriter.ToString(); } }

    private readonly ICommand clear;
    public ICommand Clear { get { return clear; } }

    internal LogViewModel()
    {
      Logger.AddWriter(guiWriter);
      clear = new Command(() =>
        {
          guiWriter.Clear();
          OnPropertyChanged("Log");
        });
    }

    public void Dispose()
    {
      Logger.RemoveWriter(guiWriter);
    }

    internal void UpdateLog()
    {
      OnPropertyChanged("Log");
    }

  }
}
