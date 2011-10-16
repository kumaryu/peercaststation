# PeerCastStation, a P2P streaming servent.
# Copyright (C) 2011 Ryuichi Sakamoto (kumaryu@kumaryu.net)
# 
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
# 
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
# 
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <http://www.gnu.org/licenses/>.

require 'socket'
require 'stringio'

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

PCP_ROOT          = "root"
PCP_ROOT_UPDINT   = "uint"
PCP_ROOT_CHECKVER	= "chkv"
PCP_ROOT_URL      = "url"
PCP_ROOT_UPDATE   = "upd"
PCP_ROOT_NEXT     = "next"

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

class GID
  def initialize(raw)
    @id = raw
  end
  attr_reader :id

  def to_s
    @id.unpack('C*').collect {|v| '%02x' % v }.join.downcase
  end

  def self.from_string(str)
    self.new(str.chars.each_slice(2).collect {|v| v.join.to_i(16) }.pack('C*'))
  end

  def self.generate
    self.new((Array.new(16) { rand(256) }).pack('C*'))
  end

  def ==(x)
    @id.eql?(x.id)
  end

  def hash
    @id.hash
  end

  def eql?(x)
    @id.eql?(x.id)
  end
end

PCPAtom = Struct.new(:name, :children, :content) do
  PacketType = {
    PCP_HELO               => :parent,
    PCP_OLEH               => :parent,
    PCP_CHAN               => :parent,
    PCP_CHAN_PKT           => :parent,
    PCP_CHAN_INFO          => :parent,
    PCP_CHAN_TRACK         => :parent,
    PCP_BCST               => :parent,
    PCP_HOST               => :parent,
    PCP_HELO_AGENT         => :string,
    PCP_HELO_SESSIONID     => :gid,
    PCP_HELO_PORT          => :short,
    PCP_HELO_PING          => :short,
    PCP_HELO_REMOTEIP      => :ip,
    PCP_HELO_VERSION       => :int,
    PCP_HELO_BCID          => :gid,
    PCP_HELO_DISABLE       => :int,
    PCP_OK                 => :int,
    PCP_CHAN_ID            => :gid,
    PCP_CHAN_BCID          => :gid,
    PCP_CHAN_PKT_TYPE      => :bytes,
    PCP_CHAN_PKT_POS       => :int,
    PCP_CHAN_PKT_DATA      => :bytes,
    PCP_CHAN_INFO_TYPE     => :bytes,
    PCP_CHAN_INFO_BITRATE  => :int,
    PCP_CHAN_INFO_GENRE    => :string,
    PCP_CHAN_INFO_NAME     => :string,
    PCP_CHAN_INFO_URL      => :string,
    PCP_CHAN_INFO_DESC     => :string,
    PCP_CHAN_INFO_COMMENT  => :string,
    PCP_CHAN_INFO_PPFLAGS  => :int,
    PCP_CHAN_TRACK_TITLE   => :string,
    PCP_CHAN_TRACK_CREATOR => :string,
    PCP_CHAN_TRACK_URL     => :string,
    PCP_CHAN_TRACK_ALBUM   => :string,
    PCP_BCST_TTL           => :byte,
    PCP_BCST_HOPS          => :byte,
    PCP_BCST_FROM          => :gid,
    PCP_BCST_DEST          => :gid,
    PCP_BCST_GROUP         => :byte,
    PCP_BCST_CHANID        => :gid,
    PCP_BCST_VERSION       => :int,
    PCP_BCST_VERSION_VP    => :int,
    PCP_HOST_ID            => :gid,
    PCP_HOST_IP            => :ip,
    PCP_HOST_PORT          => :short,
    PCP_HOST_CHANID        => :gid,
    PCP_HOST_NUML          => :int,
    PCP_HOST_NUMR          => :int,
    PCP_HOST_UPTIME        => :int,
    PCP_HOST_VERSION       => :int,
    PCP_HOST_VERSION_VP    => :int,
    PCP_HOST_CLAP_PP       => :int,
    PCP_HOST_OLDPOS        => :int,
    PCP_HOST_NEWPOS        => :int,
    PCP_HOST_FLAGS1        => :byte,
    PCP_HOST_UPHOST_IP     => :ip,
    PCP_HOST_UPHOST_PORT   => :int,
    PCP_HOST_UPHOST_HOPS   => :int,
    PCP_QUIT               => :int,
    PCP_ROOT               => :parent,
    PCP_ROOT_UPDINT        => :int,
    PCP_ROOT_NEXT          => :int,
    PCP_ROOT_CHECKVER      => :int,
    PCP_ROOT_URL           => :string,
    PCP_BCST_VERSION_EX_PREFIX => :bytes,
    PCP_BCST_VERSION_EX_NUMBER => :short,
    PCP_HOST_VERSION_EX_PREFIX => :bytes,
    PCP_HOST_VERSION_EX_NUMBER => :short,
  }

  def value
    type = PacketType[self.name]
    case type
    when nil
      self.children ? self : self.content
    when :parent
      self
    when :byte
      raise RuntimeError, "Invalid content length #{self.content.size} for 1" if self.content.size!=1
      self.content.unpack('C')[0]
    when :gid
      raise RuntimeError, "Invalid content length #{self.content.size} for 16" if self.content.size!=16
      GID.new(self.content)
    when :int
      raise RuntimeError, "Invalid content length #{self.content.size} for 4" if self.content.size!=4
      self.content.unpack('V')[0]
    when :ip
      raise RuntimeError, "Invalid content length #{self.content.size} for 4" if self.content.size!=4
      self.content.unpack('C*').reverse
    when :short
      raise RuntimeError, "Invalid content length #{self.content.size} for 2" if self.content.size!=2
      self.content.unpack('v')[0]
    when :string
      raise RuntimeError, "String must ends with null byte" if self.content[self.content.size-1]!="\0"
      self.content[0, self.content.size-1]
    when :bytes
      self.content
    else
      raise RuntimeError, "Unknown type: #{type}"
    end
  end

  def value=(v)
    type = PacketType[self.name]
    case type
    when nil
      if v.kind_of?(Array) then
        self.children = v
      else
        self.content = v
      end
    when :parent
      self.children = v
    when :byte
      self.content = [v].pack('C')
    when :gid
      if v.kind_of?(GID) then
        self.content = v.id
      elsif v.kind_of?(System::Guid) then
        value_le = v.to_byte_array
        value_be = [
          value_le[3], value_le[2], value_le[1], value_le[0],
          value_le[5], value_le[4],
          value_le[7], value_le[6],
          value_le[8],
          value_le[9],
          value_le[10],
          value_le[11],
          value_le[12],
          value_le[13],
          value_le[14],
          value_le[15],
        ]
        self.content = value_be.pack('C*')
      else
        self.content = v
      end
    when :int
      self.content = [v].pack('V')
    when :ip
      if v.kind_of?(Array) then
        self.content = v.reverse.pack('C*')
      elsif v.kind_of?(String) then
        self.content = v.split('.').collect(&:to_i).reverse.pack('C*')
      elsif v.kind_of?(System::Net::IPAddress) then
        self.content = v.get_address_bytes.to_a.reverse.pack('C*')
      end
    when :short
      self.content = [v].pack('v')
    when :string
      self.content = v + "\0"
    when :bytes
      self.content = v
    else
      raise RuntimeError, "Unknown type: #{type}"
    end
  end

  def inspect
    value = self.value
    if value.equal?(self) then
      value = children.collect {|c| c.inspect.lines.collect {|line| '  '+line }.join("\n") }.join("\n")
      "atom #{self.name}: [\n#{value}\n]"
    else
      "atom #{self.name}: #{value.inspect}"
    end
  end

  def [](name)
    childen = self.children.select {|c| c.name==name }
    case childen.size
    when 0
      nil
    when 1
      childen.first.value
    else
      childen.collect {|c| c.value }
    end
  end

  def []=(name, value)
    self.children.delete_if {|c| c.name==name }
    atom = PCPAtom.new(name)
    atom.value = value
    self.children.push(atom)
    value
  end

  def update(atom)
    atom.children.each do |c|
      self[c.name] = c.value
    end
  end

  def write(stream)
    if self.children and not self.children.empty? then
      stream.write([self.name].pack('Z4') + [0x80000000 | self.children.count].pack('V'))
      self.children.each do |c|
        c.write(stream)
      end
    else
      stream.write([self.name, self.content.bytesize].pack('Z4V'))
      stream.write(self.content)
    end
  end

  def self.read_blocking(stream, sz)
    buf = stream.read(sz)
    if buf then
      while buf.bytesize<sz do
        res = stream.read(sz-buf.bytesize)
        break unless res
        buf << res
      end
    end
    buf
  end

  def self.read(stream)
    buf = self.read_blocking(stream, 8)
    if buf then
      cmd, len = buf.unpack('Z4V')
      if (len & 0x80000000)!=0 then
        children = len & 0x7FFFFFFF
        self.new(cmd, Array.new(children) { read(stream) }, nil)
      else
        self.new(cmd, nil, self.read_blocking(stream, len))
      end
    else
      nil
    end
  end
end

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

  def write(atom)
    if atom.children then
      @stream.write([atom.name].pack('Z4') + [0x80000000 | atom.children.count].pack('V'))
      atom.children.each do |c|
        write(c)
      end
    else
      @stream.write([atom.name, atom.content.size].pack('Z4V') + atom.content)
    end
  end

  def close
    @stream.close
  end
end

