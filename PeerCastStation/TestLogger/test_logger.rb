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
require File.join(File.dirname(__FILE__), '..', 'TestCommon', 'utils.rb')
TestUtils.require_peercaststation 'Logger'
require 'test/unit'

module TestLogger
  PCSCore = PeerCastStation::Core

  class TC_Logger < Test::Unit::TestCase
    def test_static_construct
      assert(PCSCore::Logger.writers.clr_member(System::Collections::IList, :is_read_only).call)
    end

    def test_construct
      logger = PCSCore::Logger.new('TC_Logger')
      assert_equal('TC_Logger', logger.source)
      
      logger = PCSCore::Logger.new(System::String.to_clr_type)
      assert_equal('String', logger.source)
    end

    def test_add_remove_writer
      writer = System::IO::StringWriter.new
      PCSCore::Logger.add_writer(writer)
      assert_equal(1, PCSCore::Logger.writers.count)
      assert_equal(writer, PCSCore::Logger.writers[0])
      PCSCore::Logger.remove_writer(writer)
      assert_equal(0, PCSCore::Logger.writers.count)

      PCSCore::Logger.add_writer(writer)
      PCSCore::Logger.clear_writer
      assert_equal(0, PCSCore::Logger.writers.count)
    end

    def test_output_format_string
      writer = System::IO::StringWriter.new
      PCSCore::Logger.add_writer(writer)
      PCSCore::Logger.level = PCSCore::LogLevel.debug
      logger = PCSCore::Logger.new('TC_Logger')
      logger.fatal('foo {0}', 'bar')
      logger.error('foo {0}', 'bar')
      logger.warn( 'foo {0}', 'bar')
      logger.info( 'foo {0}', 'bar')
      logger.debug('foo {0}', 'bar')
      log = writer.to_string.to_s.chomp.split(/\n/).to_a
      assert_equal(5, log.size)
      [
        /FATAL/, 
        /ERROR/, 
        /WARN/, 
        /INFO/, 
        /DEBUG/, 
      ].each_with_index do |level, i|
        assert_match(level,       log[i])
        assert_match(/TC_Logger/, log[i])
        assert_match(/foo bar/,   log[i])
      end
    end

    def test_output_format_string_error
      writer = System::IO::StringWriter.new
      PCSCore::Logger.add_writer(writer)
      PCSCore::Logger.level = PCSCore::LogLevel.debug
      logger = PCSCore::Logger.new('TC_Logger')
      assert_nothing_raised do
        logger.debug('foo {1}', 'bar')
      end
    end

    def test_output_format_exception
      writer = System::IO::StringWriter.new
      PCSCore::Logger.add_writer(writer)
      PCSCore::Logger.level = PCSCore::LogLevel.debug
      logger = PCSCore::Logger.new('TC_Logger')
      exception = begin
                    raise System::ApplicationException.new('hoge')
                  rescue System::ApplicationException => e
                    e
                  end
      [
        [:fatal, /FATAL/],
        [:error, /ERROR/],
        [:warn,  /WARN/], 
        [:info,  /INFO/], 
        [:debug, /DEBUG/],
      ].each do |method, level|
        logger.send(method, exception)
        log = writer.to_string.to_s.chomp.split(/\n/).to_a
        assert_match(level,       log[0])
        assert_match(/TC_Logger/, log[0])
        assert_match(/hoge/,      log[0])
        writer.get_string_builder.length = 0
      end
    end

    def test_level
      writer = System::IO::StringWriter.new
      logger = PCSCore::Logger.new('TC_Logger')
      PCSCore::Logger.add_writer(writer)
      [
        PCSCore::LogLevel.debug,
        PCSCore::LogLevel.info,
        PCSCore::LogLevel.warn,
        PCSCore::LogLevel.error,
        PCSCore::LogLevel.fatal,
        PCSCore::LogLevel.none,
      ].each_with_index do |level, i|
        PCSCore::Logger.level = level
        logger.fatal('hoge')
        logger.error('hoge')
        logger.warn( 'hoge')
        logger.info( 'hoge')
        logger.debug('hoge')
        log = writer.to_string.to_s.chomp.split(/\n/).to_a
        assert_match(/FATAL/, log[0]) if i<5
        assert_match(/ERROR/, log[1]) if i<4
        assert_match(/WARN/,  log[2]) if i<3
        assert_match(/INFO/,  log[3]) if i<2
        assert_match(/DEBUG/, log[4]) if i<1
        writer.get_string_builder.length = 0
      end
    end
  end
end

