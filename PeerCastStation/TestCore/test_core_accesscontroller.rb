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

class TC_CoreAccessController < Test::Unit::TestCase
  def setup
    @peercast = PeerCastStation::Core::PeerCast.new
  end
  
  def teardown
    @peercast.close if @peercast and not @peercast.is_closed
  end

  def new_output(type, is_local, bitrate)
    output_stream_type = PeerCastStation::Core::OutputStreamType.play
    case type
    when :play
      output_stream_type = PeerCastStation::Core::OutputStreamType.play
    when :relay
      output_stream_type = PeerCastStation::Core::OutputStreamType.relay
    end
    output = MockOutputStream.new(output_stream_type)
    output.is_local = is_local
    output.upstream_rate = bitrate
    output
  end

  def new_channel(bitrate)
    channel = PeerCastStation::Core::Channel.new(@peercast, System::Guid.empty, System::Uri.new('mock://localhost'))
    chan_info = PeerCastStation::Core::AtomCollection.new
    chan_info.set_chan_info_bitrate(bitrate)
    channel.channel_info = PeerCastStation::Core::ChannelInfo.new(chan_info)
    @peercast.add_channel(channel)
    channel
  end

  def test_construct
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    assert_equal(@peercast, ac.PeerCast)
    assert_equal(0, ac.max_relays)
    assert_equal(0, ac.max_relays_per_channel)
    assert_equal(0, ac.max_plays)
    assert_equal(0, ac.max_plays_per_channel)
    assert_equal(0, ac.max_upstream_rate)
  end

  def test_property_changed
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    log = []
    ac.PropertyChanged do |sender, args|
      assert_equal(ac, sender)
      log << args.PropertyName
    end
    ac.max_relays             = 4
    ac.max_relays_per_channel = 3
    ac.max_plays              = 2
    ac.max_plays_per_channel  = 1
    ac.max_upstream_rate      = 10000
    assert_equal('MaxRelays',           log[0])
    assert_equal('MaxRelaysPerChannel', log[1])
    assert_equal('MaxPlays',            log[2])
    assert_equal('MaxPlaysPerChannel',  log[3])
    assert_equal('MaxUpstreamRate',     log[4])

    log.clear
    ac.max_relays             = 4
    ac.max_relays_per_channel = 3
    ac.max_plays              = 2
    ac.max_plays_per_channel  = 1
    ac.max_upstream_rate      = 10000
    assert_equal(0, log.size)
  end

  def test_is_channel_relayable_empty
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel = new_channel(7144)
    assert(ac.is_channel_relayable(channel))
  end

  def test_is_channel_relayable_all_reset
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel = new_channel(7144)
    channel.add_output_stream(new_output(:play, true, 0))
    channel.add_output_stream(new_output(:relay, false, 7144))
    assert(ac.is_channel_relayable(channel))
  end

  def test_is_channel_relayable_max_relays
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel1 = new_channel(7144)
    channel1.add_output_stream(new_output(:play, true, 0))
    channel1.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 1
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 0
    assert(!ac.is_channel_relayable(channel1))

    ac.max_relays             = 2
    ac.max_relays_per_channel = 0
    ac.max_plays              = 1
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 0
    assert(ac.is_channel_relayable(channel1))

    channel2 = new_channel(7144)
    channel2.add_output_stream(new_output(:play, true, 0))
    channel2.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 2
    ac.max_relays_per_channel = 0
    ac.max_plays              = 1
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 0
    assert(!ac.is_channel_relayable(channel1))
    assert(!ac.is_channel_relayable(channel2))
  end

  def test_is_channel_relayable_upstream_rate_max_relay
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel = new_channel(7144)
    channel.add_output_stream(new_output(:play, true, 0))
    channel.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 7144
    assert(!ac.is_channel_relayable(channel))
  end

  def test_is_channel_relayable_upstream_rate_max_play
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel = new_channel(7144)
    channel.add_output_stream(new_output(:play, false, 7144))
    channel.add_output_stream(new_output(:relay, true, 0))
    ac.max_relays             = 2
    ac.max_relays_per_channel = 0
    ac.max_plays              = 1
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 7144
    assert(!ac.is_channel_relayable(channel))
  end

  def test_is_channel_relayable_upstream_rate
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel1 = new_channel(7144)
    channel1.add_output_stream(new_output(:play, true, 0))
    channel1.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 7144*2
    assert(ac.is_channel_relayable(channel1))

    channel2 = new_channel(7144)
    channel2.add_output_stream(new_output(:play, false, 7144))
    channel2.add_output_stream(new_output(:relay, true, 0))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 7144*2
    assert(!ac.is_channel_relayable(channel1))
    assert(!ac.is_channel_relayable(channel2))
  end

  def test_is_channel_relayable_upstream_rate_by_output_stream
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel1 = new_channel(7144)
    channel1.add_output_stream(new_output(:play, true, 0))
    channel1.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 7144
    assert(!ac.is_channel_relayable(channel1))

    os = new_output(:relay, true, 0)
    assert(ac.is_channel_relayable(channel1, os))
  end

  def test_is_channel_relayable_per_channel
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel1 = new_channel(7144)
    channel1.add_output_stream(new_output(:play, true, 0))
    channel1.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 2
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 0
    assert(ac.is_channel_relayable(channel1))

    channel2 = new_channel(7144)
    channel2.add_output_stream(new_output(:play, true, 0))
    channel2.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 2
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 1
    ac.max_upstream_rate      = 0
    assert(ac.is_channel_relayable(channel1))
    assert(ac.is_channel_relayable(channel2))

    channel2.add_output_stream(new_output(:play, true, 0))
    channel2.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 2
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 1
    ac.max_upstream_rate      = 0
    assert(ac.is_channel_relayable(channel1))
    assert(!ac.is_channel_relayable(channel2))
  end

  def test_is_channel_playable
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel = new_channel(7144)
    assert(ac.is_channel_playable(channel))
  end

  def test_is_channel_playable_all_reset
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel = new_channel(7144)
    channel.add_output_stream(new_output(:play, true, 0))
    channel.add_output_stream(new_output(:relay, false, 7144))
    assert(ac.is_channel_playable(channel))
  end

  def test_is_channel_playable_max_plays
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel1 = new_channel(7144)
    channel1.add_output_stream(new_output(:play, true, 0))
    channel1.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 1
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 0
    assert(!ac.is_channel_playable(channel1))

    ac.max_relays             = 1
    ac.max_relays_per_channel = 0
    ac.max_plays              = 2
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 0
    assert(ac.is_channel_playable(channel1))

    channel2 = new_channel(7144)
    channel2.add_output_stream(new_output(:play, true, 0))
    channel2.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 1
    ac.max_relays_per_channel = 0
    ac.max_plays              = 2
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 0
    assert(!ac.is_channel_playable(channel1))
    assert(!ac.is_channel_playable(channel2))
  end

  def test_is_channel_playable_upstream_rate_max_relay
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel = new_channel(7144)
    channel.add_output_stream(new_output(:play, true, 0))
    channel.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 7144
    assert(!ac.is_channel_playable(channel))
  end

  def test_is_channel_relayable_upstream_rate_max_play
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel = new_channel(7144)
    channel.add_output_stream(new_output(:play, false, 7144))
    channel.add_output_stream(new_output(:relay, true, 0))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 7144
    assert(!ac.is_channel_playable(channel))
  end

  def test_is_channel_playable_upstream_rate
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel1 = new_channel(7144)
    channel1.add_output_stream(new_output(:play, true, 0))
    channel1.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 7144*2
    assert(ac.is_channel_playable(channel1))

    channel2 = new_channel(7144)
    channel2.add_output_stream(new_output(:play, false, 7144))
    channel2.add_output_stream(new_output(:relay, true, 0))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 7144*2
    assert(!ac.is_channel_playable(channel1))
    assert(!ac.is_channel_playable(channel2))
  end

  def test_is_channel_playable_upstream_rate_by_output_stream
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel1 = new_channel(7144)
    channel1.add_output_stream(new_output(:play, true, 0))
    channel1.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 0
    ac.max_upstream_rate      = 7144
    assert(!ac.is_channel_playable(channel1))

    os = new_output(:play, true, 0)
    assert(ac.is_channel_playable(channel1, os))
  end

  def test_is_channel_playable_per_channel
    ac = PeerCastStation::Core::AccessController.new(@peercast)
    channel1 = new_channel(7144)
    channel1.add_output_stream(new_output(:play, true, 0))
    channel1.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 0
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 2
    ac.max_upstream_rate      = 0
    assert(ac.is_channel_playable(channel1))

    channel2 = new_channel(7144)
    channel2.add_output_stream(new_output(:play, true, 0))
    channel2.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 1
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 2
    ac.max_upstream_rate      = 0
    assert(ac.is_channel_playable(channel1))
    assert(ac.is_channel_playable(channel2))

    channel2.add_output_stream(new_output(:play, true, 0))
    channel2.add_output_stream(new_output(:relay, false, 7144))
    ac.max_relays             = 0
    ac.max_relays_per_channel = 1
    ac.max_plays              = 0
    ac.max_plays_per_channel  = 2
    ac.max_upstream_rate      = 0
    assert(ac.is_channel_playable(channel1))
    assert(!ac.is_channel_playable(channel2))
  end
end

