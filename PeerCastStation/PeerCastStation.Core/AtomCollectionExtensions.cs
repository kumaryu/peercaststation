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
using System.Net;

namespace PeerCastStation.Core
{
  public static class AtomCollectionExtensions
  {
    public static int? GetIntFrom(IAtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      int value = 0;
      if (atom != null && atom.TryGetInt32(out value)) {
        return value;
      }
      else {
        return null;
      }
    }

    public static uint? GetUIntFrom(IAtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      uint value = 0;
      if (atom != null && atom.TryGetUInt32(out value)) {
        return value;
      }
      else {
        return null;
      }
    }

    public static short? GetShortFrom(IAtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      short value = 0;
      if (atom != null && atom.TryGetInt16(out value)) {
        return value;
      }
      else {
        return null;
      }
    }

    public static ushort? GetUShortFrom(IAtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      ushort value = 0;
      if (atom != null && atom.TryGetUInt16(out value)) {
        return value;
      }
      else {
        return null;
      }
    }

    public static byte? GetByteFrom(IAtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      byte value = 0;
      if (atom != null && atom.TryGetByte(out value)) {
        return value;
      }
      else {
        return null;
      }
    }

    public static Guid? GetIDFrom(IAtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      byte[] value = null;
      if (atom != null && atom.TryGetBytes(out value) && value.Length==16) {
        return ByteArrayToID(value);
      }
      else {
        return null;
      }
    }

    public static ID4? GetID4From(IAtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      byte[] value = null;
      if (atom != null && atom.TryGetBytes(out value) && value.Length==4) {
        return new ID4(value);
      }
      else {
        return null;
      }
    }

    public static byte[] GetBytesFrom(IAtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      byte[] value = null;
      if (atom != null && atom.TryGetBytes(out value)) {
        return value;
      }
      else {
        return null;
      }
    }

    public static IPAddress GetIPAddressFrom(IAtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      IPAddress value = null;
      if (atom != null && atom.TryGetIPAddress(out value)) {
        return value;
      }
      else {
        return null;
      }
    }

    public static string GetStringFrom(IAtomCollection collection, ID4 name)
    {
      string res = null;
      var atom = collection.FindByName(name);
      if (atom != null && atom.TryGetString(out res)) {
        return res;
      }
      else {
        return null;
      }
    }

    public static Atom GetAtomFrom(IAtomCollection collection, ID4 name)
    {
      return collection.FindByName(name);
    }

    public static IAtomCollection GetCollectionFrom(IAtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      if (atom != null && atom.HasChildren) {
        return atom.Children;
      }
      else {
        return null;
      }
    }

    public static void SetAtomTo(IAtomCollection collection, Atom value)
    {
      for (var i = 0; i < collection.Count; i++) {
        if (value.Name == collection[i].Name) {
          collection[i] = value;
          return;
        }
      }
      collection.Add(value);
    }

    public static byte[] IDToByteArray(Guid value)
    {
      if (BitConverter.IsLittleEndian) {
        var value_le = value.ToByteArray();
        var value_be = new byte[16] {
          value_le[3], value_le[2], value_le[1], value_le[0],
          value_le[5], value_le[4],
          value_le[7], value_le[6],
          value_le[8],
          value_le[9],
          value_le[10],
          value_le[11],
          value_le[12],
          value_le[13],
          value_le[14],
          value_le[15],
        };
        return value_be;
      }
      else {
        return value.ToByteArray();
      }
    }

    public static Guid ByteArrayToID(byte[] value)
    {
      if (value==null) throw new ArgumentNullException("value");
      if (value.Length<16) throw new ArgumentException("value");
      if (BitConverter.IsLittleEndian) {
        var value_le = new byte[16] {
          value[3], value[2], value[1], value[0],
          value[5], value[4],
          value[7], value[6],
          value[8],
          value[9],
          value[10],
          value[11],
          value[12],
          value[13],
          value[14],
          value[15],
        };
        return new Guid(value_le);
      }
      else {
        return new Guid(value);
      }
    }

    public static IAtomCollection GetHelo(this IAtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_HELO);
    }

    public static string GetHeloAgent(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_HELO_AGENT);
    }

    public static Guid? GetHeloSessionID(this IAtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_HELO_SESSIONID);
    }

    public static int? GetHeloPort(this IAtomCollection collection)
    {
      return (int?)GetUShortFrom(collection, Atom.PCP_HELO_PORT);
    }

    public static int? GetHeloPing(this IAtomCollection collection)
    {
      return (int?)GetUShortFrom(collection, Atom.PCP_HELO_PING);
    }

    public static IPAddress GetHeloRemoteIP(this IAtomCollection collection)
    {
      return GetIPAddressFrom(collection, Atom.PCP_HELO_REMOTEIP);
    }

    public static int? GetHeloRemotePort(this IAtomCollection collection)
    {
      return (int?)GetUShortFrom(collection, Atom.PCP_HELO_REMOTEPORT);
    }

    public static int? GetHeloVersion(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HELO_VERSION);
    }

    public static Guid? GetHeloBCID(this IAtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_HELO_BCID);
    }

    public static int? GetHeloDisable(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HELO_DISABLE);
    }

    public static IAtomCollection GetOleh(this IAtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_OLEH);
    }

    public static int? GetOk(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_OK);
    }

    public static IAtomCollection GetChan(this IAtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_CHAN);
    }

    public static Guid? GetChanID(this IAtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_CHAN_ID);
    }

    public static Guid? GetChanBCID(this IAtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_CHAN_BCID);
    }

    public static IAtomCollection GetChanPkt(this IAtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_CHAN_PKT);
    }

    public static ID4? GetChanPktType(this IAtomCollection collection)
    {
      return GetID4From(collection, Atom.PCP_CHAN_PKT_TYPE);
    }

    public static uint? GetChanPktPos(this IAtomCollection collection)
    {
      return GetUIntFrom(collection, Atom.PCP_CHAN_PKT_POS);
    }

    public static PCPChanPacketContinuation GetChanPktCont(this IAtomCollection collection)
    {
      return (PCPChanPacketContinuation)(GetByteFrom(collection, Atom.PCP_CHAN_PKT_CONTINUATION) ?? 0);
    }

    public static byte[] GetChanPktData(this IAtomCollection collection)
    {
      return GetBytesFrom(collection, Atom.PCP_CHAN_PKT_DATA);
    }

    public static IAtomCollection GetChanInfo(this IAtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_CHAN_INFO);
    }

    public static string GetChanInfoType(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_TYPE);
    }

    public static int? GetChanInfoBitrate(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_CHAN_INFO_BITRATE);
    }

    public static string GetChanInfoGenre(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_GENRE);
    }

    public static string GetChanInfoName(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_NAME);
    }

    public static string GetChanInfoURL(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_URL);
    }

    public static string GetChanInfoDesc(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_DESC);
    }

    public static string GetChanInfoComment(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_COMMENT);
    }

    public static int? GetChanInfoPPFlags(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_CHAN_INFO_PPFLAGS);
    }

    public static string GetChanInfoStreamType(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_STREAMTYPE);
    }

    public static string GetChanInfoStreamExt(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_STREAMEXT);
    }

    public static IAtomCollection GetChanTrack(this IAtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_CHAN_TRACK);
    }

    public static string GetChanTrackTitle(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_TRACK_TITLE);
    }

    public static string GetChanTrackCreator(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_TRACK_CREATOR);
    }

    public static string GetChanTrackURL(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_TRACK_URL);
    }

    public static string GetChanTrackAlbum(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_TRACK_ALBUM);
    }

    public static string GetChanTrackGenre(this IAtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_TRACK_GENRE);
    }

    public static IAtomCollection GetBcst(this IAtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_BCST);
    }

    public static byte? GetBcstTTL(this IAtomCollection collection)
    {
      return GetByteFrom(collection, Atom.PCP_BCST_TTL);
    }

    public static byte? GetBcstHops(this IAtomCollection collection)
    {
      return GetByteFrom(collection, Atom.PCP_BCST_HOPS);
    }

    public static Guid? GetBcstFrom(this IAtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_BCST_FROM);
    }

    public static Guid? GetBcstDest(this IAtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_BCST_DEST);
    }

    public static BroadcastGroup? GetBcstGroup(this IAtomCollection collection)
    {
      var res = GetByteFrom(collection, Atom.PCP_BCST_GROUP);
      if (res.HasValue) {
        return (BroadcastGroup)res.Value;
      }
      else {
        return null;
      }
    }

    public static Guid? GetBcstChannelID(this IAtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_BCST_CHANID);
    }

    public static int? GetBcstVersion(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_BCST_VERSION);
    }

    public static int? GetBcstVersionVP(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_BCST_VERSION_VP);
    }

    public static byte[] GetBcstVersionEXPrefix(this IAtomCollection collection)
    {
      return GetBytesFrom(collection, Atom.PCP_BCST_VERSION_EX_PREFIX);
    }

    public static short? GetBcstVersionEXNumber(this IAtomCollection collection)
    {
      return GetShortFrom(collection, Atom.PCP_BCST_VERSION_EX_NUMBER);
    }

    public static IAtomCollection GetHost(this IAtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_HOST);
    }

    public static Guid? GetHostSessionID(this IAtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_HOST_ID);
    }

    public static IPEndPoint[] GetHostEndPoints(this IAtomCollection collection)
    {
      var addresses = new List<IPAddress>();
      var ports = new List<ushort>();
      foreach (var atom in collection) {
        if (atom.Name==Atom.PCP_HOST_IP) {
          IPAddress value;
          if (atom.TryGetIPAddress(out value)) {
            addresses.Add(value);
          }
        }
        else if (atom.Name==Atom.PCP_HOST_PORT) {
          ushort value;
          if (atom.TryGetUInt16(out value)) {
            ports.Add(value);
          }
        }
      }
      var cnt = Math.Min(addresses.Count, ports.Count);
      var res = new IPEndPoint[cnt];
      for (var i=0; i<cnt; i++) {
        res[i] = new IPEndPoint(addresses[i], ports[i]);
      }
      return res;
    }

    public static IPAddress GetHostIP(this IAtomCollection collection)
    {
      return GetIPAddressFrom(collection, Atom.PCP_HOST_IP);
    }

    public static int? GetHostPort(this IAtomCollection collection)
    {
      return (int?)GetUShortFrom(collection, Atom.PCP_HOST_PORT);
    }

    public static Guid? GetHostChannelID(this IAtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_HOST_CHANID);
    }

    public static int? GetHostNumListeners(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_NUML);
    }

    public static int? GetHostNumRelays(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_NUMR);
    }

    public static TimeSpan? GetHostUptime(this IAtomCollection collection)
    {
      int? res = GetIntFrom(collection, Atom.PCP_HOST_UPTIME);
      if (res.HasValue) {
        return TimeSpan.FromSeconds(res.Value);
      }
      else {
        return null;
      }
    }

    public static int? GetHostVersion(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_VERSION);
    }

    public static int? GetHostVersionVP(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_VERSION_VP);
    }

    public static byte[] GetHostVersionEXPrefix(this IAtomCollection collection)
    {
      return GetBytesFrom(collection, Atom.PCP_HOST_VERSION_EX_PREFIX);
    }

    public static short? GetHostVersionEXNumber(this IAtomCollection collection)
    {
      return GetShortFrom(collection, Atom.PCP_HOST_VERSION_EX_NUMBER);
    }

    public static int? GetHostClapPP(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_CLAP_PP);
    }

    public static uint? GetHostOldPos(this IAtomCollection collection)
    {
      return GetUIntFrom(collection, Atom.PCP_HOST_OLDPOS);
    }

    public static uint? GetHostNewPos(this IAtomCollection collection)
    {
      return GetUIntFrom(collection, Atom.PCP_HOST_NEWPOS);
    }

    public static PCPHostFlags1? GetHostFlags1(this IAtomCollection collection)
    {
      return (PCPHostFlags1?)GetByteFrom(collection, Atom.PCP_HOST_FLAGS1);
    }

    public static IPAddress GetHostUphostIP(this IAtomCollection collection)
    {
      return GetIPAddressFrom(collection, Atom.PCP_HOST_UPHOST_IP);
    }

    public static int? GetHostUphostPort(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_UPHOST_PORT);
    }

    public static IPEndPoint GetHostUphostEndPoint(this IAtomCollection collection)
    {
      var ip   = GetIPAddressFrom(collection, Atom.PCP_HOST_UPHOST_IP);
      var port = GetIntFrom(collection, Atom.PCP_HOST_UPHOST_PORT);
      if (ip!=null && port!=null) {
        return new IPEndPoint(ip, port.Value);
      }
      else {
        return null;
      }
    }

    public static int? GetHostUphostHops(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_UPHOST_HOPS);
    }

    public static int? GetQuit(this IAtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_QUIT);
    }

    public static void SetHelo(this IAtomCollection collection, IList<Atom> value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO, value));
    }

    public static void SetHeloAgent(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_AGENT, value));
    }

    public static void SetHeloBCID(this IAtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_BCID, IDToByteArray(value)));
    }

    public static void SetHeloDisable(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_DISABLE, value));
    }

    public static void SetHeloPing(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_PING, (ushort)value));
    }

    public static void SetHeloPort(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_PORT, (ushort)value));
    }

    public static void SetHeloRemotePort(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_REMOTEPORT, (ushort)value));
    }

    public static void SetHeloRemoteIP(this IAtomCollection collection, IPAddress value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_REMOTEIP, value));
    }

    public static void SetHeloSessionID(this IAtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_SESSIONID, IDToByteArray(value)));
    }

    public static void SetHeloVersion(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_VERSION, value));
    }

    public static void SetBcst(this IAtomCollection collection, IList<Atom> value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST, value));
    }

    public static void SetBcstChannelID(this IAtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_CHANID, IDToByteArray(value)));
    }

    public static void SetBcstDest(this IAtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_DEST, IDToByteArray(value)));
    }

    public static void SetBcstFrom(this IAtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_FROM, IDToByteArray(value)));
    }

    public static void SetBcstGroup(this IAtomCollection collection, BroadcastGroup value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_GROUP, (byte)value));
    }

    public static void SetBcstHops(this IAtomCollection collection, byte value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_HOPS, value));
    }

    public static void SetBcstTTL(this IAtomCollection collection, byte value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_TTL, value));
    }

    public static void SetBcstVersion(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_VERSION, value));
    }

    public static void SetBcstVersionVP(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_VERSION_VP, value));
    }

    public static void SetBcstVersionEXNumber(this IAtomCollection collection, short value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_VERSION_EX_NUMBER, value));
    }

    public static void SetBcstVersionEXPrefix(this IAtomCollection collection, byte[] value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_VERSION_EX_PREFIX, value));
    }

    public static void SetChan(this IAtomCollection collection, IList<Atom> value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN, value));
    }

    public static void SetChanBCID(this IAtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_BCID, IDToByteArray(value)));
    }

    public static void SetChanID(this IAtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_ID, IDToByteArray(value)));
    }

    public static void SetChanInfo(this IAtomCollection collection, IList<Atom> value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO, value));
    }

    public static void SetChanInfoBitrate(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_BITRATE, value));
    }

    public static void SetChanInfoPPFlags(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_PPFLAGS, value));
    }

    public static void SetChanInfoComment(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_COMMENT, value));
    }

    public static void SetChanInfoDesc(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_DESC, value));
    }

    public static void SetChanInfoGenre(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_GENRE, value));
    }

    public static void SetChanInfoName(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_NAME, value));
    }

    public static void SetChanInfoType(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_TYPE, value));
    }

    public static void SetChanInfoURL(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_URL, value));
    }

    public static void SetChanInfoStreamType(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_STREAMTYPE, value));
    }

    public static void SetChanInfoStreamExt(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_STREAMEXT, value));
    }

    public static void SetChanPkt(this IAtomCollection collection, IList<Atom> value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_PKT, value));
    }

    public static void SetChanPktData(this IAtomCollection collection, byte[] value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_PKT_DATA, value));
    }

    public static void SetChanPktCont(this IAtomCollection collection, PCPChanPacketContinuation value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_PKT_CONTINUATION, (byte)value));
    }

    public static void SetChanPktPos(this IAtomCollection collection, uint value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_PKT_POS, value));
    }

    public static void SetChanPktType(this IAtomCollection collection, ID4 value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_PKT_TYPE, value.GetBytes()));
    }

    public static void SetChanTrack(this IAtomCollection collection, IList<Atom> value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK, value));
    }

    public static void SetChanTrackAlbum(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK_ALBUM, value));
    }

    public static void SetChanTrackGenre(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK_GENRE, value));
    }

    public static void SetChanTrackCreator(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK_CREATOR, value));
    }

    public static void SetChanTrackTitle(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK_TITLE, value));
    }

    public static void SetChanTrackURL(this IAtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK_URL, value));
    }

    public static void SetHost(this IAtomCollection collection, IList<Atom> value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST, value));
    }

    public static void SetHostChannelID(this IAtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_CHANID, IDToByteArray(value)));
    }

    public static void SetHostClapPP(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_CLAP_PP, value));
    }

    public static void SetHostFlags1(this IAtomCollection collection, PCPHostFlags1 value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_FLAGS1, (byte)value));
    }

    public static void SetHostIP(this IAtomCollection collection, IPAddress value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_IP, value));
    }

    public static void AddHostIP(this IAtomCollection collection, IPAddress value)
    {
      collection.Add(new Atom(Atom.PCP_HOST_IP, value));
    }

    public static void SetHostNewPos(this IAtomCollection collection, uint value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_NEWPOS, value));
    }

    public static void SetHostOldPos(this IAtomCollection collection, uint value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_OLDPOS, value));
    }

    public static void SetHostNumListeners(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_NUML, value));
    }

    public static void SetHostNumRelays(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_NUMR, value));
    }

    public static void SetHostPort(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_PORT, (ushort)value));
    }

    public static void AddHostPort(this IAtomCollection collection, int value)
    {
      collection.Add(new Atom(Atom.PCP_HOST_PORT, (ushort)value));
    }

    public static void SetHostSessionID(this IAtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_ID, IDToByteArray(value)));
    }

    public static void SetHostUphostHops(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_UPHOST_HOPS, value));
    }

    public static void SetHostUphostIP(this IAtomCollection collection, IPAddress value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_UPHOST_IP, value));
    }

    public static void SetHostUphostPort(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_UPHOST_PORT, value));
    }

    public static void SetHostUptime(this IAtomCollection collection, TimeSpan value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_UPTIME, (int)value.TotalSeconds));
    }

    public static void SetHostVersion(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_VERSION, value));
    }

    public static void SetHostVersionVP(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_VERSION_VP, value));
    }

    public static void SetHostVersionEXNumber(this IAtomCollection collection, short value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_VERSION_EX_NUMBER, value));
    }

    public static void SetHostVersionEXPrefix(this IAtomCollection collection, byte[] value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_VERSION_EX_PREFIX, value));
    }

    public static void SetOk(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_OK, value));
    }

    public static void SetOleh(this IAtomCollection collection, IList<Atom> value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_OLEH, value));
    }

    public static void SetQuit(this IAtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_QUIT, value));
    }

  }
}
