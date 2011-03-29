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
    @peercast.close if @peercast and not @peercast.is_closed
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
    @peercast.channels.add(channel)
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
      end
      inst
    end
    attr_accessor :body_type, :write_enabled

    def get_body_type
      @body_type
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
    @peercast.close if @peercast and not @peercast.is_closed
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
    assert(!stream.is_closed)
    assert(stream.is_local)
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    assert_equal(@peercast,stream.PeerCast)
    assert_equal(@channel, stream.channel)
    assert_equal(s,        stream.stream)
    assert_equal(PCSCore::OutputStreamType.play, stream.output_stream_type)
    assert(!stream.is_closed)
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
    @channel.channel_info.extra.set_chan_info(chaninfo)
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

    @channel.channel_info.content_type = 'OGG'
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
    ['WMV', 'WMA', 'ASX'].each do |mms_type|
      @channel.channel_info.content_type = mms_type
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
    @channel.channel_info.content_type = 'OGG'
    stream.write_response_header
    assert_equal(stream.create_response_header+"\r\n", s.to_array.to_a.pack('C*'))
  end

  def test_write_content_header
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)

    @channel.content_header = nil
    s.position = 0
    assert(!stream.write_content_header)
    assert_equal(0, s.position)
    assert(!stream.is_closed)

    @channel.content_header = PCSCore::Content.new(0, 'header')
    s.position = 0
    assert(stream.write_content_header)
    assert_equal('header'.size, s.position)
    assert(!stream.is_closed)

    stream.write_enabled = false
    s.position = 0
    assert(!stream.write_content_header)
    assert_equal(0, s.position)
    assert(stream.is_closed)
  end

  def test_write_content
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)

    @channel.contents.clear
    s.position = 0
    assert_equal(-1, stream.write_content(-1))
    assert_equal(0, s.position)
    assert(!stream.is_closed)

    @channel.contents.add(PCSCore::Content.new(0, 'content0'))
    s.position = 0
    assert_equal(0, stream.write_content(-1))
    assert_equal('content0'.size, s.position)
    assert(!stream.is_closed)

    @channel.contents.add(PCSCore::Content.new( 7, 'content1'))
    @channel.contents.add(PCSCore::Content.new(14, 'content2'))
    @channel.contents.add(PCSCore::Content.new(21, 'content3'))
    @channel.contents.add(PCSCore::Content.new(28, 'content4'))
    s.position = 0; s.set_length(0)
    assert_equal(7, stream.write_content(0))
    assert_equal('content1', s.to_array.to_a.pack('C*'))
    assert(!stream.is_closed)

    s.position = 0; s.set_length(0)
    assert_equal(14, stream.write_content(7))
    assert_equal('content2', s.to_array.to_a.pack('C*'))
    assert(!stream.is_closed)

    s.position = 0; s.set_length(0)
    assert_equal(21, stream.write_content(14))
    assert_equal('content3', s.to_array.to_a.pack('C*'))
    assert(!stream.is_closed)

    s.position = 0; s.set_length(0)
    assert_equal(28, stream.write_content(21))
    assert_equal('content4', s.to_array.to_a.pack('C*'))
    assert(!stream.is_closed)

    s.position = 0; s.set_length(0)
    assert_equal(28, stream.write_content(28))
    assert_equal(0, s.position)
    assert(!stream.is_closed)

    stream.write_enabled = false
    s.position = 0; s.set_length(0)
    assert_equal(-1, stream.write_content(-1))
    assert_equal(0, s.position)
    assert(stream.is_closed)
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

  def test_close
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    stream.close
    assert(stream.is_closed)
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

  def test_write_response_body
    s = System::IO::MemoryStream.new
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = TestHTTPOutputStream.new(@peercast, s, endpoint, @channel, req)
    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.none 
    stream.write_response_body
    assert_equal(0, s.position)

    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.playlist 
    stream.write_response_body
    assert_equal('http://localhost/stream/9778E62BDC59DF56F9216D0387F80BF2', s.to_array.to_a.pack('C*').chomp)
    s.position = 0
    s.set_length(0)

    stream.body_type = PCSHTTP::HTTPOutputStream::BodyType.content 
    @channel.content_header = PCSCore::Content.new(0, 'header')
    @channel.contents.add(PCSCore::Content.new( 6, 'content0'))
    write_thread = Thread.new {
      stream.write_response_body
    }
    sleep(0.1)
    @channel.contents.add(PCSCore::Content.new(13, 'content1'))
    sleep(0.1)
    @channel.contents.add(PCSCore::Content.new(20, 'content2'))
    sleep(0.1)
    @channel.contents.add(PCSCore::Content.new(27, 'content3'))
    sleep(0.1)
    @channel.contents.add(PCSCore::Content.new(34, 'content4'))
    sleep(0.1)
    stream.close
    write_thread.join
    assert_equal('headercontent0content1content2content3content4', s.to_array.to_a.pack('C*'))
  end
end

