
require 'test/unit'
require File.join(File.dirname(__FILE__), '..', 'TestCommon', 'utils.rb')
TestUtils.require_peercaststation 'Core'
TestUtils.require_peercaststation 'PCP'
TestUtils.explicit_extensions PeerCastStation::Core::AtomCollectionExtensions
require 'thread'

module TestPCP
  PCSCore = PeerCastStation::Core
  PCSPCP  = PeerCastStation::PCP

  class PipeStream
    def recv(readbuf, result)
      if result and result.is_completed then
        begin 
          sz = @reader.end_read(result)
          @lock.synchronize do
            @reads.concat(readbuf.to_a[0,sz])
            @read_event.set
          end
        rescue System::IO::IOException
          result = nil
          @closed = true
        end
        result = nil
      end
      if not result then
        begin 
          result = @reader.begin_read(readbuf, 0, readbuf.length, nil, nil)
        rescue System::IO::IOException
          result = nil
          @closed = true
        end
      end
      result
    end

    def client_thread
      @listener.start
      @server = @listener.accept_tcp_client
      @listener.stop
      @writer = @reader = @server.get_stream
      readbuf = System::Array[System::Byte].new(8192)
      read_result  = nil
      until @closed do
        read_result = recv(readbuf, read_result)
        @lock.synchronize do
          if @writes.bytesize>0 then
            begin
              @writer.write(@writes, 0, @writes.bytesize)
              @writes = ''
            rescue System::IO::IOException
              @closed = true
            end
          end
        end
        wait_handles = [@write_event]
        wait_handles << read_result.async_wait_handle if read_result
        System::Threading::EventWaitHandle.wait_any(
          System::Array[System::Threading::EventWaitHandle].new(wait_handles.compact))
      end
        @lock.synchronize do
          if @writes.bytesize>0 then
            begin
              @writer.write(@writes, 0, @writes.bytesize)
              @writes = ''
            rescue System::ObjectDisposedException, System::IO::IOException
            end
          end
        end
      if read_result and read_result.is_completed then
        begin
          sz = @reader.end_read(read_result)
          @lock.synchronize do
            @reads.concat(readbuf.to_a[0,sz])
            @read_event.set
          end
        rescue System::ObjectDisposedException, System::IO::IOException
        end
      end
      @writer.close
      @reader.close
      @read_event.set
      @server.close
    end

    def initialize
      @listener = System::Net::Sockets::TcpListener.new(System::Net::IPAddress.any, 14483)
      @reads  = []
      @writes = ''
      @closed = false
      @write_event = System::Threading::AutoResetEvent.new(false)
      @read_event  = System::Threading::AutoResetEvent.new(false)
      @lock = Mutex.new

      @iothread = Thread.new { client_thread }
      @client = System::Net::Sockets::TcpClient.new
      @client.connect('127.0.0.1', @listener.local_endpoint.port)
      @output = @input = @client.get_stream
    end
    attr_reader :input, :output

    def read(size=nil)
      if size then
        while @reads.size<size and not @closed do
          @read_event.wait_one
        end
        @lock.synchronize do
          size = [@reads.size, size].min
          res = @reads[0, size]
          @reads = @reads[size, @reads.size-size]
          res.pack('C*')
        end
      else
        @lock.synchronize do
          res = @reads
          @reads = []
          res.pack('C*')
        end
      end
    end

    def write(bytes)
      @lock.synchronize do
        @writes.concat(bytes)
      end
      @write_event.set
    end

    def puts(string)
      write(string.to_s + "\n")
    end

    def close
      @closed = true
      @write_event.set
      @iothread.join
    end
  end
end

