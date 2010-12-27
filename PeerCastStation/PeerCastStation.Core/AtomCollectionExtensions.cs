using System;
using System.Collections.Generic;
using System.Net;

namespace PeerCastStation.Core
{
  public static class AtomCollectionExtensions
  {
    public static int? GetIntFrom(AtomCollection collection, ID4 name)
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

    public static short? GetShortFrom(AtomCollection collection, ID4 name)
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

    public static byte? GetByteFrom(AtomCollection collection, ID4 name)
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

    public static Guid? GetIDFrom(AtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      byte[] value = null;
      if (atom != null && atom.TryGetBytes(out value) && value.Length==16) {
        return new Guid(value);
      }
      else {
        return null;
      }
    }

    public static ID4? GetID4From(AtomCollection collection, ID4 name)
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

    public static byte[] GetBytesFrom(AtomCollection collection, ID4 name)
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

    public static IPAddress GetIPAddressFrom(AtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      IPAddress value = null;
      if (atom != null && atom.TryGetIPv4Address(out value)) {
        return value;
      }
      else {
        return null;
      }
    }

    public static string GetStringFrom(AtomCollection collection, ID4 name)
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

    public static Atom GetAtomFrom(AtomCollection collection, ID4 name)
    {
      return collection.FindByName(name);
    }

    public static AtomCollection GetCollectionFrom(AtomCollection collection, ID4 name)
    {
      var atom = collection.FindByName(name);
      if (atom != null && atom.HasChildren) {
        return atom.Children;
      }
      else {
        return null;
      }
    }

    public static void SetAtomTo(AtomCollection collection, Atom value)
    {
      for (var i = 0; i < collection.Count; i++) {
        if (value.Name == collection[i].Name) {
          collection[i] = value;
          return;
        }
      }
      collection.Add(value);
    }

    public static AtomCollection GetHelo(this AtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_HELO);
    }

    public static string GetHeloAgent(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_HELO_AGENT);
    }

    public static Guid? GetHeloSessionID(this AtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_HELO_SESSIONID);
    }

    public static short? GetHeloPort(this AtomCollection collection)
    {
      return GetShortFrom(collection, Atom.PCP_HELO_PORT);
    }

    public static short? GetHeloPing(this AtomCollection collection)
    {
      return GetShortFrom(collection, Atom.PCP_HELO_PING);
    }

    public static IPAddress GetHeloRemoteIP(this AtomCollection collection)
    {
      return GetIPAddressFrom(collection, Atom.PCP_HELO_REMOTEIP);
    }

    public static int? GetHeloVersion(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HELO_VERSION);
    }

    public static Guid? GetHeloBCID(this AtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_HELO_BCID);
    }

    public static int? GetHeloDisable(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HELO_DISABLE);
    }

    public static AtomCollection GetOleh(this AtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_OLEH);
    }

    public static int? GetOk(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_OK);
    }

    public static AtomCollection GetChan(this AtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_CHAN);
    }

    public static Guid? GetChanID(this AtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_CHAN_ID);
    }

    public static Guid? GetChanBCID(this AtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_CHAN_BCID);
    }

    public static AtomCollection GetChanPkt(this AtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_CHAN_PKT);
    }

    public static ID4? GetChanPktType(this AtomCollection collection)
    {
      return GetID4From(collection, Atom.PCP_CHAN_PKT_TYPE);
    }

    public static int? GetChanPktPos(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_CHAN_PKT_POS);
    }

    public static byte[] GetChanPktData(this AtomCollection collection)
    {
      return GetBytesFrom(collection, Atom.PCP_CHAN_PKT_DATA);
    }

    public static AtomCollection GetChanInfo(this AtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_CHAN_INFO);
    }

    public static string GetChanInfoType(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_TYPE);
    }

    public static int? GetChanInfoBitrate(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_CHAN_INFO_BITRATE);
    }

    public static string GetChanInfoGenre(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_GENRE);
    }

    public static string GetChanInfoName(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_NAME);
    }

    public static string GetChanInfoURL(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_URL);
    }

    public static string GetChanInfoDesc(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_DESC);
    }

    public static string GetChanInfoComment(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_INFO_COMMENT);
    }

    public static int? GetChanInfoPPFlags(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_CHAN_INFO_PPFLAGS);
    }

    public static AtomCollection GetChanTrack(this AtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_CHAN_TRACK);
    }

    public static string GetChanTrackTitle(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_TRACK_TITLE);
    }

    public static string GetChanTrackCreator(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_TRACK_CREATOR);
    }

    public static string GetChanTrackURL(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_TRACK_URL);
    }

    public static string GetChanTrackAlbum(this AtomCollection collection)
    {
      return GetStringFrom(collection, Atom.PCP_CHAN_TRACK_ALBUM);
    }

    public static AtomCollection GetBcst(this AtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_BCST);
    }

    public static byte? GetBcstTTL(this AtomCollection collection)
    {
      return GetByteFrom(collection, Atom.PCP_BCST_TTL);
    }

    public static byte? GetBcstHops(this AtomCollection collection)
    {
      return GetByteFrom(collection, Atom.PCP_BCST_HOPS);
    }

    public static Guid? GetBcstFrom(this AtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_BCST_FROM);
    }

    public static Guid? GetBcstDest(this AtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_BCST_DEST);
    }

    public static byte? GetBcstGroup(this AtomCollection collection)
    {
      return GetByteFrom(collection, Atom.PCP_BCST_GROUP);
    }

    public static Guid? GetBcstChannelID(this AtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_BCST_CHANID);
    }

    public static int? GetBcstVersion(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_BCST_VERSION);
    }

    public static int? GetBcstVersionVP(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_BCST_VERSION_VP);
    }

    public static byte[] GetBcstVersionEXPrefix(this AtomCollection collection)
    {
      return GetBytesFrom(collection, Atom.PCP_BCST_VERSION_EX_PREFIX);
    }

    public static short? GetBcstVersionEXNumber(this AtomCollection collection)
    {
      return GetShortFrom(collection, Atom.PCP_BCST_VERSION_EX_NUMBER);
    }

    public static AtomCollection GetHost(this AtomCollection collection)
    {
      return GetCollectionFrom(collection, Atom.PCP_HOST);
    }

    public static Guid? GetHostSessionID(this AtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_HOST_ID);
    }

    public static IPAddress GetHostIP(this AtomCollection collection)
    {
      return GetIPAddressFrom(collection, Atom.PCP_HOST_IP);
    }

    public static short? GetHostPort(this AtomCollection collection)
    {
      return GetShortFrom(collection, Atom.PCP_HOST_PORT);
    }

    public static Guid? GetHostChannelID(this AtomCollection collection)
    {
      return GetIDFrom(collection, Atom.PCP_HOST_CHANID);
    }

    public static int? GetHostNumListeners(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_NUML);
    }

    public static int? GetHostNumRelays(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_NUMR);
    }

    public static TimeSpan? GetHostUptime(this AtomCollection collection)
    {
      int? res = GetIntFrom(collection, Atom.PCP_HOST_UPTIME);
      if (res.HasValue) {
        return TimeSpan.FromSeconds(res.Value);
      }
      else {
        return null;
      }
    }

    public static int? GetHostVersion(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_VERSION);
    }

    public static int? GetHostVersionVP(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_VERSION_VP);
    }

    public static byte[] GetHostVersionEXPrefix(this AtomCollection collection)
    {
      return GetBytesFrom(collection, Atom.PCP_HOST_VERSION_EX_PREFIX);
    }

    public static short? GetHostVersionEXNumber(this AtomCollection collection)
    {
      return GetShortFrom(collection, Atom.PCP_HOST_VERSION_EX_NUMBER);
    }

    public static int? GetHostClapPP(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_CLAP_PP);
    }

    public static int? GetHostOldPos(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_OLDPOS);
    }

    public static int? GetHostNewPos(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_NEWPOS);
    }

    public static PCPHostFlags1? GetHostFlags1(this AtomCollection collection)
    {
      return (PCPHostFlags1?)GetByteFrom(collection, Atom.PCP_HOST_FLAGS1);
    }

    public static IPAddress GetHostUphostIP(this AtomCollection collection)
    {
      return GetIPAddressFrom(collection, Atom.PCP_HOST_UPHOST_IP);
    }

    public static int? GetHostUphostPort(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_HOST_UPHOST_PORT);
    }

    public static byte? GetHostUphostHops(this AtomCollection collection)
    {
      return GetByteFrom(collection, Atom.PCP_HOST_UPHOST_HOPS);
    }

    public static int? GetQuit(this AtomCollection collection)
    {
      return GetIntFrom(collection, Atom.PCP_QUIT);
    }

    public static void SetHelo(this AtomCollection collection, AtomCollection value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO, value));
    }

    public static void SetHeloAgent(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_AGENT, value));
    }

    public static void SetHeloBCID(this AtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_BCID, value.ToByteArray()));
    }

    public static void SetHeloDisable(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_DISABLE, value));
    }

    public static void SetHeloPing(this AtomCollection collection, short value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_PING, value));
    }

    public static void SetHeloPort(this AtomCollection collection, short value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_PORT, value));
    }

    public static void SetHeloRemoteIP(this AtomCollection collection, IPAddress value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_REMOTEIP, value));
    }

    public static void SetHeloSessionID(this AtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_SESSIONID, value.ToByteArray()));
    }

    public static void SetHeloVersion(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HELO_VERSION, value));
    }

    public static void SetBcst(this AtomCollection collection, AtomCollection value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST, value));
    }

    public static void SetBcstChannelID(this AtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_CHANID, value.ToByteArray()));
    }

    public static void SetBcstDest(this AtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_DEST, value.ToByteArray()));
    }

    public static void SetBcstFrom(this AtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_FROM, value.ToByteArray()));
    }

    public static void SetBcstGroup(this AtomCollection collection, byte value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_GROUP, value));
    }

    public static void SetBcstHops(this AtomCollection collection, byte value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_HOPS, value));
    }

    public static void SetBcstTTL(this AtomCollection collection, byte value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_TTL, value));
    }

    public static void SetBcstVersion(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_VERSION, value));
    }

    public static void SetBcstVersionVP(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_VERSION_VP, value));
    }

    public static void SetBcstVersionEXNumber(this AtomCollection collection, short value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_VERSION_EX_NUMBER, value));
    }

    public static void SetBcstVersionEXPrefix(this AtomCollection collection, byte[] value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_BCST_VERSION_EX_PREFIX, value));
    }

    public static void SetChan(this AtomCollection collection, AtomCollection value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN, value));
    }

    public static void SetChanBCID(this AtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_BCID, value.ToByteArray()));
    }

    public static void SetChanID(this AtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_ID, value.ToByteArray()));
    }

    public static void SetChanInfo(this AtomCollection collection, AtomCollection value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO, value));
    }

    public static void SetChanInfoBitrate(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_BITRATE, value));
    }

    public static void SetChanInfoPPFlags(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_PPFLAGS, value));
    }

    public static void SetChanInfoComment(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_COMMENT, value));
    }

    public static void SetChanInfoDesc(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_DESC, value));
    }

    public static void SetChanInfoGenre(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_GENRE, value));
    }

    public static void SetChanInfoName(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_NAME, value));
    }

    public static void SetChanInfoType(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_TYPE, value));
    }

    public static void SetChanInfoURL(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_INFO_URL, value));
    }

    public static void SetChanPkt(this AtomCollection collection, AtomCollection value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_PKT, value));
    }

    public static void SetChanPktData(this AtomCollection collection, byte[] value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_PKT_DATA, value));
    }

    public static void SetChanPktPos(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_PKT_POS, value));
    }

    public static void SetChanPktType(this AtomCollection collection, ID4 value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_PKT_TYPE, value.GetBytes()));
    }

    public static void SetChanTrack(this AtomCollection collection, AtomCollection value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK, value));
    }

    public static void SetChanTrackAlbum(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK_ALBUM, value));
    }

    public static void SetChanTrackCreator(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK_CREATOR, value));
    }

    public static void SetChanTrackTitle(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK_TITLE, value));
    }

    public static void SetChanTrackURL(this AtomCollection collection, string value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_CHAN_TRACK_URL, value));
    }

    public static void SetHost(this AtomCollection collection, AtomCollection value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST, value));
    }

    public static void SetHostChannelID(this AtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_CHANID, value.ToByteArray()));
    }

    public static void SetHostClapPP(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_CLAP_PP, value));
    }

    public static void SetHostFlags1(this AtomCollection collection, PCPHostFlags1 value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_FLAGS1, (byte)value));
    }

    public static void SetHostIP(this AtomCollection collection, IPAddress value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_IP, value));
    }

    public static void AddHostIP(this AtomCollection collection, IPAddress value)
    {
      collection.Add(new Atom(Atom.PCP_HOST_IP, value));
    }

    public static void SetHostNewPos(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_NEWPOS, value));
    }

    public static void SetHostOldPos(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_OLDPOS, value));
    }

    public static void SetHostNumListeners(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_NUML, value));
    }

    public static void SetHostNumRelays(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_NUMR, value));
    }

    public static void SetHostPort(this AtomCollection collection, short value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_PORT, value));
    }

    public static void AddHostPort(this AtomCollection collection, short value)
    {
      collection.Add(new Atom(Atom.PCP_HOST_PORT, value));
    }

    public static void SetHostSessionID(this AtomCollection collection, Guid value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_ID, value.ToByteArray()));
    }

    public static void SetHostUphostHops(this AtomCollection collection, byte value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_UPHOST_HOPS, value));
    }

    public static void SetHostUphostIP(this AtomCollection collection, IPAddress value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_UPHOST_IP, value));
    }

    public static void SetHostUphostPort(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_UPHOST_PORT, value));
    }

    public static void SetHostUptime(this AtomCollection collection, TimeSpan value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_UPTIME, (int)value.TotalSeconds));
    }

    public static void SetHostVersion(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_VERSION, value));
    }

    public static void SetHostVersionVP(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_VERSION_VP, value));
    }

    public static void SetHostVersionEXNumber(this AtomCollection collection, short value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_VERSION_EX_NUMBER, value));
    }

    public static void SetHostVersionEXPrefix(this AtomCollection collection, byte[] value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_HOST_VERSION_EX_PREFIX, value));
    }

    public static void SetOk(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_OK, value));
    }

    public static void SetOleh(this AtomCollection collection, AtomCollection value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_OLEH, value));
    }

    public static void SetQuit(this AtomCollection collection, int value)
    {
      SetAtomTo(collection, new Atom(Atom.PCP_QUIT, value));
    }

  }
}
