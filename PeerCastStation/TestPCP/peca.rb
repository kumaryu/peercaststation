
require 'socket'
require 'stringio'

class GID
  def initialize(raw)
    @id = raw
  end
  attr_reader :id

  def to_s
    @id.unpack('C*').collect {|v| '%02x' % v }.join
  end

  def self.from_string(str)
    self.new(str.chars.each_slice(2).collect {|v| v.join.to_i(16) }.pack('C*'))
  end

  def self.generate
    self.new((Array.new(16) { rand(256) }).pack('C*'))
  end

  def hash
    @id.hash
  end

  def eql?(x)
    @id.eql?(x.id)
  end
end

PCPAtom = Struct.new(:command, :children, :content)
class AtomStream
  def initialize(stream)
    @stream = stream
    @count = 0
  end
  attr_reader :stream, :count

  def read
    buf = @stream.read(8)
    if buf then
      cmd, len = buf.unpack('Z4V')
      if (len & 0x80000000)!=0 then
        children = len & 0x7FFFFFFF
        PCPAtom.new(cmd, Array.new(children) { self.read }, nil)
      else
        PCPAtom.new(cmd, [], @stream.read(len))
      end
    else
      nil
    end
  end

  def write_int(name, value)
    @count += 1
    @stream.write([name].pack('Z4') + [4, value].pack('VV'))
    @stream.flush
  end

  def write_byte(name, value)
    @count += 1
    @stream.write([name].pack('Z4') + [1, value].pack('VC'))
    @stream.flush
  end

  def write_short(name, value)
    @count += 1
    @stream.write([name].pack('Z4') + [2, value].pack('Vv'))
    @stream.flush
  end

  def write_bytes(name, value)
    @count += 1
    @stream.write([name].pack('Z4') + [value.bytesize].pack('V') + value)
    @stream.flush
  end
  
  def write_string(name, value)
    @count += 1
    @stream.write([name].pack('Z4') + [value.bytesize+1].pack('V') + value + "\0")
    @stream.flush
  end

  def write_parent(name, &block)
    @count += 1
    substream = AtomStream.new(StringIO.new)
    block.call(substream)
    @stream.write([name].pack('Z4') + [0x80000000 | substream.count].pack('V'))
    @stream.write(substream.stream.string)
    @stream.flush
  end
  

  def close
    @stream.close
  end
end

PCP_HELO           = "helo"
PCP_HELO_AGENT     = "agnt"
PCP_HELO_OSTYPE    = "ostp"
PCP_HELO_SESSIONID = "sid"
PCP_HELO_PORT      = "port"
PCP_HELO_PING      = "ping"
PCP_HELO_PONG      = "pong"
PCP_HELO_REMOTEIP  = "rip"
PCP_HELO_VERSION   = "ver"
PCP_HELO_BCID      = "bcid"
PCP_HELO_DISABLE   = "dis"
PCP_OLEH           = "oleh"
PCP_OK             = "ok"

PCP_CHAN          = "chan"
PCP_CHAN_ID       = "id"
PCP_CHAN_BCID     = "bcid"
PCP_CHAN_PKT      = "pkt"
PCP_CHAN_PKT_TYPE = "type"
PCP_CHAN_PKT_HEAD = "head"
PCP_CHAN_PKT_META = "meta"
PCP_CHAN_PKT_POS  = "pos"
PCP_CHAN_PKT_DATA = "data"
PCP_CHAN_INFO          = "info"
PCP_CHAN_INFO_TYPE     = "type"
PCP_CHAN_INFO_BITRATE  = "bitr"
PCP_CHAN_INFO_GENRE    = "gnre"
PCP_CHAN_INFO_NAME     = "name"
PCP_CHAN_INFO_URL      = "url"
PCP_CHAN_INFO_DESC     = "desc"
PCP_CHAN_INFO_COMMENT  = "cmnt"
PCP_CHAN_INFO_PPFLAGS  = "pflg"
PCP_CHAN_TRACK         = "trck"
PCP_CHAN_TRACK_TITLE   = "titl"
PCP_CHAN_TRACK_CREATOR = "crea"
PCP_CHAN_TRACK_URL     = "url"
PCP_CHAN_TRACK_ALBUM   = "albm"

PCP_BCST       = "bcst"
PCP_BCST_TTL   = "ttl"
PCP_BCST_HOPS  = "hops"
PCP_BCST_FROM  = "from"
PCP_BCST_DEST  = "dest"
PCP_BCST_GROUP = "grp"
PCP_BCST_GROUP_ALL = 0xff
PCP_BCST_GROUP_ROOT = 1
PCP_BCST_GROUP_TRACKERS = 2
PCP_BCST_GROUP_RELAYS = 4
PCP_BCST_CHANID  = "cid"
PCP_BCST_VERSION = "vers"
PCP_BCST_VERSION_VP = "vrvp"
PCP_BCST_VERSION_EX_PREFIX = "vexp"
PCP_BCST_VERSION_EX_NUMBER = "vexn"
PCP_HOST         = "host"
PCP_HOST_ID      = "id"
PCP_HOST_IP      = "ip"
PCP_HOST_PORT    = "port"
PCP_HOST_CHANID  = "cid"
PCP_HOST_NUML    = "numl"
PCP_HOST_NUMR    = "numr"
PCP_HOST_UPTIME  = "uptm"
PCP_HOST_TRACKER = "trkr"
PCP_HOST_VERSION = "ver"
PCP_HOST_VERSION_VP = "vevp"
PCP_HOST_VERSION_EX_PREFIX = "vexp"
PCP_HOST_VERSION_EX_NUMBER = "vexn"
PCP_HOST_CLAP_PP = "clap"
PCP_HOST_OLDPOS  = "oldp"
PCP_HOST_NEWPOS  = "newp"
PCP_HOST_FLAGS1  = "flg1"
PCP_HOST_FLAGS1_TRACKER = 0x01
PCP_HOST_FLAGS1_RELAY   = 0x02
PCP_HOST_FLAGS1_DIRECT  = 0x04
PCP_HOST_FLAGS1_PUSH    = 0x08
PCP_HOST_FLAGS1_RECV    = 0x10
PCP_HOST_FLAGS1_CIN     = 0x20
PCP_HOST_FLAGS1_PRIVATE = 0x40
PCP_HOST_UPHOST_IP   = "upip"
PCP_HOST_UPHOST_PORT = "uppt"
PCP_HOST_UPHOST_HOPS = "uphp"

PCP_QUIT = "quit"
PCP_ERROR_QUIT    = 1000
PCP_ERROR_BCST    = 2000
PCP_ERROR_READ    = 3000
PCP_ERROR_WRITE   = 4000
PCP_ERROR_GENERAL = 5000

PCP_ERROR_SKIP             = 1
PCP_ERROR_ALREADYCONNECTED = 2
PCP_ERROR_UNAVAILABLE      = 3
PCP_ERROR_LOOPBACK         = 4
PCP_ERROR_NOTIDENTIFIED    = 5
PCP_ERROR_BADRESPONSE      = 6
PCP_ERROR_BADAGENT         = 7
PCP_ERROR_OFFAIR           = 8
PCP_ERROR_SHUTDOWN         = 9
PCP_ERROR_NOROOT           = 10
PCP_ERROR_BANNED           = 11
