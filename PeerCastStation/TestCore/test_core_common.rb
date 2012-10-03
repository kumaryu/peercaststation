
require 'test/unit'
require File.join(File.dirname(__FILE__), '..', 'TestCommon', 'utils.rb')
TestUtils.require_peercaststation 'Core'
TestUtils.explicit_extensions PeerCastStation::Core::AtomCollectionExtensions

module TestCore
  class MockSourceStreamFactory
    include PeerCastStation::Core::ISourceStreamFactory
    def initialize
      @log = []
    end
    attr_reader :log
    
    def name
      'MockSourceStream'
    end
    
    def scheme
      'mock'
    end
    
    def create(channel, uri, reader=nil)
      @log << [:create, channel, uri, reader]
      MockSourceStream.new(channel, uri, reader)
    end
  end

  class MockSourceStream
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

    def send_rate
      0.0
    end

    def recv_rate
      0.0
    end
  end

  class MockOutputStream
    include PeerCastStation::Core::IOutputStream
    
    def initialize(type=0)
      @type = type
      @remote_endpoint = nil
      @upstream_rate = 0
      @is_local = false
      @log = []
      @stopped = []
    end
    attr_reader :log
    attr_accessor :remote_endpoint, :upstream_rate, :is_local

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

    def send_rate
      0.0
    end

    def recv_rate
      0.0
    end
  end

  class MockOutputStreamFactory
    include PeerCastStation::Core::IOutputStreamFactory
    
    def initialize(type=0)
      @type = type
      @priority = 0
      @log = []
    end
    attr_reader :log, :priority
    
    def name
      'MockOutputStream'
    end
    
    def output_stream_type
      @type
    end

    def ParseChannelID(header)
      @log << [:parse_channel_id, header]
      header = header.to_a.pack('C*')
      if /^mock ([a-fA-F0-9]{32})/=~header then
        System::Guid.new($1.to_clr_string)
      else
        nil
      end
    end
    
    def create(input_stream, output_stream, remote_endpoint, channel_id, header)
      @log << [:create, input_stream, output_stream, remote_endpoint, channel_id, header]
      MockOutputStream.new(@type)
    end
  end
  
end

