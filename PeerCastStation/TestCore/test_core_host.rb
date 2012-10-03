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
require 'test_core_common'

module TestCore
  class TC_CoreHostBuilder < Test::Unit::TestCase
    def test_construct
      obj = PeerCastStation::Core::HostBuilder.new
      assert_nil(obj.local_end_point)
      assert_nil(obj.global_end_point)
      assert_equal(System::Guid.empty, obj.SessionID)
      assert_equal(System::Guid.empty, obj.BroadcastID)
      assert(!obj.is_firewalled)
      assert_equal(0, obj.extensions.count)
      assert_equal(0, obj.relay_count)
      assert_equal(0, obj.direct_count)
      assert(!obj.is_tracker)
      assert(!obj.is_relay_full)
      assert(!obj.is_direct_full)
      assert(!obj.is_control_full)
      assert(!obj.is_receiving)
      assert_not_nil(obj.extensions)
      assert_equal(0, obj.extensions.count)
      assert_not_nil(obj.extra)
      assert_equal(0, obj.extra.count)
      assert(obj.GetType.get_custom_attributes(System::SerializableAttribute.to_clr_type, true).length==0)
      assert(!obj.respond_to?(:create_obj_ref))
    end
    
    def test_construct_from_host
      extra = PeerCastStation::Core::AtomCollection.new
      extra.add(PeerCastStation::Core::Atom.new(
        PeerCastStation::Core::ID4.new('test'.to_clr_string), 'foo'.to_clr_string))
      extensions = System::Array[System::String].new(['test'])
      host = PeerCastStation::Core::Host.new(
        System::Guid.new_guid, # SessionID
        System::Guid.new_guid, # BroadcastID
        System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144), # LocalEndPoint
        System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144), # GlobalEndPoint
        1, # RelayCount
        1, # DirectCount
        false, # IsFirewalled
        true, # IsTracker
        true, # IsRelayFull
        true, # IsDirectFull
        true, # IsReceiving
        true, # IsControlFull
        extensions, # Extensions
        extra) # Extra
      obj = PeerCastStation::Core::HostBuilder.new(host)
      assert_equal(host.LocalEndPoint , obj.LocalEndPoint )
      assert_equal(host.GlobalEndPoint, obj.GlobalEndPoint)
      assert_equal(host.SessionID     , obj.SessionID     )
      assert_equal(host.BroadcastID   , obj.BroadcastID   )
      assert_equal(host.IsFirewalled  , obj.IsFirewalled  )
      assert_equal(host.RelayCount    , obj.RelayCount    )
      assert_equal(host.DirectCount   , obj.DirectCount   )
      assert_equal(host.IsTracker     , obj.IsTracker     )
      assert_equal(host.IsRelayFull   , obj.IsRelayFull   )
      assert_equal(host.IsDirectFull  , obj.IsDirectFull  )
      assert_equal(host.IsControlFull , obj.IsControlFull )
      assert_equal(host.IsReceiving   , obj.IsReceiving   )
      assert_equal(host.Extra.count   , obj.Extra.count   )
      obj.Extra.count.times do |i|
        assert_equal(host.Extra[i], obj.Extra[i])
      end
      assert_equal(host.Extensions.count, obj.Extensions.count)
      obj.Extensions.count.times do |i|
        assert_equal(host.Extensions[i], obj.Extensions[i])
      end
    end
    
    def test_construct_from_host_builder
      src = PeerCastStation::Core::HostBuilder.new
      src.local_end_point  = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
      src.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
      src.SessionID        = System::Guid.new_guid
      src.BroadcastID      = System::Guid.new_guid
      src.is_firewalled    = false
      src.relay_count      = 1
      src.direct_count     = 1
      src.is_tracker       = true
      src.is_relay_full    = true
      src.is_direct_full   = true
      src.is_control_full  = true
      src.is_receiving     = true
      src.extra.add(
        PeerCastStation::Core::Atom.new(
          PeerCastStation::Core::ID4.new('test'.to_clr_string), 'foo'.to_clr_string))
      src.extensions.add('test')
      obj = PeerCastStation::Core::HostBuilder.new(src)
      assert_equal(src.LocalEndPoint , obj.LocalEndPoint )
      assert_equal(src.GlobalEndPoint, obj.GlobalEndPoint)
      assert_equal(src.SessionID     , obj.SessionID     )
      assert_equal(src.BroadcastID   , obj.BroadcastID   )
      assert_equal(src.IsFirewalled  , obj.IsFirewalled  )
      assert_equal(src.RelayCount    , obj.RelayCount    )
      assert_equal(src.DirectCount   , obj.DirectCount   )
      assert_equal(src.IsTracker     , obj.IsTracker     )
      assert_equal(src.IsRelayFull   , obj.IsRelayFull   )
      assert_equal(src.IsDirectFull  , obj.IsDirectFull  )
      assert_equal(src.IsControlFull , obj.IsControlFull )
      assert_equal(src.IsReceiving   , obj.IsReceiving   )
      assert_equal(src.Extra.count   , obj.Extra.count   )
      obj.Extra.count.times do |i|
        assert_equal(src.Extra[i], obj.Extra[i])
      end
      assert_equal(src.Extensions.count, obj.Extensions.count)
      obj.Extensions.count.times do |i|
        assert_equal(src.Extensions[i], obj.Extensions[i])
      end
    end
    
    def test_to_host
      obj = PeerCastStation::Core::HostBuilder.new
      obj.local_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
      obj.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
      obj.SessionID = System::Guid.new_guid
      obj.BroadcastID = System::Guid.new_guid
      obj.is_firewalled   = false
      obj.relay_count     = 1
      obj.direct_count    = 1
      obj.is_tracker      = true
      obj.is_relay_full   = true
      obj.is_direct_full  = true
      obj.is_control_full = true
      obj.is_receiving    = true
      obj.extra.add(
        PeerCastStation::Core::Atom.new(
          PeerCastStation::Core::ID4.new('test'.to_clr_string), 'foo'.to_clr_string))
      obj.extensions.add('test')
      host = obj.to_host
      assert_equal(obj.LocalEndPoint,  host.LocalEndPoint)
      assert_equal(obj.GlobalEndPoint, host.GlobalEndPoint)
      assert_equal(obj.SessionID,      host.SessionID)
      assert_equal(obj.BroadcastID,    host.BroadcastID)
      assert_equal(obj.IsFirewalled,   host.IsFirewalled)
      assert_equal(obj.RelayCount,     host.RelayCount)
      assert_equal(obj.DirectCount,    host.DirectCount)
      assert_equal(obj.IsTracker,      host.IsTracker)
      assert_equal(obj.IsRelayFull,    host.IsRelayFull)
      assert_equal(obj.IsDirectFull,   host.IsDirectFull)
      assert_equal(obj.IsControlFull,  host.IsControlFull)
      assert_equal(obj.IsReceiving,    host.IsReceiving)
      assert_equal(obj.Extra.count,    host.Extra.count)
      host.Extra.count.times do |i|
        assert_equal(obj.Extra[i], host.Extra[i])
      end
      assert_equal(obj.Extensions.count, host.Extensions.count)
      host.Extensions.count.times do |i|
        assert_equal(obj.Extensions[i], host.Extensions[i])
      end
    end
  end

  class TC_CoreHost < Test::Unit::TestCase
    def test_construct
      extra = PeerCastStation::Core::AtomCollection.new
      extra.add(PeerCastStation::Core::Atom.new(
        PeerCastStation::Core::ID4.new('test'.to_clr_string), 'foo'.to_clr_string))
      extensions = System::Array[System::String].new(['test'])
      host = PeerCastStation::Core::Host.new(
        System::Guid.new_guid, # SessionID
        System::Guid.new_guid, # BroadcastID
        System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144), # LocalEndPoint
        System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144), # GlobalEndPoint
        1, # RelayCount
        1, # DirectCount
        false, # IsFirewalled
        true, # IsTracker
        true, # IsRelayFull
        true, # IsDirectFull
        true, # IsReceiving
        true, # IsControlFull
        extensions, # Extesnsions
        extra) # Extra
      assert_not_equal(System::Guid.empty, host.SessionID)
      assert_not_equal(System::Guid.empty, host.BroadcastID)
      assert_equal(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144), host.LocalEndPoint)
      assert_equal(System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144), host.GlobalEndPoint)
      assert_equal(1, host.RelayCount)
      assert_equal(1, host.DirectCount)
      assert(!host.IsFirewalled)
      assert(host.IsTracker)
      assert(host.IsRelayFull)
      assert(host.IsDirectFull)
      assert(host.IsControlFull)
      assert(host.IsReceiving)
      assert_equal(1,      host.Extra.count)
      assert_equal('test', host.Extra[0].name.to_s)
      assert_equal(1,      host.Extensions.count)
      assert_equal('test', host.Extensions[0])
      assert_not_equal(0, host.LastUpdated)
      assert(host.GetType.get_custom_attributes(System::SerializableAttribute.to_clr_type, true).length>0)
      assert(!host.respond_to?(:create_obj_ref))
    end
  end
end

