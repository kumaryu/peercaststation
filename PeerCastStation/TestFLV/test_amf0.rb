# coding:utf-8
# PeerCastStation, a P2P streaming servent.
# Copyright (C) 2013 Ryuichi Sakamoto (kumaryu@kumaryu.net)
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
TestUtils.require_peercaststation 'Core'
TestUtils.require_peercaststation 'FLV'
require 'test/unit'
require 'shoulda/context'

module TestFLV
  PCSCore = PeerCastStation::Core unless defined?(PCSCore)
  PCSFLV  = PeerCastStation::FLV unless defined?(PCSFLV)

  class TC_AMF0 < Test::Unit::TestCase
    context '初期化' do
      should 'BaseStreamはReaderのコンストラクタに渡された値になる' do
        stream = System::IO::MemoryStream.new
        obj = PCSFLV::AMF0Reader.new(stream)
        assert_equal stream, obj.base_stream
      end

      should 'BaseStreamはWriterのコンストラクタに渡された値になる' do
        stream = System::IO::MemoryStream.new
        obj = PCSFLV::AMF0Writer.new(stream)
        assert_equal stream, obj.base_stream
      end
    end

    context 'read write' do
      setup do
        @stream = System::IO::MemoryStream.new
      end

      def assert_equal_amfvalue(expect, value)
        assert_equal(expect.type, value.type)
        case expect.type
        when PCSFLV::AMFValueType.null
        when PCSFLV::AMFValueType.boolean
        when PCSFLV::AMFValueType.integer
        when PCSFLV::AMFValueType.string
          assert_equal(expect.value, value.value)
        when PCSFLV::AMFValueType.ECMAArray
          assert_equal(expect.value.count, value.value.count)
          expect.value.keys.each do |key|
            assert value.value.contains_key(key)
            assert_equal_amfvalue expect.value[key], value.value[key]
          end
        end
      end

      def assert_write_and_read(type, value, stream)
        pos = stream.position
        writer = PCSFLV::AMF0Writer.new(stream)
        writer.write_value PCSFLV::AMFValue.new(type, value)
        stream.position = pos
        reader = PCSFLV::AMF0Reader.new(stream)
        val = reader.read_value
        assert_equal_amfvalue PCSFLV::AMFValue.new(type, value), val
      end

      should 'write integer then read same number' do
        pos = @stream.position
        writer = PCSFLV::AMF0Writer.new(@stream)
        writer.write_value PCSFLV::AMFValue.new(PCSFLV::AMFValueType.integer, 42)
        @stream.position = pos
        reader = PCSFLV::AMF0Reader.new(@stream)
        assert_equal_amfvalue PCSFLV::AMFValue.new(PCSFLV::AMFValueType.double, 42.0), reader.read_value
      end

      should 'write number then read same number' do
        assert_write_and_read PCSFLV::AMFValueType.double, 42.0, @stream
      end

      should 'write empty string then read empty string' do
        assert_write_and_read PCSFLV::AMFValueType.string, '', @stream
      end

      should 'write string then read same string' do
        assert_write_and_read PCSFLV::AMFValueType.string, 'hoge', @stream
        assert_write_and_read PCSFLV::AMFValueType.string, 'asdfg'*(0xFFFF/5), @stream
      end

      should 'write long string then read same string' do
        assert_write_and_read PCSFLV::AMFValueType.string, 'a'*0x10000, @stream
        assert_write_and_read PCSFLV::AMFValueType.string, 'asdfg'*0xFFFF, @stream
      end

      should 'write nil then read nil' do
        assert_write_and_read PCSFLV::AMFValueType.null, nil, @stream
      end

      def amf_value(val)
        case val
        when nil
          PCSFLV::AMFValue.new
        when true, false
          PCSFLV::AMFValue.new(val)
        when String
          PCSFLV::AMFValue.new(PCSFLV::AMFValueType.string, val)
        when Integer
          PCSFLV::AMFValue.new(val)
        when Numeric
          PCSFLV::AMFValue.new(val)
        when Hash
          PCSFLV::AMFValue.new(dictionary(val))
        when Array
          PCSFLV::AMFValue.new(System::Array[PCSFLV::AMFValue].new(val.collect {|v| amf_value(v) }))
        else
          PCSFLV::AMFValue.new(val)
        end
      end

      def dictionary(val)
        dic = System::Collections::Generic::Dictionary[System::String, PCSFLV::AMFValue].new
        val.each do |key, value|
          dic.add(key, amf_value(value))
        end
        dic
      end

      should 'write dictionary then read same dictionary' do
        dic = dictionary(
          'hoge' => 'foo',
          'fuga' => 'bar',
          'puyo' => 'baz',
          '42' => 42.0,
        )
        assert_write_and_read PCSFLV::AMFValueType.ECMAArray, dic, @stream
      end

      def array(val)
        System::Array[PCSFLV::AMFValue].new(val.collect {|v| amf_value(v) })
      end

      should 'write array then read same array' do
        ary = array([
          'hoge', 'foo',
          'fuga', 'bar',
          'puyo', 'baz',
          '42', 42.0,
        ])
        assert_write_and_read PCSFLV::AMFValueType.strict_array, ary, @stream
      end

      def amf_object(val)
        PCSFLV::AMFObject.new(dictionary(val))
      end

      should 'write object then read same object' do
        obj = amf_object(
          'hoge' => 'foo',
          'fuga' => 'bar',
          'puyo' => 'baz',
          '42' => 42.0,
        )
        assert_write_and_read PCSFLV::AMFValueType.object, obj, @stream
      end

      should 'write date then read same date' do
        val = System::DateTime.now
        assert_write_and_read PCSFLV::AMFValueType.date, val, @stream
      end

      should 'raise InvalidDataException if read invalid reference' do
        pos = @stream.position
        writer = PCSFLV::AMF0Writer.new(@stream)
        writer.write_reference 0
        @stream.position = pos
        reader = PCSFLV::AMF0Reader.new(@stream)
        assert_raise System::IO::InvalidDataException do
          reader.read_value
        end
      end

      should 'raise InvalidDataException if read unknown marker' do
        pos = @stream.position
        writer = PCSFLV::AMF0Writer.new(@stream)
        writer.write_marker 42
        @stream.position = pos
        reader = PCSFLV::AMF0Reader.new(@stream)
        assert_raise System::IO::InvalidDataException do
          reader.read_value
        end
      end

    end
  end

end

