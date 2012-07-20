
require 'test/unit'
require File.join(File.dirname(__FILE__), '..', 'TestCommon', 'utils.rb')
TestUtils.require_peercaststation 'Core'
TestUtils.require_peercaststation 'UI.HTTP'
TestUtils.explicit_extensions PeerCastStation::Core::AtomCollectionExtensions
require 'test/unit'

module TestUI_HTTP
  PCSCore   = PeerCastStation::Core     unless defined?(PCSCore)
  PCSHTTPUI = PeerCastStation::UI::HTTP unless defined?(PCSHTTPUI)

  class TC_HTMLHostFactory < Test::Unit::TestCase
    def test_name
      factory = PCSHTTPUI::HTMLHostFactory.new
      assert_kind_of(System::String, factory.name)
    end

    def test_create_user_interface
      factory = PCSHTTPUI::HTMLHostFactory.new
      assert_kind_of(PCSHTTPUI::HTMLHost, factory.create_user_interface)
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

  class TC_HTMLHost < Test::Unit::TestCase
    def test_construct
      host = PCSHTTPUI::HTMLHost.new
      assert_equal(
        File.join(File.dirname(File.expand_path(PCSHTTPUI::HTMLHost.to_clr_type.assembly.location)), 'html'),
        host.physical_path.gsub('\\', '/'))
      assert_equal(host.virtual_path, '/html/')
    end

    def test_start_stop
      app = TestApplication.new
      host = PCSHTTPUI::HTMLHost.new
      host.start(app)
      assert_equal(1, app.peercast.output_stream_factories.count)
      assert_kind_of(PCSHTTPUI::HTMLHost::HTMLHostOutputStreamFactory, app.peercast.output_stream_factories[0])
      host.stop
      assert_equal(0, app.peercast.output_stream_factories.count)
    end
  end

  class TCHTMLHostOutputStreamFactory < Test::Unit::TestCase
    def test_construct
      app     = TestApplication.new
      host    = PCSHTTPUI::HTMLHost.new
      factory = PCSHTTPUI::HTMLHost::HTMLHostOutputStreamFactory.new(host, app.peercast)
      assert_same(host, factory.owner)
      assert(factory.priority>0)
      assert(factory.priority<100)
    end

    def test_parse_channel_id
      app     = TestApplication.new
      host    = PCSHTTPUI::HTMLHost.new
      factory = PCSHTTPUI::HTMLHost::HTMLHostOutputStreamFactory.new(host, app.peercast)
      req = [
        "GET / HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      assert_nil(factory.ParseChannelID(req))
      req = [
        "GET #{host.virtual_path} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      assert_equal(System::Guid.empty, factory.ParseChannelID(req))
    end

    def test_create
      app     = TestApplication.new
      host    = PCSHTTPUI::HTMLHost.new
      factory = PCSHTTPUI::HTMLHost::HTMLHostOutputStreamFactory.new(host, app.peercast)
      req = [
        "GET #{host.virtual_path} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      assert_kind_of(
        PCSHTTPUI::HTMLHost::HTMLHostOutputStream,
        factory.create(nil, nil, nil, System::Guid.empty, req))
    end

    def test_create_with_other_path
      app     = TestApplication.new
      host    = PCSHTTPUI::HTMLHost.new
      factory = PCSHTTPUI::HTMLHost::HTMLHostOutputStreamFactory.new(host, app.peercast)
      host.virtual_path = '/hogefuga/'
      req = [
        "GET #{host.virtual_path} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      assert_kind_of(
        PCSHTTPUI::HTMLHost::HTMLHostOutputStream,
        factory.create(nil, nil, nil, System::Guid.empty, req))
    end
  end

  class TCHTMLHostOutputStream < Test::Unit::TestCase
    def setup
      @app             = TestApplication.new
      @host            = PCSHTTPUI::HTMLHost.new
      @host.virtual_path = '/hoge/fuga/'
      @host.physical_path = File.join(File.dirname(__FILE__), 'fixtures')
      @input_stream    = System::IO::MemoryStream.new
      @output_stream   = System::IO::MemoryStream.new
      @remote_endpoint = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
      @factory = PCSHTTPUI::HTMLHost::HTMLHostOutputStreamFactory.new(@host, @app.peercast)
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
        "GET #{@host.virtual_path} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
      assert_equal(PCSCore::OutputStreamType.interface, os.output_stream_type)
    end

    def test_start_method_not_allowed
      req = [
        "POST #{File.join(@host.virtual_path, 'index.html')} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
      os.start
      os.join
      res = parse_response(@output_stream.to_array.to_a.pack('C*'))
      assert_equal(405, res.status)
    end

    def test_start_forbidden
      req = [
        "GET #{File.join(@host.virtual_path, '..', 'index.html')} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
      os.start
      os.join
      res = parse_response(@output_stream.to_array.to_a.pack('C*'))
      assert_equal(403, res.status)
    end

    def test_start_get
      req = [
        "GET #{File.join(@host.virtual_path, 'foo', 'bar.html')} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
      os.start
      os.join
      res = parse_response(@output_stream.to_array.to_a.pack('C*'))
      data = File.open(File.join(@host.physical_path, 'foo/bar.html'), 'rb') {|f| f.read }
      assert_equal(200, res.status)
      assert_equal('text/html', res.headers['Content-Type'.upcase])
      assert_equal(data.bytesize, res.headers['Content-Length'.upcase].to_i)
      assert_equal(data, res.body)
    end

    def test_start_head
      req = [
        "HEAD #{File.join(@host.virtual_path, 'foo', 'baz.txt')} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
      os.start
      os.join
      res = parse_response(@output_stream.to_array.to_a.pack('C*'))
      data = File.open(File.join(@host.physical_path, 'foo', 'baz.txt'), 'rb') {|f| f.read }
      assert_equal(200, res.status)
      assert_equal('text/plain', res.headers['Content-Type'.upcase])
      assert_equal(data.bytesize, res.headers['Content-Length'.upcase].to_i)
      assert_equal('', res.body)
    end

    def test_start_not_found
      req = [
        "GET #{File.join(@host.virtual_path, 'hoge.html')} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
      os.start
      os.join
      res = parse_response(@output_stream.to_array.to_a.pack('C*'))
      assert_equal(404, res.status)
    end

    def test_start_request_directory_index
      req = [
        "GET #{@host.virtual_path} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
      os.start
      os.join
      res = parse_response(@output_stream.to_array.to_a.pack('C*'))
      data = File.open(File.join(@host.physical_path, 'index.html'), 'rb') {|f| f.read }
      assert_equal(200, res.status)
      assert_equal('text/html', res.headers['Content-Type'.upcase])
      assert_equal(data.bytesize, res.headers['Content-Length'.upcase].to_i)
      assert_equal(data, res.body)
    end

    def test_start_request_directory_without_index
      req = [
        "GET #{File.join(@host.virtual_path, 'foo/')} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
      os.start
      os.join
      res = parse_response(@output_stream.to_array.to_a.pack('C*'))
      assert_equal(403, res.status)
    end

    def test_start_request_directory_not_found
      req = [
        "GET #{File.join(@host.virtual_path, 'hoge/')} HTTP/1.0",
      ].join("\r\n") + "\r\n\r\n"
      os = @factory.create(@input_stream, @output_stream, @remote_endpoint, System::Guid.empty, req)
      os.start
      os.join
      res = parse_response(@output_stream.to_array.to_a.pack('C*'))
      assert_equal(404, res.status)
    end
  end
end

