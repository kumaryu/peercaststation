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
  [Serializable]
  public class Host
  {
    /// <summary>
    /// ホストのセッションIDを取得および設定します
    /// </summary>
    public Guid SessionID { get; private set; }
    /// <summary>
    /// ホストのブロードキャストIDを取得および設定します
    /// </summary>
    public Guid BroadcastID { get; private set; }
    /// <summary>
    /// ホストが持つローカルなアドレス情報を取得します
    /// </summary>
    public IPEndPoint LocalEndPoint { get; private set; }
    /// <summary>
    /// ホストが持つグローバルなアドレス情報を取得および設定します
    /// </summary>
    public IPEndPoint GlobalEndPoint { get; private set; }
    /// <summary>
    /// ホストへの接続が可能かどうかを取得および設定します
    /// </summary>
    public bool IsFirewalled { get; private set; }
    /// <summary>
    /// リレーしている数を取得および設定します
    /// </summary>
    public int RelayCount { get; private set; }
    /// <summary>
    /// 直接視聴している数を取得および設定します
    /// </summary>
    public int DirectCount { get; private set; }
    /// <summary>
    /// このホストがトラッカーかどうかを取得および設定します
    /// </summary>
    public bool IsTracker { get; private set; }
    /// <summary>
    /// リレー数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsRelayFull { get; private set; }
    /// <summary>
    /// 直接視聴数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsDirectFull { get; private set; }
    /// <summary>
    /// コンテントの受信中かどうかを取得および設定します
    /// </summary>
    public bool IsReceiving { get; private set; }
    /// <summary>
    /// Control接続数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsControlFull { get; private set; }
    /// <summary>
    /// ホストの拡張リストを取得します
    /// </summary>
    public IList<string> Extensions { get; private set; }
    /// <summary>
    /// その他のホスト情報リストを取得します
    /// </summary>
    public IAtomCollection Extra { get; private set; }
    /// <summary>
    /// ノードの最終更新時間を取得します
    /// </summary>
    public TimeSpan LastUpdated { get; private set; }

    public int Hops { get { return Extra.GetHostUphostHops() ?? 0; } }
    public TimeSpan Uptime { get { return Extra.GetHostUptime() ?? TimeSpan.Zero; } }
    public int Version { get { return Extra.GetHostVersion() ?? 0; } }

    /// <summary>
    /// ホスト情報を初期化します
    /// </summary>
    public Host(
      Guid sessionID,
      Guid broadcastID,
      IPEndPoint localEndPoint,
      IPEndPoint globalEndPoint,
      int relayCount,
      int directCount,
      bool isFirewalled,
      bool isTracker,
      bool isRelayFull,
      bool isDirectFull,
      bool isReceiving,
      bool isControlFull,
      IEnumerable<string> extensions,
      IAtomCollection extra)
    {
      this.SessionID      = sessionID;
      this.BroadcastID    = broadcastID;
      this.LocalEndPoint  = localEndPoint;
      this.GlobalEndPoint = globalEndPoint;
      this.RelayCount     = relayCount;
      this.DirectCount    = directCount;
      this.IsFirewalled   = isFirewalled;
      this.IsTracker      = isTracker;
      this.IsRelayFull    = isRelayFull;
      this.IsDirectFull   = isDirectFull;
      this.IsReceiving    = isReceiving;
      this.IsControlFull  = isControlFull;
      this.Extensions     = new List<string>(extensions).AsReadOnly();
      this.Extra          = (new AtomCollection(extra)).AsReadOnly();
      this.LastUpdated    = TimeSpan.FromMilliseconds(Environment.TickCount);
    }
  }

  /// <summary>
  /// Hostを構築するためのヘルパークラスです
  /// </summary>
  public class HostBuilder
  {
    /// <summary>
    /// ホストのセッションIDを取得および設定します
    /// </summary>
    public Guid SessionID { get; set; }
    /// <summary>
    /// ホストのブロードキャストIDを取得および設定します
    /// </summary>
    public Guid BroadcastID { get; set; }
    /// <summary>
    /// ホストが持つローカルなアドレス情報を取得および設定します
    /// </summary>
    public IPEndPoint LocalEndPoint { get; set; }
    /// <summary>
    /// ホストが持つグローバルなアドレス情報を取得および設定します
    /// </summary>
    public IPEndPoint GlobalEndPoint { get; set; }
    /// <summary>
    /// ホストへの接続が可能かどうかを取得および設定します
    /// </summary>
    public bool IsFirewalled { get; set; }
    /// <summary>
    /// リレーしている数を取得および設定します
    /// </summary>
    public int RelayCount { get; set; }
    /// <summary>
    /// 直接視聴している数を取得および設定します
    /// </summary>
    public int DirectCount { get; set; }
    /// <summary>
    /// このホストがトラッカーかどうかを取得および設定します
    /// </summary>
    public bool IsTracker { get; set; }
    /// <summary>
    /// リレー数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsRelayFull { get; set; }
    /// <summary>
    /// 直接視聴数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsDirectFull { get; set; }
    /// <summary>
    /// コンテントの受信中かどうかを取得および設定します
    /// </summary>
    public bool IsReceiving { get; set; }
    /// <summary>
    /// Control接続数が一杯かどうかを取得および設定します
    /// </summary>
    public bool IsControlFull { get; set; }
    /// <summary>
    /// ホストの拡張リストを取得します
    /// </summary>
    public IList<string> Extensions { get; private set; }
    /// <summary>
    /// その他のホスト情報リストを取得します
    /// </summary>
    public AtomCollection Extra { get; private set; }

    /// <summary>
    /// 今の設定からHostを構築します
    /// </summary>
    /// <returns>構築されたHost</returns>
    public Host ToHost()
    {
      return new Host(
        SessionID,
        BroadcastID,
        LocalEndPoint,
        GlobalEndPoint,
        RelayCount,
        DirectCount,
        IsFirewalled,
        IsTracker,
        IsRelayFull,
        IsDirectFull,
        IsReceiving,
        IsControlFull,
        Extensions,
        Extra);
    }

    /// <summary>
    /// HostBuilderを初期化します
    /// </summary>
    public HostBuilder()
    {
      SessionID = Guid.Empty;
      BroadcastID = Guid.Empty;
      LocalEndPoint = null;
      GlobalEndPoint = null;
      RelayCount = 0;
      DirectCount = 0;
      IsFirewalled = false;
      IsTracker = false;
      IsRelayFull = false;
      IsDirectFull = false;
      IsReceiving = false;
      IsControlFull = false;
      Extensions = new List<string>();
      Extra = new AtomCollection();
    }

    /// <summary>
    /// 指定されたHostの値でHostBuilderを初期化します
    /// </summary>
    /// <param name="host">初期化元のHost</param>
    public HostBuilder(Host host)
    {
      if (host!=null) {
        SessionID = host.SessionID;
        BroadcastID = host.BroadcastID;
        LocalEndPoint = host.LocalEndPoint;
        GlobalEndPoint = host.GlobalEndPoint;
        RelayCount = host.RelayCount;
        DirectCount = host.DirectCount;
        IsFirewalled = host.IsFirewalled;
        IsTracker = host.IsTracker;
        IsRelayFull = host.IsRelayFull;
        IsDirectFull = host.IsDirectFull;
        IsReceiving = host.IsReceiving;
        IsControlFull = host.IsControlFull;
        Extensions = new List<string>(host.Extensions);
        Extra = new AtomCollection(host.Extra);
      }
      else {
        SessionID = Guid.Empty;
        BroadcastID = Guid.Empty;
        LocalEndPoint = null;
        GlobalEndPoint = null;
        RelayCount = 0;
        DirectCount = 0;
        IsFirewalled = false;
        IsTracker = false;
        IsRelayFull = false;
        IsDirectFull = false;
        IsReceiving = false;
        IsControlFull = false;
        Extensions = new List<string>();
        Extra = new AtomCollection();
      }
    }

    /// <summary>
    /// 指定されたHostBuilderの値でHostBuilderを初期化します
    /// </summary>
    /// <param name="host">初期化元のHostBuilder</param>
    public HostBuilder(HostBuilder host)
    {
      if (host!=null) {
        SessionID = host.SessionID;
        BroadcastID = host.BroadcastID;
        LocalEndPoint = host.LocalEndPoint;
        GlobalEndPoint = host.GlobalEndPoint;
        RelayCount = host.RelayCount;
        DirectCount = host.DirectCount;
        IsFirewalled = host.IsFirewalled;
        IsTracker = host.IsTracker;
        IsRelayFull = host.IsRelayFull;
        IsDirectFull = host.IsDirectFull;
        IsReceiving = host.IsReceiving;
        IsControlFull = host.IsControlFull;
        Extensions = new List<string>(host.Extensions);
        Extra = new AtomCollection(host.Extra);
      }
      else {
        SessionID = Guid.Empty;
        BroadcastID = Guid.Empty;
        LocalEndPoint = null;
        GlobalEndPoint = null;
        RelayCount = 0;
        DirectCount = 0;
        IsFirewalled = false;
        IsTracker = false;
        IsRelayFull = false;
        IsDirectFull = false;
        IsReceiving = false;
        IsControlFull = false;
        Extensions = new List<string>();
        Extra = new AtomCollection();
      }
    }
  }

}
