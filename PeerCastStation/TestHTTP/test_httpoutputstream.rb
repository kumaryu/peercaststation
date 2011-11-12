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
    assert(factory.respond_to?(:create_obj_ref))
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
    input  = System::IO::MemoryStream.new('hogehoge')
    output = System::IO::MemoryStream.new
    header = [
      "GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1\r\n",
      "Connection:close\r\n",
      "User-Agent:hoge hoge\r\n",
      "\r\n"
    ].join
    @peercast.add_channel(channel)
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.2'), 7144)
    output_stream = factory.create(input, output, endpoint, channel_id, header)
    assert_not_nil(output_stream)
    assert_equal(@peercast, output_stream.PeerCast)
    assert_equal(input, output_stream.input_stream)
    assert_equal(output, output_stream.output_stream)
    assert_equal(channel, output_stream.channel)
    assert(output_stream.is_local)
    assert_equal(0, output_stream.upstream_rate)

    header = [
      "GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1\r\n",
    ].join
    output_stream = factory.create(input, output, endpoint, channel_id, header)
    assert_nil(output_stream)
  end
end

class TC_HTTPOutputStream < Test::Unit::TestCase
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

  def set_chan_info(name, type, bitrate)
    chaninfo = PCSCore::AtomCollection.new
    chaninfo.set_chan_info_name(name)
    chaninfo.set_chan_info_type(type)
    chaninfo.set_chan_info_bitrate(bitrate)
    @channel.channel_info = PCSCore::ChannelInfo.new(chaninfo)
  end

  def get_header(value)
    value.sub(/\r\n\r\n.*$/m, "\r\n\r\n")
  end

  def get_body(value)
    value.sub(/^.*?\r\n\r\n/m, '')
  end

  def assert_http_header(status, headers, value)
    header = get_header(value)
    values = header.split("\r\n")
    assert_match(%r;^HTTP/1.\d #{status} .*$;, values.shift)
    assert_match(%r;\r\n\r\n$$;, header)
    header_values = {}
    values.each do |v|
      md = /^(\S+):(.*)$/.match(v)
      assert_not_nil(md)
      header_values[md[1]] = md[2].strip
    end
    headers.each do |k, v|
      assert_match(v, header_values[k])
    end
  end

  def test_construct
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
      'Connection:close',
      'User-Agent:hoge hoge',
    ]))
    input  = System::IO::MemoryStream.new
    output = System::IO::MemoryStream.new

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.2'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)
    assert_equal(@peercast,stream.PeerCast)
    assert_equal(@channel, stream.channel)
    assert_equal(input,    stream.input_stream)
    assert_equal(output,   stream.output_stream)
    assert_equal(PCSCore::OutputStreamType.play, stream.output_stream_type)
    assert(!stream.is_stopped)
    assert(stream.is_local)
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)
    assert_equal(@peercast,stream.PeerCast)
    assert_equal(@channel, stream.channel)
    assert_equal(input,    stream.input_stream)
    assert_equal(output,   stream.output_stream)
    assert_equal(PCSCore::OutputStreamType.play, stream.output_stream_type)
    assert(!stream.is_stopped)
    assert(!stream.is_local)
    assert(stream.respond_to?(:create_obj_ref))
  end

  def test_upstream_rate
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
      'Connection:close',
      'User-Agent:hoge hoge',
    ]))
    input  = System::IO::MemoryStream.new
    output = System::IO::MemoryStream.new
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, nil, @channel, req)
    assert(stream.is_local)
    assert_equal(0, stream.upstream_rate)

    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)
    assert(!stream.is_local)
    assert_equal(0, stream.upstream_rate)

    set_chan_info('Test Channel', 'WMV', 7144)
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('127.0.0.1'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)
    assert(stream.is_local)
    assert_equal(0, stream.upstream_rate)

    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)
    assert(!stream.is_local)
    assert_equal(7144, stream.upstream_rate)
  end

  def test_head_stream_wmv
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'HEAD /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    input  = System::IO::MemoryStream.new
    output = System::IO::MemoryStream.new
    set_chan_info('Test Channel', 'WMV', 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)

    stopped = false
    stream.stopped.add(proc { stopped = true })
    stream.start
    sleep(0.1) until stopped
    res = output.to_array.to_a.pack('C*')
    assert_http_header(
      200,
      {
        'Server' => 'Rex/9.0.2980',
        'Content-Type' => 'application/x-mms-framed',
      },
      res
    )
  end

  def test_head_stream_other
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'HEAD /stream/9778E62BDC59DF56F9216D0387F80BF2.ogm HTTP/1.1',
    ]))
    set_chan_info('Test Channel', 'OGM', 7144)
    input  = System::IO::MemoryStream.new
    output = System::IO::MemoryStream.new
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)

    stopped = false
    stream.stopped.add(proc { stopped = true })
    stream.start
    sleep(0.1) until stopped
    res = output.to_array.to_a.pack('C*')
    assert_http_header(
      200,
      {
        'Content-Type' => 'video/ogg',
      },
      res
    )
  end

  def test_head_stream_not_found
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'HEAD /stream/4868E62BDC73DF56F9322D0387F80BF9.ogm HTTP/1.1',
    ]))
    input  = System::IO::MemoryStream.new
    output = System::IO::MemoryStream.new
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, nil, req)

    stopped = false
    stream.stopped.add(proc { stopped = true })
    stream.start
    sleep(0.1) until stopped
    res = output.to_array.to_a.pack('C*')
    assert_http_header(
      404,
      {},
      res
    )
  end

  def test_head_playlist
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'HEAD /pls/9778E62BDC59DF56F9216D0387F80BF2.asx HTTP/1.1',
    ]))
    set_chan_info('Test Channel', 'WMV', 7144)
    input  = System::IO::MemoryStream.new
    output = System::IO::MemoryStream.new
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)

    stopped = false
    stream.stopped.add(proc { stopped = true })
    stream.start
    sleep(0.1) until stopped
    res = output.to_array.to_a.pack('C*')
    assert_http_header(
      200,
      {
        'Content-Type' => 'video/x-ms-asf',
      },
      res
    )
  end

  def test_head_playlist_not_found
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'HEAD /stream/4868E62BDC73DF56F9322D0387F80BF9.ogm HTTP/1.1',
    ]))
    input  = System::IO::MemoryStream.new
    output = System::IO::MemoryStream.new
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, nil, req)

    stopped = false
    stream.stopped.add(proc { stopped = true })
    stream.start
    sleep(0.1) until stopped
    res = output.to_array.to_a.pack('C*')
    assert_http_header(404, {}, res)
  end

  def test_get_stream_wmv
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    set_chan_info('Test Channel', 'WMV', 7144)
    input  = System::IO::MemoryStream.new
    output = System::IO::MemoryStream.new
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)
    stopped = false
    stream.stopped.add(proc { stopped = true })

    stream.start
    @channel.content_header = PCSCore::Content.new(0, 'header')
    @channel.contents.add(PCSCore::Content.new( 6, 'content1'))
    @channel.contents.add(PCSCore::Content.new(14, 'content2'))
    @channel.contents.add(PCSCore::Content.new(22, 'content3'))
    @channel.contents.add(PCSCore::Content.new(30, 'content4'))
    sleep(1)
    res = output.to_array.to_a.pack('C*')
    assert_http_header(
      200,
      {
        'Server' => 'Rex/9.0.2980',
        'Content-Type' => 'application/x-mms-framed',
      },
      res
    )
    assert_equal('headercontent1content2content3content4', get_body(res))
    stream.stop
    sleep(0.1) until stopped
  end

  def test_post
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    input  = System::IO::MemoryStream.new
    output = System::IO::MemoryStream.new
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)
    stream.start
    stopped = false
    stream.stopped.add(proc { stopped = true })
    stream.post(nil, PCSCore::Atom.new(PCSCore::Atom.PCP_HELO, 1))
    sleep(1)
    stream.stop
    sleep(0.1) until stopped
    assert(output.to_array.to_a.empty?)
  end

  def test_stop
    req = PCSHTTP::HTTPRequest.new(System::Array[System::String].new([
      'GET /stream/9778E62BDC59DF56F9216D0387F80BF2.wmv HTTP/1.1',
    ]))
    input  = System::IO::MemoryStream.new
    output = System::IO::MemoryStream.new
    endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('219.117.192.180'), 7144)
    stream = PCSHTTP::HTTPOutputStream.new(@peercast, input, output, endpoint, @channel, req)
    stopped = false
    stream.stopped.add(proc { stopped = true })
    stream.start
    stream.stop
    sleep(0.1) unless stopped
    assert(stream.is_stopped)
    assert(!input.can_read)
    assert(!output.can_write)
  end
end

