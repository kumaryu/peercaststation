using System;
using System.Collections.Generic;
using System.Linq;
using PeerCastStation.FLV.AMF;

namespace PeerCastStation.FLV.RTMP
{
  /// <summary>
  /// Enhanced RTMP の connect 能力交渉に関する純粋なヘルパ。
  /// </summary>
  /// <remarks>
  /// 副作用を持たないため単体テスト可能(internal、テストへは InternalsVisibleTo で公開)。
  /// </remarks>
  internal static class EnhancedRTMP
  {
    /// <summary>
    /// サーバがサポートを広告する映像コーデックの FourCC。
    /// </summary>
    /// <remarks>
    /// 当面の現実解は AV1/HEVC + AAC で、後方互換として AVC(H.264) も広告する。
    /// </remarks>
    public static readonly string[] SupportedVideoFourCc = { "av01", "hvc1", "avc1" };

    /// <summary>
    /// connect 応答に載せる拡張能力ビットマスク。
    /// reconnect/multitrack/modEx/nano-timestamp はいずれも未対応なので 0。
    /// フィールド自体を返すことで「Enhanced RTMP を理解するサーバ」であることを示す。
    /// </summary>
    /// <remarks>
    /// Enhanced RTMPに対応する機能が実装された場合、この定数を変更する必要がある。
    /// </remarks>
    public const int CapsEx = 0;

    /// <summary>
    /// connect の CommandObject からクライアントが扱える映像コーデックの一覧を抽出する。
    /// Enhanced RTMP v1 の <c>fourCcList</c>(StrictArray/ECMAArray)を優先し、
    /// 無ければ v2 の <c>videoFourCcInfoMap</c>(キーが FourCC のマップ)のキーを使う。
    /// どちらも無ければ空配列を返す。
    /// </summary>
    public static IReadOnlyList<string> ParseClientFourCcList(AMFValue commandObject)
    {
      if (commandObject==null) return Array.Empty<string>();

      if (commandObject.ContainsKey("fourCcList")) {
        var list = ReadStringList(commandObject["fourCcList"]);
        if (list.Count>0) return list;
      }

      if (commandObject.ContainsKey("videoFourCcInfoMap")) {
        var keys = ReadMapKeys(commandObject["videoFourCcInfoMap"]);
        if (keys.Count>0) return keys;
      }

      return Array.Empty<string>();
    }

    /// <summary>
    /// クライアントの FourCC 一覧とサーバのサポート一覧の交差を、
    /// <see cref="SupportedVideoFourCc"/> の順で返す。
    /// クライアントが何も指定しなかった(旧 OBS / 非拡張)場合はサポート一覧をそのまま広告する。
    /// </summary>
    public static string[] NegotiateVideoFourCc(IReadOnlyList<string> clientList)
    {
      if (clientList==null || clientList.Count==0) {
        return (string[])SupportedVideoFourCc.Clone();
      }
      var requested = new HashSet<string>(clientList, StringComparer.OrdinalIgnoreCase);
      return SupportedVideoFourCc.Where(fourcc => requested.Contains(fourcc)).ToArray();
    }

    private static IReadOnlyList<string> ReadStringList(AMFValue value)
    {
      if (value==null) return Array.Empty<string>();
      switch (value.Type) {
      case AMFValueType.StrictArray:
        return ((AMFValue[])value)
          .Select(v => (string?)v)
          .Where(s => !String.IsNullOrEmpty(s))
          .Select(s => s!)
          .ToArray();
      case AMFValueType.ECMAArray:
        return ReadMapValues(value);
      default:
        return Array.Empty<string>();
      }
    }

    private static IReadOnlyList<string> ReadMapValues(AMFValue value)
    {
      if (value.Value is IDictionary<string,AMFValue> dic) {
        return dic.Values
          .Select(v => (string?)v)
          .Where(s => !String.IsNullOrEmpty(s))
          .Select(s => s!)
          .ToArray();
      }
      return Array.Empty<string>();
    }

    private static IReadOnlyList<string> ReadMapKeys(AMFValue value)
    {
      if (value==null) return Array.Empty<string>();
      switch (value.Type) {
      case AMFValueType.ECMAArray:
        if (value.Value is IDictionary<string,AMFValue> dic) {
          return dic.Keys.ToArray();
        }
        return Array.Empty<string>();
      case AMFValueType.Object:
        return ((AMFObject)value).Select(kv => kv.Key).ToArray();
      default:
        return Array.Empty<string>();
      }
    }
  }
}
