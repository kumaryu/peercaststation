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
using System.Linq;

namespace PeerCastStation.Core
{
  /// <summary>
  /// チャンネルへの接続制御を行なうクラスです
  /// </summary>
  public class AccessController
  {
    /// <summary>
    /// 所属するPeerCastオブジェクトを取得します
    /// </summary>
    public PeerCast PeerCast { get; private set; }
    /// <summary>
    /// PeerCast全体での最大リレー数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxRelays {
      get { return maxRelays; }
      set { if (maxRelays!=value) { maxRelays = value; } }
    }

    /// <summary>
    /// 配信チャンネル毎の最大リレー数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxRelaysPerBroadcastChannel {
      get { return maxRelaysPerBroadcastChannel; }
      set { maxRelaysPerBroadcastChannel = value; }
    }

    /// <summary>
    /// リレーチャンネル毎の最大リレー数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxRelaysPerRelayChannel {
      get { return maxRelaysPerRelayChannel; }
      set { maxRelaysPerRelayChannel = value; }
    }

    /// <summary>
    /// PeerCast全体での最大視聴数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxPlays {
      get { return maxPlays; }
      set { if (maxPlays!=value) { maxPlays = value; }  }
    }

    /// <summary>
    /// 配信チャンネル毎の最大視聴数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxPlaysPerBroadcastChannel {
      get { return maxPlaysPerBroadcastChannel; }
      set { maxPlaysPerBroadcastChannel = value; }
    }

    /// <summary>
    /// リレーチャンネル毎の最大視聴数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxPlaysPerRelayChannel {
      get { return maxPlaysPerRelayChannel; }
      set { maxPlaysPerRelayChannel = value; }
    }

    /// <summary>
    /// PeerCast全体での最大上り帯域を取得および設定します。
    /// </summary>
    /// <value>負数は無制限です。</value>
    public int MaxUpstreamRate {
      get { return maxUpstreamRate; }
      set { if (maxUpstreamRate!=value) { maxUpstreamRate = value; }  }
    }

    public int MaxUpstreamRateIPv6 {
      get { return maxUpstreamRateIPv6; }
      set { if (maxUpstreamRateIPv6!=value) { maxUpstreamRateIPv6 = value; }  }
    }

    /// <summary>
    /// 配信チャンネル毎の最大上り帯域を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxUpstreamRatePerBroadcastChannel {
      get { return maxUpstreamRatePerBroadcastChannel; }
      set { maxUpstreamRatePerBroadcastChannel = value; }
    }

    /// <summary>
    /// リレーチャンネル毎の最大上り帯域を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxUpstreamRatePerRelayChannel {
      get { return maxUpstreamRatePerRelayChannel; }
      set { maxUpstreamRatePerRelayChannel = value; }
    }

    private int maxRelays = 0;
    private int maxRelaysPerBroadcastChannel = 0;
    private int maxRelaysPerRelayChannel = 0;
    private int maxPlays = 0;
    private int maxPlaysPerBroadcastChannel = 0;
    private int maxPlaysPerRelayChannel = 0;
    private int maxUpstreamRate = 0;
    private int maxUpstreamRateIPv6 = 0;
    private int maxUpstreamRatePerBroadcastChannel = 0;
    private int maxUpstreamRatePerRelayChannel = 0;

    /// <summary>
    /// 指定したチャンネルに新しいリレー接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">リレー接続先のチャンネル</param>
    /// <returns>リレー可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelRelayable(Channel channel)
    {
      return IsChannelRelayable(channel, false);
    }

    /// <summary>
    /// 指定したチャンネルに新しいリレー接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">リレー接続先のチャンネル</param>
    /// <param name="local">接続しようとする接続がローカル接続かどうか</param>
    /// <returns>リレー可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelRelayable(Channel channel, bool local)
    {
      switch (channel.Network) {
      case NetworkType.IPv6:
        return IsChannelRelayableIPv6(channel, local);
      case NetworkType.IPv4:
      default:
        return IsChannelRelayableIPv4(channel, local);
      }
    }

    protected bool IsChannelRelayableIPv4(Channel channel, bool local)
    {
      var channels = PeerCast.Channels.Where(c => c.Network==channel.Network);
      int channel_bitrate = local ? 0 : channel.ChannelInfo.Bitrate;
      var total_upstream_rate = channels.Sum(c => c.GetUpstreamRate());
      var channel_upstream_rate = channel.GetUpstreamRate();
      if (channel.IsBroadcasting) {
        return
          (this.MaxRelays<=0 || this.MaxRelays>channels.Sum(c => c.LocalRelays)) &&
          (this.MaxRelaysPerBroadcastChannel<=0 || this.MaxRelaysPerBroadcastChannel>channel.LocalRelays) &&
          (this.MaxUpstreamRate<0 || this.MaxUpstreamRate>=total_upstream_rate+channel_bitrate) &&
          (this.MaxUpstreamRatePerBroadcastChannel<=0 || this.MaxUpstreamRatePerBroadcastChannel>=channel_upstream_rate+channel_bitrate);
      }
      else {
        return
          (this.MaxRelays<=0 || this.MaxRelays>channels.Sum(c => c.LocalRelays)) &&
          (this.MaxRelaysPerRelayChannel<=0 || this.MaxRelaysPerRelayChannel>channel.LocalRelays) &&
          (this.MaxUpstreamRate<0 || this.MaxUpstreamRate>=total_upstream_rate+channel_bitrate) &&
          (this.MaxUpstreamRatePerRelayChannel<=0 || this.MaxUpstreamRatePerRelayChannel>=channel_upstream_rate+channel_bitrate);
      }
    }

    protected bool IsChannelRelayableIPv6(Channel channel, bool local)
    {
      var channels = PeerCast.Channels.Where(c => c.Network==channel.Network);
      int channel_bitrate = local ? 0 : channel.ChannelInfo.Bitrate;
      var total_upstream_rate = channels.Sum(c => c.GetUpstreamRate());
      var channel_upstream_rate = channel.GetUpstreamRate();
      if (channel.IsBroadcasting) {
        return
          (this.MaxRelays<=0 || this.MaxRelays>channels.Sum(c => c.LocalRelays)) &&
          (this.MaxRelaysPerBroadcastChannel<=0 || this.MaxRelaysPerBroadcastChannel>channel.LocalRelays) &&
          (this.MaxUpstreamRateIPv6<0 || this.MaxUpstreamRateIPv6>=total_upstream_rate+channel_bitrate) &&
          (this.MaxUpstreamRatePerBroadcastChannel<=0 || this.MaxUpstreamRatePerBroadcastChannel>=channel_upstream_rate+channel_bitrate);
      }
      else {
        return
          (this.MaxRelays<=0 || this.MaxRelays>channels.Sum(c => c.LocalRelays)) &&
          (this.MaxRelaysPerRelayChannel<=0 || this.MaxRelaysPerRelayChannel>channel.LocalRelays) &&
          (this.MaxUpstreamRateIPv6<0 || this.MaxUpstreamRateIPv6>=total_upstream_rate+channel_bitrate) &&
          (this.MaxUpstreamRatePerRelayChannel<=0 || this.MaxUpstreamRatePerRelayChannel>=channel_upstream_rate+channel_bitrate);
      }
    }

    /// <summary>
    /// 指定したチャンネルに新しい視聴接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">視聴接続先のチャンネル</param>
    /// <returns>視聴可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelPlayable(Channel channel)
    {
      switch (channel.Network) {
      case NetworkType.IPv6:
        return IsChannelPlayableIPv6(channel, false);
      case NetworkType.IPv4:
      default:
        return IsChannelPlayableIPv4(channel, false);
      }
    }

    /// <summary>
    /// 指定したチャンネルに新しい視聴接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">視聴接続先のチャンネル</param>
    /// <param name="local">接続しようとする接続がローカル接続かどうか</param>
    /// <returns>視聴可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelPlayable(Channel channel, bool local)
    {
      switch (channel.Network) {
      case NetworkType.IPv6:
        return IsChannelPlayableIPv6(channel, local);
      case NetworkType.IPv4:
      default:
        return IsChannelPlayableIPv4(channel, local);
      }
    }

    protected bool IsChannelPlayableIPv4(Channel channel, bool local)
    {
      var channels = PeerCast.Channels.Where(c => c.Network==channel.Network);
      int channel_bitrate = local ? 0 : channel.ChannelInfo.Bitrate;
      var total_upstream_rate = channels.Sum(c => c.GetUpstreamRate());
      var channel_upstream_rate = channel.GetUpstreamRate();
      if (channel.IsBroadcasting) {
        return
          (this.MaxPlays<=0 || this.MaxPlays>channels.Sum(c => c.LocalDirects)) &&
          (this.MaxPlaysPerBroadcastChannel<=0 || this.MaxPlaysPerBroadcastChannel>channel.LocalDirects) &&
          (this.MaxUpstreamRate<0 || this.MaxUpstreamRate>=total_upstream_rate+channel_bitrate) &&
          (this.MaxUpstreamRatePerBroadcastChannel<=0 || this.MaxUpstreamRatePerBroadcastChannel>=channel_upstream_rate+channel_bitrate);
      }
      else {
        return
          (this.MaxPlays<=0 || this.MaxPlays>channels.Sum(c => c.LocalDirects)) &&
          (this.MaxPlaysPerRelayChannel<=0 || this.MaxPlaysPerRelayChannel>channel.LocalDirects) &&
          (this.MaxUpstreamRate<0 || this.MaxUpstreamRate>=total_upstream_rate+channel_bitrate) &&
          (this.MaxUpstreamRatePerRelayChannel<=0 || this.MaxUpstreamRatePerRelayChannel>=channel_upstream_rate+channel_bitrate);
      }
    }

    protected bool IsChannelPlayableIPv6(Channel channel, bool local)
    {
      var channels = PeerCast.Channels.Where(c => c.Network==channel.Network);
      int channel_bitrate = local ? 0 : channel.ChannelInfo.Bitrate;
      var total_upstream_rate = channels.Sum(c => c.GetUpstreamRate());
      var channel_upstream_rate = channel.GetUpstreamRate();
      if (channel.IsBroadcasting) {
        return
          (this.MaxPlays<=0 || this.MaxPlays>channels.Sum(c => c.LocalDirects)) &&
          (this.MaxPlaysPerBroadcastChannel<=0 || this.MaxPlaysPerBroadcastChannel>channel.LocalDirects) &&
          (this.MaxUpstreamRateIPv6<0 || this.MaxUpstreamRateIPv6>=total_upstream_rate+channel_bitrate) &&
          (this.MaxUpstreamRatePerBroadcastChannel<=0 || this.MaxUpstreamRatePerBroadcastChannel>=channel_upstream_rate+channel_bitrate);
      }
      else {
        return
          (this.MaxPlays<=0 || this.MaxPlays>channels.Sum(c => c.LocalDirects)) &&
          (this.MaxPlaysPerRelayChannel<=0 || this.MaxPlaysPerRelayChannel>channel.LocalDirects) &&
          (this.MaxUpstreamRateIPv6<0 || this.MaxUpstreamRateIPv6>=total_upstream_rate+channel_bitrate) &&
          (this.MaxUpstreamRatePerRelayChannel<=0 || this.MaxUpstreamRatePerRelayChannel>=channel_upstream_rate+channel_bitrate);
      }
    }

    /// <summary>
    /// AccessControllerオブジェクトを初期化します
    /// </summary>
    /// <param name="peercast">所属するPeerCastオブジェクト</param>
    public AccessController(PeerCast peercast)
    {
      this.PeerCast = peercast;
    }
  }

  public class AuthenticationKey
  {
    private static char[] KeyCharTable = new char[] {
      'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
      'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
      '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
      '-', '.', '_', '~', '!', '$', '&', '\'', '(', ')', '*', '+', ',', ';', '=',
    };
    private static Random random = new Random();

    public string Id       { get; private set; }
    public string Password { get; private set; }

    public AuthenticationKey()
    {
    }

    public AuthenticationKey(string id, string password)
    {
      this.Id = id;
      this.Password = password;
    }

    public static AuthenticationKey Generate()
    {
      return new AuthenticationKey(
        new String(Enumerable.Range(0, 16).Select(i => KeyCharTable[random.Next(KeyCharTable.Length)]).ToArray()),
        new String(Enumerable.Range(0, 16).Select(i => KeyCharTable[random.Next(KeyCharTable.Length)]).ToArray())
      );
    }
  }

  /// <summary>
  /// アクセス可否を判断するための情報を保持します
  /// </summary>
  public class AccessControlInfo
  {
    public OutputStreamType Accepts { get; private set; }

    public bool AuthorizationRequired { get; private set; }

    public AuthenticationKey AuthenticationKey { get; private set; }

    public AccessControlInfo(
      OutputStreamType accepts,
      bool auth_required,
      AuthenticationKey key)
    {
      this.Accepts                = accepts;
      this.AuthorizationRequired  = auth_required;
      this.AuthenticationKey      = key;
    }

    public bool CheckAuthorization(string id, string pass)
    {
      if (!AuthorizationRequired || AuthenticationKey==null) return true;
      return AuthenticationKey.Id==id && AuthenticationKey.Password==pass;
    }
  }

}
