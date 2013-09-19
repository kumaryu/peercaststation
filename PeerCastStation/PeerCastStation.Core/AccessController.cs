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
    /// チャンネル毎の最大リレー数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxRelaysPerChannel {
      get { return maxRelaysPerChannel; }
      set { if (maxRelaysPerChannel!=value) { maxRelaysPerChannel = value; } }
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
    /// チャンネル毎の最大視聴数を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxPlaysPerChannel {
      get { return maxPlaysPerChannel; }
      set { if (maxPlaysPerChannel!=value) { maxPlaysPerChannel = value; }  }
    }
    /// <summary>
    /// PeerCast全体での最大上り帯域を取得および設定します。
    /// </summary>
    /// <value>0は無制限です。</value>
    public int MaxUpstreamRate {
      get { return maxUpstreamRate; }
      set { if (maxUpstreamRate!=value) { maxUpstreamRate = value; }  }
    }

    private int maxRelays = 0;
    private int maxRelaysPerChannel = 0;
    private int maxPlays = 0;
    private int maxPlaysPerChannel = 0;
    private int maxUpstreamRate = 0;

    /// <summary>
    /// 指定したチャンネルに新しいリレー接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">リレー接続先のチャンネル</param>
    /// <returns>リレー可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelRelayable(Channel channel)
    {
      int channel_bitrate = channel.ChannelInfo.Bitrate;
      var upstream_rate = PeerCast.Channels.Sum(c => c.OutputStreams.Sum(o => o.IsLocal ? 0 : o.UpstreamRate));
      return
        (this.MaxRelays<=0 || this.MaxRelays>PeerCast.Channels.Sum(c => c.LocalRelays)) &&
        (this.MaxRelaysPerChannel<=0 || this.MaxRelaysPerChannel>channel.LocalRelays) &&
        (this.MaxUpstreamRate<=0 || this.MaxUpstreamRate>=upstream_rate+channel_bitrate);
    }

    /// <summary>
    /// 指定したチャンネルに新しいリレー接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">リレー接続先のチャンネル</param>
    /// <param name="output_stream">接続しようとするOutputStream</param>
    /// <returns>リレー可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelRelayable(Channel channel, IOutputStream output_stream)
    {
      var upstream_rate = PeerCast.Channels.Sum(c => c.OutputStreams.Sum(o => o.IsLocal ? 0 : o.UpstreamRate));
      return
        (this.MaxRelays<=0 || this.MaxRelays>PeerCast.Channels.Sum(c => c.LocalRelays)) &&
        (this.MaxRelaysPerChannel<=0 || this.MaxRelaysPerChannel>channel.LocalRelays) &&
        (this.MaxUpstreamRate<=0 || this.MaxUpstreamRate>=upstream_rate+(output_stream.IsLocal ? 0 : output_stream.UpstreamRate));
    }

    /// <summary>
    /// 指定したチャンネルに新しい視聴接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">視聴接続先のチャンネル</param>
    /// <returns>視聴可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelPlayable(Channel channel)
    {
      int channel_bitrate = channel.ChannelInfo.Bitrate;
      var upstream_rate = PeerCast.Channels.Sum(c => c.OutputStreams.Sum(o => o.IsLocal ? 0 : o.UpstreamRate));
      return
        (this.MaxPlays<=0 || this.MaxPlays>PeerCast.Channels.Sum(c => c.LocalDirects)) &&
        (this.MaxPlaysPerChannel<=0 || this.MaxPlaysPerChannel>channel.LocalDirects) &&
        (this.MaxUpstreamRate<=0 || this.MaxUpstreamRate>=upstream_rate+channel_bitrate);
    }

    /// <summary>
    /// 指定したチャンネルに新しい視聴接続ができるかどうかを取得します
    /// </summary>
    /// <param name="channel">視聴接続先のチャンネル</param>
    /// <param name="output_stream">接続しようとするOutputStream</param>
    /// <returns>視聴可能な場合はtrue、それ以外はfalse</returns>
    public virtual bool IsChannelPlayable(Channel channel, IOutputStream output_stream)
    {
      var upstream_rate = PeerCast.Channels.Sum(c => c.OutputStreams.Sum(o => o.IsLocal ? 0 : o.UpstreamRate));
      return
        (this.MaxPlays<=0 || this.MaxPlays>PeerCast.Channels.Sum(c => c.LocalDirects)) &&
        (this.MaxPlaysPerChannel<=0 || this.MaxPlaysPerChannel>channel.LocalDirects) &&
        (this.MaxUpstreamRate<=0 || this.MaxUpstreamRate>=upstream_rate+(output_stream.IsLocal ? 0 : output_stream.UpstreamRate));
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
    public AuthenticationKey AuthenticationKey { get; private set; }
    public AccessControlInfo(AuthenticationKey key)
    {
      this.AuthenticationKey = key;
    }

    public bool CheckAuthorization(string id, string pass)
    {
      if (AuthenticationKey==null) return true;
      return AuthenticationKey.Id==id && AuthenticationKey.Password==pass;
    }
  }

}
