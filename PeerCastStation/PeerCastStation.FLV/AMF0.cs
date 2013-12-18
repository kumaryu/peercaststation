
namespace PeerCastStation.FLV
{
  public enum AMF0Marker
  {
    Number        = 0x00,
    Boolean       = 0x01,
    String        = 0x02,
    Object        = 0x03,
    MovieClip     = 0x04,
    Null          = 0x05,
    Undefined     = 0x06,
    Reference     = 0x07,
    Array         = 0x08,
    ObjectEnd     = 0x09,
    StrictArray   = 0x0A,
    Date          = 0x0B,
    LongString    = 0x0C,
    Unsupported   = 0x0D,
    RecordSet     = 0x0E,
    XMLDocument   = 0x0F,
    TypedObject   = 0x10,
    AVMPlusObject = 0x11,
  }

}
