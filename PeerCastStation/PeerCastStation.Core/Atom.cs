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
using System.IO;
using System.Net;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.Core
{
  /// <summary>
  /// 主にAtomの名前などに使われる4文字の識別子クラスです
  /// </summary>
  public struct ID4
    : IEquatable<ID4>
  {
    private static System.Text.UTF8Encoding encoding = new System.Text.UTF8Encoding(false);
    private uint value;

    /// <summary>
    /// 文字列からID4インスタンスを初期化します
    /// </summary>
    /// <param name="name">設定する4文字以内のASCII文字列</param>
    public ID4(string name)
    {
      var nameb = encoding.GetBytes(name);
      if (nameb.Length > 4) {
        throw new ArgumentException("ID4 length must be 4 or less.");
      }
      var v = new byte[] { 0, 0, 0, 0 };
      nameb.CopyTo(v, 0);
      this.value = BitConverter.ToUInt32(v, 0);
    }

    /// <summary>
    /// バイト列からID4インスタンスを初期化します
    /// </summary>
    /// <param name="value">設定する4バイト以内のバイト配列</param>
    public ID4(byte[] value)
    {
      if (value.Length > 4) {
        throw new ArgumentException("ID4 length must be 4 or less.");
      }
      var v = new byte[] { 0, 0, 0, 0 };
      value.CopyTo(v, 0);
      this.value = BitConverter.ToUInt32(v, 0);
    }

    /// <summary>
    /// バイト列の一部からID4インスタンスを初期化します
    /// </summary>
    /// <param name="value">設定する4バイト以内のバイト配列</param>
    /// <param name="index">valueの先頭からのオフセット</param>
    public ID4(byte[] value, int index)
    {
      var v = new byte[] { 0, 0, 0, 0 };
      Array.Copy(value, index, v, 0, 4);
      this.value = BitConverter.ToUInt32(v, 0);
    }

    /// <summary>
    /// 保持しているバイト列を取得します
    /// </summary>
    /// <returns>4バイトのバイト配列</returns>
    public byte[] GetBytes()
    {
      return BitConverter.GetBytes(value);
    }

    /// <summary>
    /// 保持しているバイト列を文字列として取得します
    /// </summary>
    /// <returns>文字列に変換された値</returns>
    public override string ToString()
    {
      return encoding.GetString(GetBytes().TakeWhile(x => x != 0).ToArray());
    }

    public override int GetHashCode()
    {
      return (int)value;
    }

    public override bool Equals(object obj)
    {
      if (obj is ID4) {
        return Equals((ID4)obj);
      }
      else {
        return false;
      }
    }

    public bool Equals(ID4 x)
    {
      return x.value == value;
    }

    public static bool operator ==(ID4 a, ID4 b)
    {
      return a.Equals(b);
    }

    public static bool operator !=(ID4 a, ID4 b)
    {
      return !a.Equals(b);
    }
  }

  /// <summary>
  /// PCP_HOST_FLAGS1に設定する値です
  /// </summary>
  [Flags]
  public enum PCPHostFlags1 : byte
  {
    None          = 0x00,
    /// <summary>
    /// 自分がトラッカーである
    /// </summary>
    Tracker       = 0x01,
    /// <summary>
    /// リレー接続が可能である
    /// </summary>
    Relay         = 0x02,
    /// <summary>
    /// 視聴接続が可能である
    /// </summary>
    Direct        = 0x04,
    /// <summary>
    /// ポートが開いていない
    /// </summary>
    Firewalled    = 0x08,
    /// <summary>
    /// データ受信中である
    /// </summary>
    Receiving     = 0x10,
    /// <summary>
    /// Control接続が可能である(Rootのみ)
    /// </summary>
    ControlIn     = 0x20,
  }

  /// <summary>
  /// PCPプロトコルの基本通信単位を表すクラスです。
  /// 4文字以下の名前と対応する値を保持します
  /// </summary>
  public class Atom
  {
    public static readonly ID4 PCP_HELO                   = new ID4("helo");
    public static readonly ID4 PCP_HELO_AGENT             = new ID4("agnt");
    public static readonly ID4 PCP_HELO_OSTYPE            = new ID4("ostp");
    public static readonly ID4 PCP_HELO_SESSIONID         = new ID4("sid");
    public static readonly ID4 PCP_HELO_PORT              = new ID4("port");
    public static readonly ID4 PCP_HELO_PING              = new ID4("ping");
    public static readonly ID4 PCP_HELO_PONG              = new ID4("pong");
    public static readonly ID4 PCP_HELO_REMOTEIP          = new ID4("rip");
    public static readonly ID4 PCP_HELO_REMOTEPORT        = new ID4("port");
    public static readonly ID4 PCP_HELO_VERSION           = new ID4("ver");
    public static readonly ID4 PCP_HELO_BCID              = new ID4("bcid");
    public static readonly ID4 PCP_HELO_DISABLE           = new ID4("dis");
    public static readonly ID4 PCP_OLEH                   = new ID4("oleh");
    public static readonly ID4 PCP_OK                     = new ID4("ok");
    public static readonly ID4 PCP_CHAN                   = new ID4("chan");
    public static readonly ID4 PCP_CHAN_ID                = new ID4("id");
    public static readonly ID4 PCP_CHAN_BCID              = new ID4("bcid");
    public static readonly ID4 PCP_CHAN_PKT               = new ID4("pkt");
    public static readonly ID4 PCP_CHAN_PKT_TYPE          = new ID4("type");
    public static readonly ID4 PCP_CHAN_PKT_HEAD          = new ID4("head");
    public static readonly ID4 PCP_CHAN_PKT_META          = new ID4("meta");
    public static readonly ID4 PCP_CHAN_PKT_POS           = new ID4("pos");
    public static readonly ID4 PCP_CHAN_PKT_DATA          = new ID4("data");
    public static readonly ID4 PCP_CHAN_PKT_TYPE_HEAD     = new ID4("head");
    public static readonly ID4 PCP_CHAN_PKT_TYPE_META     = new ID4("meta");
    public static readonly ID4 PCP_CHAN_PKT_TYPE_DATA     = new ID4("data");
    public static readonly ID4 PCP_CHAN_INFO              = new ID4("info");
    public static readonly ID4 PCP_CHAN_INFO_TYPE         = new ID4("type");
    public static readonly ID4 PCP_CHAN_INFO_BITRATE      = new ID4("bitr");
    public static readonly ID4 PCP_CHAN_INFO_GENRE        = new ID4("gnre");
    public static readonly ID4 PCP_CHAN_INFO_NAME         = new ID4("name");
    public static readonly ID4 PCP_CHAN_INFO_URL          = new ID4("url");
    public static readonly ID4 PCP_CHAN_INFO_DESC         = new ID4("desc");
    public static readonly ID4 PCP_CHAN_INFO_COMMENT      = new ID4("cmnt");
    public static readonly ID4 PCP_CHAN_INFO_PPFLAGS      = new ID4("pflg");
    public static readonly ID4 PCP_CHAN_TRACK             = new ID4("trck");
    public static readonly ID4 PCP_CHAN_TRACK_TITLE       = new ID4("titl");
    public static readonly ID4 PCP_CHAN_TRACK_CREATOR     = new ID4("crea");
    public static readonly ID4 PCP_CHAN_TRACK_URL         = new ID4("url");
    public static readonly ID4 PCP_CHAN_TRACK_ALBUM       = new ID4("albm");
    public static readonly ID4 PCP_BCST                   = new ID4("bcst");
    public static readonly ID4 PCP_BCST_TTL               = new ID4("ttl");
    public static readonly ID4 PCP_BCST_HOPS              = new ID4("hops");
    public static readonly ID4 PCP_BCST_FROM              = new ID4("from");
    public static readonly ID4 PCP_BCST_DEST              = new ID4("dest");
    public static readonly ID4 PCP_BCST_GROUP             = new ID4("grp");
    public static readonly ID4 PCP_BCST_CHANID            = new ID4("cid");
    public static readonly ID4 PCP_BCST_VERSION           = new ID4("vers");
    public static readonly ID4 PCP_BCST_VERSION_VP        = new ID4("vrvp");
    public static readonly ID4 PCP_BCST_VERSION_EX_PREFIX = new ID4("vexp");
    public static readonly ID4 PCP_BCST_VERSION_EX_NUMBER = new ID4("vexn");
    public static readonly ID4 PCP_HOST                   = new ID4("host");
    public static readonly ID4 PCP_HOST_ID                = new ID4("id");
    public static readonly ID4 PCP_HOST_IP                = new ID4("ip");
    public static readonly ID4 PCP_HOST_PORT              = new ID4("port");
    public static readonly ID4 PCP_HOST_CHANID            = new ID4("cid");
    public static readonly ID4 PCP_HOST_NUML              = new ID4("numl");
    public static readonly ID4 PCP_HOST_NUMR              = new ID4("numr");
    public static readonly ID4 PCP_HOST_UPTIME            = new ID4("uptm");
    public static readonly ID4 PCP_HOST_TRACKER           = new ID4("trkr");
    public static readonly ID4 PCP_HOST_VERSION           = new ID4("ver");
    public static readonly ID4 PCP_HOST_VERSION_VP        = new ID4("vevp");
    public static readonly ID4 PCP_HOST_VERSION_EX_PREFIX = new ID4("vexp");
    public static readonly ID4 PCP_HOST_VERSION_EX_NUMBER = new ID4("vexn");
    public static readonly ID4 PCP_HOST_CLAP_PP           = new ID4("clap");
    public static readonly ID4 PCP_HOST_OLDPOS            = new ID4("oldp");
    public static readonly ID4 PCP_HOST_NEWPOS            = new ID4("newp");
    public static readonly ID4 PCP_HOST_FLAGS1            = new ID4("flg1");
    public static readonly ID4 PCP_HOST_UPHOST_IP         = new ID4("upip");
    public static readonly ID4 PCP_HOST_UPHOST_PORT       = new ID4("uppt");
    public static readonly ID4 PCP_HOST_UPHOST_HOPS       = new ID4("uphp");
    public static readonly ID4 PCP_QUIT                   = new ID4("quit");
    public static readonly ID4 PCP_CONNECT                = new ID4("pcp\n");
    public const byte PCP_HOST_FLAGS1_TRACKER = 0x01;
    public const byte PCP_HOST_FLAGS1_RELAY   = 0x02;
    public const byte PCP_HOST_FLAGS1_DIRECT  = 0x04;
    public const byte PCP_HOST_FLAGS1_PUSH    = 0x08;
    public const byte PCP_HOST_FLAGS1_RECV    = 0x10;
    public const byte PCP_HOST_FLAGS1_CIN     = 0x20;
    public const byte PCP_HOST_FLAGS1_PRIVATE = 0x40;
    public const int PCP_ERROR_QUIT    = 1000;
    public const int PCP_ERROR_BCST    = 2000;
    public const int PCP_ERROR_READ    = 3000;
    public const int PCP_ERROR_WRITE   = 4000;
    public const int PCP_ERROR_GENERAL = 5000;

    public const int PCP_ERROR_SKIP             = 1;
    public const int PCP_ERROR_ALREADYCONNECTED = 2;
    public const int PCP_ERROR_UNAVAILABLE      = 3;
    public const int PCP_ERROR_LOOPBACK         = 4;
    public const int PCP_ERROR_NOTIDENTIFIED    = 5;
    public const int PCP_ERROR_BADRESPONSE      = 6;
    public const int PCP_ERROR_BADAGENT         = 7;
    public const int PCP_ERROR_OFFAIR           = 8;
    public const int PCP_ERROR_SHUTDOWN         = 9;
    public const int PCP_ERROR_NOROOT           = 10;
    public const int PCP_ERROR_BANNED           = 11;

    private byte[] value = null;
    private IAtomCollection children = null;

    /// <summary>
    /// 名前を取得します
    /// </summary>
    public ID4 Name  { get; private set; }

    /// <summary>
    /// 子ATOMを保持しているかどうかを取得します
    /// </summary>
    public bool HasChildren { get { return children!=null; } }

    /// <summary>
    /// 値を保持しているかどうかを取得します
    /// </summary>
    public bool HasValue { get { return value!=null; } }

    /// <summary>
    /// 子Atomのコレクションを取得します。値を保持している場合はnullを返します
    /// </summary>
    public IAtomCollection Children { get { return children; } }

    /// <summary>
    /// 保持している値をbyteとして取得します。
    /// </summary>
    /// <returns>保持している値</returns>
    /// <exception cref="FormatException">
    /// 値の長さが合わない、または値を保持していません
    /// </exception>
    public byte GetByte()
    {
      byte res;
      if (TryGetByte(out res)) {
        return res;
      }
      else {
        throw new FormatException();
      }
    }

    /// <summary>
    /// 保持している値をbyteとして取得しようと試みます。
    /// </summary>
    /// <param name="res">保持している値の書き込み先</param>
    /// <returns>値がbyteとして解析できた場合はtrue、そうでない場合はfalse</returns>
    public bool TryGetByte(out byte res)
    {
      if (value != null && value.Length == 1) {
        res = value[0];
        return true;
      }
      else {
        res = 0;
        return false;
      }
    }

    /// <summary>
    /// 保持している値をInt16として取得します。
    /// </summary>
    /// <returns>保持している値</returns>
    /// <exception cref="FormatException">
    /// 値の長さが合わない、または値を保持していません
    /// </exception>
    public short GetInt16()
    {
      short res;
      if (TryGetInt16(out res)) {
        return res;
      }
      else {
        throw new FormatException();
      }
    }

    /// <summary>
    /// 保持している値をInt16として取得しようと試みます。
    /// </summary>
    /// <param name="res">保持している値の書き込み先</param>
    /// <returns>値がInt16として解析できた場合はtrue、そうでない場合はfalse</returns>
    public bool TryGetInt16(out short res)
    {
      if (value != null && value.Length == 2) {
        res = BitConverter.ToInt16(value, 0);
        return true;
      }
      else {
        res = 0;
        return false;
      }
    }

    /// <summary>
    /// 保持している値をInt32として取得します。
    /// </summary>
    /// <returns>保持している値</returns>
    /// <exception cref="FormatException">
    /// 値の長さが合わない、または値を保持していません
    /// </exception>
    public int GetInt32()
    {
      int res;
      if (TryGetInt32(out res)) {
        return res;
      }
      else {
        throw new FormatException();
      }
    }

    /// <summary>
    /// 保持している値をInt32として取得しようと試みます。
    /// </summary>
    /// <param name="res">保持している値の書き込み先</param>
    /// <returns>値がInt32として解析できた場合はtrue、そうでない場合はfalse</returns>
    public bool TryGetInt32(out int res)
    {
      if (value != null && value.Length == 4) {
        res = BitConverter.ToInt32(value, 0);
        return true;
      }
      else {
        res = 0;
        return false;
      }
    }

    /// <summary>
    /// 保持している値をUInt32として取得します。
    /// </summary>
    /// <returns>保持している値</returns>
    /// <exception cref="FormatException">
    /// 値の長さが合わない、または値を保持していません
    /// </exception>
    public uint GetUInt32()
    {
      uint res;
      if (TryGetUInt32(out res)) {
        return res;
      }
      else {
        throw new FormatException();
      }
    }

    /// <summary>
    /// 保持している値をUInt32として取得しようと試みます。
    /// </summary>
    /// <param name="res">保持している値の書き込み先</param>
    /// <returns>値がUInt32として解析できた場合はtrue、そうでない場合はfalse</returns>
    public bool TryGetUInt32(out uint res)
    {
      if (value != null && value.Length == 4) {
        res = BitConverter.ToUInt32(value, 0);
        return true;
      }
      else {
        res = 0;
        return false;
      }
    }

    /// <summary>
    /// 保持している値を文字列として取得します。
    /// </summary>
    /// <returns>保持している値</returns>
    /// <exception cref="FormatException">
    /// 値がNULL文字で終端されていない、または値を保持していません
    /// </exception>
    public string GetString()
    {
      string res;
      if (TryGetString(out res)) {
        return res;
      }
      else {
        throw new FormatException();
      }
    }

    /// <summary>
    /// 保持している値を文字列として取得しようと試みます。
    /// </summary>
    /// <param name="res">保持している値の書き込み先</param>
    /// <returns>値が文字列として解析できた場合はtrue、そうでない場合はfalse</returns>
    public bool TryGetString(out string res)
    {
      if (value != null && value[value.Length - 1] == 0) {
        res = System.Text.Encoding.UTF8.GetString(value, 0, value.Length - 1);
        return true;
      }
      else {
        res = "";
        return false;
      }
    }

    /// <summary>
    /// 保持している値をバイト配列として取得します
    /// </summary>
    /// <exception cref="FormatException">
    /// 値を保持していません
    /// </exception>
    /// <returns>値のバイト列</returns>
    public byte[] GetBytes()
    {
      if (value != null) {
        return value;
      }
      else {
        throw new FormatException();
      }
    }

    /// <summary>
    /// 保持している値をバイト配列として取得しようと試みます。
    /// </summary>
    /// <param name="res">保持している値の書き込み先</param>
    /// <returns>値を保持していた場合はtrue、そうでない場合はfalse</returns>
    public bool TryGetBytes(out byte[] res)
    {
      if (value != null) {
        res = value;
        return true;
      }
      else {
        res = null;
        return false;
      }
    }

    /// <summary>
    /// 保持している値をIPv4アドレスとして取得します
    /// </summary>
    /// <exception cref="FormatException">
    /// 値の長さが合わない、または値を保持していません
    /// </exception>
    /// <returns>値のIPv4アドレス</returns>
    public IPAddress GetIPv4Address()
    {
      IPAddress res;
      if (TryGetIPv4Address(out res)) {
        return res;
      }
      else {
        throw new FormatException();
      }
    }

    /// <summary>
    /// 保持している値をIPv4アドレスとして取得しようと試みます。
    /// </summary>
    /// <param name="res">保持している値の書き込み先</param>
    /// <returns>値がIPv4アドレスとして解析できた場合はtrue、そうでない場合はfalse</returns>
    public bool TryGetIPv4Address(out IPAddress res)
    {
      if (value != null && value.Length==4) {
        var ip_ary = new byte[value.Length];
        value.CopyTo(ip_ary, 0);
        Array.Reverse(ip_ary);
        res = new IPAddress(ip_ary);
        return true;
      }
      else {
        res = null;
        return false;
      }
    }

    /// <summary>
    /// 名前と値を指定してAtomを初期化します。
    /// </summary>
    /// <param name="name">4文字以下の名前</param>
    /// <param name="value">byte値</param>
    public Atom(ID4 name, byte value)
    {
      Name = name;
      this.value = new byte[] { value };
    }

    /// <summary>
    /// 名前と値を指定してAtomを初期化します。
    /// </summary>
    /// <param name="name">4文字以下の名前</param>
    /// <param name="value">Int16値</param>
    public Atom(ID4 name, short value)
    {
      Name = name;
      this.value = BitConverter.GetBytes(value);
      if (!BitConverter.IsLittleEndian) Array.Reverse(this.value);
    }

    /// <summary>
    /// 名前と値を指定してAtomを初期化します。
    /// </summary>
    /// <param name="name">4文字以下の名前</param>
    /// <param name="value">Int32値</param>
    public Atom(ID4 name, int value)
    {
      Name = name;
      this.value = BitConverter.GetBytes(value);
      if (!BitConverter.IsLittleEndian) Array.Reverse(this.value);
    }

    /// <summary>
    /// 名前と値を指定してAtomを初期化します。
    /// </summary>
    /// <param name="name">4文字以下の名前</param>
    /// <param name="value">UInt32値</param>
    public Atom(ID4 name, uint value)
    {
      Name = name;
      this.value = BitConverter.GetBytes(value);
      if (!BitConverter.IsLittleEndian) Array.Reverse(this.value);
    }

    /// <summary>
    /// 名前と値を指定してAtomを初期化します。
    /// </summary>
    /// <param name="name">4文字以下の名前</param>
    /// <param name="value">byte配列</param>
    public Atom(ID4 name, byte[] value)
    {
      Name = name;
      this.value = value;
    }

    /// <summary>
    /// 名前と値を指定してAtomを初期化します。
    /// </summary>
    /// <param name="name">4文字以下の名前</param>
    /// <param name="value">文字列</param>
    public Atom(ID4 name, string value)
    {
      Name = name;
      var str = System.Text.Encoding.UTF8.GetBytes(value);
      this.value = new byte[str.Length + 1];
      str.CopyTo(this.value, 0);
      this.value[str.Length] = 0;
    }

    /// <summary>
    /// 名前と値を指定してAtomを初期化します。
    /// </summary>
    /// <param name="name">4文字以下の名前</param>
    /// <param name="value">IPアドレス</param>
    public Atom(ID4 name, IPAddress value)
    {
      Name = name;
      var ip = value.GetAddressBytes();
      Array.Reverse(ip);
      this.value = new byte[ip.Length];
      ip.CopyTo(this.value, 0);
    }

    /// <summary>
    /// 名前と子のコレクションを指定してAtomを初期化します。
    /// </summary>
    /// <param name="name">4文字以下の名前</param>
    /// <param name="children">保持する子のコレクション</param>
    public Atom(ID4 name, IList<Atom> children)
    {
      Name = name;
      this.children = new ReadOnlyAtomCollection(new AtomCollection(children));
    }
  }

  /// <summary>
  /// ストリームにAtomを書き込むためのアダプタクラスです
  /// </summary>
  public class AtomWriter
    : IDisposable
  {
    private bool disposed = false;
    private Stream stream;

    /// <summary>
    /// 指定したストリームに書き込むインスタンスを初期化します
    /// </summary>
    /// <param name="stream">書き込み先のストリーム</param>
    public AtomWriter(Stream stream)
    {
      this.stream = stream;
    }

    /// <summary>
    /// 基になるストリームを取得します
    /// </summary>
    public Stream BaseStream
    {
      get
      {
        return stream;
      }
    }

    /// <summary>
    /// 保持しているストリームを閉じます
    /// </summary>
    public void Close()
    {
      Dispose();
    }

    /// <summary>
    /// このインスタンスによって使用されているすべてのリソースを解放します
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected void Dispose(bool disposing)
    {
      if (!disposed) {
        if (disposing) {
          stream.Dispose();
        }
        disposed = true;
      }
    }

    /// <summary>
    /// Atom全体をストリームに書き込みます
    /// </summary>
    /// <param name="atom">書き込むAtom</param>
    public void Write(Atom atom)
    {
      var name = atom.Name.GetBytes();
      stream.Write(name, 0, name.Length);
      if (atom.HasValue) {
        var value = atom.GetBytes();
        var len = BitConverter.GetBytes(value.Length);
        if (!BitConverter.IsLittleEndian) Array.Reverse(len);
        stream.Write(len, 0, len.Length);
        stream.Write(value, 0, value.Length);
      }
      else {
        var cnt = BitConverter.GetBytes(0x80000000U | (uint)atom.Children.Count);
        if (!BitConverter.IsLittleEndian) Array.Reverse(cnt);
        stream.Write(cnt, 0, cnt.Length);
        foreach (var child in atom.Children) {
          Write(child);
        }
      }
    }

    /// <summary>
    /// Atom全体を指定したストリームに書き込みます
    /// </summary>
    /// <param name="stream">書き込み先のストリーム</param>
    /// <param name="atom">書き込むAtom</param>
    static public void Write(Stream stream, Atom atom)
    {
      var name = atom.Name.GetBytes();
      stream.Write(name, 0, name.Length);
      if (atom.HasValue) {
        var value = atom.GetBytes();
        var len = BitConverter.GetBytes(value.Length);
        if (!BitConverter.IsLittleEndian) Array.Reverse(len);
        stream.Write(len, 0, len.Length);
        stream.Write(value, 0, value.Length);
      }
      else {
        var cnt = BitConverter.GetBytes(0x80000000U | (uint)atom.Children.Count);
        if (!BitConverter.IsLittleEndian) Array.Reverse(cnt);
        stream.Write(cnt, 0, cnt.Length);
        foreach (var child in atom.Children) {
          Write(stream, child);
        }
      }
    }
  }

  /// <summary>
  /// ストリームからAtomを読み取るアダプタクラスです
  /// </summary>
  public class AtomReader
    : IDisposable
  {
    private bool disposed = false;
    private Stream stream;

    /// <summary>
    /// 指定したストリームから読み取るインスタンスを初期化します
    /// </summary>
    /// <param name="stream">読み取り元のストリーム</param>
    public AtomReader(Stream stream)
    {
      this.stream = stream;
    }

    /// <summary>
    /// 基になるストリームを取得します
    /// </summary>
    public Stream BaseStream
    {
      get
      {
        return stream;
      }
    }

    /// <summary>
    /// 現在のAtomReaderと基になるストリームを閉じます
    /// </summary>
    public void Close()
    {
      Dispose();
    }

    /// <summary>
    /// このインスタンスによって使用されているすべてのリソースを解放します
    /// </summary>
    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    protected void Dispose(bool disposing)
    {
      if (!disposed) {
        if (disposing) {
          stream.Dispose();
        }
        disposed = true;
      }
    }

    /// <summary>
    /// ストリームからAtomを読み取ります
    /// </summary>
    /// <returns>読み取ったAtomのインスタンス</returns>
    /// <exception cref="EndOfStreamException">ストリームの末尾に達しました</exception>
    public Atom Read()
    {
      var header = new byte[8];
      int pos = 0;
      while (pos<8) {
        var r = stream.Read(header, pos, 8-pos);
        if (r<=0) {
          throw new EndOfStreamException();
        }
        pos += r;
      }
      var name = new ID4(header, 0);
      if (!BitConverter.IsLittleEndian) Array.Reverse(header, 4, 4);
      uint len = BitConverter.ToUInt32(header, 4);
      if ((len & 0x80000000U)!=0) {
        var children = new AtomCollection();
        for (var i=0; i<(len&0x7FFFFFFF); i++) {
          children.Add(Read());
        }
        return new Atom(name, children);
      }
      else {
        var value = new byte[len];
        pos = 0;
        while (pos<len) {
          var r = stream.Read(value, pos, (int)len-pos);
          if (r<=0) {
            throw new EndOfStreamException();
          }
          pos += r;
        }
        return new Atom(name, value);
      }
    }

    /// <summary>
    /// 指定したストリームからAtomを読み取ります
    /// </summary>
    /// <param name="stream">読み取り元のストリーム</param>
    /// <returns>読み取ったAtomのインスタンス</returns>
    /// <exception cref="EndOfStreamException">ストリームの末尾に達しました</exception>
    static public Atom Read(Stream stream)
    {
      var header = new byte[8];
      int pos = 0;
      while (pos<8) {
        var r = stream.Read(header, pos, 8-pos);
        if (r<=0) {
          throw new EndOfStreamException();
        }
        pos += r;
      }
      var name = new ID4(header, 0);
      if (!BitConverter.IsLittleEndian) Array.Reverse(header, 4, 4);
      uint len = BitConverter.ToUInt32(header, 4);
      if ((len & 0x80000000U)!=0) {
        var children = new AtomCollection();
        for (var i=0; i<(len&0x7FFFFFFF); i++) {
          children.Add(Read(stream));
        }
        return new Atom(name, children);
      }
      else {
        var value = new byte[len];
        pos = 0;
        while (pos<len) {
          var r = stream.Read(value, pos, (int)len-pos);
          if (r<=0) {
            throw new EndOfStreamException();
          }
          pos += r;
        }
        return new Atom(name, value);
      }
    }
  }

  public interface IAtomCollection : IList<Atom>
  {
    /// <summary>
    /// コレクションから指定した名前を持つAtomを探して取得します
    /// </summary>
    /// <param name="name">検索する名前</param>
    /// <returns>指定した名前を持つ最初のAtom、無かった場合はnull</returns>
    Atom FindByName(ID4 name);

    /// <summary>
    /// 他のAtomCollectionの内容をこのインスタンスに上書きします。
    /// 同じ名前の要素は上書きされ、異なる名前の要素は追加または残されます
    /// </summary>
    /// <param name="other">上書きするコレクション</param>
    void Update(IAtomCollection other);
  }

  /// <summary>
  /// Atomを保持するコレクションクラスです
  /// </summary>
  public class AtomCollection
    : ObservableCollection<Atom>,
      IAtomCollection
  {
    /// <summary>
    /// 空のAtomCollectionを初期化します
    /// </summary>
    public AtomCollection()
    {
    }

    /// <summary>
    /// 指定したリストから内容をコピーしたAtomCollectionを初期化します
    /// </summary>
    /// <param name="other">コピー元のリスト</param>
    public AtomCollection(IList<Atom> other)
      : base(other)
    {
    }

    /// <summary>
    /// コレクションから指定した名前を持つAtomを探して取得します
    /// </summary>
    /// <param name="name">検索する名前</param>
    /// <returns>指定した名前を持つ最初のAtom、無かった場合はnull</returns>
    public Atom FindByName(ID4 name)
    {
      return this.FirstOrDefault((x) => { return x.Name==name; });
    }

    /// <summary>
    /// 他のAtomCollectionの内容をこのインスタンスに上書きします。
    /// 同じ名前の要素は上書きされ、異なる名前の要素は追加または残されます
    /// </summary>
    /// <param name="other">上書きするコレクション</param>
    public void Update(IAtomCollection other)
    {
      foreach (var atom in other) {
        var updated = false;
        for (var i = 0; i < this.Count; i++) {
          if (atom.Name == this[i].Name) {
            updated = true;
            this[i] = atom;
            break;
          }
        }
        if (!updated) {
          this.Add(atom);
        }
      }
    }

    /// <summary>
    /// 読み取り専用のラッパオブジェクトを取得します
    /// </summary>
    /// <returns>このコレクションの読み取り専用のラッパ</returns>
    public ReadOnlyAtomCollection AsReadOnly()
    {
      return new ReadOnlyAtomCollection(this);
    }
  }

  /// <summary>
  /// AtomCollectionの読み取り専用コレクションを表すラッパクラスです
  /// </summary>
  public class ReadOnlyAtomCollection
    : ReadOnlyObservableCollection<Atom>,
      IAtomCollection
  {
    private AtomCollection baseCollection;

    /// <summary>
    /// 指定された元のコレクションから読み取り専用のラッパオブジェクトを初期化します
    /// </summary>
    /// <param name="base_collection">元になるコレクション</param>
    public ReadOnlyAtomCollection(AtomCollection base_collection)
      : base(base_collection)
    {
      baseCollection = base_collection;
    }

    /// <summary>
    /// 元のコレクションから指定した名前を持つAtomを探して取得します
    /// </summary>
    /// <param name="name">検索する名前</param>
    /// <returns>指定した名前を持つ最初のAtom、無かった場合はnull</returns>
    public Atom FindByName(ID4 name)
    {
      return baseCollection.FindByName(name);
    }

    /// <summary>
    /// 他のAtomCollectionの内容をこのインスタンスに上書きします。
    /// 常にNotSupporetedExceptionを投げます。
    /// </summary>
    /// <param name="other">上書きするコレクション</param>
    public void Update(IAtomCollection other)
    {
      throw new NotSupportedException();
    }
  }

}
