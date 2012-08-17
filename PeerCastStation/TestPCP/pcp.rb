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

module PCP
  HELO           = "helo"
  HELO_AGENT     = "agnt"
  HELO_OSTYPE    = "ostp"
  HELO_SESSIONID = "sid"
  HELO_PORT      = "port"
  HELO_PING      = "ping"
  HELO_PONG      = "pong"
  HELO_REMOTEIP  = "rip"
  HELO_VERSION   = "ver"
  HELO_BCID      = "bcid"
  HELO_DISABLE   = "dis"
  OLEH           = "oleh"
  OK             = "ok"

  CHAN          = "chan"
  CHAN_ID       = "id"
  CHAN_BCID     = "bcid"
  CHAN_PKT      = "pkt"
  CHAN_PKT_TYPE = "type"
  CHAN_PKT_HEAD = "head"
  CHAN_PKT_META = "meta"
  CHAN_PKT_POS  = "pos"
  CHAN_PKT_DATA = "data"
  CHAN_INFO          = "info"
  CHAN_INFO_TYPE     = "type"
  CHAN_INFO_BITRATE  = "bitr"
  CHAN_INFO_GENRE    = "gnre"
  CHAN_INFO_NAME     = "name"
  CHAN_INFO_URL      = "url"
  CHAN_INFO_DESC     = "desc"
  CHAN_INFO_COMMENT  = "cmnt"
  CHAN_INFO_PPFLAGS  = "pflg"
  CHAN_TRACK         = "trck"
  CHAN_TRACK_TITLE   = "titl"
  CHAN_TRACK_CREATOR = "crea"
  CHAN_TRACK_URL     = "url"
  CHAN_TRACK_ALBUM   = "albm"

  BCST       = "bcst"
  BCST_TTL   = "ttl"
  BCST_HOPS  = "hops"
  BCST_FROM  = "from"
  BCST_DEST  = "dest"
  BCST_GROUP = "grp"
  BCST_GROUP_ALL = 0xff
  BCST_GROUP_ROOT = 1
  BCST_GROUP_TRACKERS = 2
  BCST_GROUP_RELAYS = 4
  BCST_CHANID  = "cid"
  BCST_VERSION = "vers"
  BCST_VERSION_VP = "vrvp"
  BCST_VERSION_EX_PREFIX = "vexp"
  BCST_VERSION_EX_NUMBER = "vexn"
  HOST         = "host"
  HOST_ID      = "id"
  HOST_IP      = "ip"
  HOST_PORT    = "port"
  HOST_CHANID  = "cid"
  HOST_NUML    = "numl"
  HOST_NUMR    = "numr"
  HOST_UPTIME  = "uptm"
  HOST_TRACKER = "trkr"
  HOST_VERSION = "ver"
  HOST_VERSION_VP = "vevp"
  HOST_VERSION_EX_PREFIX = "vexp"
  HOST_VERSION_EX_NUMBER = "vexn"
  HOST_CLAP_PP = "clap"
  HOST_OLDPOS  = "oldp"
  HOST_NEWPOS  = "newp"
  HOST_FLAGS1  = "flg1"
  HOST_FLAGS1_TRACKER = 0x01
  HOST_FLAGS1_RELAY   = 0x02
  HOST_FLAGS1_DIRECT  = 0x04
  HOST_FLAGS1_PUSH    = 0x08
  HOST_FLAGS1_RECV    = 0x10
  HOST_FLAGS1_CIN     = 0x20
  HOST_FLAGS1_PRIVATE = 0x40
  HOST_UPHOST_IP   = "upip"
  HOST_UPHOST_PORT = "uppt"
  HOST_UPHOST_HOPS = "uphp"

  ROOT          = "root"
  ROOT_UPDINT   = "uint"
  ROOT_CHECKVER	= "chkv"
  ROOT_URL      = "url"
  ROOT_UPDATE   = "upd"
  ROOT_NEXT     = "next"

  QUIT = "quit"
  ERROR_QUIT    = 1000
  ERROR_BCST    = 2000
  ERROR_READ    = 3000
  ERROR_WRITE   = 4000
  ERROR_GENERAL = 5000

  ERROR_SKIP             = 1
  ERROR_ALREADYCONNECTED = 2
  ERROR_UNAVAILABLE      = 3
  ERROR_LOOPBACK         = 4
  ERROR_NOTIDENTIFIED    = 5
  ERROR_BADRESPONSE      = 6
  ERROR_BADAGENT         = 7
  ERROR_OFFAIR           = 8
  ERROR_SHUTDOWN         = 9
  ERROR_NOROOT           = 10
  ERROR_BANNED           = 11

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

  Atom = Struct.new(:name, :children, :content) do
    PacketType = {
      PCP::HELO               => :parent,
      PCP::OLEH               => :parent,
      PCP::CHAN               => :parent,
      PCP::CHAN_PKT           => :parent,
      PCP::CHAN_INFO          => :parent,
      PCP::CHAN_TRACK         => :parent,
      PCP::BCST               => :parent,
      PCP::HOST               => :parent,
      PCP::HELO_AGENT         => :string,
      PCP::HELO_SESSIONID     => :gid,
      PCP::HELO_PORT          => :short,
      PCP::HELO_PING          => :short,
      PCP::HELO_REMOTEIP      => :ip,
      PCP::HELO_VERSION       => :int,
      PCP::HELO_BCID          => :gid,
      PCP::HELO_DISABLE       => :int,
      PCP::OK                 => :int,
      PCP::CHAN_ID            => :gid,
      PCP::CHAN_BCID          => :gid,
      PCP::CHAN_PKT_TYPE      => :bytes,
      PCP::CHAN_PKT_POS       => :int,
      PCP::CHAN_PKT_DATA      => :bytes,
      PCP::CHAN_INFO_TYPE     => :bytes,
      PCP::CHAN_INFO_BITRATE  => :int,
      PCP::CHAN_INFO_GENRE    => :string,
      PCP::CHAN_INFO_NAME     => :string,
      PCP::CHAN_INFO_URL      => :string,
      PCP::CHAN_INFO_DESC     => :string,
      PCP::CHAN_INFO_COMMENT  => :string,
      PCP::CHAN_INFO_PPFLAGS  => :int,
      PCP::CHAN_TRACK_TITLE   => :string,
      PCP::CHAN_TRACK_CREATOR => :string,
      PCP::CHAN_TRACK_URL     => :string,
      PCP::CHAN_TRACK_ALBUM   => :string,
      PCP::BCST_TTL           => :byte,
      PCP::BCST_HOPS          => :byte,
      PCP::BCST_FROM          => :gid,
      PCP::BCST_DEST          => :gid,
      PCP::BCST_GROUP         => :byte,
      PCP::BCST_CHANID        => :gid,
      PCP::BCST_VERSION       => :int,
      PCP::BCST_VERSION_VP    => :int,
      PCP::HOST_ID            => :gid,
      PCP::HOST_IP            => :ip,
      PCP::HOST_PORT          => :short,
      PCP::HOST_CHANID        => :gid,
      PCP::HOST_NUML          => :int,
      PCP::HOST_NUMR          => :int,
      PCP::HOST_UPTIME        => :int,
      PCP::HOST_VERSION       => :int,
      PCP::HOST_VERSION_VP    => :int,
      PCP::HOST_CLAP_PP       => :int,
      PCP::HOST_OLDPOS        => :int,
      PCP::HOST_NEWPOS        => :int,
      PCP::HOST_FLAGS1        => :byte,
      PCP::HOST_UPHOST_IP     => :ip,
      PCP::HOST_UPHOST_PORT   => :int,
      PCP::HOST_UPHOST_HOPS   => :int,
      PCP::QUIT               => :int,
      PCP::ROOT               => :parent,
      PCP::ROOT_UPDINT        => :int,
      PCP::ROOT_NEXT          => :int,
      PCP::ROOT_CHECKVER      => :int,
      PCP::ROOT_URL           => :string,
      PCP::BCST_VERSION_EX_PREFIX => :bytes,
      PCP::BCST_VERSION_EX_NUMBER => :short,
      PCP::HOST_VERSION_EX_PREFIX => :bytes,
      PCP::HOST_VERSION_EX_NUMBER => :short,
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
      atom = Atom.new(name)
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
        stream.flush
      else
        stream.write([self.name, self.content.bytesize].pack('Z4V'))
        stream.write(self.content)
        stream.flush
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
          Atom.new(cmd, Array.new(children) { self.read }, nil)
        else
          Atom.new(cmd, [], @stream.read(len))
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
end

