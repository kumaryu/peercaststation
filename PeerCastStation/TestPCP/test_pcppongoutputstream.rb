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
require 'pcp'
require 'timeout'
require 'test_pcp_common'

module TestPCP
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
      accepts =
        PeerCastStation::Core::OutputStreamType.metadata |
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.interface
      @peercast.start_listen(@endpoint, accepts, accepts)
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
      accepts =
        PeerCastStation::Core::OutputStreamType.metadata |
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.interface
      @peercast.start_listen(@endpoint, accepts, accepts)
      @pipe = PipeStream.new
      @input  = @pipe.input
      @output = @pipe.output
      @header = ["pcp\n", 4, 1].pack('Z4VV')
    end
    
    def teardown
      @peercast.stop if @peercast
      @pipe.close if @pipe
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
      stopped = false
      stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, @endpoint, @header)
      stream.stopped { stopped = true }
      stream.start
      helo = PCP::Atom.new(PCP::HELO, [], nil)
      helo[PCP::HELO_VERSION] = 1218
      helo.write(@pipe)
      oleh = PCP::Atom.read(@pipe)
      assert_equal(PCP::OLEH, oleh.name)
      assert_equal(oleh[PCP::HELO_SESSIONID].to_s, @peercast.SessionID.to_s)
      assert(stream.is_stopped)
      timeout(10) { sleep 0.1 until stopped }
    end

    def test_helo
      stopped = false
      stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, @endpoint, @header)
      stream.stopped { stopped = true }
      stream.start
      helo = PCP::Atom.new(PCP::HELO, [], nil)
      helo[PCP::HELO_SESSIONID] = PCP::GID.generate
      helo.write(@pipe)
      oleh = PCP::Atom.read(@pipe)
      assert_equal(PCP::OLEH, oleh.name)
      assert_equal(oleh[PCP::HELO_SESSIONID].to_s, @peercast.SessionID.to_s)
      assert(stream.is_stopped)
      timeout(10) { sleep 0.1 until stopped }
    end

    def test_quit
      stopped = false
      stream = PCSPCP::PCPPongOutputStream.new(@peercast, @input, @output, @endpoint, @header)
      stream.stopped { stopped = true }
      stream.start
      quit = PCP::Atom.new(PCP::QUIT, nil, nil)
      quit.value = PCP::ERROR_QUIT
      quit.write(@pipe)
      sleep(0.1)
      assert(stream.is_stopped)
      timeout(10) { sleep 0.1 until stopped }
    end
  end
end

