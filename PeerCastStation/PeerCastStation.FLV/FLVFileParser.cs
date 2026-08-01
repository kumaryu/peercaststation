using System;
using System.Linq;
using System.IO;
using PeerCastStation.FLV.RTMP;
using PeerCastStation.Core;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace PeerCastStation.FLV
{
  public class FLVFileHeader
  {
    public byte[] Binary     { get; private set; }
    public byte[] Signature  { get; private set; }
    public int    Version    { get; private set; }
    public bool   HasAudio   { get; private set; }
    public bool   HasVideo   { get; private set; }
    public long   DataOffset { get; private set; }
    public long   Size       { get; private set; }

    public FLVFileHeader(byte[] binary)
    {
      this.Binary     = binary;
      this.Signature  = new byte[] { binary[0], binary[1], binary[2] };
      this.Version    = binary[3];
      this.HasAudio   = (binary[4] & 0x4)!=0;
      this.HasVideo   = (binary[4] & 0x1)!=0;
      this.DataOffset = (binary[5]<<24) | (binary[ 6]<<16) | (binary[ 7]<<8) | (binary[ 8]<<0);
      this.Size       = (binary[9]<<24) | (binary[10]<<16) | (binary[11]<<8) | (binary[12]<<0);
    }

    public bool IsValid {
      get {
        return Signature[0]=='F' && Signature[1]=='L' && Signature[2]=='V' && Version==1 && Size==0;
      }
    }
  }

  public class FLVFileParser
  {
    private enum TagType {
      Audio  = 8,
      Video  = 9,
      Script = 18,
    }

    private struct FLVTagHeader
    {
      public byte[] Binary { get; }

      public bool Filter => (Binary[0] & 0x20)!=0;
      public TagType Type => (TagType)(Binary[0] & 0x1F);
      public int DataSize => (Binary[1]<<16) | (Binary[2]<<8) | (Binary[3]);
      public long Timestamp => (Binary[7]<<24) | (Binary[4]<<16) | (Binary[5]<<8) | (Binary[6]);
      public int StreamID => (Binary[8]<<16) | (Binary[9]<<8) | (Binary[10]);

      private FLVTagHeader(byte[] binary)
      {
        Binary = binary;
      }

      public static bool TryCreate(byte[] binary, [NotNullWhen(true)] out FLVTagHeader? header)
      {
        if (binary.Length<11) {
          header = null;
          return false;
        }
        if ((binary[0] & 0xC0)!=0) {
          header = null;
          return false;
        }
        var type = (TagType)(binary[0] & 0x1F);
        if (type==TagType.Audio || type==TagType.Video || type==TagType.Script) {
          header = new FLVTagHeader(binary);
          return true;
        }
        else {
          header = null;
          return false;
        }
      }
    }

    private class FLVTag
    {
      public FLVTagHeader Header { get; private set; }
      public byte[] Body   { get; private set; }
      public byte[] Footer { get; private set; }
      public bool Filter => Header.Filter;
      public TagType Type => Header.Type;
      public int DataSize => Header.DataSize;
      public long Timestamp => Header.Timestamp;
      public int StreamID => Header.StreamID;
      public long TagSize {
        get { return FLVFileParser.GetUInt32(this.Footer); }
      }

      private FLVTag(FLVTagHeader header, byte[] body, byte[] footer)
      {
        this.Header    = header;
        this.Body      = body;
        this.Footer    = footer;
      }

      public static bool IsValidHeader(byte[] binary)
      {
        if (binary.Length<11) {
          return false;
        }
        if ((binary[0] & 0xC0)!=0) {
          return false;
        }
        var type = (TagType)(binary[0] & 0x1F);
        return type==TagType.Audio || type==TagType.Video || type==TagType.Script;
      }

      public static bool TryReadTag(FLVFileParser owner, FLVTagHeader header, Stream stream, [NotNullWhen(true)] out FLVTag? tag)
      {
        bool eos;
        var body = owner.ReadBytes(stream, header.DataSize, out eos);
        if (eos) {
          tag = null;
          return false;
        }
        var footer = owner.ReadBytes(stream, 4, out eos);
        if (eos) {
          tag = null;
          return false;
        }
        tag = new FLVTag(header, body, footer);
        return true;
      }

      public static async Task<FLVTag> TryReadTagAsync(FLVTagHeader header, Stream stream, CancellationToken cancel_token)
      {
        var body = await stream.ReadBytesAsync(header.DataSize, cancel_token).ConfigureAwait(false);
        var footer = await stream.ReadBytesAsync(4, cancel_token).ConfigureAwait(false);
        var tagsize = FLVFileParser.GetUInt32(footer);
        return new FLVTag(header, body, footer);
      }

      public bool IsValidFooter {
        get { return this.DataSize+11==this.TagSize; }
      }

      public RTMPMessage ToRTMPMessage()
      {
        return new RTMPMessage(
          (RTMPMessageType)this.Type,
          this.Timestamp,
          this.StreamID,
          this.Body);
      }
    }

    private byte[] ReadBytes(Stream stream, int len, out bool eos)
    {
      var res = new byte[len];
      var read = stream.Read(res, 0, len);
      eos = read<len;
      return res;
    }

    private static long GetUInt32(byte[] bin)
    {
      return (bin[0]<<24) | (bin[1]<<16) | (bin[2]<<8) | (bin[3]<<0);
    }

    private enum ReaderState {
      Header,
      Body,
    };
    private ReaderState state = ReaderState.Header;
    private static readonly Logger logger = new Logger(typeof(FLVFileParser));

    private bool warnedBrokenTag = false;

    /// <summary>
    /// 読み終えた1タグを sink へ配る。
    ///
    /// Script タグは DataAMF0Message(RTMPMessage) のコンストラクタで即座に AMF0 解析される。
    /// タグ本体はストリームから完全に読み出せているため、ここで出る例外は「データ待ち」ではなく
    /// タグ内容の破損(切り詰められた AMF、未知マーカー、不正な参照)である。
    /// 呼び出し元の catch(EndOfStreamException)/catch(BadDataException) に巻き込むと
    /// タグ先頭まで巻き戻してしまい、同じ毒タグを永久に再パースし続ける
    /// (=出力の恒久停止とバッファの無限成長)ため、ここで区別して握り、次のタグへ進む。
    ///
    /// 握るのは AMF の復号だけで、sink の呼び出しは try の外に置く。
    /// IsBrokenTagException は ArgumentException や IndexOutOfRangeException を含むため、
    /// sink 呼び出しまで囲うと下流(FLVContentBuffer / FLVToMKV / FLVToMPEG2TS)の
    /// 実装バグが「壊れた入力」として毎タグ握り潰され、出力が無音のまま止まっているのに
    /// ログには入力のせいだと書かれる状態になる。下流側は各自 TryParse と範囲チェックで
    /// 破損入力を弾く責任を持ち、それでも出る例外は本物のバグとして表に出す。
    /// </summary>
    private void DispatchTag(FLVTag tag, IRTMPContentSink sink)
    {
      switch (tag.Type) {
      case TagType.Audio:
        sink.OnAudio(tag.ToRTMPMessage());
        break;
      case TagType.Video:
        sink.OnVideo(tag.ToRTMPMessage());
        break;
      case TagType.Script:
        {
          DataAMF0Message data;
          try {
            data = new DataAMF0Message(tag.ToRTMPMessage());
          }
          catch (EndOfStreamException) {
            LogBrokenTag(tag, "タグ内容が途中で終わっています");
            return;
          }
          catch (Exception e) when (IsBrokenTagException(e)) {
            LogBrokenTag(tag, e.Message);
            return;
          }
          sink.OnData(data);
        }
        break;
      }
    }

    /// <summary>
    /// タグ内容の破損として握り潰してよい例外か。
    /// OutOfMemoryException や OperationCanceledException のように、握っても回復しない
    /// / 呼び出し側が扱うべき例外は意図的に含めない。
    /// </summary>
    private static bool IsBrokenTagException(Exception e)
    {
      return e is InvalidDataException      // AMF0Reader: 未知マーカー/不正な参照
          || e is BadDataException
          || e is ArgumentException         // Span.Slice 等の範囲外
          || e is IndexOutOfRangeException
          || e is OverflowException
          || e is FormatException
          || e is InvalidCastException;     // AMFValue の数値キャスト演算子(非数値型のメタデータ値)
    }

    /// <summary>
    /// 破損タグの読み飛ばしを記録する。壊れた入力では毎タグ発生しうるので
    /// 警告は最初の1回だけにし、以降は Debug に落とす。
    /// </summary>
    private void LogBrokenTag(FLVTag tag, string reason)
    {
      if (warnedBrokenTag) {
        logger.Debug("破損したタグを読み飛ばしました (type={0}, size={1}): {2}", tag.Type, tag.Body.Length, reason);
        return;
      }
      logger.Warn("破損したタグを読み飛ばしました (type={0}, size={1}): {2}", tag.Type, tag.Body.Length, reason);
      warnedBrokenTag = true;
    }

    public bool Read(Stream stream, IRTMPContentSink sink)
    {
      var processed = false;
      var eos = false;
      while (!eos) {
        retry:
        var start_pos = stream.Position;
        // データ不足で抜けるときは通常タグ先頭まで巻き戻し、次の Feed を待つ。
        // ただし再同期スキャンがタグ候補バイトを1つも見つけないまま末尾へ達した場合、
        // 走査済みの範囲にタグ先頭はあり得ないので巻き戻してはならない。
        // 巻き戻すと FLVParseBuffer.Trim が consumed==0 と判断して何も捨てられず、
        // 次の Feed が同じゴミを先頭から再走査するため、0xFF 埋めのような壊れた入力で
        // バッファが無制限に伸び、走査量がバイト数の二乗で増える。
        var resume_pos = start_pos;
        try {
          switch (state) {
          case ReaderState.Header:
            {
              var bin = ReadBytes(stream, 13, out eos);
              if (eos) goto error;
              var header = new FLVFileHeader(bin);
              if (header.IsValid) {
                sink.OnFLVHeader(header);
                state = ReaderState.Body;
              }
              else {
                throw new BadDataException();
              }
            }
            break;
          case ReaderState.Body:
            {
              var bin = ReadBytes(stream, 11, out eos);
              if (eos) goto error;
              var read_valid = false;
              if (FLVTagHeader.TryCreate(bin, out var header)) {
                if (FLVTag.TryReadTag(this, header.Value, stream, out var tag)) {
                  if (tag.IsValidFooter) {
                    read_valid = true;
                    DispatchTag(tag, sink);
                  }
                }
                else {
                  eos = true;
                  goto error;
                }
              }
              else {
                stream.Position = start_pos;
                var headerbin = ReadBytes(stream, 13, out eos);
                if (eos) goto error;
                var fileheader = new FLVFileHeader(headerbin);
                if (fileheader.IsValid) {
                  read_valid = true;
                  sink.OnFLVHeader(fileheader);
                }
              }
              if (!read_valid) {
                stream.Position = start_pos+1;
                var b = stream.ReadByte();
                while (true) {
                  if (b<0) {
                    // 走査した範囲にタグ候補は存在しないので、丸ごと消費済みとして捨てさせる。
                    resume_pos = stream.Position;
                    eos = true;
                    goto error;
                  }
                  if ((b & 0xC0)==0 && ((b & 0x1F)==8 || (b & 0x1F)==9 || (b & 0x1F)==18)) {
                    break;
                  }
                  b = stream.ReadByte();
                }
                stream.Position = stream.Position-1;
                goto retry;
              }
            }
            break;
          }
          processed = true;
        }
        catch (EndOfStreamException) {
          stream.Position = start_pos;
          eos = true;
        }
        catch (BadDataException) {
          stream.Position = start_pos+1;
        }
      error:
        if (eos) {
          stream.Position = resume_pos;
        }
      }
      return processed;
    }

    public async Task ReadAsync(
      Stream stream,
      IRTMPContentSink sink,
      CancellationToken cancel_token)
    {
      int len = 0;
      var bin = new byte[13];
      try {
        len += await stream.ReadBytesAsync(bin, len, 13-len, cancel_token).ConfigureAwait(false);
      }
      catch (EndOfStreamException) {
        return;
      }
      var header = new FLVFileHeader(bin);
      if (!header.IsValid) throw new BadDataException();
      sink.OnFLVHeader(header);
      len = 0;

      bool eos = false;
      while (!eos) {
        try {
          len += await stream.ReadBytesAsync(bin, len, 11-len, cancel_token).ConfigureAwait(false);
          var read_valid = false;
          if (FLVTagHeader.TryCreate(bin, out var tagheader)) {
            var tag = await FLVTag.TryReadTagAsync(tagheader.Value, stream, cancel_token).ConfigureAwait(false);
            if (tag.IsValidFooter) {
              len = 0;
              read_valid = true;
              DispatchTag(tag, sink);
            }
          }
          else {
            len += await stream.ReadBytesAsync(bin, len, 13-len, cancel_token).ConfigureAwait(false);
            var fileheader = new FLVFileHeader(bin);
            if (fileheader.IsValid) {
              read_valid = true;
              sink.OnFLVHeader(fileheader);
            }
          }
          if (!read_valid) {
            int pos = 1;
            for (; pos<len; pos++) {
              var b = bin[pos];
              if ((b & 0xC0)==0 && ((b & 0x1F)==8 || (b & 0x1F)==9 || (b & 0x1F)==18)) {
                break;
              }
            }
            if (pos==len) {
              len = 0;
            }
            else {
              Array.Copy(bin, pos, bin, 0, len-pos);
              len -= pos;
            }
          }
        }
        catch (EndOfStreamException) {
          eos = true;
        }
      }

    }

  }

}
