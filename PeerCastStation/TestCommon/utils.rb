
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

module TestUtils
  module_function
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

