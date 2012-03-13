
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core',    'bin', 'Debug')
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.UI.HTTP', 'bin', 'Debug')
require 'System.Core'
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.UI.HTTP.dll'
require 'test/unit'
require 'rexml/document'

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

class TC_AdminHostFactory < Test::Unit::TestCase
  def test_name
    factory = PCSHTTPUI::AdminHostFactory.new
    assert_kind_of(System::String, factory.name)
  end

  def test_create_user_interface
    factory = PCSHTTPUI::AdminHostFactory.new
    assert_kind_of(PCSHTTPUI::AdminHost, factory.create_user_interface)
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

class TC_AdminHost < Test::Unit::TestCase
  def test_start_stop
    app = TestApplication.new
    host = PCSHTTPUI::AdminHost.new
    host.start(app)
    assert_equal(1, app.peercast.output_stream_factories.count)
    assert_kind_of(PCSHTTPUI::AdminHost::AdminHostOutputStreamFactory, app.peercast.output_stream_factories[0])
    host.stop
    assert_equal(0, app.peercast.output_stream_factories.count)
  end
end

class TCAdminHostOutputStreamFactory < Test::Unit::TestCase
  def test_construct
    app     = TestApplication.new
    host    = PCSHTTPUI::AdminHost.new
    factory = PCSHTTPUI::AdminHost::AdminHostOutputStreamFactory.new(host, app.peercast)
    assert(factory.priority>0)
    assert(factory.priority<100)
  end
  
  def test_parse_channel_id
    app     = TestApplication.new
    host    = PCSHTTPUI::AdminHost.new
    factory = PCSHTTPUI::AdminHost::AdminHostOutputStreamFactory.new(host, app.peercast)
    req = [
      "GET / HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    assert_nil(factory.ParseChannelID(req))
    req = [
      "GET /admin HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    assert_equal(System::Guid.empty, factory.ParseChannelID(req))
  end

  def test_create
    app     = TestApplication.new
    host    = PCSHTTPUI::AdminHost.new
    factory = PCSHTTPUI::AdminHost::AdminHostOutputStreamFactory.new(host, app.peercast)
    req = [
      "GET /admin HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    assert_kind_of(
      PCSHTTPUI::AdminHost::AdminHostOutputStream,
      factory.create(nil, nil, nil, System::Guid.empty, req))
  end
end

class TCAdminHostOutputStream < Test::Unit::TestCase
  def setup
    @app             = TestApplication.new
    @host            = PCSHTTPUI::AdminHost.new
    @input_stream    = System::IO::MemoryStream.new
    @output_stream   = System::IO::MemoryStream.new
    @remote_endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    @factory = PCSHTTPUI::AdminHost::AdminHostOutputStreamFactory.new(@host, @app.peercast)
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

  def test_construct
    req = [
      "GET /admin HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    assert_equal(PCSCore::OutputStreamType.interface, os.output_stream_type)
  end

  def test_start_method_not_allowed
    req = [
      "POST /admin HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(405, res.status)
  end

  def test_start_without_params
    req = [
      "GET /admin HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(400, res.status)
  end

  def test_start_unknown_command
    req = [
      "GET /admin?cmd=hoge HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(400, res.status)
  end

  def create_chan_info(name, type, bitrate)
    chaninfo = PCSCore::AtomCollection.new
    chaninfo.set_chan_info_name(name)
    chaninfo.set_chan_info_type(type)
    chaninfo.set_chan_info_bitrate(bitrate)
    PCSCore::ChannelInfo.new(chaninfo)
  end

  def create_chan_track(name, album, creator)
    chantrack = PCSCore::AtomCollection.new
    chantrack.set_chan_track_title(name)
    chantrack.set_chan_track_album(album)
    chantrack.set_chan_track_creator(creator)
    PCSCore::ChannelTrack.new(chantrack)
  end

  def create_host
    host = PCSCore::HostBuilder.new
    host.SessionID = System::Guid.new_guid
    host.BroadcastID = System::Guid.new_guid
    host.local_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.1'), 7144)
    host.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.parse('192.168.1.1'), 7144)
    host.relay_count  = rand(10)
    host.direct_count = rand(10)
    host.is_firewalled = rand(2)==0
    host.is_tracker    = rand(2)==0
    host.is_relay_full   = rand(2)==0
    host.is_direct_full  = rand(2)==0
    host.is_receiving    = rand(2)==0
    host.is_control_full = rand(2)==0
    host.extra.set_host_uphost_hops(rand(10))
    host.extra.set_host_version(1218)
    host.to_host
  end

  def assert_channel(channel, chanxml)
    assert_equal(channel.ChannelID.ToString('N').to_s.upcase, chanxml.attributes['id'])
    assert_equal(channel.channel_info.name         || '', chanxml.attributes['name'])
    assert_equal(channel.channel_info.bitrate,            chanxml.attributes['bitrate'].to_i)
    assert_equal(channel.channel_info.comment      || '', chanxml.attributes['comment'])
    assert_equal(channel.channel_info.genre        || '', chanxml.attributes['genre'])
    assert_equal(channel.channel_info.content_type || '', chanxml.attributes['type'])
    assert_equal(channel.channel_info.URL          || '', chanxml.attributes['url'])
    assert_match(/\d+/, chanxml.attributes['uptime'])
    assert_match(/\d+/, chanxml.attributes['age'])
    assert_equal('0',   chanxml.attributes['skip'])
    assert_equal('0',   chanxml.attributes['bcflags'])
    track = chanxml.elements['track']
    assert_equal(channel.channel_track.name    || '', track.attributes['title'])
    assert_equal(channel.channel_track.album   || '', track.attributes['album'])
    assert_equal(channel.channel_track.genre   || '', track.attributes['genre'])
    assert_equal(channel.channel_track.creator || '', track.attributes['artist'])
    assert_equal(channel.channel_track.URL     || '', track.attributes['contact'])
    relay = chanxml.elements['relay']
    assert_equal(channel.local_directs, relay.attributes['listeners'].to_i)
    assert_equal(channel.local_relays,  relay.attributes['relays'].to_i)
    assert_equal(channel.nodes.count,   relay.attributes['hosts'].to_i)
    assert_equal(channel.status.to_s,   relay.attributes['status'])
    hits = chanxml.elements['hits']
    assert_equal(channel.nodes.count, hits.attributes['hosts'].to_i)
    assert_equal(channel.nodes.inject(0) {|r,n| r+n.direct_count }, hits.attributes['listeners'].to_i)
    assert_equal(channel.nodes.inject(0) {|r,n| r+n.relay_count }, hits.attributes['relays'].to_i)
    assert_equal(channel.nodes.select {|n| n.is_firewalled }.size, hits.attributes['firewalled'].to_i)
    assert_equal(channel.nodes.collect {|n| n.hops==0 ? 0xFFFFFFFF : n.hops }.min || 0, hits.attributes['closest'].to_i)
    assert_equal(channel.nodes.collect {|n| n.hops }.max || 0, hits.attributes['furthest'].to_i)
    assert_match(/\d+/, hits.attributes['newest'])
    hosts = hits.elements.to_a('host')
    assert_equal(channel.nodes.count, hosts.size)
    channel.nodes.to_a.zip(hosts).each do |node, host|
      assert_equal(node.global_end_point.address.to_s, host.attributes['ip'])
      assert_equal(node.hops, host.attributes['hops'].to_i)
      assert_equal(node.direct_count, host.attributes['listeners'].to_i)
      assert_equal(node.relay_count, host.attributes['relays'].to_i)
      assert_equal(node.uptime.total_seconds.to_i, host.attributes['uptime'].to_i)
      assert_equal(node.is_firewalled ? 1 : 0, host.attributes['push'].to_i)
      assert_equal(node.is_relay_full ? 0 : 1, host.attributes['relay'].to_i)
      assert_equal(node.is_direct_full ? 0 : 1, host.attributes['direct'].to_i)
      assert_equal(node.is_control_full ? 0 : 1, host.attributes['cin'].to_i)
      assert_equal(node.is_tracker ? 1 : 0, host.attributes['tracker'].to_i)
      assert_equal(0, host.attributes['stable'].to_i)
      assert_match(/\d+/, host.attributes['update'])
    end
  end

  def test_start_viewxml
    channel0 = PCSCore::Channel.new(
      @app.peercast,
      System::Guid.new_guid,
      System::Uri.new('pcp://example.com'))
    channel0.channel_info = create_chan_info('Foo ch', 'WMV', 774)
    5.times do
      channel0.add_node(create_host)
    end
    @app.peercast.add_channel(channel0)
    channel1 = PCSCore::Channel.new(
      @app.peercast,
      System::Guid.new_guid,
      System::Uri.new('pcp://example.com'))
    channel1.channel_info  = create_chan_info('Bar ch', 'OGG', 7144)
    channel1.channel_track = create_chan_track('PeerCastStation', 'PeCa', 'kumaryu')
    @app.peercast.add_channel(channel1)

    req = [
      "GET /admin?cmd=viewxml HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(200, res.status)
    assert_equal('text/xml', res.headers['Content-Type'.upcase])
    assert_equal(res.body.bytesize, res.headers['Content-Length'.upcase].to_i)
    doc = REXML::Document.new(res.body)
    assert_equal('peercast', doc.root.name)

    servent = doc.root.elements['servent']
    assert_match(/\d+/, servent.attributes['uptime'])

    bandwidth = doc.root.elements['bandwidth']
    assert_equal(7144+774, bandwidth.attributes['in'].to_i)
    assert_equal(0,        bandwidth.attributes['out'].to_i)

    connections = doc.root.elements['connections']
    assert_equal(0, connections.attributes['total'].to_i)
    assert_equal(0, connections.attributes['relays'].to_i)
    assert_equal(0, connections.attributes['direct'].to_i)

    channels_relayed = doc.root.elements['channels_relayed']
    assert_equal(2, channels_relayed.attributes['total'].to_i)
    channels = channels_relayed.elements.to_a('channel')
    [channel0, channel1].zip(channels).each do |channel, chanxml|
      assert_channel(channel, chanxml)
    end

    channels_found = doc.root.elements['channels_found']
    assert_equal(2, channels_relayed.attributes['total'].to_i)
    channels = channels_relayed.elements.to_a('channel')
    [channel0, channel1].zip(channels).each do |channel, chanxml|
      assert_channel(channel, chanxml)
    end
  end

  def test_start_viewxml_head
    req = [
      "HEAD /admin?cmd=viewxml HTTP/1.0",
    ].join("\r\n") + "\r\n\r\n"
    os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
    os.start
    os.join
    res = parse_response(@output_stream.to_array.to_a.pack('C*'))
    assert_equal(200, res.status)
    assert_equal('text/xml', res.headers['Content-Type'.upcase])
    assert(0!=res.headers['Content-Length'.upcase].to_i)
    assert_equal('', res.body)
  end
end

