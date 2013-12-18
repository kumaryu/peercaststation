
namespace PeerCastStation.FLV
{
  public enum AMF3Marker {
    Undefined   = 0x00,
    Null        = 0x01,
    False       = 0x02,
    True        = 0x03,
    Integer     = 0x04,
    Double      = 0x05,
    String      = 0x06,
    XMLDocument = 0x07,
    Date        = 0x08,
    Array       = 0x09,
    Object      = 0x0A,
    XML         = 0x0B,
    ByteArray   = 0x0C,
  }
}
