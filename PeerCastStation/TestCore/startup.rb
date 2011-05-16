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
require 'System.Core'
require 'PeerCastStation.Core.dll'

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

require 'test_core'
require 'test_core_host'
require 'test_core_peercast'
require 'test_core_atom'
require 'test_core_channel'
require 'test_core_accesscontroller'
require 'test_core_queuedsynchronizationcontext'
