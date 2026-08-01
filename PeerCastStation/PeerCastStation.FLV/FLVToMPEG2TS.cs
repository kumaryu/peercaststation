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

      private Span<byte> WriteUInt16BE(Span<byte> dst, int value)
      {
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
        dst[0] = (byte)value;
        return dst.Slice(1);
      }

      private Span<byte> WriteBytes(Span<byte> dst, ReadOnlySpan<byte> value)
      {
        value.CopyTo(dst);
        return dst.Slice(value.Length);
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
        body = WriteUInt16BE(body, (15<<12) | program_info_sz);
        foreach (var entry in pmt.ProgramInfo) {
          body = WriteByte(body, entry.Tag);
          body = WriteByte(body, entry.Data.Length);
          body = WriteBytes(body, entry.Data);
        }
        foreach (var entry in pmt.Table) {
          body = WriteByte(body, entry.StreamType);
          body = WriteUInt16BE(body, (7<<13) | (entry.PID & 0x1FFF));
          body = WriteUInt16BE(body, (15<<12) | (entry.ESInfo.Length & 0xFFF));
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
        section = WriteUInt16BE(section, section_syntax_indicator | reserved | (section_length & 0xFFF));
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

    public class PESPacket
    {
      public byte StreamId { get; private set; }
      public TSTimeStamp? PTS { get; private set; }
      public TSTimeStamp? DTS { get; private set; }
      public byte[] Payload { get; private set; }

      public PESPacket(byte stream_id, TSTimeStamp? pts, TSTimeStamp? dts, byte[] payload)
      {
        StreamId = stream_id;
        PTS = pts;
        DTS = dts;
        Payload = payload;
      }

      private static readonly byte[] PESStartPrefix = new byte[] { 0, 0, 1 };
      public static void WriteTo(Stream s, PESPacket pkt)
      {
        var pes_scrambling_control    = 0;
        var pes_priority              = false;
        var data_alignment_indicator  = false;
        var copyright                 = false;
        var original_or_copy          = false;
        var pts_dts_flags             = (pkt.PTS.HasValue ? (pkt.DTS.HasValue ? 3 : 2) : 0);
        var escr_flag                 = false;
        var es_rate_flag              = false;
        var dsm_trick_mode_flag       = false;
        var additional_copy_info_flag = false;
        var pes_crc_flag              = false;
        var pes_extension_flag        = false;
        var pes_header_data = new byte[0];
        if (pkt.PTS.HasValue && pkt.DTS.HasValue) {
          var pts = pkt.PTS.Value.Tick / 300;
          var dts = pkt.DTS.Value.Tick / 300;
          pes_header_data = new byte[10] {
            (byte)((0x3 << 4) | (((pts >> 30) & 0x0007)<<1) | 1),
            (byte)((            (((pts >> 15) & 0x7FFF)<<1) | 1)>>8),
            (byte)((            (((pts >> 15) & 0x7FFF)<<1) | 1)&0xFF),
            (byte)((            (((pts >>  0) & 0x7FFF)<<1) | 1)>>8),
            (byte)((            (((pts >>  0) & 0x7FFF)<<1) | 1)&0xFF),
            (byte)((0x1 << 4) | (((dts >> 30) & 0x0007)<<1) | 1),
            (byte)((            (((dts >> 15) & 0x7FFF)<<1) | 1)>>8),
            (byte)((            (((dts >> 15) & 0x7FFF)<<1) | 1)&0xFF),
            (byte)((            (((dts >>  0) & 0x7FFF)<<1) | 1)>>8),
            (byte)((            (((dts >>  0) & 0x7FFF)<<1) | 1)&0xFF),
          };
        }
        else if (pkt.PTS.HasValue) {
          var pts = pkt.PTS.Value.Tick / 300;
          pes_header_data = new byte[5] {
            (byte)((0x2 << 4) | (((pts >> 30) & 0x0007)<<1) | 1),
            (byte)((            (((pts >> 15) & 0x7FFF)<<1) | 1)>>8),
            (byte)((            (((pts >> 15) & 0x7FFF)<<1) | 1)&0xFF),
            (byte)((            (((pts >>  0) & 0x7FFF)<<1) | 1)>>8),
            (byte)((            (((pts >>  0) & 0x7FFF)<<1) | 1)&0xFF),
          };
        }
        var packet_length = pkt.Payload.Length + 3 + pes_header_data.Length;
        // PES_packet_length は16bit。WriteUInt16BE は黙って下位16bitに丸めるため、
        // 65535を超えるアクセスユニット(1080pのIDRフレームなど珍しくない)では
        // 実長と無関係な短い長さを宣言してしまい、下流のデマルチプレクサが同期を失う。
        // 映像ESに限り 0 = 長さ未指定が許されている(次の PES 開始まで)ので 0 を書く。
        // 音声(ADTS)は OnAACBody が 0x1FFF 上限で弾くためここには到達しない。
        if (packet_length>0xFFFF && (pkt.StreamId & 0xF0)==0xE0) {
          packet_length = 0;
        }

        s.Write(PESStartPrefix, 0, PESStartPrefix.Length);
        s.WriteByte(pkt.StreamId);
        s.WriteUInt16BE(packet_length);
        var header_data1 =
          (0x2 << 6) |
          ((pes_scrambling_control & 0x3) << 4) |
          ((pes_priority             ? 1 : 0) << 3) |
          ((data_alignment_indicator ? 1 : 0) << 2) |
          ((copyright                ? 1 : 0) << 1) |
          ((original_or_copy         ? 1 : 0) << 0);
        s.WriteByte((byte)header_data1);
        var header_data2 =
          ((pts_dts_flags & 0x3) << 6) |
          ((escr_flag                 ? 1 : 0) << 5) |
          ((es_rate_flag              ? 1 : 0) << 4) |
          ((dsm_trick_mode_flag       ? 1 : 0) << 3) |
          ((additional_copy_info_flag ? 1 : 0) << 2) |
          ((pes_crc_flag              ? 1 : 0) << 1) |
          ((pes_extension_flag        ? 1 : 0) << 0);
        s.WriteByte((byte)header_data2);
        s.WriteByte((byte)pes_header_data.Length);
        s.Write(pes_header_data, 0, pes_header_data.Length);
        s.Write(pkt.Payload, 0, pkt.Payload.Length);
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

      public static NALUnit ReadFrom(Stream s, int len)
      {
        var data = s.ReadByte();
        var nal_ref_idc   = (data & 0x60)>>5;
        var nal_unit_type = (data & 0x1F);
        var rbsp_bytes = new byte[len-1];
        s.Read(rbsp_bytes, 0, rbsp_bytes.Length);
        return new NALUnit(nal_ref_idc, nal_unit_type, rbsp_bytes);
      }

      private static readonly byte[] WritePrefix = new byte[] { 0, 0, 0, 1 };
      public static void WriteToByteStream(Stream s, NALUnit unit)
      {
        s.Write(WritePrefix, 0, WritePrefix.Length);
        s.WriteByte((byte)((unit.NALRefIdc << 5) | (unit.NALUnitType & 0x1F)));
        s.Write(unit.Payload, 0, unit.Payload.Length);
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

      public void Write(int bits, int value)
      {
        buffer = (buffer << bits) | ((long)value & ((1<<bits)-1));
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
      : IRTMPContentSink
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
      private ADTSHeader adtsHeader = ADTSHeader.Default;
      private int nalSizeLen = 0;
      private long ptsBase = -1;
      private bool warnedBrokenAudioConfig = false;
      private bool warnedBrokenVideoConfig = false;
      private bool warnedMissingAudioConfig = false;
      private bool warnedOversizedAudioFrame = false;
      private bool warnedMissingVideoConfig = false;
      private bool warnedBrokenVideoFrame = false;
      private bool warnedUnsupportedVideo = false;
      private bool warnedUnsupportedAudio = false;
      private readonly Logger logger = new Logger(typeof(FLVToMPEG2TS));

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
        adtsHeader = ADTSHeader.Default;
        nalSizeLen = 0;
        ptsBase = -1;
        warnedBrokenAudioConfig = false;
        warnedBrokenVideoConfig = false;
        warnedMissingAudioConfig = false;
        warnedOversizedAudioFrame = false;
        warnedMissingVideoConfig = false;
        warnedBrokenVideoFrame = false;
        warnedUnsupportedVideo = false;
        warnedUnsupportedAudio = false;
      }

      public void OnFLVHeader(FLVFileHeader header)
      {
        Clear();
      }

      private void WritePATPMT(TSWriter writer)
      {
        if (isHeaderSent) {
          return;
        }
        if (hasVideo) {
          pmt.Table.Add(new ProgramMapEntry(VideoPID, 0x1B, new byte[0]));
          pmt.PCRPID = VideoPID;
        }
        if (hasAudio) {
          pmt.Table.Add(new ProgramMapEntry(AudioPID, 0x0F, new byte[0]));
          if (!hasVideo) {
            pmt.PCRPID = AudioPID;
          }
        }
        pat.PIDToProgramNumber[ProgramMapTablePID] = 1;
        writer.WritePAT(pat);
        writer.WritePMT(ProgramMapTablePID, pmt);
        isHeaderSent = true;
      }

      private void OnAACHeader(byte[] body, int offset)
      {
        // 切り詰められた AudioSpecificConfig はここで捨てる。ビット読み出しで例外を投げると
        // FLVFileParser.Read の EndOfStreamException catch がタグ先頭まで巻き戻すため
        // (=「データ待ち」と誤認される)、毒タグがバッファ先頭に残って以後の全パースが
        // 再スローし続け、出力が恒久停止したうえで contentBuffer が無限に成長する。
        if (offset<0 || body.Length<=offset) {
          WarnBrokenAudioConfig();
          return;
        }
        var config = new byte[body.Length-offset];
        Array.Copy(body, offset, config, 0, config.Length);
        if (!AudioSpecificConfig.TryParse(config, out var asc)) {
          WarnBrokenAudioConfig();
          return;
        }
        // ADTS は表引きインデックス(0-12)しか表現できない。明示レートのエスケープ(15)や
        // 予約値(13/14)をそのまま書くと禁止インデックス入りの ADTS が全フレームに付き、
        // デコーダが黙って全音声を拒否するため、設定ごと破棄する。
        if (asc.SamplingFrequencyIndex>=13) {
          WarnBrokenAudioConfig();
          return;
        }
        // ADTS の profile は2bit(audioObjectType-1、すなわち AOT 1..4 のみ表現できる)。
        // 範囲外をそのまま書くと BitWriter が下位2bitに丸め、明示signalingのHE-AAC(AOT=5)は
        // profile=0(Main)と嘘をつき、AOT=0 は予約値の3になってデコーダが全フレームを拒否する。
        // サンプリング周波数インデックスと同じく、設定ごと破棄する。
        if (asc.AudioObjectType<1 || asc.AudioObjectType>4) {
          WarnBrokenAudioConfig();
          return;
        }
        var type = asc.AudioObjectType;
        var sampling_freq_idx = asc.SamplingFrequencyIndex;
        var channel_configuration = asc.ChannelConfiguration;
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
        hasAudio = true;
      }

      private void WarnBrokenAudioConfig()
      {
        if (warnedBrokenAudioConfig) return;
        logger.Warn("FLVToMPEG2TS: AudioSpecificConfigが不完全なため音声シーケンスヘッダを破棄します");
        warnedBrokenAudioConfig = true;
      }

      private void OnAACBody(RTMPMessage msg, int offset)
      {
        if (offset<0 || msg.Body.Length<=offset) return;
        // シーケンスヘッダ未受信のまま流すと adtsHeader が Default(sync=0 の全ゼロ)のままで、
        // ゴミ ADTS が AudioPID に出力される上、最初の WritePATPMT が音声 ES 抜きの PMT を
        // 確定させてしまう。映像側(nalSizeLen<1)と同様に破棄する。
        if (!hasAudio) {
          WarnMissingAudioConfig();
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
        // 最初に実際に出力するフレーム(ts=0を含む)を基準にする。FLVToMKV.OnAudioBody と同じ規則。
        if (ptsBase<0) ptsBase = msg.Timestamp;
        // ptsBase より小さいタイムスタンプで負になった PTS は、PESPacket のビット詰めで
        // 2^33 にラップした値として出力されるためクランプする。
        var pts = Math.Max(0, msg.Timestamp - Math.Max(0, ptsBase));
        var header = new ADTSHeader(adtsHeader, frame_length);
        var pes_payload = new MemoryStream();
        using (pes_payload) {
          ADTSHeader.WriteTo(pes_payload, header);
          pes_payload.Write(msg.Body, offset, raw_length);
        }
        var pes = new PESPacket(0xC0, TSTimeStamp.FromMilliseconds(pts), null, pes_payload.ToArray());
        var pes_packet = new MemoryStream();
        using (pes_packet) {
          PESPacket.WriteTo(pes_packet, pes);
        }
        WritePATPMT(writer);
        writer.WriteTSPackets(
          AudioPID,
          true,
          null,
          pes_packet.ToArray()
        );
      }

      private static bool TryReadByte(ref ReadOnlySpan<byte> bytes, out byte value)
      {
        if (bytes.Length<1) {
          value = 0;
          return false;
        }
        value = bytes[0];
        bytes = bytes.Slice(1);
        return true;
      }

      private static bool TryReadNALUnitArray(ref ReadOnlySpan<byte> data, int cnt, out NALUnit[] result)
      {
        result = new NALUnit[0];
        var ary = new NALUnit[cnt];
        for (int i = 0; i<cnt; i++) {
          if (data.Length<2) return false;
          var len = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(data);
          data = data.Slice(2);
          // NALUnit.ReadFrom は先頭1バイトをヘッダとして消費するので len>=1 が要る。
          if (len<1 || data.Length<len) return false;
          ary[i] = NALUnit.ReadFrom(data, len);
          data = data.Slice(len);
        }
        result = ary;
        return true;
      }

      /// <summary>
      /// AVCDecoderConfigurationRecord(avcC, ISO/IEC 14496-15)を解析する。
      /// バイトが不足する場合は false を返す(例外は投げない)。
      /// 方針は音声側の TryParseAudioSpecificConfig と揃えてある。
      /// </summary>
      private static bool TryParseAVCDecoderConfig(
        ReadOnlySpan<byte> data,
        out int nal_size_len,
        out NALUnit[] sps,
        out NALUnit[] pps,
        out NALUnit[] sps_ext)
      {
        nal_size_len = 0;
        sps     = new NALUnit[0];
        pps     = new NALUnit[0];
        sps_ext = new NALUnit[0];
        if (!TryReadByte(ref data, out var configuration_version)) return false;
        if (!TryReadByte(ref data, out var avc_profile_indication)) return false;
        if (!TryReadByte(ref data, out var profile_compatibility)) return false;
        if (!TryReadByte(ref data, out var avc_level_indication)) return false;
        if (!TryReadByte(ref data, out var length_size_minus_one)) return false;
        nal_size_len = (length_size_minus_one & 0x3) + 1;
        if (!TryReadByte(ref data, out var sps_count)) return false;
        if (!TryReadNALUnitArray(ref data, sps_count & 0x1F, out sps)) return false;
        if (!TryReadByte(ref data, out var pps_count)) return false;
        if (!TryReadNALUnitArray(ref data, pps_count, out pps)) return false;
        if (data.Length>0 &&
            (avc_profile_indication==100 ||
             avc_profile_indication==110 ||
             avc_profile_indication==122 ||
             avc_profile_indication==144)) {
          // chroma_format / bit_depth_luma / bit_depth_chroma は使わないが位置を進める。
          if (!TryReadByte(ref data, out _)) return false;
          if (!TryReadByte(ref data, out _)) return false;
          if (!TryReadByte(ref data, out _)) return false;
          if (!TryReadByte(ref data, out var sps_ext_count)) return false;
          if (!TryReadNALUnitArray(ref data, sps_ext_count, out sps_ext)) return false;
        }
        return true;
      }

      private void OnAVCHeader(byte[] body, int offset)
      {
        // 切り詰められた/矛盾した avcC はここで捨てる。音声側(OnAACHeader)と同じ理由で、
        // 境界外アクセスの例外を投げると FLVFileParser がタグ先頭まで巻き戻して
        // 同じ毒タグを永久に再パースし、出力が恒久停止する。
        // sps_count/pps_count は実データ量と無関係に最大31/255を名乗れるので、
        // 読み出し前に必ず残バイト数と照合する。
        if (offset<0 || body.Length<=offset) {
          WarnBrokenVideoConfig();
          return;
        }
        var data = new ReadOnlySpan<byte>(body, offset, body.Length-offset);
        if (!TryParseAVCDecoderConfig(data, out var nal_size_len, out var sps, out var pps, out var sps_ext)) {
          WarnBrokenVideoConfig();
          return;
        }
        this.nalSizeLen = nal_size_len;
        this.sps        = sps;
        this.pps        = pps;
        this.spsExt     = sps_ext;
        hasVideo = true;
      }

      private void WarnBrokenVideoConfig()
      {
        if (warnedBrokenVideoConfig) return;
        logger.Warn("FLVToMPEG2TS: avcCが不完全なため映像シーケンスヘッダを破棄します");
        warnedBrokenVideoConfig = true;
      }

      private void WarnMissingAudioConfig()
      {
        if (warnedMissingAudioConfig) return;
        logger.Warn("FLVToMPEG2TS: 音声シーケンスヘッダ(AudioSpecificConfig)より前のフレームを破棄します");
        warnedMissingAudioConfig = true;
      }

      private void WarnOversizedAudioFrame()
      {
        if (warnedOversizedAudioFrame) return;
        logger.Warn("FLVToMPEG2TS: ADTSのframe_length(13bit)に収まらない音声フレームを破棄します");
        warnedOversizedAudioFrame = true;
      }

      private void WarnMissingVideoConfig()
      {
        if (warnedMissingVideoConfig) return;
        logger.Warn("FLVToMPEG2TS: 映像シーケンスヘッダ(avcC)より前のフレームを破棄します");
        warnedMissingVideoConfig = true;
      }

      private void WarnBrokenVideoFrame()
      {
        if (warnedBrokenVideoFrame) return;
        logger.Warn("FLVToMPEG2TS: NALユニット長が不正なため映像フレームを破棄します");
        warnedBrokenVideoFrame = true;
      }

      private void OnAVCBody(RTMPMessage msg, int offset, int cts, bool keyframe)
      {
        if (offset<0 || msg.Body.Length<=offset) return;
        // avcC(シーケンスヘッダ)より先に CodedFrames が来ると nalSizeLen が 0 のまま。
        // その場合 NAL 長は常に 0 と読めてしまい NALUnit.ReadFrom が new byte[-1] で落ちる。
        // NAL の区切りが分からない以上このフレームは復号できないので破棄する。
        if (nalSizeLen<1) {
          WarnMissingVideoConfig();
          return;
        }
        var access_unit_delimiter = false;
        var idr = false;
        var broken = false;
        var nalbytestream = new MemoryStream();
        int units = 0;
        using (nalbytestream)
        using (var body=new MemoryStream(msg.Body, 0, msg.Body.Length)) {
          body.Seek(offset, SeekOrigin.Begin);
          while (body.Position<body.Length) {
            // 長さは long に符号なしで読む。Int32 で読むと nalSizeLen==4
            // (lengthSizeMinusOne=3、一般的な既定値)で最上位ビットが立つ入力が
            // 負値になり new byte[負数] で OverflowException、0x7FFFFFFF なら
            // 約2GBの確保を試みて OutOfMemoryException になる。
            if (body.Length-body.Position < nalSizeLen) {
              broken = true;
              break;
            }
            var len = 0L;
            for (var i=0; i<nalSizeLen; i++) {
              len = (len<<8) | (uint)body.ReadByte();
            }
            // NALUnit.ReadFrom は先頭1バイトをヘッダとして消費するので len>=1 が要る。
            // 残バイト数を超える長さは壊れた入力なのでフレームごと捨てる。
            if (len<1 || len > body.Length-body.Position) {
              broken = true;
              break;
            }
            var nalu = NALUnit.ReadFrom(body, (int)len);
            if (nalu.NALUnitType==9) {
              access_unit_delimiter = true;
            }
            if (!access_unit_delimiter) {
              NALUnit.WriteToByteStream(nalbytestream, NALUnit.AccessUnitDelimiter);
              access_unit_delimiter = true;
            }
            if (nalu.NALUnitType==5) {
              idr = true;
              foreach (var unit in sps) {
                NALUnit.WriteToByteStream(nalbytestream, unit);
              }
              foreach (var unit in pps) {
                NALUnit.WriteToByteStream(nalbytestream, unit);
              }
              foreach (var unit in spsExt) {
                NALUnit.WriteToByteStream(nalbytestream, unit);
              }
            }
            NALUnit.WriteToByteStream(nalbytestream, nalu);
            units += 1;
          }
        }
        // 途中で壊れたフレームは部分出力せず丸ごと捨てる。
        // 中途半端な NAL 列を流すと下流のデコーダを壊すだけで得がない。
        if (broken) {
          WarnBrokenVideoFrame();
          return;
        }
        // 最初に実際に出力するフレーム(ts=0を含む)を基準にする。FLVToMKV.OnVideoBody と同じ規則。
        // 破棄したタグや2番目のフレームで基準を確定させると、先頭フレームと PTS が衝突・逆行する。
        if (ptsBase<0) ptsBase = msg.Timestamp;
        // ptsBase より小さいタイムスタンプや先頭Bフレームの負CTS(符号拡張済み)で
        // pts/dts が負になると、PESPacket のビット詰めで 2^33 にラップした
        // 約26.5時間先のタイムスタンプとして出力されるためクランプする。
        // pts>=dts のクランプは MPEG-TS の PTS>=DTS 制約の維持も兼ねる。
        var dts = Math.Max(0, msg.Timestamp - Math.Max(0, ptsBase));
        var pts = Math.Max(dts, dts + cts);
        var pes = new PESPacket(
          0xE0,
          TSTimeStamp.FromMilliseconds(pts),
          TSTimeStamp.FromMilliseconds(dts),
          nalbytestream.ToArray()
        );
        var pes_packet = new MemoryStream();
        using (pes_packet) {
          PESPacket.WriteTo(pes_packet, pes);
        }
        WritePATPMT(writer);
        writer.WriteTSPackets(
          VideoPID,
          keyframe || idr,
          idr ? (TSTimeStamp?)TSTimeStamp.FromMilliseconds(dts) : null,
          pes_packet.ToArray()
        );
      }

      // タグ分類は共有分類器(FLVTagClassifier)に一本化する。
      // 以前はここで body[0]&0x0F を codecId として直接読んでいたため、E-RTMP チャンネルでは
      // Ex タグの packetType を codecId と誤読し(例: ModEx の 7 を AVC と誤認)、
      // ゴミTSを出力していた。本フィルタは H.264/AAC のみ対応なので、
      // それ以外はレガシー/Ex を問わず警告して破棄する。
      public void OnAudio(RTMPMessage msg)
      {
        var info = FLVTagClassifier.Classify(msg);
        if (info.Kind==FLVTagKind.AudioSequenceEnd || info.Kind==FLVTagKind.Control) return;
        if (info.FourCc!=FLVTagClassifier.FourCcAac) {
          WarnUnsupportedAudio(info.FourCc);
          return;
        }
        switch (info.Kind) {
        case FLVTagKind.AudioSequenceHeader:
          OnAACHeader(msg.Body, info.PayloadOffset);
          break;
        case FLVTagKind.AudioFrame:
          OnAACBody(msg, info.PayloadOffset);
          break;
        default:
          WarnUnsupportedAudio(info.FourCc);
          break;
        }
      }

      public void OnVideo(RTMPMessage msg)
      {
        var info = FLVTagClassifier.Classify(msg);
        if (info.Kind==FLVTagKind.VideoSequenceEnd || info.Kind==FLVTagKind.Control) return;
        if (info.FourCc!=FLVTagClassifier.FourCcAvc) {
          WarnUnsupportedVideo(info.FourCc);
          return;
        }
        switch (info.Kind) {
        case FLVTagKind.VideoSequenceHeader:
          OnAVCHeader(msg.Body, info.PayloadOffset);
          break;
        case FLVTagKind.VideoKeyFrame:
        case FLVTagKind.VideoInterFrame:
          OnAVCBody(msg, info.PayloadOffset, info.CompositionTime, info.Kind==FLVTagKind.VideoKeyFrame);
          break;
        default:
          WarnUnsupportedVideo(info.FourCc);
          break;
        }
      }

      private void WarnUnsupportedVideo(string? fourcc)
      {
        if (warnedUnsupportedVideo) return;
        logger.Warn("FLVToMPEG2TS: 未対応の映像コーデック/構成のため破棄します (FourCC={0})", fourcc ?? "(none)");
        warnedUnsupportedVideo = true;
      }

      private void WarnUnsupportedAudio(string? fourcc)
      {
        if (warnedUnsupportedAudio) return;
        logger.Warn("FLVToMPEG2TS: 未対応の音声コーデック/構成のため破棄します (FourCC={0})", fourcc ?? "(none)");
        warnedUnsupportedAudio = true;
      }

      public void OnData(DataMessage msg)
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
      return new FLVToTSContentFilterSink(sink);
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
        public Content? HeaderContent { get; set; } = null;
        public Content? RecentContent { get; set; } = null;
        private ReadOnlyMemory<byte> patBuffer = ReadOnlyMemory<byte>.Empty;
        private ReadOnlyMemory<byte> pmtBuffer = ReadOnlyMemory<byte>.Empty;

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
          pmtBuffer = bytes;
          if (patBuffer.Length>0 && pmtBuffer.Length>0 && HeaderContent!=null) {
            var header = new Memory<byte>(new byte[patBuffer.Length + pmtBuffer.Length]);
            patBuffer.CopyTo(header);
            pmtBuffer.CopyTo(header.Slice(patBuffer.Length));
            TargetSink.OnContentHeader(
              new Content(
                HeaderContent.Stream,
                HeaderContent.Timestamp,
                HeaderContent.Position,
                header,
                HeaderContent.ContFlag
              )
            );
          }
        }

        public void OnTSPackets(ReadOnlyMemory<byte> bytes)
        {
          if (RecentContent!=null) {
            TargetSink.OnContent(
              new Content(
                RecentContent.Stream,
                RecentContent.Timestamp,
                RecentContent.Position,
                bytes,
                RecentContent.ContFlag
              )
            );
          }
        }
      }

      protected override async Task ProcessMessagesLoopAsync(IContentSink targetSink, CancellationToken cancellationToken)
      {
        var tsSink = new MPEG2TSSink(targetSink);
        var context = new FLVToMPEG2TS.Context(tsSink);
        var parseBuffer = new FLVParseBuffer();
        var msg = await MessageQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
        while (msg.Type!=ContentMessage.MessageType.Stop) {
          switch (msg.Type) {
          case ContentMessage.MessageType.ChannelInfo:
            targetSink.OnChannelInfo(msg.ChannelInfo);
            break;
          case ContentMessage.MessageType.ChannelTrack:
            targetSink.OnChannelTrack(msg.ChannelTrack);
            break;
          // MPEG2TSSink は出力Contentの位置採番に上流Contentを流用するため、
          // どちらを受けたかを覚えてからバッファへ流す。
          case ContentMessage.MessageType.ContentHeader:
            tsSink.HeaderContent = msg.Content;
            parseBuffer.Feed(msg.Content.Data.Span, context);
            break;
          case ContentMessage.MessageType.ContentBody:
            tsSink.RecentContent = msg.Content;
            parseBuffer.Feed(msg.Content.Data.Span, context);
            break;
          }
          msg = await MessageQueue.DequeueAsync(cancellationToken).ConfigureAwait(false);
        }
        targetSink.OnStop(msg.StopReason);
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
