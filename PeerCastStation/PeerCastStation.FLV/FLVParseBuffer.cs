// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 Philmist
using System;
using System.IO;
using PeerCastStation.FLV.RTMP;

namespace PeerCastStation.FLV
{
  /// <summary>
  /// 上流 Content を貯めて <see cref="FLVFileParser"/> に食わせ、消費済み分を捨てるバッファ。
  /// FLVToMPEG2TS / FLVToMKV の両コンテンツフィルタが同じ手順を必要とするため共有する。
  /// </summary>
  internal class FLVParseBuffer
  {
    private readonly FLVFileParser parser = new FLVFileParser();
    private MemoryStream buffer = new MemoryStream();

    /// <summary>受信データを追記し、読める分だけタグとして sink へ流す。</summary>
    public void Feed(ReadOnlySpan<byte> data, IRTMPContentSink sink)
    {
      var pos = buffer.Position;
      buffer.Seek(0, SeekOrigin.End);
      buffer.Write(data);
      buffer.Position = pos;
      parser.Read(buffer, sink);
      Trim();
    }

    /// <summary>
    /// パーサが消費した先頭部分を捨てる。
    /// 未消費分だけを先頭へ詰め直すので、コピー量はパケットごとのバッファ全体ではなく
    /// 「タグ途中で切れた端数」に比例する。
    /// </summary>
    private void Trim()
    {
      var consumed = buffer.Position;
      if (consumed==0) return;
      var remain = (int)(buffer.Length - consumed);
      if (remain>0) {
        // buffer は自前の new MemoryStream() なので内部配列は常に公開されている
        // (publiclyVisible=true)。GetBuffer() が失敗する経路は存在しない。
        var raw = buffer.GetBuffer();
        Array.Copy(raw, (int)consumed, raw, 0, remain);
      }
      buffer.SetLength(remain);
      buffer.Position = 0;
    }
  }

}
