
require 'System'
require 'System.Core'

Thread.abort_on_exception = true
class System::Guid
  def to_s
    self.ToString('N').to_s
  end
end

class System::IO::MemoryStream
  def to_s
    self.to_array.to_a.pack('*')
  end
end

class MockStream < System::IO::Stream
  def initialize
    @disposed       = System::Threading::ManualResetEvent.new(false)
    @reading        = System::Threading::Mutex.new(false)
    @writing        = System::Threading::Mutex.new(false)
    @read_data      = ''
    @read_delay     = 100
    @read_exception = nil
    @write_data     = ''
    @write_delay    = 100
    @witr_exception = nil
  end
  attr_accessor :read_data
  attr_accessor :read_delay
  attr_accessor :read_exception
  attr_accessor :write_data
  attr_accessor :write_delay
  attr_accessor :write_exception

  def Dispose(disposing)
    @disposed.Set
    @reading.WaitOne
    @writing.WaitOne
    super
  end

  def disposed?
    @disposed.WaitOne(0)
  end

  def CanRead
    !@disposed.WaitOne(0)
  end

  def CanWrite
    !@disposed.WaitOne(0)
  end

  def CanSeek
    false
  end

  def Read(buffer, offset, count)
    @reading.WaitOne
    return 0 if @disposed.WaitOne(@read_delay)
    raise @read_exception if @read_exception
    len = [@read_data.bytesize, count].min
    System::Array.copy(System::Array[System::Byte].new(@read_data.bytes.to_a), 0, buffer, offset, len)
    len
  ensure
    @reading.ReleaseMutex
  end

  def Write(buffer, offset, count)
    @writing.WaitOne
    return if @disposed.WaitOne(@write_delay)
    raise @write_exception if @write_exception
    @write_data += buffer.to_a[offset, count].pack('C*')
  ensure
    @writing.ReleaseMutex
  end

  def Flush
  end

  def Length
    raise System::NotSupportedException.new
  end

  def SetLength(value)
    raise System::NotSupportedException.new
  end

  def Position
    raise System::NotSupportedException.new
  end

  def Position=(value)
    raise System::NotSupportedException.new
  end

  def Seek(offset, origin)
    raise System::NotSupportedException.new
  end
end

module TestUtils
  module_function
  def show_log(&block)
    require_peercaststation 'Logger'
    writer = System::Console.out
    level  = PeerCastStation::Core::Logger.level
    begin
      PeerCastStation::Core::Logger.add_writer(writer)
      PeerCastStation::Core::Logger.level = PeerCastStation::Core::LogLevel.debug
      block.call
    ensure
      PeerCastStation::Core::Logger.level = level
      PeerCastStation::Core::Logger.remove_writer(writer)
    end
  end

  def require_peercaststation(name, config='Debug')
    path = File.join(File.dirname(__FILE__), '..', "PeerCastStation.#{name}", 'bin', config)
    $: << path unless $:.include?(path)
    require "PeerCastStation.#{name}.dll"
  end

  def rubyize_name(name)
    unless /[A-Z]{2,}/=~name then
      name.gsub(/(?!^)[A-Z]/, '_\&').downcase
    else
      nil
    end
  end

  @@extented = []
  def explicit_extensions(klass)
    return if @@extented.include?(klass)
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
          [method.name, TestUtils.rubyize_name(method.name)].compact.each do |name|
            define_method(name) do |*args|
              klass.__send__(method.name, self, *args)
            end
          end
        end
      end
    end
    @@extented << klass
  end
end

