
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core',    'bin', 'Debug')
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.UI.HTTP', 'bin', 'Debug')
require 'System.Core'
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.UI.HTTP.dll'
require 'test/unit'

def rubyize_name(name)
  unless /[A-Z]{2,}/=~name then
    name.gsub(/(?!^)[A-Z]/, '_\&').downcase
  else
    nil
  end
end

def explicit_extensions(klass)
  methods = klass.to_clr_type.get_methods(System::Reflection::BindingFlags.public | System::Reflection::BindingFlags.static)
  methods.each do |method|
    if not method.get_custom_attributes(System::Runtime::CompilerServices::ExtensionAttribute.to_clr_type, true).empty? then
      target_type = method.get_parameters[0].parameter_type
      if target_type.is_interface then
        type = target_type.to_module
      else
        type = target_type.to_class
      end
      type.module_eval do
        [method.name, rubyize_name(method.name)].compact.each do |name|
          define_method(name) do |*args|
            klass.__send__(method.name, self, *args)
          end
        end
      end
    end
  end
end
explicit_extensions PeerCastStation::Core::AtomCollectionExtensions
using_clr_extensions PeerCastStation::Core

PCSCore   = PeerCastStation::Core     unless defined?(PCSCore)
PCSHTTPUI = PeerCastStation::UI::HTTP unless defined?(PCSHTTPUI)

class TC_APIHostFactory < Test::Unit::TestCase
  def test_name
    factory = PCSHTTPUI::APIHostFactory.new
    assert_kind_of(System::String, factory.name)
  end

  def test_create_user_interface
    factory = PCSHTTPUI::APIHostFactory.new
    assert_kind_of(PCSHTTPUI::APIHost, factory.create_user_interface)
  end
end

class TestApplication < PCSCore::PeerCastApplication
  def peer_cast
    @peercast ||= PCSCore::PeerCast.new
  end

  def peercast
    @peercast ||= PCSCore::PeerCast.new
  end

  def stop
    @peercast.stop
  end
end

class TC_APIHost < Test::Unit::TestCase
  def test_start_stop
    app = TestApplication.new
    host = PCSHTTPUI::APIHost.new
    host.start(app)
    assert_equal(1, app.peercast.output_stream_factories.count)
    assert_kind_of(PCSHTTPUI::APIHost::APIHostOutputStreamFactory, app.peercast.output_stream_factories[0])
    host.stop
    assert_equal(0, app.peercast.output_stream_factories.count)
  end
end

class TCAPIHostOutputStreamFactory < Test::Unit::TestCase
  def test_parse_channel_id
    app     = TestApplication.new
    host    = PCSHTTPUI::APIHost.new
    factory = PCSHTTPUI::APIHost::APIHostOutputStreamFactory.new(host, app.peercast)
    req = [
      "GET / HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    assert_nil(factory.ParseChannelID(req))
    req = [
      "GET /api/1 HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    assert_equal(System::Guid.empty, factory.ParseChannelID(req))
  end

  def test_create
    app     = TestApplication.new
    host    = PCSHTTPUI::APIHost.new
    factory = PCSHTTPUI::APIHost::APIHostOutputStreamFactory.new(host, app.peercast)
    req = [
      "GET /api/1 HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    assert_kind_of(
      PCSHTTPUI::APIHost::APIHostOutputStream,
      factory.create(nil, nil, nil, System::Guid.empty, req))
  end
end

class TCAPIHostOutputStream < Test::Unit::TestCase
  def setup
    @app             = TestApplication.new
    @host            = PCSHTTPUI::APIHost.new
    @output_stream   = System::IO::MemoryStream.new
    @remote_endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    @factory = PCSHTTPUI::APIHost::APIHostOutputStreamFactory.new(@host, @app.peercast)
  end

  def teardown
    @app.stop
  end

  def fix_json_str(obj)
    case obj
    when String
      obj.gsub('=>', ':').gsub(/\bnil\b/, 'null')
    when Hash
      res = {}
      obj.each do |key, value|
        res[fix_json_str(key)] = fix_json_str(value)
      end
      res
    when Array
      obj.collect {|value| fix_json_str(value) }
    else
      obj
    end
  end

  def parse_json(str)
    fix_json_str(eval(str.gsub(':', '=>').gsub(/\bnull\b/, 'nil')))
  end

  Response = Struct.new(:version, :status, :headers, :body)
  def parse_response(res)
    res = res.scan(/^.*$/).collect(&:chomp)
    header = []
    header << res.shift until res.first==''
    res.shift
    if /^HTTP\/(1\.\d) (\d+) .*$/i=~header.shift then
      version = $1.to_f
      status  = $2.to_i
      headers = {}
      header.each do |line|
        md = /(.*):(.*)/.match(line)
        headers[md[1].strip.upcase] = md[2].strip
      end
      body = res.join("\r\n")
      Response.new(version, status, headers, body)
    else
      nil
    end
  end

  RPCResponse = Struct.new(:version, :id, :result, :error)
  def parse_jsonrpc_response(res_str)
    res = parse_response(res_str)
    if res then
      doc = parse_json(res.body.to_s)
      res.body = RPCResponse.new(
        doc['jsonrpc'],
        doc['id'],
        doc['result'],
        doc['error'])
    end
    res
  end

  def test_construct
    json = <<JSON
{
  "jsonrpc": "2.0",
  "method": "foo",
  "params": ["hoge", "fuga"],
  "id": 1
}
JSON
    req = [
      "POST /api/1 HTTP/1.0",
      "Content-Type: application/json",
      "Content-Length: #{json.bytesize}"
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(System::IO::MemoryStream.new(json), @output_stream, @remote_endpoint, System::Guid.empty, req)
    assert_equal(PCSCore::OutputStreamType.interface, os.output_stream_type)
  end

  def test_get
    req = [
      "GET /api/1 HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(System::IO::MemoryStream.new, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(200, res.status)
    assert_equal('application/json', res.headers['Content-Type'.upcase])
    assert(0!=res.headers['Content-Length'.upcase].to_i)
    doc = parse_json(res.body.to_s)
    assert_equal(@app.peercast.agent_name, doc['agentName'])
    assert_equal('1.0.0', doc['apiVersion'])
    assert_equal('2.0', doc['jsonrpc'])
  end

  def test_post_without_content_length
    req = [
      "POST /api/1 HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(System::IO::MemoryStream.new, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(411, res.status)
  end

  def test_post_with_bad_content_length
    req = [
      "POST /api/1 HTTP/1.0",
      "Content-Length: -1",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(System::IO::MemoryStream.new, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(400, res.status)
  end

  def test_post_content_length_too_long
    req = [
      "POST /api/1 HTTP/1.0",
      "Content-Length: 65537",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(System::IO::MemoryStream.new, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(400, res.status)
  end

  def test_post_timeout
    req = [
      "POST /api/1 HTTP/1.0",
      "Content-Length: 10",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(System::IO::MemoryStream.new, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(408, res.status)
  end

  def test_post_with_bad_json
    json = <<JSON
{
JSON
    req = [
      "POST /api/1 HTTP/1.0",
      "Content-Length: #{json.bytesize}",
    ].join("\r\n") + "\r\n\r\n"
    input_stream = System::IO::MemoryStream.new(json)
    os = @factory.create(input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(200, res.status)
    assert_equal('application/json', res.headers['Content-Type'.upcase])
    assert(0!=res.headers['Content-Length'.upcase].to_i)
    doc = parse_json(res.body.to_s)
    assert_nil(doc['id'])
    assert(!doc.include?('result'))
    assert_not_nil(doc['error'])
    assert_equal(-32700, doc['error']['code'])
    assert_not_nil(doc['error']['message'])
  end

  def test_without_version
    json = <<JSON
{
  "id": 1,
  "method": "getVersionInfo",
}
JSON
    req = [
      "POST /api/1 HTTP/1.0",
      "Content-Length: #{json.bytesize}",
    ].join("\r\n") + "\r\n\r\n"
    input_stream = System::IO::MemoryStream.new(json)
    os = @factory.create(input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(200, res.status)
    assert_equal('application/json', res.headers['Content-Type'.upcase])
    assert(0!=res.headers['Content-Length'.upcase].to_i)
    doc = parse_json(res.body.to_s)
    assert_nil(doc['id'])
    assert(!doc.include?('result'))
    assert_not_nil(doc['error'])
    assert_equal(-32600, doc['error']['code'])
    assert_not_nil(doc['error']['message'])
  end

  def test_with_bad_version
    json = <<JSON
{
  "jsonrpc": "1.1",
  "id": 1,
  "method": "getVersionInfo",
}
JSON
    req = [
      "POST /api/1 HTTP/1.0",
      "Content-Length: #{json.bytesize}",
    ].join("\r\n") + "\r\n\r\n"
    input_stream = System::IO::MemoryStream.new(json)
    os = @factory.create(input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(200, res.status)
    assert_equal('application/json', res.headers['Content-Type'.upcase])
    assert(0!=res.headers['Content-Length'.upcase].to_i)
    doc = parse_json(res.body.to_s)
    assert_nil(doc['id'])
    assert(!doc.include?('result'))
    assert_not_nil(doc['error'])
    assert_equal(-32600, doc['error']['code'])
    assert_not_nil(doc['error']['message'])
  end

  def test_without_method
    json = <<JSON
{
  "jsonrpc": "2.0",
  "id": 1,
  "params": ["foo", "bar"]
}
JSON
    req = [
      "POST /api/1 HTTP/1.0",
      "Content-Length: #{json.bytesize}",
    ].join("\r\n") + "\r\n\r\n"
    input_stream = System::IO::MemoryStream.new(json)
    os = @factory.create(input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(200, res.status)
    assert_equal('application/json', res.headers['Content-Type'.upcase])
    assert(0!=res.headers['Content-Length'.upcase].to_i)
    doc = parse_json(res.body.to_s)
    assert_nil(doc['id'])
    assert(!doc.include?('result'))
    assert_not_nil(doc['error'])
    assert_equal(-32600, doc['error']['code'])
    assert_not_nil(doc['error']['message'])
  end

  def test_notification
    json = <<JSON
{
  "jsonrpc": "2.0",
  "method": "getVersionInfo",
}
JSON
    req = [
      "POST /api/1 HTTP/1.0",
      "Content-Length: #{json.bytesize}",
    ].join("\r\n") + "\r\n\r\n"
    input_stream = System::IO::MemoryStream.new(json)
    os = @factory.create(input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(204, res.status)
  end

  def test_getVersionInfo
    json = <<JSON
{
  "jsonrpc": "2.0",
  "id": 1,
  "method": "getVersionInfo",
}
JSON
    req = [
      "POST /api/1 HTTP/1.0",
      "Content-Length: #{json.bytesize}",
    ].join("\r\n") + "\r\n\r\n"
    input_stream = System::IO::MemoryStream.new(json)
    os = @factory.create(input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(200, res.status)
    assert_equal('application/json', res.headers['Content-Type'.upcase])
    assert(0!=res.headers['Content-Length'.upcase].to_i)
    doc = parse_json(res.body.to_s)
    assert_equal(1, doc['id'])
    assert(!doc.include?('error'))
    assert_not_nil(doc['result'])
    result = doc['result']
    assert_equal(@app.peercast.agent_name, result['agentName'])
    assert_equal('1.0.0', result['apiVersion'])
    assert_equal('2.0',   result['jsonrpc'])
  end

  def rpc_request(method, params=nil, id=1)
    if params.nil? then
      json = <<JSON
{
  "jsonrpc": "2.0",
  "id": #{id},
  "method": #{method.inspect},
}
JSON
    else
      json = <<JSON
{
  "jsonrpc": "2.0",
  "id": #{id},
  "method": #{method.inspect},
  "params": #{params.inspect.gsub('=>', ':').gsub('nil', 'null')},
}
JSON
    end
    header = [
      "POST /api/1 HTTP/1.0",
      "Content-Length: #{json.bytesize}",
    ].join("\r\n") + "\r\n\r\n"
    body = System::IO::MemoryStream.new(json)
    [header, body]
  end

  def invoke_method(method, params=nil, id=1)
    req, input_stream = rpc_request(method, params, id)
    @output_stream = System::IO::MemoryStream.new
    os = @factory.create(input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    parse_jsonrpc_response(@output_stream.to_array.to_a.pack('C*'))
  end

  def test_getSettings
    actrl = @app.peercast.access_controller
    actrl.max_plays              = rand(100)
    actrl.max_relays             = rand(100)
    actrl.max_plays_per_channel  = rand(100)
    actrl.max_relays_per_channel = rand(100)
    actrl.max_upstream_rate      = rand(10000)
    res = invoke_method('getSettings')
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(actrl.max_plays,              res.body.result['maxDirects'])
    assert_equal(actrl.max_relays,             res.body.result['maxRelays'])
    assert_equal(actrl.max_plays_per_channel,  res.body.result['maxDirectsPerChannel'])
    assert_equal(actrl.max_relays_per_channel, res.body.result['maxRelaysPerChannel'])
    assert_equal(actrl.max_upstream_rate,      res.body.result['maxUpstreamRate'])
  end

  def test_setSettings_args_by_position
    actrl = @app.peercast.access_controller
    params = [
      {
        'maxDirects'           => rand(100),
        'maxRelays'            => rand(100),
        'maxDirectsPerChannel' => rand(100),
        'maxRelaysPerChannel'  => rand(100),
        'maxUpstreamRate'      => rand(10000),
      }
    ]
    res = invoke_method('setSettings', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(params[0]['maxDirects'],           actrl.max_plays)
    assert_equal(params[0]['maxRelays'],            actrl.max_relays)
    assert_equal(params[0]['maxDirectsPerChannel'], actrl.max_plays_per_channel)
    assert_equal(params[0]['maxRelaysPerChannel'],  actrl.max_relays_per_channel)
    assert_equal(params[0]['maxUpstreamRate'],      actrl.max_upstream_rate)
  end

  def test_setSettings_args_by_name
    actrl = @app.peercast.access_controller
    params = {
      'settings' => {
        'maxDirects'           => rand(100),
        'maxRelays'            => rand(100),
        'maxDirectsPerChannel' => rand(100),
        'maxRelaysPerChannel'  => rand(100),
        'maxUpstreamRate'      => rand(10000),
      }
    }
    res = invoke_method('setSettings', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(params['settings']['maxDirects'],           actrl.max_plays)
    assert_equal(params['settings']['maxRelays'],            actrl.max_relays)
    assert_equal(params['settings']['maxDirectsPerChannel'], actrl.max_plays_per_channel)
    assert_equal(params['settings']['maxRelaysPerChannel'],  actrl.max_relays_per_channel)
    assert_equal(params['settings']['maxUpstreamRate'],      actrl.max_upstream_rate)
  end

  def test_getChannels
    channels = Array.new(10) {
      PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        System::Uri.new('pcp://example.com'))
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    res = invoke_method('getChannels')
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(channels.size, res.body.result.size)
    assert_equal(channels.collect {|c| c.ChannelID.to_string('N').to_s.upcase }.sort, res.body.result.sort)
  end

  def test_getChannelStatus_args_by_position
    channels = Array.new(10) {
      PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        System::Uri.new('pcp://example.com'))
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    res = invoke_method('getChannelStatus', [channels[3].ChannelID.to_string('N')])
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    channel = channels[3]
    assert_equal(channel.status.to_s,    res.body.result['status'])
    assert_equal(channel.uptime.total_seconds.to_i, res.body.result['uptime'])
    assert_equal(channel.total_relays,   res.body.result['totalRelays'])
    assert_equal(channel.total_directs,  res.body.result['totalDirects'])
    assert_equal(channel.BroadcastID==@app.peercast.BroadcastID, res.body.result['isBroadcasting'])
    assert_equal(channel.is_relay_full,  res.body.result['isRelayFull'])
    assert_equal(channel.is_direct_full, res.body.result['isDirectFull'])
  end

  def test_getChannelStatus_args_by_name
    channels = Array.new(10) {
      PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        System::Uri.new('pcp://example.com'))
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    res = invoke_method('getChannelStatus', { 'channelId' => channels[3].ChannelID.to_string('N') })
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    channel = channels[3]
    assert_equal(channel.status.to_s,    res.body.result['status'])
    assert_equal(channel.uptime.total_seconds.to_i, res.body.result['uptime'])
    assert_equal(channel.total_relays,   res.body.result['totalRelays'])
    assert_equal(channel.total_directs,  res.body.result['totalDirects'])
    assert_equal(channel.BroadcastID==@app.peercast.BroadcastID, res.body.result['isBroadcasting'])
    assert_equal(channel.is_relay_full,  res.body.result['isRelayFull'])
    assert_equal(channel.is_direct_full, res.body.result['isDirectFull'])
  end

  def create_chan_info(name, type, bitrate)
    chaninfo = PCSCore::AtomCollection.new
    chaninfo.set_chan_info_name(name)
    chaninfo.set_chan_info_type(type)
    chaninfo.set_chan_info_bitrate(bitrate)
    PCSCore::ChannelInfo.new(chaninfo)
  end

  def test_getChannelInfo_args_by_position
    channels = Array.new(10) {|i|
      c = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        System::Uri.new('pcp://example.com'))
      c.channel_info = create_chan_info("#{i}ch", 'WMV', 774*i)
      c
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    actrl = @app.peercast.access_controller
    params = [channels[3].ChannelID.to_string('N')]
    res = invoke_method('getChannelInfo', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    info = channels[3].channel_info
    assert_equal(info.name,         res.body.result['info']['name'])
    assert_equal(info.URL,          res.body.result['info']['url'])
    assert_equal(info.genre,        res.body.result['info']['genre'])
    assert_equal(info.desc,         res.body.result['info']['desc'])
    assert_equal(info.comment,      res.body.result['info']['comment'])
    assert_equal(info.bitrate,      res.body.result['info']['bitrate'])
    assert_equal(info.content_type, res.body.result['info']['contentType'])
    assert_equal(info.MIMEType,     res.body.result['info']['mimeType'])
    track = channels[3].channel_track
    assert_equal(track.name,        res.body.result['track']['name'])
    assert_equal(track.genre,       res.body.result['track']['genre'])
    assert_equal(track.album,       res.body.result['track']['album'])
    assert_equal(track.creator,     res.body.result['track']['creator'])
    assert_equal(track.URL,         res.body.result['track']['url'])
  end

  def test_getChannelInfo_args_by_name
    channels = Array.new(10) {|i|
      c = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        System::Uri.new('pcp://example.com'))
      c.channel_info = create_chan_info("#{i}ch", 'WMV', 774*i)
      c
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    actrl = @app.peercast.access_controller
    params = { 'channelId' => channels[3].ChannelID.to_string('N') }
    res = invoke_method('getChannelInfo', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    info = channels[3].channel_info
    assert_equal(info.name,         res.body.result['info']['name'])
    assert_equal(info.URL,          res.body.result['info']['url'])
    assert_equal(info.genre,        res.body.result['info']['genre'])
    assert_equal(info.desc,         res.body.result['info']['desc'])
    assert_equal(info.comment,      res.body.result['info']['comment'])
    assert_equal(info.bitrate,      res.body.result['info']['bitrate'])
    assert_equal(info.content_type, res.body.result['info']['contentType'])
    assert_equal(info.MIMEType,     res.body.result['info']['mimeType'])
    track = channels[3].channel_track
    assert_equal(track.name,        res.body.result['track']['name'])
    assert_equal(track.genre,       res.body.result['track']['genre'])
    assert_equal(track.album,       res.body.result['track']['album'])
    assert_equal(track.creator,     res.body.result['track']['creator'])
    assert_equal(track.URL,         res.body.result['track']['url'])
  end

  def test_setChannelInfo_args_by_position
    channels = Array.new(10) {|i|
      c = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
      c.channel_info = create_chan_info("#{i}ch", 'WMV', 774*i)
      c
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    actrl = @app.peercast.access_controller
    new_info = {
      'name'        => 'Foo ch',
      'url'         => 'http://www.example.com/',
      'genre'       => 'Test',
      'desc'        => 'Foo',
      'comment'     => 'Bar',
      'bitrate'     => 42,
      'contentType' => 'OGG',
      'mimeType'    => 'application/octet-stream',
    }
    new_track = {
      'name'    => 'Hoge',
      'genre'   => 'Game',
      'album'   => 'Fuga',
      'creator' => 'Piyo',
      'url'     => 'TrackURL',
    }
    params = [
      channels[3].ChannelID.to_string('N'),
      new_info,
      new_track,
    ]
    res = invoke_method('setChannelInfo', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    info = channels[3].channel_info
    assert_equal(new_info['name'],        info.name)
    assert_equal(new_info['url'],         info.URL)
    assert_equal(new_info['genre'],       info.genre)
    assert_equal(new_info['desc'],        info.desc)
    assert_equal(new_info['comment'],     info.comment)
    assert_not_equal(new_info['bitrate'],     info.bitrate)
    assert_not_equal(new_info['contentType'], info.content_type)
    assert_not_equal(new_info['mimeType'],    info.MIMEType)
    track = channels[3].channel_track
    assert_equal(new_track['name'],    track.name)
    assert_equal(new_track['genre'],   track.genre)
    assert_equal(new_track['album'],   track.album)
    assert_equal(new_track['creator'], track.creator)
    assert_equal(new_track['url'],     track.URL)
  end

  def test_setChannelInfo_args_by_name
    channels = Array.new(10) {|i|
      c = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
      c.channel_info = create_chan_info("#{i}ch", 'WMV', 774*i)
      c
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    actrl = @app.peercast.access_controller
    new_info = {
      'name'        => 'Foo ch',
      'url'         => 'http://www.example.com/',
      'genre'       => 'Test',
      'desc'        => 'Foo',
      'comment'     => 'Bar',
      'bitrate'     => 42,
      'contentType' => 'OGG',
      'mimeType'    => 'application/octet-stream',
    }
    new_track = {
      'name'    => 'Hoge',
      'genre'   => 'Game',
      'album'   => 'Fuga',
      'creator' => 'Piyo',
      'url'     => 'TrackURL',
    }
    params = {
      'channelId' => channels[3].ChannelID.to_string('N'),
      'info'      => new_info,
      'track'     => new_track,
    }
    res = invoke_method('setChannelInfo', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    info = channels[3].channel_info
    assert_equal(new_info['name'],        info.name)
    assert_equal(new_info['url'],         info.URL)
    assert_equal(new_info['genre'],       info.genre)
    assert_equal(new_info['desc'],        info.desc)
    assert_equal(new_info['comment'],     info.comment)
    assert_not_equal(new_info['bitrate'],     info.bitrate)
    assert_not_equal(new_info['contentType'], info.content_type)
    assert_not_equal(new_info['mimeType'],    info.MIMEType)
    track = channels[3].channel_track
    assert_equal(new_track['name'],    track.name)
    assert_equal(new_track['genre'],   track.genre)
    assert_equal(new_track['album'],   track.album)
    assert_equal(new_track['creator'], track.creator)
    assert_equal(new_track['url'],     track.URL)
  end

  def test_setChannelInfo_args_by_position
    channels = Array.new(10) {|i|
      c = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
      c.channel_info = create_chan_info("#{i}ch", 'WMV', 774*i)
      c
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    actrl = @app.peercast.access_controller
    new_info = {
      'name'        => 'Foo ch',
      'url'         => 'http://www.example.com/',
      'genre'       => 'Test',
      'desc'        => 'Foo',
      'comment'     => 'Bar',
      'bitrate'     => 42,
      'contentType' => 'OGG',
      'mimeType'    => 'application/octet-stream',
    }
    new_track = {
      'name'    => 'Hoge',
      'genre'   => 'Game',
      'album'   => 'Fuga',
      'creator' => 'Piyo',
      'url'     => 'TrackURL',
    }
    params = [
      channels[3].ChannelID.to_string('N'),
      new_info,
      new_track,
    ]
    res = invoke_method('setChannelInfo', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    info = channels[3].channel_info
    assert_equal(new_info['name'],        info.name)
    assert_equal(new_info['url'],         info.URL)
    assert_equal(new_info['genre'],       info.genre)
    assert_equal(new_info['desc'],        info.desc)
    assert_equal(new_info['comment'],     info.comment)
    assert_not_equal(new_info['bitrate'],     info.bitrate)
    assert_not_equal(new_info['contentType'], info.content_type)
    assert_not_equal(new_info['mimeType'],    info.MIMEType)
    track = channels[3].channel_track
    assert_equal(new_track['name'],    track.name)
    assert_equal(new_track['genre'],   track.genre)
    assert_equal(new_track['album'],   track.album)
    assert_equal(new_track['creator'], track.creator)
    assert_equal(new_track['url'],     track.URL)
  end

  def test_stopChannel_args_by_name
    channels = Array.new(10) {|i|
      c = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
      c.channel_info = create_chan_info("#{i}ch", 'WMV', 774*i)
      c
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    params = {
      'channelId' => channels[3].ChannelID.to_string('N'),
    }
    res = invoke_method('stopChannel', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_not_equal(channels.size, @app.peercast.channels.count)
    assert(@app.peercast.channels.all? {|c| c.ChannelID!=channels[3].ChannelID })
  end

  def test_stopChannel_args_by_position
    channels = Array.new(10) {|i|
      c = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
      c.channel_info = create_chan_info("#{i}ch", 'WMV', 774*i)
      c
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    params = [
      channels[3].ChannelID.to_string('N'),
    ]
    res = invoke_method('stopChannel', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_not_equal(channels.size, @app.peercast.channels.count)
    assert(@app.peercast.channels.all? {|c| c.ChannelID!=channels[3].ChannelID })
  end

  def test_bumpChannel_args_by_name
    channels = Array.new(10) {|i|
      c = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
      c.channel_info = create_chan_info("#{i}ch", 'WMV', 774*i)
      c
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    params = {
      'channelId' => channels[3].ChannelID.to_string('N'),
    }
    res = invoke_method('bumpChannel', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(channels.size, @app.peercast.channels.count)
  end

  def test_bumpChannel_args_by_position
    channels = Array.new(10) {|i|
      c = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
      c.channel_info = create_chan_info("#{i}ch", 'WMV', 774*i)
      c
    }
    channels.each do |c|
      @app.peercast.add_channel(c)
    end
    params = [
      channels[3].ChannelID.to_string('N'),
    ]
    res = invoke_method('bumpChannel', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(channels.size, @app.peercast.channels.count)
  end

  class TestOutputStream
    include PeerCastStation::Core::IOutputStream
    
    def initialize(name, type=0)
      @name = name
      @type = type
      @remote_endpoint = nil
      @upstream_rate = 0
      @is_local = false
      @log = []
      @stopped = []
    end
    attr_reader :log
    attr_accessor :remote_endpoint, :upstream_rate, :is_local

    def to_s
      @name.to_s
    end

    def add_Stopped(event)
      @stopped << event
    end

    def remove_Stopped(event)
      @stopped.delete(event)
    end

    def output_stream_type
      @type
    end

    def post(from, packet)
      @log << [:post, from, packet]
    end
    
    def start
      @log << [:start]
      stop
    end
    
    def stop
      @log << [:stop]
      @stopped.each do |event|
        event.invoke(self, System::EventArgs.new)
      end
    end
  end

  def test_getChannelOutputs_args_by_name
    channel = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
    channel.channel_info = create_chan_info("Foo ch", 'WMV', 774)
    @app.peercast.add_channel(channel)
    channel.add_output_stream(TestOutputStream.new('hoge'))
    channel.add_output_stream(TestOutputStream.new('fuga'))
    channel.add_output_stream(TestOutputStream.new('piyo'))
    params = { 'channelId' => channel.ChannelID.to_string('N'), }
    res = invoke_method('getChannelOutputs', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(channel.output_streams.count, res.body.result.size)
    channel.output_streams.count.times do |i|
      assert_equal(channel.output_streams[i].hash, res.body.result[i]['id'])
      assert_equal(channel.output_streams[i].to_s, res.body.result[i]['name'])
    end
  end

  def test_getChannelOutputs_args_by_position
    channel = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
    channel.channel_info = create_chan_info("Foo ch", 'WMV', 774)
    @app.peercast.add_channel(channel)
    channel.add_output_stream(TestOutputStream.new('hoge'))
    channel.add_output_stream(TestOutputStream.new('fuga'))
    channel.add_output_stream(TestOutputStream.new('piyo'))
    params = [ channel.ChannelID.to_string('N'), ]
    res = invoke_method('getChannelOutputs', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(channel.output_streams.count, res.body.result.size)
    channel.output_streams.count.times do |i|
      assert_equal(channel.output_streams[i].hash, res.body.result[i]['id'])
      assert_equal(channel.output_streams[i].to_s, res.body.result[i]['name'])
    end
  end

  def test_stopChannelOutput_args_by_name
    channel = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
    channel.channel_info = create_chan_info("Foo ch", 'WMV', 774)
    @app.peercast.add_channel(channel)
    outputs = [
      TestOutputStream.new('hoge'),
      TestOutputStream.new('fuga'),
      TestOutputStream.new('piyo'),
    ]
    outputs.each do |output|
      channel.add_output_stream(output)
    end
    params = {
      'channelId' => channel.ChannelID.to_string('N'),
      'id' => outputs[1].hash,
    }
    res = invoke_method('stopChannelOutput', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_not_equal(outputs.size, channel.output_streams.count)
    assert(channel.output_streams.all? {|os| os!=outputs[1] })
  end

  def test_stopChannelOutput_args_by_position
    channel = PCSCore::Channel.new(
        @app.peercast,
        System::Guid.new_guid,
        @app.peercast.BroadcastID,
        System::Uri.new('pcp://example.com'))
    channel.channel_info = create_chan_info("Foo ch", 'WMV', 774)
    @app.peercast.add_channel(channel)
    outputs = [
      TestOutputStream.new('hoge'),
      TestOutputStream.new('fuga'),
      TestOutputStream.new('piyo'),
    ]
    outputs.each do |output|
      channel.add_output_stream(output)
    end
    params = [
      channel.ChannelID.to_string('N'),
      outputs[1].hash,
    ]
    res = invoke_method('stopChannelOutput', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_not_equal(outputs.size, channel.output_streams.count)
    assert(channel.output_streams.all? {|os| os!=outputs[1] })
  end

  class TestContentReader
    include PCSCore::IContentReader
    def initialize(name)
      @name = name
    end
    attr_reader :name

    def read(channel, stream)
      nil
    end
  end

  def test_getContentReaders
    readers = [
      TestContentReader.new('WMV'),
      TestContentReader.new('OGG'),
      TestContentReader.new('RAW'),
    ]
    readers.each do |reader|
      @app.peercast.add_content_reader(reader)
    end
    res = invoke_method('getContentReaders')
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(@app.peercast.content_readers.count, res.body.result.size)
    readers.size.times do |i|
      assert_equal(readers[i].hash, res.body.result[i]['id'])
      assert_equal(readers[i].name, res.body.result[i]['name'])
    end
  end

  class TestYPClient
    include PCSCore::IYellowPageClient
    def initialize(name, uri)
      @name = name
      @uri  = uri
      @log  = []
    end
    attr_reader :name, :uri, :log

    def find_tracker(channel_id)
      @log << :find_tracker
      nil
    end

    def announce(channel)
      @log << :announce
    end

    def stop_announce
      @log << :stop_announce
    end

    def restart_announce
      @log << :restart_announce
    end
  end

  class TestYPClientFactory
    include PCSCore::IYellowPageClientFactory
    def initialize(name)
      @name = name
    end
    attr_reader :name

    def create(yp_name, uri)
      TestYPClient.new(yp_name, uri)
    end
  end

  def test_getYellowPageProtocols
    factories = [
      TestYPClientFactory.new('pcp'),
      TestYPClientFactory.new('neetyp'),
      TestYPClientFactory.new('twitteryp'),
      TestYPClientFactory.new('wp'),
    ]
    factories.each do |factory|
      @app.peercast.yellow_page_factories.add(factory)
    end
    res = invoke_method('getYellowPageProtocols')
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(@app.peercast.yellow_page_factories.count, res.body.result.size)
    factories.size.times do |i|
      assert_equal(factories[i].hash, res.body.result[i]['id'])
      assert_equal(factories[i].name, res.body.result[i]['name'])
    end
  end

  def test_getYellowPages
    @app.peercast.yellow_page_factories.add(TestYPClientFactory.new('pcp'))
    yps = [
      ['foo', 'pcp://foo.example.com/'],
      ['bar', 'pcp://bar.example.com/'],
      ['baz', 'pcp://baz.example.com/'],
    ].collect {|name, uri|
      @app.peercast.add_yellow_page('pcp', name, System::Uri.new(uri))
    }
    res = invoke_method('getYellowPages')
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(@app.peercast.yellow_pages.count, res.body.result.size)
    yps.size.times do |i|
      assert_equal(yps[i].hash,     res.body.result[i]['id'])
      assert_equal(yps[i].name,     res.body.result[i]['name'])
      assert_equal(yps[i].uri.to_s, res.body.result[i]['uri'])
    end
  end

  def test_addYellowPage_args_by_name
    @app.peercast.yellow_page_factories.add(TestYPClientFactory.new('pcp'))
    params = {
      'protocol' => @app.peercast.yellow_page_factories[0].hash,
      'name'     => 'foo',
      'uri'      => 'pcp://foo.example.com/',
    }
    res = invoke_method('addYellowPage', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(1, @app.peercast.yellow_pages.count)
    yp = @app.peercast.yellow_pages[0]
    assert_equal(yp.hash,     res.body.result['id'])
    assert_equal(yp.name,     res.body.result['name'])
    assert_equal(yp.uri.to_s, res.body.result['uri'])
    assert_equal(params['name'], yp.name)
    assert_equal(params['uri'],  yp.uri.to_s)
  end

  def test_addYellowPage_args_by_position
    @app.peercast.yellow_page_factories.add(TestYPClientFactory.new('pcp'))
    params = [
      @app.peercast.yellow_page_factories[0].hash,
      'foo',
      'pcp://foo.example.com/',
    ]
    res = invoke_method('addYellowPage', params)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(1, @app.peercast.yellow_pages.count)
    yp = @app.peercast.yellow_pages[0]
    assert_equal(yp.hash,     res.body.result['id'])
    assert_equal(yp.name,     res.body.result['name'])
    assert_equal(yp.uri.to_s, res.body.result['uri'])
    assert_equal(params[1], yp.name)
    assert_equal(params[2], yp.uri.to_s)
  end
  
  def test_removeYellowPage
    @app.peercast.yellow_page_factories.add(TestYPClientFactory.new('pcp'))
    yps = [
      ['foo', 'pcp://foo.example.com/'],
      ['bar', 'pcp://bar.example.com/'],
      ['baz', 'pcp://baz.example.com/'],
    ].collect {|name, uri|
      @app.peercast.add_yellow_page('pcp', name, System::Uri.new(uri))
    }
    res = invoke_method('removeYellowPage', 'id' => @app.peercast.yellow_pages[1].hash)
    assert_equal(1, res.body.id)
    assert_equal(2, @app.peercast.yellow_pages.count)
    assert_equal('foo', @app.peercast.yellow_pages[0].name)
    assert_equal('baz', @app.peercast.yellow_pages[1].name)
  end

  def test_getListeners
    listeners = [
      ['127.0.0.1', 7144],
      ['0.0.0.0',   7145],
      ['127.0.0.1', 7146],
    ].collect {|addr, port|
      @app.peercast.start_listen(System::Net::IPEndPoint.new(System::Net::IPAddress.parse(addr), port))
    }
    res = invoke_method('getListeners')
    assert_equal(1, res.body.id)
    assert_equal(3, res.body.result.count)
    3.times do |i|
      assert_equal(listeners[i].hash,                       res.body.result[i]['id'])
      assert_equal(listeners[i].LocalEndPoint.address.to_s, res.body.result[i]['address'])
      assert_equal(listeners[i].LocalEndPoint.port,         res.body.result[i]['port'])
    end
  end

  def test_addListener
    listeners = [
      ['127.0.0.1', '127.0.0.1', 7144],
      ['0.0.0.0',   '0.0.0.0',   7145],
      [nil,         '0.0.0.0',   7146],
    ]
    listeners.each do |addr, expected, port|
      res = invoke_method('addListener', 'address' => addr, 'port' => port)
      assert_equal(1, res.body.id)
      assert_nil(res.body.error)
      new_listener = @app.peercast.output_listeners[@app.peercast.output_listeners.count-1]
      assert_equal(new_listener.hash,                       res.body.result['id'])
      assert_equal(new_listener.LocalEndPoint.address.to_s, res.body.result['address'])
      assert_equal(new_listener.LocalEndPoint.port,         res.body.result['port'])
    end
    assert_equal(listeners.count, @app.peercast.output_listeners.count)
  end

  def test_removeListener
    listeners = [
      ['127.0.0.1', 7144],
      ['0.0.0.0',   7145],
      ['127.0.0.1', 7146],
    ].collect {|addr, port|
      @app.peercast.start_listen(System::Net::IPEndPoint.new(System::Net::IPAddress.parse(addr), port))
    }
    res = invoke_method('removeListener', 'id' => listeners[1].hash)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    assert_equal(listeners.count-1, @app.peercast.output_listeners.count)
    assert_same(listeners[0], @app.peercast.output_listeners[0])
    assert_same(listeners[2], @app.peercast.output_listeners[1])
  end

  class TestSourceStreamFactory
    include PeerCastStation::Core::ISourceStreamFactory
    def initialize
      @log = []
    end
    attr_reader :log
    
    def name
      'TestSourceStream'
    end
    
    def create(channel, uri, reader=nil)
      @log << [:create, channel, uri, reader]
      TestSourceStream.new(channel, uri, reader)
    end
  end

  class TestSourceStream
    include PeerCastStation::Core::ISourceStream
    
    def initialize(channel, tracker, reader=nil)
      @channel = channel
      @tracker = tracker
      @reader  = reader
      @status_changed = []
      @status = PeerCastStation::Core::SourceStreamStatus.idle
      @start_proc = nil
      @stopped = []
      @log = []
    end
    attr_reader :log, :reader, :tracker, :channel, :status
    attr_accessor :start_proc

    def add_StatusChanged(handler)
      @status_changed << handler
    end
    
    def remove_StatusChanged(handler)
      @status_changed.delete(handler)
    end

    def add_Stopped(handler)
      @stopped << handler
    end
    
    def remove_Stopped(handler)
      @stopped.delete(handler)
    end

    def post(from, packet)
      @log << [:post, from, packet]
    end
    
    def start
      @log << [:start]
      @start_proc.call if @start_proc
      args = System::EventArgs.new
      @stopped.each do |handler|
        handler.invoke(self, args)
      end
    end
    
    def reconnect
      @log << [:reconnect]
    end
    
    def stop
      @log << [:stop]
    end
  end

  def test_broadcastChannel
    @app.peercast.source_stream_factories.add('test', TestSourceStreamFactory.new)
    @app.peercast.yellow_page_factories.add(TestYPClientFactory.new('pcp'))
    yp = @app.peercast.add_yellow_page('pcp', 'foo', System::Uri.new('pcp://foo.example.com'))
    reader = TestContentReader.new('WMV')
    @app.peercast.add_content_reader(reader)
    source = 'test://example.com:8080/'
    info = {
      'name'    => 'TestChannel',
      'genre'   => 'test',
      'url'     => 'http://www.example.com/',
      'desc'    => 'Foo',
      'comment' => 'Bar',
    }
    track = {
      'name'    => 'Test Track',
      'genre'   => 'hoge',
      'album'   => 'fuga',
      'creator' => 'piyo',
      'url'     => 'http://test.example.com/',
    }
    res = invoke_method('broadcastChannel',
      'yellowPage'    => yp.hash,
      'sourceUri'     => source,
      'contentReader' => reader.hash,
      'info'          => info,
      'track'         => track)
    assert_equal(1, res.body.id)
    assert_nil(res.body.error)
    channel = @app.peercast.channels[0]
    assert_equal(channel.ChannelID.to_string('N').upcase, res.body.result)
    assert_equal(@app.peercast.BroadcastID, channel.BroadcastID)
    assert_equal(source, channel.source_uri.to_s)
    assert_equal(info['name'],    channel.channel_info.name)
    assert_equal(info['genre'],   channel.channel_info.genre)
    assert_equal(info['url'],     channel.channel_info.URL)
    assert_equal(info['desc'],    channel.channel_info.desc)
    assert_equal(info['comment'], channel.channel_info.comment)
    assert_equal(track['name'],    channel.channel_track.name)
    assert_equal(track['genre'],   channel.channel_track.genre)
    assert_equal(track['url'],     channel.channel_track.URL)
    assert_equal(track['album'],   channel.channel_track.album)
    assert_equal(track['creator'], channel.channel_track.creator)
    assert(yp.log.size>0)
    assert(yp.log.include?(:announce))
  end
end

