using System;
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
    /// <summary>
    /// FLV ファイルヘッダのバイト数。
    /// 再同期時に「次の Feed でヘッダに育つかもしれない末尾の断片」を判断するためにも使う。
    /// </summary>
    private const int FLVFileHeaderSize = 13;

    /// <summary>
    /// タグ本体として受け入れる最大バイト数。
    /// </summary>
    /// <remarks>
    /// DataSize フィールドの上限(24bit=16MB)ではなく、
    /// 実際の配信で起こりうる大きさで頭打ちにして、破損した長さフィールドで
    /// パーサが延々とデータを待ち続けるのを防ぐ。
    /// 上限を下げすぎると、正当な巨大タグ(超高ビットレートのキーフレームや大きな
    /// スクリプトタグ)をヘッダ破損と誤判定して再同期スキャンに回し、そのタグを
    /// 落としたうえでパーサが同期を失う。この誤判定の窓を狭めるため、想定しうる
    /// 最大のタグより十分に大きく、かつフィールド上限(16MB)より下に置く。
    /// </remarks>
    private const int MaxTagDataSize = 12*1024*1024;

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
        if (type!=TagType.Audio && type!=TagType.Video && type!=TagType.Script) {
          header = null;
          return false;
        }
        // DataSize は24bitなので、破損した1バイトだけで最大16MBのタグ長になりうる。
        // その長さが揃うまでパーサはタグを読み出せず、待っている間バッファは伸び続ける
        // (2Mbps の配信なら数分ぶん貯め込んだうえ、その間は何も出力されない)。
        // 実在のタグは大きなキーフレームでも上限に遠く及ばないので、桁違いの長さは
        // タグ先頭ではないと判断して再同期スキャンに回す。
        if (((binary[1]<<16) | (binary[2]<<8) | binary[3])>MaxTagDataSize) {
          header = null;
          return false;
        }
        header = new FLVTagHeader(binary);
        return true;
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

      public static bool TryReadTag(FLVFileParser owner, FLVTagHeader header, Stream stream, [NotNullWhen(true)] out FLVTag? tag)
      {
        // 本体+フッターが揃っているかを、確保する前に長さで確かめる(FLVParseBuffer の
        // MemoryStream なので残量は既知)。揃わないまま new byte[DataSize] まで進むと、
        // 大きなタグが Feed をまたいで貯まる間、Feed のたびに全長ぶんの確保と部分コピーを
        // 行っては捨てることになる(破損 DataSize が上限近くだと 12MB の確保を繰り返す)。
        if (stream.CanSeek && stream.Length-stream.Position < header.DataSize+4L) {
          tag = null;
          return false;
        }
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
    /// </summary>
    /// <remarks>
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
    ///
    /// このメソッド自体も Read/ReadAsync のパース用 try の外から呼ぶ必要がある。
    /// 中から呼ぶと、下流の EndOfStreamException を catch がデータ待ちと誤認して
    /// タグ先頭へ巻き戻し、同じタグを永久に再パースし続けることになる。
    /// </remarks>
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
    /// <remarks>
    /// ここで拾うのは AMF の復号中に出たものだけで、sink の呼び出しは範囲外
    /// (DispatchTag の注記を参照)。メタデータの値を読む側は AMFValue.TryGetDouble 等の
    /// 投げない読み出しを使う約束にしてあるが、AMF0Reader が構造そのものを復号する
    /// 過程では依然これらの例外が出るため、型の列挙は残している。
    /// </remarks>
    private static bool IsBrokenTagException(Exception e)
    {
      return e is InvalidDataException      // AMF0Reader: 未知マーカー/不正な参照
          || e is BadDataException
          || e is ArgumentException         // Span.Slice 等の範囲外
          || e is IndexOutOfRangeException
          || e is OverflowException
          || e is FormatException
          || e is InvalidCastException;
    }

    /// <summary>
    /// 再同期スキャンの停止位置になりうるバイトか。
    /// タグヘッダの先頭(予約ビットが 0 で type が 8/9/18)に加えて、
    /// FLV ファイルヘッダの先頭('F')でも止まる。
    /// </summary>
    /// <remarks>
    /// 'F' を含めないと、ヘッダの途中で切れた入力を拾えない。"FLV" もヘッダ内の
    /// 0x01/0x05/0x00 もタグ候補バイトではないため、スキャンは末尾まで空振りして
    /// その範囲を走査済み(=消費してよい)と判断し、断片ごと捨ててしまう。
    /// OnFLVHeader は下流フィルタが状態をリセットする唯一の契機なので、取りこぼすと
    /// 古い avcC やヘッダ送信済みフラグを抱えたまま新しいストリームを処理することになる。
    /// 'F' で止まった後にヘッダ13バイトが揃っていなければ、続く読み出しがデータ不足で
    /// 抜けて 'F' の位置まで巻き戻るので、断片は次の Feed まで保持される。
    /// </remarks>
    private static bool IsResyncCandidate(int b)
    {
      if (b=='F') return true;
      return (b & 0xC0)==0 && ((b & 0x1F)==8 || (b & 0x1F)==9 || (b & 0x1F)==18);
    }

    /// <summary>
    /// FLV ファイルヘッダを sink へ配る。
    /// </summary>
    /// <remarks>
    /// 新しいストリームの開始なので、警告の抑止状態も
    /// ここで捨てる。リセットしないと、パーサはチャンネルと同寿命のため、最初の破損以降は
    /// 再起動後のストリームの破損まで Debug でしか記録されなくなる。
    /// </remarks>
    private void DispatchFLVHeader(FLVFileHeader header, IRTMPContentSink sink)
    {
      warnedBrokenTag = false;
      sink.OnFLVHeader(header);
    }

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
        // sink への配信はパース用の try の外で行う。下流(FLVContentBuffer / FLVToMKV /
        // FLVToMPEG2TS)が投げた EndOfStreamException をここの catch が拾うと、
        // 「データ待ち」と誤認してタグ先頭へ巻き戻す。FLVParseBuffer.Trim は consumed==0 で
        // 何も捨てないので、次の Feed が同じ毒タグを再配信して再び例外になり、
        // 出力が恒久停止したうえでバッファが配信レートのまま伸び続ける。
        // 下流は各自 TryParse と範囲チェックで破損入力を弾く責任を持ち、
        // それでも出る例外は本物のバグとして表に出す。
        FLVTag? pending_tag = null;
        FLVFileHeader? pending_header = null;
        try {
          switch (state) {
          case ReaderState.Header:
            {
              var bin = ReadBytes(stream, FLVFileHeaderSize, out eos);
              if (eos) goto error;
              var header = new FLVFileHeader(bin);
              if (header.IsValid) {
                pending_header = header;
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
                    pending_tag = tag;
                  }
                }
                else {
                  eos = true;
                  goto error;
                }
              }
              else {
                stream.Position = start_pos;
                var headerbin = ReadBytes(stream, FLVFileHeaderSize, out eos);
                if (eos) goto error;
                var fileheader = new FLVFileHeader(headerbin);
                if (fileheader.IsValid) {
                  read_valid = true;
                  pending_header = fileheader;
                }
              }
              if (!read_valid) {
                stream.Position = start_pos+1;
                var b = stream.ReadByte();
                while (true) {
                  if (b<0) {
                    // 走査した範囲に再同期候補は存在しないので、丸ごと消費済みとして捨てさせる。
                    // ヘッダ断片は IsResyncCandidate が 'F' で止まることで保持されるため、
                    // ここへ来た範囲に残す価値のあるバイトはない。
                    resume_pos = stream.Position;
                    eos = true;
                    goto error;
                  }
                  if (IsResyncCandidate(b)) {
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
        // ここから先は try の外。ストリーム位置は確定済みなので、下流が投げても
        // 巻き戻しは起きず、同じタグを再配信し続けることはない。
        if (pending_header!=null) {
          DispatchFLVHeader(pending_header, sink);
        }
        if (pending_tag!=null) {
          DispatchTag(pending_tag, sink);
        }
      }
      return processed;
    }

    /// <summary>
    /// 非同期ストリームを読み、チャンクごとに <see cref="Read"/> と同じ増分パーサへ流す。
    /// </summary>
    /// <remarks>
    /// 以前はタグの状態機械を非同期側にも別実装していたが、再同期の規則が同期側と
    /// 食い違っていた。非同期側はフッター不一致で破棄したタグの古いヘッダ11バイトしか
    /// 再走査せず(本体・フッターとして消費したバイトは再走査されない)、残骸のタグ候補
    /// バイトが先頭に残ったまま次のストリームバイトと連結されるため、非連続なキメラ
    /// ヘッダを TryCreate が受理して正常データを最大タグ長ぶん誤消費できた。
    /// パースを FLVParseBuffer 経由で Read に一本化し、実ストリームを1バイトずつ
    /// 再走査する同期側の再同期規則を非同期経路にも適用する。こちらは読み込みだけを
    /// 受け持ち、sink への配信(と毒タグ処理)も核の側の規則に従う。
    ///
    /// 先頭13バイトだけは厳格に検査する。ここが FLV ファイルヘッダでない入力は
    /// コンテンツタイプの誤判定なので、ゴミを再同期スキャンし続けるより
    /// BadDataException で呼び出し元へ伝えて早期に打ち切る。
    /// </remarks>
    public async Task ReadAsync(
      Stream stream,
      IRTMPContentSink sink,
      CancellationToken cancel_token)
    {
      var head = new byte[FLVFileHeaderSize];
      try {
        await stream.ReadBytesAsync(head, 0, head.Length, cancel_token).ConfigureAwait(false);
      }
      catch (EndOfStreamException) {
        return;
      }
      if (!new FLVFileHeader(head).IsValid) throw new BadDataException();
      var buffer = new FLVParseBuffer(this);
      // ヘッダ自身も核へ通す。OnFLVHeader の配信(と警告抑止状態のリセット)は核が行う。
      buffer.Feed(head, sink);
      var chunk = new byte[64*1024];
      while (true) {
        cancel_token.ThrowIfCancellationRequested();
        var len = await stream.ReadAsync(chunk, 0, chunk.Length, cancel_token).ConfigureAwait(false);
        if (len<=0) break;
        buffer.Feed(chunk.AsSpan(0, len), sink);
      }
    }

  }

}
