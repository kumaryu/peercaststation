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
using System.ComponentModel;
using System.Net;

namespace PeerCastStation.Core
{
  /// <summary>
  /// 接続情報を保持するクラスです
  /// </summary>
  public class Host
    : INotifyPropertyChanged
  {
    private IPEndPoint localEndPoint = null;
    private IPEndPoint globalEndPoint = null;
    private Guid sessionID = Guid.Empty;
    private Guid broadcastID = Guid.Empty;
    private int relayCount = 0;
    private int directCount = 0;
    private bool isFirewalled = true;
    private bool isRelayFull = false;
    private bool isDirectFull = false;
    private bool isReceiving = false;
    private bool isControlFull = false;
    private TimeSpan lastUpdated = TimeSpan.FromMilliseconds(Environment.TickCount);
    private System.Collections.ObjectModel.ObservableCollection<string> extensions = new System.Collections.ObjectModel.ObservableCollection<string>();
    private AtomCollection extra = new AtomCollection();
    /// <summary>
    /// ホストが持つローカルなアドレス情報を取得および設定します
    /// </summary>
    public IPEndPoint LocalEndPoint {
      get { return localEndPoint; }
      set
      {
        if (localEndPoint!=value) {
          localEndPoint = value;
          OnPropertyChanged("LocalEndPoint");
        }
      }
    }
    /// <summary>
    /// ホストが持つグローバルなアドレス情報を取得および設定します
    /// </summary>
    public IPEndPoint GlobalEndPoint {
      get { return globalEndPoint; }
      set
      {
        if (globalEndPoint!=value) {
          globalEndPoint = value;
          OnPropertyChanged("GlobalEndPoint");
        }
      }
    }
    /// <summary>
    /// ホストのセッションIDを取得および設定します
    /// </summary>
    public Guid SessionID {
      get { return sessionID; }
      set
      {
        if (sessionID!=value) {
          sessionID = value;
          OnPropertyChanged("SessionID");
        }
      }
    }
    /// <summary>
    /// ホストのブロードキャストIDを取得および設定します
    /// </summary>
    public Guid BroadcastID {
      get { return broadcastID; }
      set
      {
        if (broadcastID!=value) {
          broadcastID = value;
          OnPropertyChanged("BroadcastID");
        }
      }
    }
    /// <summary>
    /// ホストの拡張リストを取得します
    /// </summary>
    public IList<string> Extensions { get { return extensions; } }
    /// <summary>
    /// その他のホスト情報リストを取得します
    /// </summary>
    public AtomCollection Extra { get { return extra; } }

    /// <summary>
    /// ホストへの接続が可能かどうかを取得および設定します
    /// </summary>
    public bool IsFirewalled {
      get { return isFirewalled; }
      set
      {
        if (isFirewalled!=value) {
          isFirewalled = value;
          OnPropertyChanged("IsFirewalled");
        }
      }
    }

    /// <summary>
    /// リレーしている数を取得および設定します
    /// </summary>
    public int RelayCount {
      get { return relayCount; }
      set
      {
        if (relayCount!=value) {
          relayCount = value;
          OnPropertyChanged("RelayCount");
        }
      }
    }
    /// <summary>
    /// 直接視聴している数を取得および設定します
    /// </summary>
    public int DirectCount {
      get { return directCount; }
      set
      {
        if (directCount!=value) {
          directCount = value;
          OnPropertyChanged("DirectCount");
        }
      }
    }
    /// <summary>
    /// リレー数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsRelayFull {
      get { return isRelayFull; }
      set
      {
        if (isRelayFull!=value) {
          isRelayFull = value;
          OnPropertyChanged("IsRelayFull");
        }
      }
    }
    /// <summary>
    /// 直接視聴数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsDirectFull {
      get { return isDirectFull; }
      set
      {
        if (isDirectFull!=value) {
          isDirectFull = value;
          OnPropertyChanged("IsDirectFull");
        }
      }
    }

    /// <summary>
    /// コンテントの受信中かどうかを取得および設定します
    /// </summary>
    public bool IsReceiving {
      get { return isReceiving; }
      set
      {
        if (isReceiving!=value) {
          isReceiving = value;
          OnPropertyChanged("IsReceiving");
        }
      }
    }

    /// <summary>
    /// Control接続数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsControlFull {
      get { return isControlFull; }
      set
      {
        if (isControlFull!=value) {
          isControlFull = value;
          OnPropertyChanged("IsControlFull");
        }
      }
    }

    /// <summary>
    /// ノードの最終更新時間を取得します
    /// </summary>
    public TimeSpan LastUpdated {
      get { return lastUpdated; }
    }

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged(string name)
    {
      lastUpdated = TimeSpan.FromMilliseconds(Environment.TickCount);
      if (PropertyChanged != null) {
        PropertyChanged(this, new PropertyChangedEventArgs(name));
      }
    }

    /// <summary>
    /// ホスト情報を初期化します
    /// </summary>
    public Host()
    {
      extensions.CollectionChanged += (sender, e) => { OnPropertyChanged("Extensions"); };
      extra.CollectionChanged += (sender, e) => { OnPropertyChanged("Extra"); };
    }
  }

}
