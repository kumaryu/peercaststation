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
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.HTTP', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.HTTP.dll'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

PCSCore = PeerCastStation::Core
PCSHTTP = PeerCastStation::HTTP

class TC_HTTPRequest < Test::Unit::TestCase
  def test_construct
    value = System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
      'Connection:close',
      'User-Agent:hoge hoge',
    ])
    req = PCSHTTP::HTTPRequest.new(value)
    assert_equal('GET', req.Method)
    assert_kind_of(System::Uri, req.uri)
    assert(req.uri.is_absolute_uri)
    assert_equal('http',      req.uri.scheme)
    assert_equal('localhost', req.uri.host)
    assert_equal('/stream/9778E62BDC59DF56F9216D0387F80BF2.wmv', req.uri.absolute_path)
  end
end

class TC_HTTPRequestReader < Test::Unit::TestCase
  def test_read
    stream = System::IO::MemoryStream.new([
      "GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1\r\n",
      "Connection:close\r\n",
      "User-Agent:hoge hoge\r\n",
      "\r\n"
    ].join)
    req = nil
    assert_nothing_raised {
      req = PCSHTTP::HTTPRequestReader.read(stream)
    }
    assert_kind_of(PCSHTTP::HTTPRequest, req)
    assert_equal('GET', req.Method)
    assert_kind_of(System::Uri, req.uri)
    assert(req.uri.is_absolute_uri)
    assert_equal('http',      req.uri.scheme)
    assert_equal('localhost', req.uri.host)
    assert_equal('/stream/9778E62BDC59DF56F9216D0387F80BF2.wmv', req.uri.absolute_path)
  end

  def test_read_failed
    stream = System::IO::MemoryStream.new([
      "GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1\r\n",
    ].join)
    assert_raise(System::IO::EndOfStreamException) {
      PCSHTTP::HTTPRequestReader.read(stream)
    }
  end
end

class TC_HTTPOutputStreamFactory < Test::Unit::TestCase
  def setup
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    @peercast = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(endpoint)
  end

  def teardown
    @peercast.stop if @peercast
  end

  def test_construct
    factory = PCSHTTP::HTTPOutputStreamFactory.new(@peercast)
    assert_equal('HTTP', factory.Name)
  end

  def test_parse_channel_id
    factory = PCSHTTP::HTTPOutputStreamFactory.new(@peercast)
    channel_id = factory.ParseChannelID([
      "GET /html/ja/index.html HTTP/1.1\r\n",
      "Connection:close\r\n",
      "User-Agent:hoge hoge\r\n",
      "\r\n"
    ].join)
    assert_nil(channel_id)
    channel_id = factory.ParseChannelID([
      "GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1\r\n",
      "Connection:close\r\n",
      "User-Agent:hoge hoge\r\n",
    ].join)
    assert_nil(channel_id)
    channel_id = factory.ParseChannelID([
      "GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1\r\n",
      "Connection:close\r\n",
      "User-Agent:hoge hoge\r\n",
      "\r\n"
    ].join)
    assert_equal(System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string), channel_id)
    channel_id = factory.ParseChannelID([
      "POST /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1\r\n",
      "Connection:close\r\n",
      "User-Agent:hoge hoge\r\n",
      "\r\n"
    ].join)
    assert_nil(channel_id)
  end

  def test_create
    channel_id = System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string)
    channel = PCSCore::Channel.new(
      @peercast,
      channel_id,
      System::Uri.new('http://localhost:7147/'))
    factory = PCSHTTP::HTTPOutputStreamFactory.new(@peercast)
    stream = System::IO::MemoryStream.new('hogehoge')
    header = [
      "GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1\r\n",
      "Connection:close\r\n",
      "User-Agent:hoge hoge\r\n",
      "\r\n"
    ].join
    @peercast.add_channel(channel)
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.2'), 7144)
    output_stream = factory.create(stream, endpoint, channel_id, header)
    assert_not_nil(output_stream)
    assert_equal(@peercast, output_stream.PeerCast)
    assert_equal(stream, output_stream.stream)
    assert_equal(channel, output_stream.channel)
    assert(output_stream.is_local)
    assert_equal(0, output_stream.upstream_rate)

    header = [
      "GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1\r\n",
    ].join
    output_stream = factory.create(stream, endpoint, channel_id, header)
    assert_nil(output_stream)
  end
end

class TC_HTTPOutputStream < Test::Unit::TestCase
  class HTTPOutputStream < PCSHTTP::HTTPOutputStream 
  end

  class TestHTTPOutputStream < PCSHTTP::HTTPOutputStream 
    def self.new(*args)
      inst = super
      inst.instance_eval do
        @body_type = PCSHTTP::HTTPOutputStream::BodyType.none 
        @write_enabled = true
        @error = false
      end
      inst
    end
    attr_accessor :body_type, :write_enabled, :error

    def get_body_type
      @body_type
    end

    def on_error
      @error = true
    end

    def write_bytes(bytes)
      if @write_enabled then
        super
      else
        false
      end
    end
  end

  def setup
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7147)
    @peercast = PeerCastStation::Core::PeerCast.new
    @peercast.start_listen(endpoint)
    @channel = PCSCore::Channel.new(
      @peercast,
      System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string), 
      System::Uri.new('http://localhost:7147/'))
  end

  def teardown
    @peercast.stop if @peercast
  end

  def test_construct
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
      'Connection:close',
      'User-Agent:hoge hoge',
    ]))
    s = System::IO::MemoryStream.new

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.2'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    assert_equal(@peercast,stream.PeerCast)
    assert_equal(@channel, stream.channel)
    assert_equal(s,        stream.stream)
    assert_equal(PCSCore::OutputStreamType.play, stream.output_stream_type)
    assert(!stream.is_stopped)
    assert(stream.is_local)
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    assert_equal(@peercast,stream.PeerCast)
    assert_equal(@channel, stream.channel)
    assert_equal(s,        stream.stream)
    assert_equal(PCSCore::OutputStreamType.play, stream.output_stream_type)
    assert(!stream.is_stopped)
    assert(!stream.is_local)
  end

  def test_upstream_rate
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
      'Connection:close',
      'User-Agent:hoge hoge',
    ]))
    s = System::IO::MemoryStream.new
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, s, nil, @channel, req)
    assert(stream.is_local)
    assert_equal(0, stream.upstream_rate)

    stream = PCSHTTP::HTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    assert(!stream.is_local)
    assert_equal(0, stream.upstream_rate)

    chaninfo = PCSCore::AtomCollection.new
    chaninfo.set_chan_info_bitrate(7144)
    @channel.channel_info = PCSCore::ChannelInfo.new(chaninfo)
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    assert(stream.is_local)
    assert_equal(0, stream.upstream_rate)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    assert(!stream.is_local)
    assert_equal(7144, stream.upstream_rate)
  end

  def test_get_body_type
    s = System::IO::MemoryStream.new
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)

    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    stream = HTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    assert_equal(PCSHTTP::HTTPOutputStream::BodyType.content, stream.get_body_type)

    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /pls/9778E62BDC59DF56F9216D0387F80BF2.pls HTTP/1.1',
    ]))
    stream = HTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    assert_equal(PCSHTTP::HTTPOutputStream::BodyType.playlist, stream.get_body_type)

    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /index.html HTTP/1.1',
    ]))
    stream = HTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    assert_equal(PCSHTTP::HTTPOutputStream::BodyType.none, stream.get_body_type)

    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    stream = HTTPOutputStream.new(@peercast, s, endpoint, nil, req)
    assert_equal(PCSHTTP::HTTPOutputStream::BodyType.none, stream.get_body_type)
  end

  def test_create_response_header
    s = System::IO::MemoryStream.new

    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)

    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, nil, req)
    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.playlist
    head = stream.create_response_header.split(/\r\n/)
    assert_match(%r;^HTTP/1.0 404 ;, head[0])

    info = PCSCore::AtomCollection.new(@channel.channel_info.extra)
    info.set_chan_info_type('OGG')
    @channel.channel_info = PCSCore::ChannelInfo.new(info)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.none
    head = stream.create_response_header.split(/\r\n/)
    assert_match(%r;^HTTP/1.0 404 ;, head[0])

    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.playlist
    head = stream.create_response_header.split(/\r\n/)
    assert_match(%r;^HTTP/1.0 200 ;, head[0])

    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.content
    head = stream.create_response_header.split(/\r\n/)
    assert_match(%r;^HTTP/1.0 200 ;, head[0])
    assert(head.any? {|line| /^Content-Type:\s*#{@channel.channel_info.MIMEType}/=~line})

    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.content
    info = PCSCore::AtomCollection.new(@channel.channel_info.extra)
    ['WMV', 'WMA', 'ASX'].each do |mms_type|
      info.set_chan_info_type(mms_type)
      @channel.channel_info = PCSCore::ChannelInfo.new(info)
      head = stream.create_response_header.split(/\r\n/)
      assert_match(%r;^HTTP/1.0 200 ;, head[0])
      assert(head.any? {|line| /^Content-Type:\s*application\/x-mms-framed/=~line})
      assert(head.any? {|line| /^Server:\s*Rex\/9\.0\.\d+/=~line})
    end
  end

  def test_write_response_header
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.content
    info = PCSCore::AtomCollection.new(@channel.channel_info.extra)
    info.set_chan_info_type('OGG')
    @channel.channel_info = PCSCore::ChannelInfo.new(info)
    stream.write_response_header
    stream.on_idle
    assert_equal(stream.create_response_header+"\r\n", s.to_array.to_a.pack('C*'))
  end

  def test_write_content_header
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)

    content = PCSCore::Content.new(0, 'header')
    s.position = 0; s.set_length(0)
    assert(stream.write_content_header(content))
    stream.on_idle
    assert_equal('header'.size, s.position)
    assert_equal('header', s.to_array.to_a.pack('C*'))
    assert(!stream.error)

    stream.write_enabled = false
    s.position = 0; s.set_length(0)
    assert(!stream.write_content_header(content))
    stream.on_idle
    assert_equal(0, s.position)
    assert(stream.error)
  end

  def test_write_content
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)

    content = PCSCore::Content.new(0, 'content0')
    s.position = 0; s.set_length(0)
    assert(stream.write_content(content))
    stream.on_idle
    assert_equal('content0'.size, s.position)
    assert_equal('content0', s.to_array.to_a.pack('C*'))
    assert(!stream.error)

    stream.write_enabled = false
    s.position = 0; s.set_length(0)
    assert(!stream.write_content(content))
    stream.on_idle
    assert_equal(0, s.position)
    assert(stream.error)
  end

  def test_post
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    stream.post(nil, PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, 1))
    assert_equal(0, s.position)
  end

  def test_stop
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    stopped = false
    stream.start
    stream.stopped.add(proc { stopped = true })
    stream.stop
    sleep(1) unless stopped
    assert(stream.is_stopped)
    assert(!s.can_read)
  end

  def test_write_bytes
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = HTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    assert(stream.write_bytes('hogehoge'))
    assert_equal('hogehoge', s.to_array.to_a.pack('C*'))

    s.close
    assert(!stream.write_bytes('hogehoge'))
  end

  def test_write_response_body_none
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.none 
    stream.write_response_body
    stream.on_idle
    assert_equal(0, s.position)
  end

  def test_write_response_body_none
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.playlist 
    stream.write_response_body
    stream.on_idle
    assert_equal('http://localhost/stream/9778E62BDC59DF56F9216D0387F80BF2', s.to_array.to_a.pack('C*').chomp)
    s.position = 0
    s.set_length(0)
  end

  def test_write_response_body_content
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.content 
    stream.write_response_body
    @channel.content_header = PCSCore::Content.new(0, 'header')
    @channel.contents.add(PCSCore::Content.new( 6, 'content0'))
    stream.on_idle
    @channel.contents.add(PCSCore::Content.new(13, 'content1'))
    stream.on_idle
    @channel.contents.add(PCSCore::Content.new(20, 'content2'))
    stream.on_idle
    @channel.contents.add(PCSCore::Content.new(27, 'content3'))
    stream.on_idle
    @channel.contents.add(PCSCore::Content.new(34, 'content4'))
    stream.on_idle
    assert_equal('headercontent0content1content2content3content4', s.to_array.to_a.pack('C*'))
  end
end

