using PeerCastStation.Core;
using PeerCastStation.FLV.RTMP;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PeerCastStation.FLV
{
  public class FLVToMPEG2TS
  {
    public interface IMPEG2TSContentSink
    {
      void OnPAT(ReadOnlyMemory<byte> bytes);
      void OnPMT(ReadOnlyMemory<byte> bytes);
      void OnTSPackets(ReadOnlyMemory<byte> bytes);
    }

    public class TSWriter
    {
      private static readonly uint[] CRC32Table = new uint[] {
          0x00000000, 0xB71DC104, 0x6E3B8209, 0xD926430D, 0xDC760413, 0x6B6BC517,
          0xB24D861A, 0x0550471E, 0xB8ED0826, 0x0FF0C922, 0xD6D68A2F, 0x61CB4B2B,
          0x649B0C35, 0xD386CD31, 0x0AA08E3C, 0xBDBD4F38, 0x70DB114C, 0xC7C6D048,
          0x1EE09345, 0xA9FD5241, 0xACAD155F, 0x1BB0D45B, 0xC2969756, 0x758B5652,
          0xC836196A, 0x7F2BD86E, 0xA60D9B63, 0x11105A67, 0x14401D79, 0xA35DDC7D,
          0x7A7B9F70, 0xCD665E74, 0xE0B62398, 0x57ABE29C, 0x8E8DA191, 0x39906095,
          0x3CC0278B, 0x8BDDE68F, 0x52FBA582, 0xE5E66486, 0x585B2BBE, 0xEF46EABA,
          0x3660A9B7, 0x817D68B3, 0x842D2FAD, 0x3330EEA9, 0xEA16ADA4, 0x5D0B6CA0,
          0x906D32D4, 0x2770F3D0, 0xFE56B0DD, 0x494B71D9, 0x4C1B36C7, 0xFB06F7C3,
          0x2220B4CE, 0x953D75CA, 0x28803AF2, 0x9F9DFBF6, 0x46BBB8FB, 0xF1A679FF,
          0xF4F63EE1, 0x43EBFFE5, 0x9ACDBCE8, 0x2DD07DEC, 0x77708634, 0xC06D4730,
          0x194B043D, 0xAE56C539, 0xAB068227, 0x1C1B4323, 0xC53D002E, 0x7220C12A,
          0xCF9D8E12, 0x78804F16, 0xA1A60C1B, 0x16BBCD1F, 0x13EB8A01, 0xA4F64B05,
          0x7DD00808, 0xCACDC90C, 0x07AB9778, 0xB0B6567C, 0x69901571, 0xDE8DD475,
          0xDBDD936B, 0x6CC0526F, 0xB5E61162, 0x02FBD066, 0xBF469F5E, 0x085B5E5A,
          0xD17D1D57, 0x6660DC53, 0x63309B4D, 0xD42D5A49, 0x0D0B1944, 0xBA16D840,
          0x97C6A5AC, 0x20DB64A8, 0xF9FD27A5, 0x4EE0E6A1, 0x4BB0A1BF, 0xFCAD60BB,
          0x258B23B6, 0x9296E2B2, 0x2F2BAD8A, 0x98366C8E, 0x41102F83, 0xF60DEE87,
          0xF35DA999, 0x4440689D, 0x9D662B90, 0x2A7BEA94, 0xE71DB4E0, 0x500075E4,
          0x892636E9, 0x3E3BF7ED, 0x3B6BB0F3, 0x8C7671F7, 0x555032FA, 0xE24DF3FE,
          0x5FF0BCC6, 0xE8ED7DC2, 0x31CB3ECF, 0x86D6FFCB, 0x8386B8D5, 0x349B79D1,
          0xEDBD3ADC, 0x5AA0FBD8, 0xEEE00C69, 0x59FDCD6D, 0x80DB8E60, 0x37C64F64,
          0x3296087A, 0x858BC97E, 0x5CAD8A73, 0xEBB04B77, 0x560D044F, 0xE110C54B,
          0x38368646, 0x8F2B4742, 0x8A7B005C, 0x3D66C158, 0xE4408255, 0x535D4351,
          0x9E3B1D25, 0x2926DC21, 0xF0009F2C, 0x471D5E28, 0x424D1936, 0xF550D832,
          0x2C769B3F, 0x9B6B5A3B, 0x26D61503, 0x91CBD407, 0x48ED970A, 0xFFF0560E,
          0xFAA01110, 0x4DBDD014, 0x949B9319, 0x2386521D, 0x0E562FF1, 0xB94BEEF5,
          0x606DADF8, 0xD7706CFC, 0xD2202BE2, 0x653DEAE6, 0xBC1BA9EB, 0x0B0668EF,
          0xB6BB27D7, 0x01A6E6D3, 0xD880A5DE, 0x6F9D64DA, 0x6ACD23C4, 0xDDD0E2C0,
          0x04F6A1CD, 0xB3EB60C9, 0x7E8D3EBD, 0xC990FFB9, 0x10B6BCB4, 0xA7AB7DB0,
          0xA2FB3AAE, 0x15E6FBAA, 0xCCC0B8A7, 0x7BDD79A3, 0xC660369B, 0x717DF79F,
          0xA85BB492, 0x1F467596, 0x1A163288, 0xAD0BF38C, 0x742DB081, 0xC3307185,
          0x99908A5D, 0x2E8D4B59, 0xF7AB0854, 0x40B6C950, 0x45E68E4E, 0xF2FB4F4A,
          0x2BDD0C47, 0x9CC0CD43, 0x217D827B, 0x9660437F, 0x4F460072, 0xF85BC176,
          0xFD0B8668, 0x4A16476C, 0x93300461, 0x242DC565, 0xE94B9B11, 0x5E565A15,
          0x87701918, 0x306DD81C, 0x353D9F02, 0x82205E06, 0x5B061D0B, 0xEC1BDC0F,
          0x51A69337, 0xE6BB5233, 0x3F9D113E, 0x8880D03A, 0x8DD09724, 0x3ACD5620,
          0xE3EB152D, 0x54F6D429, 0x7926A9C5, 0xCE3B68C1, 0x171D2BCC, 0xA000EAC8,
          0xA550ADD6, 0x124D6CD2, 0xCB6B2FDF, 0x7C76EEDB, 0xC1CBA1E3, 0x76D660E7,
          0xAFF023EA, 0x18EDE2EE, 0x1DBDA5F0, 0xAAA064F4, 0x738627F9, 0xC49BE6FD,
          0x09FDB889, 0xBEE0798D, 0x67C63A80, 0xD0DBFB84, 0xD58BBC9A, 0x62967D9E,
          0xBBB03E93, 0x0CADFF97, 0xB110B0AF, 0x060D71AB, 0xDF2B32A6, 0x6836F3A2,
          0x6D66B4BC, 0xDA7B75B8, 0x035D36B5, 0xB440F7B1, 0x00000001
      };

      private static uint CRC32(ReadOnlySpan<byte> bytes, uint crc)
      {
        foreach (var b in bytes) {
          crc = CRC32Table[(crc & 0xFF) ^ b] ^ (crc >> 8);
        }
        return crc;
      }

      public IMPEG2TSContentSink Sink { get; }
      private Dictionary<int, int> continuityCounter = new Dictionary<int, int>();

      public TSWriter(IMPEG2TSContentSink sink)
      {
        Sink = sink;
      }

      /// <summary>
      /// 16bit フィールドを書く。
      /// BitWriter.Write と同じく、宣言幅に収まらない値は黙って切り捨てず例外にする。
      /// </summary>
      /// <remarks>
      /// セクション長やディスクリプタ長がここで溢れると
      /// 構造だけ妥当で長さが嘘の TS が出来上がり、デマルチプレクサが同期を失う。
      /// </remarks>
      private Span<byte> WriteUInt16BE(Span<byte> dst, int value)
      {
        if (value<0 || value>0xFFFF) {
          throw new ArgumentOutOfRangeException(nameof(value), value, "16bitのフィールドに収まらない値です");
        }
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16BigEndian(dst, (ushort)value);
        return dst.Slice(2);
      }

      private Span<byte> WriteUInt32LE(Span<byte> dst, uint value)
      {
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(dst, value);
        return dst.Slice(4);
      }

      private Span<byte> WriteByte(Span<byte> dst, int value)
      {
        if (value<0 || value>0xFF) {
          throw new ArgumentOutOfRangeException(nameof(value), value, "8bitのフィールドに収まらない値です");
        }
        dst[0] = (byte)value;
        return dst.Slice(1);
      }

      private Span<byte> WriteBytes(Span<byte> dst, ReadOnlySpan<byte> value)
      {
        value.CopyTo(dst);
        return dst.Slice(value.Length);
      }

      /// <summary>
      /// 12bit の長さフィールドを検証して返す。
      /// </summary>
      /// <remarks>
      /// 予約ビット(15&lt;&lt;12 等)と OR して書くため、
      /// 溢れた分は上位の予約ビットに吸収されて WriteUInt16BE の範囲検査に掛からない。
      /// 長さフィールドの食い違いはデマルチプレクサ側でテーブル全体の読み違えになるので、
      /// OR する前にここで弾く。
      /// </remarks>
      private static int Check12BitLength(int value, string name)
      {
        if (value<0 || value>0xFFF) {
          throw new ArgumentOutOfRangeException(name, value, "12bitの長さフィールドに収まらない値です");
        }
        return value;
      }

      public void WritePAT(ProgramAssociationTable pat)
      {
        var table_sz = pat.PIDToProgramNumber.Aggregate(0, (sz, entry) => sz + 4);
        var body_sz = 5 + table_sz;
        using var bodyMem = MemoryPool<byte>.Shared.Rent(body_sz);
        var body = bodyMem.Memory.Span;
        body = WriteUInt16BE(body, pat.TransportStreamId);
        body = WriteByte(body, (3<<6) | (pat.Version << 1) | (pat.CurrentNextIndicator ? 1 : 0));
        body = WriteByte(body, pat.SectionNumber);
        body = WriteByte(body, pat.LastSectionNumber);
        foreach (var kv in pat.PIDToProgramNumber) {
          body = WriteUInt16BE(body, kv.Value);
          body = WriteUInt16BE(body, (7<<13) | kv.Key);
        }
        var writer = new ArrayBufferWriter<byte>(188);
        WriteSection(writer, 0x00, 0x00, bodyMem.Memory.Span.Slice(0, body_sz));
        Sink.OnPAT(writer.WrittenMemory);
      }

      public void WritePMT(int pid, ProgramMapTable pmt)
      {
        var program_info_sz = pmt.ProgramInfo.Aggregate(0, (sz, entry) => sz + 2 + entry.Data.Length);
        var table_sz = pmt.Table.Aggregate(0, (sz, entry) => sz + 5 + entry.ESInfo.Length);
        var body_sz = 9 + program_info_sz + table_sz;

        using var bodyMem = MemoryPool<byte>.Shared.Rent(body_sz);
        var body = bodyMem.Memory.Span;
        body = WriteUInt16BE(body, pmt.ProgramNumber);
        body = WriteByte(body, (3<<6) | (pmt.Version << 1) | (pmt.CurrentNextIndicator ? 1 : 0));
        body = WriteByte(body, pmt.SectionNumber);
        body = WriteByte(body, pmt.LastSectionNumber);
        body = WriteUInt16BE(body, (7<<13) | (pmt.PCRPID & 0x1FFF));
        body = WriteUInt16BE(body, (15<<12) | Check12BitLength(program_info_sz, "program_info_length"));
        foreach (var entry in pmt.ProgramInfo) {
          body = WriteByte(body, entry.Tag);
          body = WriteByte(body, entry.Data.Length);
          body = WriteBytes(body, entry.Data);
        }
        foreach (var entry in pmt.Table) {
          body = WriteByte(body, entry.StreamType);
          body = WriteUInt16BE(body, (7<<13) | (entry.PID & 0x1FFF));
          body = WriteUInt16BE(body, (15<<12) | Check12BitLength(entry.ESInfo.Length, "ES_info_length"));
          body = WriteBytes(body, entry.ESInfo);
        }
        var writer = new ArrayBufferWriter<byte>(188);
        WriteSection(writer, pid, 0x02, bodyMem.Memory.Span.Slice(0, body_sz));
        Sink.OnPMT(writer.WrittenMemory);
      }

      private void WriteSection(IBufferWriter<byte> writer, int pid, int table_id, ReadOnlySpan<byte> body)
      {
        var section_sz = 8 + body.Length;
        using var sectionMem = MemoryPool<byte>.Shared.Rent(section_sz);
        var section = sectionMem.Memory.Span;
        section = WriteByte(section, 0); // pointer_field
        var section_syntax_indicator = (1<<15);
        var reserved                 = (3<<12);
        var section_length           = body.Length+4;
        section = WriteByte(section, table_id);
        section = WriteUInt16BE(
          section,
          section_syntax_indicator | reserved | Check12BitLength(section_length, "section_length"));
        section = WriteBytes(section, body);
        var crc = CRC32(sectionMem.Memory.Span.Slice(1, section_length-1), 0xFFFFFFFF);
        section = WriteUInt32LE(section, crc);
        WriteTSPackets(writer, pid, false, null, sectionMem.Memory.Span.Slice(0, section_sz));
      }

      private void WriteTSPackets(IBufferWriter<byte> writer, int pid, bool random_access, TSTimeStamp? pcr, ReadOnlySpan<byte> body)
      {
        var pos = 0;
        var payload_unit_start_indicator = true;
        while (pos<body.Length) {
          int continuity_counter;
          continuityCounter.TryGetValue(pid, out continuity_counter);
          var maxlen = 184;
          MemoryStream? adaptation_field = null;
          if ((random_access || pcr.HasValue) && payload_unit_start_indicator) {
            adaptation_field = new MemoryStream();
            adaptation_field.WriteByte((byte)(
              ((random_access ? 1 : 0)<<6) | ((pcr.HasValue ? 1 : 0)<<4)
            ));
            if (pcr.HasValue) {
              var pcr_base = pcr.Value.Tick / 300;
              var pcr_ext  = pcr.Value.Tick % 300;
              adaptation_field.WriteUInt32BE(pcr_base>>1);
              adaptation_field.WriteUInt16BE((int)(((pcr_base & 1) << 15) | (63 << 9) | pcr_ext));
            }
            maxlen -= 1;
            maxlen -= (int)adaptation_field.Length;
          }
          var len = Math.Min(body.Length-pos, maxlen);
          if (len<maxlen) {
            if (adaptation_field!=null) {
              for (int i=0; i<maxlen-len; i++) {
                adaptation_field.WriteByte(0xFF);
              }
            }
            else if (len==maxlen-1) {
              adaptation_field = new MemoryStream();
            }
            else {
              adaptation_field = new MemoryStream();
              adaptation_field.WriteByte(0);
              for (int i=0; i<maxlen-2-len; i++) {
                adaptation_field.WriteByte(0xFF);
              }
            }
          }
          var adaptation_field_control = adaptation_field!=null ? 0x03 : 0x01;
          var dst = writer.GetSpan(4);
          dst = WriteByte(dst, 0x47);
          dst = WriteUInt16BE(dst, ((payload_unit_start_indicator ? 1 : 0)<<14) | (pid & 0x1FFF));
          dst = WriteByte(dst, (byte)((adaptation_field_control << 4) | (continuity_counter & 0xF)));
          writer.Advance(4);
          if (adaptation_field!=null) {
            adaptation_field.Close();
            var ary = adaptation_field.ToArray();
            dst = writer.GetSpan(1 + ary.Length);
            dst = WriteByte(dst, (byte)ary.Length);
            dst = WriteBytes(dst, ary);
            writer.Advance(1 + ary.Length);
          }
          dst = writer.GetSpan(len);
          dst = WriteBytes(dst, body.Slice(pos, len));
          writer.Advance(len);
          pos += len;
          payload_unit_start_indicator = false;
          continuityCounter[pid] = continuity_counter + 1;
        }
      }

      public void WriteTSPackets(int pid, bool random_access, TSTimeStamp? pcr, ReadOnlySpan<byte> body)
      {
        var writer = new ArrayBufferWriter<byte>(188);
        WriteTSPackets(writer, pid, random_access, pcr, body);
        Sink.OnTSPackets(writer.WrittenMemory);
      }
    }

    public struct TSTimeStamp
    {
      public long Tick;

      public TSTimeStamp(long tick)
      {
        this.Tick = tick;
      }

      public static TSTimeStamp FromMilliseconds(long ms)
      {
        return new TSTimeStamp(ms * 27000);
      }
    }

    /// <summary>
    /// PES パケットヘッダの書き出し。
    /// </summary>
    /// <remarks>
    /// ヘッダ長は PTS/DTS の有無だけで決まるので、呼び出し側は GetHeaderSize で
    /// 出力全体を1つの配列として確保し、先頭をここで埋めてから続きへペイロードを
    /// 直接組み立てられる。以前はペイロードを持つオブジェクト+伸長する MemoryStream +
    /// ToArray() の構成で、フレームごとに複製が2回余計に発生していた
    /// (メディアタグごとに通る経路なので毎秒70回以上)。
    /// </remarks>
    public static class PESPacket
    {
      public static int GetHeaderSize(bool has_pts, bool has_dts)
      {
        return 9 + (has_pts ? (has_dts ? 10 : 5) : 0);
      }

      public static void WriteHeader(Span<byte> dst, byte stream_id, TSTimeStamp? pts, TSTimeStamp? dts, int payload_length)
      {
        var header_data_length = (pts.HasValue ? (dts.HasValue ? 10 : 5) : 0);
        var packet_length = payload_length + 3 + header_data_length;
        // PES_packet_length は16bit。黙って下位16bitに丸めると、65535を超える
        // アクセスユニット(1080pのIDRフレームなど珍しくない)で実長と無関係な短い長さを
        // 宣言してしまい、下流のデマルチプレクサが同期を失う。
        // 映像ESに限り 0 = 長さ未指定が許されている(次の PES 開始まで)ので 0 を書く。
        // 音声(ADTS)は OnAACBody が 0x1FFF 上限で弾くため到達しない想定だが、
        // 黙って丸めず不変条件の破れとして表に出す。
        if (packet_length>0xFFFF) {
          if ((stream_id & 0xF0)==0xE0) {
            packet_length = 0;
          }
          else {
            throw new ArgumentOutOfRangeException(nameof(payload_length), payload_length, "映像以外のPESは16bitの長さフィールドに収まる必要があります");
          }
        }
        dst[0] = 0;
        dst[1] = 0;
        dst[2] = 1;
        dst[3] = stream_id;
        dst[4] = (byte)(packet_length>>8);
        dst[5] = (byte)packet_length;
        // '10' + scrambling/priority/alignment/copyright/original の各フラグ0
        dst[6] = 0x80;
        // pts_dts_flags + escr/es_rate/trick/copy_info/crc/extension の各フラグ0
        dst[7] = (byte)(((pts.HasValue ? (dts.HasValue ? 3 : 2) : 0) & 0x3) << 6);
        dst[8] = (byte)header_data_length;
        if (pts.HasValue) {
          WriteTimestamp(dst.Slice(9), dts.HasValue ? 0x3 : 0x2, pts.Value.Tick/300);
        }
        if (dts.HasValue) {
          WriteTimestamp(dst.Slice(14), 0x1, dts.Value.Tick/300);
        }
      }

      /// <summary>33bit タイムスタンプの 5 バイト詰め(4bitマーカー + 3-15-15 分割、各末尾に marker_bit)。</summary>
      private static void WriteTimestamp(Span<byte> dst, int marker, long value)
      {
        dst[0] = (byte)((marker << 4) | (int)(((value >> 30) & 0x0007)<<1) | 1);
        dst[1] = (byte)(((((value >> 15) & 0x7FFF)<<1) | 1)>>8);
        dst[2] = (byte)(((((value >> 15) & 0x7FFF)<<1) | 1)&0xFF);
        dst[3] = (byte)(((((value >>  0) & 0x7FFF)<<1) | 1)>>8);
        dst[4] = (byte)(((((value >>  0) & 0x7FFF)<<1) | 1)&0xFF);
      }
    }

    public class NALUnit
    {
      public int NALRefIdc { get; private set; }
      public int NALUnitType { get; private set; }
      public byte[] Payload { get; private set; }

      public NALUnit(int nal_ref_idc, int nal_unit_type, byte[] rbsp_bytes)
      {
        NALRefIdc   = nal_ref_idc;
        NALUnitType = nal_unit_type;
        Payload     = rbsp_bytes;
      }

      public static NALUnit ReadFrom(ReadOnlySpan<byte> bytes, int len)
      {
        var data = bytes[0];
        var nal_ref_idc   = (data & 0x60)>>5;
        var nal_unit_type = (data & 0x1F);
        return new NALUnit(nal_ref_idc, nal_unit_type, bytes.Slice(1, len-1).ToArray());
      }

      /// <summary>スタートコード(4バイト)+NALヘッダ+ペイロードとして書いたときの長さ。</summary>
      public static int GetByteSize(NALUnit unit)
      {
        return 4 + 1 + unit.Payload.Length;
      }

      /// <summary>dst の先頭へ書き、書いた長さを返す。dst は GetByteSize 以上あること。</summary>
      public static int WriteTo(Span<byte> dst, NALUnit unit)
      {
        dst[0] = 0;
        dst[1] = 0;
        dst[2] = 0;
        dst[3] = 1;
        dst[4] = (byte)((unit.NALRefIdc << 5) | (unit.NALUnitType & 0x1F));
        unit.Payload.CopyTo(dst.Slice(5));
        return 5 + unit.Payload.Length;
      }

      public static readonly NALUnit AccessUnitDelimiter = new NALUnit(0, 9, new byte[] { 240 });
    }

    public class BitWriter
      : IDisposable
    {
      public Stream BaseStream { get; private set; }
      private bool leaveOpen = false;
      private long buffer = 0;
      private int bufferLen = 0;
      private long padding = 0;
      public BitWriter(Stream baseStream, bool leaveOpen)
      {
        this.BaseStream = baseStream;
        this.leaveOpen  = leaveOpen;
      }

      public void Dispose()
      {
        Flush();
        if (!leaveOpen) {
          this.BaseStream.Dispose();
        }
      }

      public void Flush()
      {
        if (bufferLen==0) return;
        BaseStream.WriteByte((byte)((buffer<<(8-bufferLen)) | (padding & ((1<<(8-bufferLen))-1))));
        buffer = 0;
        bufferLen = 0;
      }

      /// <summary>
      /// 値を指定ビット幅で書く。宣言した幅に収まらない値は例外にする。
      /// </summary>
      /// <remarks>
      /// 黙って下位ビットへ丸めると、構造としては妥当なのに内容が別物のビットストリームが
      /// 出来上がり、視聴側の症状(音が出ない/同期が外れる)からは原因を追えなくなる。
      /// フィールドごとの妥当性は呼び出し側が事前に検証する契約とし、
      /// 破った場合は実装のバグとして表に出す。
      /// </remarks>
      public void Write(int bits, int value)
      {
        if (bits<0 || bits>32) {
          throw new ArgumentOutOfRangeException(nameof(bits), bits, "ビット幅は0-32の範囲で指定してください");
        }
        var max = bits==32 ? uint.MaxValue : (1u<<bits)-1;
        if (value<0 || (uint)value>max) {
          throw new ArgumentOutOfRangeException(
            nameof(value), value, $"{bits}bitのフィールドに収まらない値です");
        }
        buffer = (buffer << bits) | ((long)value & ((1L<<bits)-1));
        bufferLen += bits;
        while (bufferLen>=8) {
          BaseStream.WriteByte((byte)(buffer >> (bufferLen-8)));
          buffer = buffer & ((1<<(bufferLen-8))-1);
          bufferLen -= 8;
        }
      }
    }

    public class ADTSHeader
    {
      public int Sync { get; private set; }
      public int Id { get; private set; }
      public int Layer { get; private set; }
      public bool CRCAbsent { get; private set; }
      public int Profile { get; private set; }
      public int SamplingFreqIndex { get; private set; }
      public int IsPrivate { get; private set; }
      public int ChannelConfigurtion { get; private set; }
      public int IsOriginal { get; private set; }
      public int IsHome { get; private set; }
      public int CopyrightIdBit { get; private set; }
      public int CopyrightIdStart { get; private set; }
      public int FrameLength { get; private set; }
      public int BufferFullness { get; private set; }
      public int RawDataBlocks { get; private set; }
      public int CRC { get; private set; }

      public int Bytesize { get { return this.CRCAbsent ? 7 : 9; } }

      public ADTSHeader(
        int sync,
        int id,
        int layer,
        bool crc_absent,
        int profile,
        int sampling_freq_index,
        int is_private,
        int channel_configuration,
        int is_original,
        int is_home,
        int copyright_id_bit,
        int copyright_id_start,
        int frame_length,
        int buffer_fullness,
        int raw_data_blocks,
        int crc)
      {
        this.Sync = sync;
        this.Id   = id;
        this.Layer = layer;
        this.CRCAbsent = crc_absent;
        this.Profile = profile;
        this.SamplingFreqIndex = sampling_freq_index;
        this.IsPrivate = is_private;
        this.ChannelConfigurtion = channel_configuration;
        this.IsOriginal = is_original;
        this.IsHome = is_home;
        this.CopyrightIdBit = copyright_id_bit;
        this.CopyrightIdStart = copyright_id_start;
        this.FrameLength = frame_length;
        this.BufferFullness = buffer_fullness;
        this.RawDataBlocks = raw_data_blocks;
        this.CRC = crc;
      }

      public ADTSHeader(
        ADTSHeader other,
        int frame_length)
      {
        this.Sync                = other.Sync;
        this.Id                  = other.Id;
        this.Layer               = other.Layer;
        this.CRCAbsent           = other.CRCAbsent;
        this.Profile             = other.Profile;
        this.SamplingFreqIndex   = other.SamplingFreqIndex;
        this.IsPrivate           = other.IsPrivate;
        this.ChannelConfigurtion = other.ChannelConfigurtion;
        this.IsOriginal          = other.IsOriginal;
        this.IsHome              = other.IsHome;
        this.CopyrightIdBit      = other.CopyrightIdBit;
        this.CopyrightIdStart    = other.CopyrightIdStart;
        this.FrameLength         = frame_length;
        this.BufferFullness      = other.BufferFullness;
        this.RawDataBlocks       = other.RawDataBlocks;
        this.CRC                 = other.CRC;
      }

      public static readonly ADTSHeader Default = new ADTSHeader(0, 0, 0, false, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

      public static void WriteTo(Stream stream, ADTSHeader header)
      {
        using (var s=new BitWriter(stream, true)) {
          s.Write(12, header.Sync);
          s.Write(1, header.Id);
          s.Write(2, header.Layer);
          s.Write(1, header.CRCAbsent ? 1 : 0);
          s.Write(2, header.Profile);
          s.Write(4, header.SamplingFreqIndex);
          s.Write(1, header.IsPrivate);
          s.Write(3, header.ChannelConfigurtion);
          s.Write(1, header.IsOriginal);
          s.Write(1, header.IsHome);
          s.Write(1, header.CopyrightIdBit);
          s.Write(1, header.CopyrightIdStart);
          s.Write(13, header.FrameLength);
          s.Write(11, header.BufferFullness);
          s.Write(2, header.RawDataBlocks);
          if (!header.CRCAbsent) {
            s.Write(16, header.CRC);
          }
        }
      }
    }

    public class ProgramAssociationTable
    {
      public int  TransportStreamId { get; set; }  = 1;
      public int  Version { get; set; } = 0;
      public bool CurrentNextIndicator { get; set; } = true;
      public int SectionNumber { get; set; } = 0;
      public int LastSectionNumber { get; set; } = 0;
      public Dictionary<int, int> PIDToProgramNumber { get; } = new Dictionary<int, int>();
    }

    public class ProgramDescriptor {
      public int Tag { get; private set; }
      public byte[] Data { get; private set; }
      public ProgramDescriptor(int tag, byte[] data)
      {
        this.Tag = tag;
        this.Data = data;
      }
    }

    public class ProgramMapEntry {
      public int PID { get; private set; }
      public int StreamType { get; private set; }
      public byte[] ESInfo { get; private set; }
      public ProgramMapEntry(int pid, int stream_type, byte[] esinfo)
      {
        this.PID        = pid;
        this.StreamType = stream_type;
        this.ESInfo     = esinfo;
      }
    }

    public class ProgramMapTable
    {
      public int ProgramNumber { get; set; } = 1;
      public int  Version { get; set; } = 0;
      public bool CurrentNextIndicator { get; set; } = true;
      public int SectionNumber { get; set; } = 0;
      public int LastSectionNumber { get; set; } = 0;
      public int PCRPID { get; set; }  = 0;
      public List<ProgramDescriptor> ProgramInfo { get; set; } = new List<ProgramDescriptor>();
      public List<ProgramMapEntry> Table { get; set; } = new List<ProgramMapEntry>();
    }

    public class Context
      : FLVRemuxContextBase
    {
      public int VideoPID { get; set; } = 0x100;
      public int AudioPID { get; set; } = 0x101;
      public int ProgramMapTablePID { get; set; } = 0x1000;

      private TSWriter writer;
      private NALUnit[] pps    = new NALUnit[0];
      private NALUnit[] sps    = new NALUnit[0];
      private NALUnit[] spsExt = new NALUnit[0];
      private ProgramAssociationTable pat = new ProgramAssociationTable();
      private ProgramMapTable pmt = new ProgramMapTable();
      private bool isHeaderSent = false;
      private bool hasAudio = false;
      private bool hasVideo = false;
      // 最後に送った PMT に宣言したトラック構成。後から揃ったトラックを反映する再送
      // (RestartTablesIfTrackChanged)の要否をこれで判定する。
      private bool declaredAudio = false;
      private bool declaredVideo = false;
      // PMT の version_number(5bit)。内容を変えて再送するときに進めないと、
      // 同じ版番号の PMT はデマルチプレクサに「変更なし」として無視される。
      private int pmtVersion = 0;
      // 設定を「まだ受信していない」のではなく「受信したが表現できず破棄した」ことの記録。
      // この場合は待っても状況が変わらないので、フレーム破棄時にも PAT/PMT だけは送出して
      // 出力が完全な無データ(視聴側からは死んだチャンネル)になるのを避ける。
      private bool audioConfigRejected = false;
      private bool videoConfigRejected = false;
      private ADTSHeader adtsHeader = ADTSHeader.Default;
      private int nalSizeLen = 0;
      // 受理済みシーケンスヘッダの生バイト。同一内容の送り直しを再解析せず弾くために持つ。
      private byte[]? audioConfigRaw = null;
      private byte[]? videoConfigRaw = null;
      private static readonly Logger logger = new Logger(typeof(FLVToMPEG2TS));

      protected override string FilterName { get { return "FLVToMPEG2TS"; } }
      protected override Logger Logger { get { return logger; } }

      private class MPEG2TSStreamWriter
        : IMPEG2TSContentSink
      {
        public Stream BaseStream { get; }
        public MPEG2TSStreamWriter(Stream stream)
        {
          BaseStream = stream;
        }

        public void OnPAT(ReadOnlyMemory<byte> bytes)
        {
          BaseStream.Write(bytes.Span);
        }

        public void OnPMT(ReadOnlyMemory<byte> bytes)
        {
          BaseStream.Write(bytes.Span);
        }

        public void OnTSPackets(ReadOnlyMemory<byte> bytes)
        {
          BaseStream.Write(bytes.Span);
        }
      }

      public Context(Stream stream)
      {
        this.writer = new TSWriter(new MPEG2TSStreamWriter(stream));
      }

      public Context(IMPEG2TSContentSink sink)
      {
        this.writer = new TSWriter(sink);
      }

      private void Clear()
      {
        pps    = new NALUnit[0];
        sps    = new NALUnit[0];
        spsExt = new NALUnit[0];
        pat = new ProgramAssociationTable();
        pmt = new ProgramMapTable();
        isHeaderSent = false;
        hasAudio = false;
        hasVideo = false;
        declaredAudio = false;
        declaredVideo = false;
        pmtVersion = 0;
        audioConfigRejected = false;
        videoConfigRejected = false;
        adtsHeader = ADTSHeader.Default;
        nalSizeLen = 0;
        // キャッシュを残すと、リセット後に届いた同一設定が弾かれて hasAudio/hasVideo が
        // 立たないまま全フレームが破棄される。
        audioConfigRaw = null;
        videoConfigRaw = null;
        ResetTimestampBase();
        ResetWarnings();
      }

      public override void OnFLVHeader(FLVFileHeader header)
      {
        Clear();
      }

      private void WritePATPMT(TSWriter writer)
      {
        if (isHeaderSent) {
          return;
        }
        // トラック構成の変化による再送があるので、テーブルは毎回作り直す。
        // 前回のエントリへ追記すると同じ ES が重複宣言される。
        pmt.Table.Clear();
        if (hasVideo) {
          pmt.Table.Add(new ProgramMapEntry(VideoPID, 0x1B, new byte[0]));
        }
        if (hasAudio) {
          pmt.Table.Add(new ProgramMapEntry(AudioPID, 0x0F, new byte[0]));
        }
        if (hasVideo) {
          pmt.PCRPID = VideoPID;
        }
        else if (hasAudio) {
          pmt.PCRPID = AudioPID;
        }
        else {
          // どの ES も宣言できないときの「PCR なし」は 0x1FFF と決められている。
          pmt.PCRPID = 0x1FFF;
        }
        pmt.Version = pmtVersion;
        pat.PIDToProgramNumber[ProgramMapTablePID] = 1;
        writer.WritePAT(pat);
        writer.WritePMT(ProgramMapTablePID, pmt);
        declaredAudio = hasAudio;
        declaredVideo = hasVideo;
        isHeaderSent = true;
      }

      /// <summary>
      /// 送出済みの PMT にないトラックが後から揃ったら、version を進めて再送を予約する。
      /// </summary>
      /// <remarks>
      /// 再接続や途中参加では音声のシーケンスヘッダが最初の映像フレームより後に届くことが
      /// あり(FLVToMKV の RestartSegmentIfTrackAvailable と同じ事情)、最初のフレームで
      /// 確定した PMT に映像しか載っていないと、以後の音声 PES はどの PMT にも宣言されない
      /// PID へ流れ続けて規格準拠のデマルチプレクサに捨てられる(そのセッションは最後まで
      /// 無音になる)。PMT は version_number を進めれば途中で更新できるので、Segment を
      /// 作り直すしかない Matroska と違いテーブルの再送だけでよい。
      /// </remarks>
      private void RestartTablesIfTrackChanged()
      {
        if (!isHeaderSent) return;
        if (declaredAudio==hasAudio && declaredVideo==hasVideo) return;
        pmtVersion = (pmtVersion+1) & 0x1F;
        isHeaderSent = false;
      }

      private void OnAACHeader(byte[] body, int offset)
      {
        // 多くのエンコーダは GOP ごとにシーケンスヘッダを送り直す。同じ内容なら
        // コピーも再解析も要らない(FLVToMKV 側と同じ規則)。
        if (audioConfigRaw!=null &&
            new ReadOnlySpan<byte>(body, offset, body.Length-offset).SequenceEqual(audioConfigRaw)) {
          return;
        }
        // 切り詰められた AudioSpecificConfig はここで捨てる。ビット読み出しで例外を投げると
        // FLVFileParser.Read の EndOfStreamException catch がタグ先頭まで巻き戻すため
        // (=「データ待ち」と誤認される)、毒タグがバッファ先頭に残って以後の全パースが
        // 再スローし続け、出力が恒久停止したうえで contentBuffer が無限に成長する。
        // ペイロードの実体があることは FLVRemuxContextBase が保証している。
        var config = FLVTagInfo.SlicePayload(body, offset);
        if (!AudioSpecificConfig.TryParse(config, out var asc)) {
          WarnBrokenAudioConfig("AudioSpecificConfigが不完全です");
          audioConfigRejected = true;
          return;
        }
        // ADTS の samplingFrequencyIndex は表引きインデックス(0-12)しか表現できない。
        // 明示レートのエスケープ(15)は、通知された実レートから表引きインデックスを逆引きして救う
        // (実レートが表にある限り ADTS で正しく表現できる)。逆引きできない実レートと
        // 予約値(13/14)だけは禁止インデックス入りの ADTS になりデコーダが全音声を拒否するため、
        // 設定ごと破棄する。
        var sampling_freq_idx = asc.SamplingFrequencyIndex;
        if (sampling_freq_idx>=13) {
          if (sampling_freq_idx!=0x0F ||
              !AudioSpecificConfig.TryGetSamplingFrequencyIndex(asc.SampleRate, out sampling_freq_idx)) {
            WarnBrokenAudioConfig($"ADTSで表現できないサンプリング周波数です (index={asc.SamplingFrequencyIndex}, rate={asc.SampleRate})");
            audioConfigRejected = true;
            return;
          }
        }
        // ADTS の profile は2bit(audioObjectType-1、すなわち AOT 1..4 のみ表現できる)。
        // HE-AAC(AOT=5)/HE-AACv2(AOT=29)は SBR/PS の明示signalingで、コアの audioObjectType
        // (通常 AAC LC=2)を別途通知している。ADTS は SBR を暗黙signalingで運ぶ形式なので、
        // 通知どおりコア側の AOT で profile を決めれば正しく再生できる。
        // ここで AOT=5 ごと破棄すると、HE-AAC 配信の音声が丸ごと出なくなる
        // (hasAudio が立たないため以後の全フレームが捨てられ、PMT も音声ES抜きで確定する)。
        var type = asc.CoreAudioObjectType;
        if (type<1 || type>4) {
          WarnBrokenAudioConfig($"ADTSで表現できないaudioObjectTypeです (type={asc.AudioObjectType}, core={asc.CoreAudioObjectType})");
          audioConfigRejected = true;
          return;
        }
        // channel_configuration は3bit。0 はチャンネルレイアウトを PCE で運ぶという指定で、
        // その PCE は raw_data_block の先頭に入っている。ADTS はここを素通しするので
        // 0 のまま宣言してよく、デコーダは raw_data_block の PCE でレイアウトを知る。
        // 逆にここで破棄すると、PCE でレイアウトを送る配信(5.1ch 超やカスタム配置)の
        // 音声が hasAudio ごと落ちて、PMT も音声ES抜きで確定してしまう。
        // 予約値(8-15)だけは3bitに収まらず BitWriter の範囲検査で例外になるので破棄する。
        var channel_configuration = asc.ChannelConfiguration;
        if (channel_configuration<0 || channel_configuration>7) {
          WarnBrokenAudioConfig($"ADTSで表現できないchannelConfigurationです ({channel_configuration})");
          audioConfigRejected = true;
          return;
        }
        this.adtsHeader = new ADTSHeader(
          0xFFF, //sync
          0, //ID
          0, //Layer
          true, //CRC Absent
          type-1, //Profile
          sampling_freq_idx, //Sampling frequency index
          0, //Private
          channel_configuration, //Channel configuration
          0, //Original/Copy
          0, //home
          0, //Copyright identification bit
          0, //Copyright identification start
          0, //frame length
          0x7FF, //buffer fullness
          0, //number of raw data blocks in frame
          0  //CRC
        );
        audioConfigRaw = config;
        audioConfigRejected = false;
        hasAudio = true;
        // 最初の映像フレームより後に音声設定が届いた場合、確定済みの PMT に音声 ES を
        // 足すためにテーブルを再送する。
        RestartTablesIfTrackChanged();
      }

      private void OnAACBody(RTMPMessage msg, int offset)
      {
        // ペイロードの実体は FLVRemuxContextBase が保証している。
        // シーケンスヘッダ未受信のまま流すと adtsHeader が Default(sync=0 の全ゼロ)のままで、
        // ゴミ ADTS が AudioPID に出力される上、最初の WritePATPMT が音声 ES 抜きの PMT を
        // 確定させてしまう。映像側(nalSizeLen<1)と同様に破棄する。
        if (!hasAudio) {
          WarnMissingAudioConfig();
          // 設定が「まだ来ていない」だけなら黙って待つ。「来たが表現できず破棄した」なら
          // 待っても状況は変わらないので、宣言できるトラックだけの PAT/PMT を送って
          // 出力が完全な無データ(視聴側からは死んだチャンネル)になるのを避ける。
          if (audioConfigRejected) {
            WritePATPMT(writer);
          }
          return;
        }
        var raw_length = msg.Body.Length-offset;
        var frame_length = raw_length + adtsHeader.Bytesize;
        // ADTS の frame_length は13bit。超過分を BitWriter が黙って落とすと実長と食い違う
        // 長さを宣言することになり、ADTS は次フレームの位置をこの長さで求めるため
        // 以降のフレーム同期が丸ごと壊れる。部分出力に意味はないのでフレームごと捨てる。
        if (frame_length>0x1FFF) {
          WarnOversizedAudioFrame();
          return;
        }
        // 時刻原点の取り方と負値のクランプは基底の共通規則。
        var pts = NormalizeTimestamp(msg.Timestamp);
        var header = new ADTSHeader(adtsHeader, frame_length);
        // PES ヘッダ長(PTS のみ)も ADTS ヘッダ+生フレームのサイズも確定しているので、
        // PES パケット全体を1つの正確な長さの配列へ直接組み立てる。伸長しながら書いて
        // 最後に ToArray() で複製する形は避ける(音声フレームは毎秒40回以上流れる)。
        var header_size = PESPacket.GetHeaderSize(has_pts: true, has_dts: false);
        var pes_packet = new byte[header_size + frame_length];
        PESPacket.WriteHeader(
          pes_packet.AsSpan(0, header_size),
          0xC0,
          TSTimeStamp.FromMilliseconds(pts),
          null,
          frame_length);
        using (var s = new MemoryStream(pes_packet, header_size, adtsHeader.Bytesize, true)) {
          ADTSHeader.WriteTo(s, header);
        }
        Buffer.BlockCopy(msg.Body, offset, pes_packet, header_size+adtsHeader.Bytesize, raw_length);
        WritePATPMT(writer);
        writer.WriteTSPackets(AudioPID, true, null, pes_packet);
      }

      private static NALUnit[] ToNALUnits(byte[][] units)
      {
        if (units.Length==0) return Array.Empty<NALUnit>();
        var result = new NALUnit[units.Length];
        for (var i=0; i<units.Length; i++) {
          result[i] = NALUnit.ReadFrom(units[i], units[i].Length);
        }
        return result;
      }

      private void OnAVCHeader(byte[] body, int offset)
      {
        // ペイロードの実体は FLVRemuxContextBase が保証している。
        var data = new ReadOnlySpan<byte>(body, offset, body.Length-offset);
        // 音声側と同じく、同じ内容の送り直しでは再解析しない。avcC の再解析は
        // SPS/PPS の配列割り当てを伴うので、GOP ごとの送り直しが数時間分積もる。
        if (videoConfigRaw!=null && data.SequenceEqual(videoConfigRaw)) {
          return;
        }
        // 切り詰められた/矛盾した avcC はここで捨てる。音声側(OnAACHeader)と同じ理由で、
        // 境界外アクセスの例外を投げると FLVFileParser がタグ先頭まで巻き戻して
        // 同じ毒タグを永久に再パースし、出力が恒久停止する。
        // avcC の走査そのものは AvcDecoderConfig に集約してある。
        if (!AvcDecoderConfig.TryParse(data, out var config)) {
          WarnBrokenVideoConfig("avcCが不完全です");
          videoConfigRejected = true;
          return;
        }
        // SPS/PPS を欠く avcC は仕様上あり得る(パラメータセットを in-band で運ぶ運用)。
        // TS ではフレーム内の SPS/PPS NAL が素通しで流れるので出力自体は成立しうる。
        // IDR への注入が空振りすることだけ警告して続行する。CodecPrivate が復号初期化の
        // 唯一の拠り所である MKV 側(OnVideoConfig)はこれを破棄する — コンテナ由来の非対称。
        if (!config.HasParameterSets) {
          WarnOnce("emptyAvcParameterSets", "avcCにSPS/PPSが含まれていません。in-bandのパラメータセット前提で続行します");
        }
        this.nalSizeLen = config.NalSizeLength;
        this.sps        = ToNALUnits(config.SequenceParameterSets);
        this.pps        = ToNALUnits(config.PictureParameterSets);
        this.spsExt     = ToNALUnits(config.SequenceParameterSetExtensions);
        videoConfigRaw  = data.ToArray();
        videoConfigRejected = false;
        hasVideo = true;
        // 最初の音声フレームより後に映像設定が届いた場合、確定済みの PMT に映像 ES を
        // 足すためにテーブルを再送する。
        RestartTablesIfTrackChanged();
      }

      private void WarnMissingAudioConfig()
      {
        WarnOnce("missingAudioConfig", "音声シーケンスヘッダ(AudioSpecificConfig)より前のフレームを破棄します");
      }

      private void WarnOversizedAudioFrame()
      {
        WarnOnce("oversizedAudioFrame", "ADTSのframe_length(13bit)に収まらない音声フレームを破棄します");
      }

      private void WarnMissingVideoConfig()
      {
        WarnOnce("missingVideoConfig", "映像シーケンスヘッダ(avcC)より前のフレームを破棄します");
      }

      private void WarnBrokenVideoFrame(int units)
      {
        if (units<1) {
          WarnOnce("brokenVideoFrame", "NALユニット長が不正なため映像フレームを破棄します");
        }
        else {
          WarnOnce("truncatedVideoFrame", "NALユニット長が不正なため映像フレームの末尾を切り捨てます");
        }
      }

      private void OnAVCBody(RTMPMessage msg, int offset, int cts, bool keyframe)
      {
        // ペイロードの実体は FLVRemuxContextBase が保証している。
        // avcC(シーケンスヘッダ)より先に CodedFrames が来ると nalSizeLen が 0 のまま。
        // その場合 NAL 長は常に 0 と読めてしまい区切りが分からないので、
        // このフレームは復号できないものとして破棄する。
        if (nalSizeLen<1) {
          WarnMissingVideoConfig();
          // 音声側と同じ理由で、破棄が確定している場合だけ PAT/PMT を送る。
          if (videoConfigRejected) {
            WritePATPMT(writer);
          }
          return;
        }
        // 1パス目: NAL 長を検証しながら、出力サイズと AUD/IDR(SPS/PPS注入)の要否を数える。
        // 2パス目で PES パケット全体を正確な長さの配列へ直接組み立てるための下拵え。
        // 以前は NAL ごとの配列確保 → 伸長する MemoryStream → ToArray() → 2つ目の
        // MemoryStream → ToArray() と、フレームの中身を5回前後複製していた
        // (1080p30 なら毎秒20MB超の回避可能なアロケーション。キーフレームは LOH に乗る)。
        var body = msg.Body;
        var idr = false;
        var broken = false;
        var units = 0;
        var payload_size = 0L;
        var inject_size = 0;
        foreach (var unit in sps)    inject_size += NALUnit.GetByteSize(unit);
        foreach (var unit in pps)    inject_size += NALUnit.GetByteSize(unit);
        foreach (var unit in spsExt) inject_size += NALUnit.GetByteSize(unit);
        var pos = offset;
        while (pos<body.Length) {
          // 長さは long に符号なしで読む。Int32 で読むと nalSizeLen==4
          // (lengthSizeMinusOne=3、一般的な既定値)で最上位ビットが立つ入力が
          // 負値になり、残量検査(len>残バイト数)をすり抜ける。
          if (body.Length-pos < nalSizeLen) {
            broken = true;
            break;
          }
          var len = 0L;
          for (var i=0; i<nalSizeLen; i++) {
            len = (len<<8) | body[pos+i];
          }
          // 先頭1バイトは NAL ヘッダなので len>=1 が要る。
          // 残バイト数を超える長さは壊れた入力なのでそこで打ち切る。
          if (len<1 || len > body.Length-pos-nalSizeLen) {
            broken = true;
            break;
          }
          var nal_type = body[pos+nalSizeLen] & 0x1F;
          // 先頭が AUD でなければ AUD を差し込む(2パス目も同じ規則で書く)。
          if (units==0 && nal_type!=9) {
            payload_size += NALUnit.GetByteSize(NALUnit.AccessUnitDelimiter);
          }
          if (nal_type==5) {
            idr = true;
            payload_size += inject_size;
          }
          payload_size += 4 + len; // スタートコード + NAL(ヘッダ含む)
          pos += nalSizeLen + (int)len;
          units += 1;
        }
        // 末尾の NAL 長が残りバイト数と合わない場合でも、そこまでに読み切れた NAL は
        // 長さが整合しており単体で復号できるので出力し、切れた末尾だけを捨てる。
        // アクセスユニットごと捨てるとフレームが1枚丸ごと欠け、キーフレームなら
        // SPS/PPS ごと落ちて次のGOPまで映像が出ない。
        // 読めた NAL が1つも無いときだけ、出せるものが無いのでフレームごと捨てる。
        if (broken) {
          WarnBrokenVideoFrame(units);
          if (units<1) return;
        }
        // 現実のフレームがこの規模になることはない(タグ長は12MB上限)が、細工された入力
        // (小さな IDR NAL の羅列×注入の繰り返し)で合計が int を溢れることだけは防ぐ。
        var header_size = PESPacket.GetHeaderSize(has_pts: true, has_dts: true);
        if (payload_size > int.MaxValue - header_size) {
          WarnBrokenVideoFrame(0);
          return;
        }
        // 時刻原点の取り方と負CTSのクランプは基底の共通規則。
        var dts = NormalizeTimestamp(msg.Timestamp);
        var pts = ComputeVideoPts(dts, cts);
        var pes_packet = new byte[header_size + (int)payload_size];
        PESPacket.WriteHeader(
          pes_packet.AsSpan(0, header_size),
          0xE0,
          TSTimeStamp.FromMilliseconds(pts),
          TSTimeStamp.FromMilliseconds(dts),
          (int)payload_size);
        // 2パス目: 1パス目で検証済みの units 個の NAL をそのまま書き写す。
        var dst = pes_packet.AsSpan(header_size);
        pos = offset;
        for (var n=0; n<units; n++) {
          var len = 0;
          for (var i=0; i<nalSizeLen; i++) {
            len = (len<<8) | body[pos+i];
          }
          pos += nalSizeLen;
          var nal_type = body[pos] & 0x1F;
          if (n==0 && nal_type!=9) {
            dst = dst.Slice(NALUnit.WriteTo(dst, NALUnit.AccessUnitDelimiter));
          }
          if (nal_type==5) {
            foreach (var unit in sps)    dst = dst.Slice(NALUnit.WriteTo(dst, unit));
            foreach (var unit in pps)    dst = dst.Slice(NALUnit.WriteTo(dst, unit));
            foreach (var unit in spsExt) dst = dst.Slice(NALUnit.WriteTo(dst, unit));
          }
          dst[0] = 0;
          dst[1] = 0;
          dst[2] = 0;
          dst[3] = 1;
          new ReadOnlySpan<byte>(body, pos, len).CopyTo(dst.Slice(4));
          // forbidden_zero ビットは従来(NAL ヘッダを再構築していた頃)と同じく 0 に正規化する。
          dst[4] &= 0x7F;
          dst = dst.Slice(4+len);
          pos += len;
        }
        WritePATPMT(writer);
        writer.WriteTSPackets(
          VideoPID,
          keyframe || idr,
          idr ? (TSTimeStamp?)TSTimeStamp.FromMilliseconds(dts) : null,
          pes_packet
        );
      }

      // タグ分類は共有分類器(FLVTagClassifier)、種別ごとの振り分けは FLVRemuxContextBase に
      // 一本化してある。以前はここで body[0]&0x0F を codecId として直接読んでいたため、
      // E-RTMP チャンネルでは Ex タグの packetType を codecId と誤読し
      // (例: ModEx の 7 を AVC と誤認)、ゴミTSを出力していた。
      // 本フィルタは H.264/AAC のみ対応なので、それ以外はレガシー/Ex を問わず破棄する。
      // 記述子は共有インスタンスなので参照比較でよい。
      protected override bool IsSupportedAudioCodec(FourCcCodec? codec)
      {
        return codec==FourCcRegistry.Aac;
      }

      protected override bool IsSupportedVideoCodec(FourCcCodec? codec)
      {
        return codec==FourCcRegistry.Avc;
      }

      protected override void OnAudioConfig(byte[] body, int offset)
      {
        OnAACHeader(body, offset);
      }

      protected override void OnAudioFrame(RTMPMessage msg, int offset)
      {
        OnAACBody(msg, offset);
      }

      protected override void OnVideoConfig(RTMPMessage msg, int offset, FourCcCodec? codec)
      {
        OnAVCHeader(msg.Body, offset);
      }

      protected override void OnVideoFrame(RTMPMessage msg, int offset, int compositionTime, bool keyframe)
      {
        OnAVCBody(msg, offset, compositionTime, keyframe);
      }

      public override void OnData(DataMessage msg)
      {
      }

    }

  }

  public class FLVToTSContentFilter
    : IContentFilter
  {
    public string Name { get { return "FLVToTS"; } }
    public IContentSink Activate(IContentSink sink)
    {
      // 変換ループの起動はコンストラクタではなくここで行う(FLVContentFilterSinkBase.Start)。
      var filter_sink = new FLVToTSContentFilterSink(sink);
      filter_sink.Start();
      return filter_sink;
    }

    public class FLVToTSContentFilterSink
      : FLVContentFilterSinkBase
    {
      private static readonly Logger logger = new Logger(typeof(FLVToTSContentFilter));

      public FLVToTSContentFilterSink(IContentSink sink)
        : base(sink, logger)
      {
      }

      protected override string ContentType      { get { return "TS"; } }
      protected override string MimeType         { get { return "video/mp2t"; } }
      protected override string ContentExtension { get { return ".ts"; } }

      class MPEG2TSSink
        : FLVToMPEG2TS.IMPEG2TSContentSink
      {
        public IContentSink TargetSink { get; }
        // TS も1つの上流Contentから複数の出力(PAT/PMT とタグごとの PES バースト)を出すため、
        // 上流Contentの位置を流用すると (Stream,Timestamp,Position) が衝突し、
        // ContentCollection の重複排除で2件目以降が黙って落ちる(BufferedContentSink が
        // 複数タグを1つの Content に束ねるので、これは通常運用で常に起きる)。
        // MKVSink と同様、上流Contentは参照せず出力側で独自に連番Positionを採番する。
        private int streamId = -1;
        private long position = 0;
        // 経過時間にしか使わないので、ローカル時刻への変換ぶん重い DateTime.Now は使わない
        // (PESバーストごとに参照される)。
        private DateTime streamOrigin = DateTime.UtcNow;
        private ReadOnlyMemory<byte> patBuffer = ReadOnlyMemory<byte>.Empty;

        public MPEG2TSSink(IContentSink targetSink)
        {
          TargetSink = targetSink;
        }

        public void OnPAT(ReadOnlyMemory<byte> bytes)
        {
          patBuffer = bytes;
        }

        public void OnPMT(ReadOnlyMemory<byte> bytes)
        {
          if (patBuffer.Length==0) return;
          var header = new Memory<byte>(new byte[patBuffer.Length + bytes.Length]);
          patBuffer.CopyTo(header);
          bytes.CopyTo(header.Slice(patBuffer.Length));
          // 新しい PAT/PMT(初回と、トラック構成が変わったときの再送)は新しい論理ストリーム。
          // stream id を進め位置を 0 へ戻す。
          streamId += 1;
          position = 0;
          streamOrigin = DateTime.UtcNow;
          TargetSink.OnContentHeader(
            new Content(streamId, TimeSpan.Zero, 0, header, PCPChanPacketContinuation.None)
          );
          position += header.Length;
        }

        public void OnTSPackets(ReadOnlyMemory<byte> bytes)
        {
          // Context は必ず WritePATPMT を先に呼ぶので、ヘッダ前にここへは来ない。
          if (streamId<0) return;
          TargetSink.OnContent(
            new Content(streamId, DateTime.UtcNow-streamOrigin, position, bytes, PCPChanPacketContinuation.None)
          );
          position += bytes.Length;
        }
      }

      // MPEG2TSSink は上流Contentを参照しないため、ヘッダも本体も同じくバッファへ流すだけでよい。
      protected override ContentProcessor CreateProcessor(IContentSink targetSink)
      {
        return new ContentProcessor(new FLVToMPEG2TS.Context(new MPEG2TSSink(targetSink)));
      }
    }

  }

  [Plugin]
  public class FLVToTSContentFilterPlugin
    : PluginBase
  {
    public override string Name {
      get { return "FLVToTSContentFilter"; }
    }

    private FLVToTSContentFilter filter = new FLVToTSContentFilter();
    protected override void OnAttach(PeerCastApplication app)
    {
      app.PeerCast.ContentFilters.Add(filter);
    }

    protected override void OnDetach(PeerCastApplication app)
    {
      app.PeerCast.ContentFilters.Remove(filter);
    }
  }
}
