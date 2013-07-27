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
require File.join(File.dirname(__FILE__), '..', 'TestCommon', 'utils.rb')
TestUtils.require_peercaststation 'Core'
TestUtils.require_peercaststation 'HTTP'
TestUtils.explicit_extensions PeerCastStation::Core::AtomCollectionExtensions
require 'test/unit'
require 'socket'

module TestHTTP
  PCSCore = PeerCastStation::Core unless defined?(PCSCore)
  PCSHTTP = PeerCastStation::HTTP unless defined?(PCSHTTP)

  class TC_HTTPResponse < Test::Unit::TestCase
    def test_construct
      value = System::Array[System::String].new([
        'HTTP/1.1 200 OK',
        'Content-Type: application/octet-stream',
        'Server:hoge hoge',
      ])
      req = PCSHTTP::HTTPResponse.new(value)
      assert_equal('1.1', req.Version)
      assert_equal(200, req.Status)
      assert_equal('hoge hoge', req.Headers['SERVER'])
      assert_equal('application/octet-stream', req.Headers['CONTENT-TYPE'])
    end
  end

  class TC_HTTPResponseReader < Test::Unit::TestCase
    def test_read
      stream = System::IO::MemoryStream.new([
        "HTTP/1.1 200 OK\r\n",
        "Content-Type:application/octet-stream\r\n",
        "Server:hoge hoge\r\n",
        "\r\n"
      ].join)
      req = nil
      assert_nothing_raised {
        req = PCSHTTP::HTTPResponseReader.read(stream)
      }
      assert_kind_of(PCSHTTP::HTTPResponse, req)
      assert_equal('1.1', req.Version)
      assert_equal(200, req.Status)
      assert_equal('hoge hoge', req.Headers['SERVER'])
      assert_equal('application/octet-stream', req.Headers['CONTENT-TYPE'])
    end

    def test_read_failed
      stream = System::IO::MemoryStream.new([
        "HTTP/1.1 200 OK\r\n",
      ].join)
      assert_raise(System::IO::EndOfStreamException) {
        PCSHTTP::HTTPResponseReader.read(stream)
      }
    end
  end

  class TC_HTTPSourceStreamFactory < Test::Unit::TestCase
    def setup
      endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
      accepts =
        PeerCastStation::Core::OutputStreamType.metadata |
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.interface
      @peercast = PeerCastStation::Core::PeerCast.new
      @peercast.start_listen(endpoint, accepts, accepts)
    end

    def teardown
      @peercast.stop if @peercast
    end

    def test_construct
      factory = PCSHTTP::HTTPSourceStreamFactory.new(@peercast)
      assert_equal('http', factory.Name)
      assert_equal('http', factory.scheme)
    end

    def test_create_relay
      channel_id = System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string)
      channel = PCSCore::Channel.new(
        @peercast,
        channel_id,
        System::Uri.new('http://localhost:8888/'))
      factory = PCSHTTP::HTTPSourceStreamFactory.new(@peercast)
      assert_raise(System::NotImplementedException) do
        source = factory.create(channel, System::Uri.new('http://localhost:8888/'))
      end
    end

    def test_create_broadcast
      channel_id = System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string)
      channel = PCSCore::Channel.new(
        @peercast,
        channel_id,
        System::Uri.new('http://localhost:8888/'))
      factory = PCSHTTP::HTTPSourceStreamFactory.new(@peercast)
      reader_factory = PCSCore::RawContentReaderFactory.new
      reader = reader_factory.create(channel)
      source = factory.create(channel, System::Uri.new('http://localhost:8888/'), reader)
      assert_kind_of(PCSHTTP::HTTPSourceStream, source)
      assert_equal(@peercast, source.PeerCast)
      assert_equal(channel, source.channel)
      assert_equal(reader, source.content_reader)
    end
  end

  class TC_HTTPSourceStream < Test::Unit::TestCase
    class TestHTTPServer
      def read_http_header(io)
        header = io.read(4)
        until /\r\n\r\n$/=~header do
          header << io.read(1)
        end
        header
      end

      HTTPRequest = Struct.new(:method, :path, :version, :headers)
      def parse_http_request(header)
        lines = header.split(/\r\n/m)
        req = /(\S+)\s+(\S+)\s+HTTP\/(\S+)/.match(lines.shift)
        if req then
          headers = {}
          lines.each do |line|
            if /(.*?):(.*)/=~line then
              headers[$1.strip.upcase] = $2.strip
            end
          end
          HTTPRequest.new(req[1], req[2], req[3], headers)
        else
          nil
        end
      end

      def initialize(addr='127.0.0.1', port=8888)
        @clients = []
        @stopped = false
        @server = TCPServer.new(addr, port)
        @server_thread = Thread.new {
          begin
            until @stopped do
              @clients << Thread.new(@server.accept) do |sock|
                request = parse_http_request(read_http_header(sock))
                @handlers.each do |handler|
                  if handler[0].match(request.path) then
                    begin
                      handler[1].call(request, sock)
                    rescue System::Net::Sockets::SocketException
                    end
                  end
                end
                sock.close
              end
            end
          rescue System::Net::Sockets::SocketException
          rescue System::ObjectDisposedException
          end
        }
        @handlers = []
      end

      def on(path, &block)
        @handlers.push([path, block])
      end

      def stop
        @stopped = true
        @server.shutdown
        @server.close
        @server_thread.join
        @clients.each(&:join)
      end
    end

    def write_response(sock, req, status, headers={})
      msg = case status
            when 200; 'OK'
            when 404; 'Not found'
            when 503; 'Temporary Unavailable'
            else; ''
            end
      sock.write("HTTP/#{req.version} #{status} #{msg}\r\n")
      headers.each do |name, value|
        sock.write("#{name}:#{value}\r\n")
      end
      sock.write("\r\n")
    end

    def setup
      endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
      @peercast = PeerCastStation::Core::PeerCast.new
      accepts =
        PeerCastStation::Core::OutputStreamType.metadata |
        PeerCastStation::Core::OutputStreamType.play |
        PeerCastStation::Core::OutputStreamType.relay |
        PeerCastStation::Core::OutputStreamType.interface
      @peercast.start_listen(endpoint, accepts, accepts)
      @source_uri = System::Uri.new('http://127.0.0.1:8888/')
      @channel = PCSCore::Channel.new(
        @peercast,
        System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string), 
        @source_uri)
      reader_factory = PCSCore::RawContentReaderFactory.new
      @reader = reader_factory.create(@channel)
      @server = TestHTTPServer.new
    end

    def teardown
      @peercast.stop if @peercast
      @server.stop
    end

    def test_construct
      source = PCSHTTP::HTTPSourceStream.new(@peercast, @channel, @source_uri, @reader)
      assert_equal(@peercast, source.PeerCast)
      assert_equal(@channel, source.channel)
      assert_equal(@source_uri, source.source_uri)
      assert_equal(@reader, source.content_reader)
    end

    def test_start_not_found
      source = PCSHTTP::HTTPSourceStream.new(@peercast, @channel, @source_uri, @reader)
      requested = 0
      @server.on('/') do |req, sock|
        assert_equal('1.1', req.version)
        assert_equal('127.0.0.1:8888', req.headers['HOST'])
        assert_match(@peercast.agent_name.to_s, req.headers['USER-AGENT'])
        write_response(sock, req, 404)
        requested += 1
      end
      source.start
      50.times do
        break if requested>=1
        sleep(0.1)
      end
      assert_nil(@channel.content_header)
      assert_equal(0, @channel.contents.count)
      assert(!source.is_stopped)
    ensure
      source.stop
    end

    def test_start_found
      source = PCSHTTP::HTTPSourceStream.new(@peercast, @channel, @source_uri, @reader)
      requested = 0
      @server.on('/') do |req, sock|
        assert_equal('1.1', req.version)
        assert_equal('127.0.0.1:8888', req.headers['HOST'])
        assert_match(@peercast.agent_name.to_s, req.headers['USER-AGENT'])
        write_response(sock, req, 200, 'Content-Type' => 'application/octet-stream')
        sock.write('header')
        sock.write('content0')
        sock.write('content1')
        sock.write('content2')
        sock.write('content3')
        sock.write('content4')
        sock.write('content5')
        requested += 1
      end
      source.start
      50.times do
        break if requested>=1
        sleep(0.1)
      end
      assert_not_nil(@channel.content_header)
      assert(@channel.contents.count>=requested)
      newest = @channel.contents.newest
      assert(newest.position+newest.data.length>0)
      assert(!source.is_stopped)
    ensure
      source.stop
    end

    def test_start_retry
      source = PCSHTTP::HTTPSourceStream.new(@peercast, @channel, @source_uri, @reader)
      requested = []
      @server.on('/') do |req, sock|
        assert_equal('1.1', req.version)
        assert_equal('127.0.0.1:8888', req.headers['HOST'])
        assert_match(@peercast.agent_name.to_s, req.headers['USER-AGENT'])
        write_response(sock, req, 404)
        requested << Time.now
      end
      source.start
      600.times do
        break if requested.size>=3
        sleep(0.1)
      end
      assert(requested[1]-requested[0]>=3)
      assert(requested[2]-requested[1]>=3)
    ensure
      source.stop
    end

    def read_header(stream)
      header = ''
      until /\r\n\r\n/m=~header do
        b = stream.read(1)
        if b and b.size>0 then
          header << b
        else
          break
        end
      end
      header
    end

    def test_reconnect
      source_uri = System::Uri.new('http://127.0.0.1:8889')
      source = PCSHTTP::HTTPSourceStream.new(@peercast, @channel, source_uri, @reader)
      server = TCPServer.new('127.0.0.1', 8889)
      source.start
      requested = 0
      server_thread = Thread.new {
        2.times do |i|
          client = server.accept 
          requested += 1
          begin
            req = read_header(client)
            client.write("HTTP/1.1 200 OK\r\n")
            client.write("Content-Type:application/octet-stream\r\n")
            client.write("\r\n")
            100.times do |j|
              client.write("content#{i}#{j}\n")
              if i==0 and j==10 then
                source.reconnect
              end
            end
            client.close
          rescue
          end
        end
      }
      server_thread.join
      server.close
      assert_equal(2, requested)
    ensure
      source.stop
    end

    def test_post
      @server.on('/') do |req, sock|
        write_response(sock, req, 404)
      end
      source = PCSHTTP::HTTPSourceStream.new(@peercast, @channel, @source_uri, @reader)
      source.start
      assert_nothing_raised do
        source.post(nil, PCSCore::Atom.new(PCSCore::Atom.PCP_QUIT, PCSCore::Atom.PCP_ERROR_QUIT))
      end
      assert(!source.is_stopped)
    ensure
      source.stop
    end

    def test_recv_rate
      source = PCSHTTP::HTTPSourceStream.new(@peercast, @channel, @source_uri, @reader)
      requested = 0
      @server.on('/') do |req, sock|
        assert_equal('1.1', req.version)
        assert_equal('127.0.0.1:8888', req.headers['HOST'])
        assert_match(@peercast.agent_name.to_s, req.headers['USER-AGENT'])
        write_response(sock, req, 200, 'Content-Type' => 'application/octet-stream')
        sock.write('header')
        sock.write('content0')
        sock.write('content1')
        sock.write('content2')
        sock.write('content3')
        sock.write('content4')
        sock.write('content5')
        requested += 1
      end
      start = Time.now
      source.start
      50.times do
        break if @channel.contents.count>=6
        sleep(0.1)
      end
      source.stop
      t = Time.now
      sleep([1.5-(t-start), 0].max)
      recv_rate = source.recv_rate
      assert_not_equal 0, recv_rate
    end
  end
end

