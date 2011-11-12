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
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.PCP', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.PCP.dll'
require 'test/unit'
require 'peca'
require 'utils'
using_clr_extensions PeerCastStation::Core
explicit_extensions PeerCastStation::Core::AtomCollectionExtensions

PCSCore = PeerCastStation::Core unless defined?(PCSCore)
PCSPCP  = PeerCastStation::PCP  unless defined?(PCSPCP)

class TC_PCPPongOutputStreamFactory < Test::Unit::TestCase
  def to_byte_array(array)
    array = array.to_a
    res = System::Array[System::Byte].new(array.size)
    array.each_with_index do |v, i|
      res[i] = v
    end
    res
  end

  def setup
    @endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @channel_id = System::Guid.empty
    @channel  = nil
  end
  
  def teardown
    @peercast.stop if @peercast
  end
  
  def test_construct
    factory = PCSPCP::PCPPongOutputStreamFactory.new(@peercast)
    assert_equal(factory.Name, 'PCPPong')
    assert(factory.respond_to?(:create_obj_ref))
  end

  def test_parse_channel_id
    factory = PCSPCP::PCPPongOutputStreamFactory.new(@peercast)
    assert_nil(factory.ParseChannelID(<<EOS))
GET /channel/531DC8DFC7FB42928AC2C0A626517A87 HTTP/1.1\r
x-peercast-pcp:1\r
x-peercast-pos: 200000000\r
User-Agent: PeerCastStation/1.0\r
\r
EOS
    assert_equal(System::Guid.empty, factory.ParseChannelID(["pcp\n", 4, 1].pack('Z4VV')))
    assert_nil(factory.ParseChannelID(["pcp\n", 4, 0].pack('Z4VV')))
  end

  def test_create
    factory = PCSPCP::PCPPongOutputStreamFactory.new(@peercast)
    s = System::IO::MemoryStream.new
    output_stream = factory.create(s, s, @endpoint, @channel_id, to_byte_array([]))
    assert_kind_of(PCSPCP::PCPPongOutputStream, output_stream)
  end
end

class TC_PCPPongOutputStream < Test::Unit::TestCase
  def to_byte_array(array)
    array = array.to_a
    res = System::Array[System::Byte].new(array.size)
    array.each_with_index do |v, i|
      res[i] = v
    end
    res
  end

  def setup
    @endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7147)
    @peercast = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(@endpoint)
    @pipe = PipeStream.new
    @input  = @pipe.input
    @output = @pipe.output
    @header = ["pcp\n", 4, 1].pack('Z4VV')
  end
  
  def teardown
    @peercast.stop if @peercast
  end
  
  def test_construct
    stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, @endpoint, @header)
    assert_equal(@peercast,stream.PeerCast)
    assert_equal(@input,   stream.InputStream)
    assert_equal(@output,  stream.OutputStream)
    assert(!stream.is_stopped)
    assert_equal(PCSCore::OutputStreamType.metadata, stream.output_stream_type)
    assert(stream.respond_to?(:create_obj_ref))
  end

  def test_is_local
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.2'), 7144)
    stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, endpoint, @header)
    assert(stream.is_local)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, endpoint, @header)
    assert(!stream.is_local)
  end
  
  def test_upstream_rate
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.2'), 7144)
    stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, endpoint, @header)
    assert_equal(0, stream.upstream_rate)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, endpoint, @header)
    assert_equal(0, stream.upstream_rate)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, endpoint, @header)
    assert_equal(0, stream.upstream_rate)
  end

  def test_helo_no_session_id
    stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, @endpoint, @header)
    stream.start
    helo = PCPAtom.new(PCP_HELO, [], nil)
    helo[PCP_HELO_VERSION] = 1218
    helo.write(@pipe)
    oleh = PCPAtom.read(@pipe)
    assert_equal(PCP_OLEH, oleh.name)
    assert_equal(oleh[PCP_HELO_SESSIONID].to_s, @peercast.SessionID.to_s)
    assert(stream.is_stopped)
  end

  def test_helo
    stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, @endpoint, @header)
    stream.start
    helo = PCPAtom.new(PCP_HELO, [], nil)
    helo[PCP_HELO_SESSIONID] = GID.generate
    helo.write(@pipe)
    oleh = PCPAtom.read(@pipe)
    assert_equal(PCP_OLEH, oleh.name)
    assert_equal(oleh[PCP_HELO_SESSIONID].to_s, @peercast.SessionID.to_s)
    assert(stream.is_stopped)
  end

  def test_quit
    stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, @endpoint, @header)
    stream.start
    quit = PCPAtom.new(PCP_QUIT, nil, nil)
    quit.value = PCP_ERROR_QUIT
    quit.write(@pipe)
    sleep(0.1)
    assert(stream.is_stopped)
  end
end

