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
require 'PeerCastStation.Core.dll'
require 'test/unit'
using_clr_extensions PeerCastStation::Core

class TC_CoreHost < Test::Unit::TestCase
  def test_construct
    obj = PeerCastStation::Core::Host.new
    assert_nil(obj.local_end_point)
    assert_nil(obj.global_end_point)
    assert_equal(System::Guid.empty, obj.SessionID)
    assert_equal(System::Guid.empty, obj.BroadcastID)
    assert(obj.is_firewalled)
    assert(obj.extensions)
    assert_equal(0, obj.extensions.count)
    assert_equal(0, obj.relay_count)
    assert_equal(0, obj.direct_count)
    assert(!obj.is_relay_full)
    assert(!obj.is_direct_full)
    assert(!obj.is_control_full)
    assert(!obj.is_receiving)
    assert_not_nil(obj.extensions)
    assert_equal(0, obj.extensions.count)
    assert_not_nil(obj.extra)
    assert_equal(0, obj.extra.count)
    assert_not_equal(0, obj.last_updated.ticks)
  end
  
  def test_changed
    log = []
    obj = PeerCastStation::Core::Host.new
    obj.property_changed {|sender, e| log << e.property_name }
    obj.local_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    obj.global_end_point = System::Net::IPEndPoint.new(System::Net::IPAddress.any, 7144)
    obj.SessionID = System::Guid.new_guid
    obj.BroadcastID = System::Guid.new_guid
    obj.is_firewalled = false
    obj.relay_count = 1
    obj.direct_count = 1
    obj.is_relay_full = true
    obj.is_direct_full = true
    obj.is_control_full = true
    obj.is_receiving = true
    obj.extra.add(
      PeerCastStation::Core::Atom.new(
        PeerCastStation::Core::ID4.new('test'.to_clr_string), 'foo'.to_clr_string))
    obj.extensions.add('test')
    assert_in_delta(System::Environment.tick_count, obj.last_updated.total_milliseconds, 100)
    assert_equal(13, log.size)
    assert_equal('LocalEndPoint',  log[0])
    assert_equal('GlobalEndPoint', log[1])
    assert_equal('SessionID',      log[2])
    assert_equal('BroadcastID',    log[3])
    assert_equal('IsFirewalled',   log[4])
    assert_equal('RelayCount',     log[5])
    assert_equal('DirectCount',    log[6])
    assert_equal('IsRelayFull',    log[7])
    assert_equal('IsDirectFull',   log[8])
    assert_equal('IsControlFull',  log[9])
    assert_equal('IsReceiving',    log[10])
    assert_equal('Extra',          log[11])
    assert_equal('Extensions',     log[12])
  end
end

