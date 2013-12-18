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
include PeerCastStation::FLV
require 'test/unit'
require 'shoulda/context'

module TestFLV
  class TC_AMF3 < Test::Unit::TestCase
    context '初期化' do
      should 'BaseStreamはReaderのコンストラクタに渡された値になる' do
        stream = System::IO::MemoryStream.new
        obj = AMF3Reader.new(stream)
        assert_equal stream, obj.base_stream
      end

      should 'BaseStreamはWriterのコンストラクタに渡された値になる' do
        stream = System::IO::MemoryStream.new
        obj = AMF3Writer.new(stream)
        assert_equal stream, obj.base_stream
      end
    end

    context 'read write' do
      setup do
        @stream = System::IO::MemoryStream.new
        @writer = AMF3Writer.new(@stream)
        @reader = AMF3Reader.new(@stream)
      end

      def assert_equal_amfvalue(expect, value)
        assert_equal(expect.type, value.type)
        case expect.type
        when AMFValueType.null
        when AMFValueType.boolean
        when AMFValueType.integer
        when AMFValueType.string
          assert_equal(expect.value, value.value)
        when AMFValueType.ECMAArray
          assert_equal(expect.value.count, value.value.count)
          expect.value.keys.each do |key|
            assert value.value.contains_key(key)
            assert_equal_amfvalue expect.value[key], value.value[key]
          end
        end
      end

      def assert_write_and_read(type, value, stream)
        pos = stream.position
        @writer.write_value AMFValue.new(type, value)
        stream.position = pos
        assert_equal_amfvalue AMFValue.new(type, value), @reader.read_value
      end

      def assert_write_integer_and_read_number(value, stream)
        pos = stream.position
        @writer.write_value AMFValue.new(AMFValueType.integer, value)
        stream.position = pos
        assert_equal_amfvalue AMFValue.new(AMFValueType.double, value.to_f), @reader.read_value
      end

      should 'write integer then read same integer' do
        assert_write_and_read AMFValueType.integer, 0, @stream
        assert_write_and_read AMFValueType.integer, 42, @stream
        assert_write_and_read AMFValueType.integer, 0x20000000-1, @stream
      end

      should 'write negative integer then read same number' do
        assert_write_integer_and_read_number -1, @stream
        assert_write_integer_and_read_number -42, @stream
      end

      should 'write large integer then read same number' do
        assert_write_integer_and_read_number 0x20000000, @stream
      end

      should 'write number then read same number' do
        assert_write_and_read AMFValueType.double, 42.0, @stream
      end

      should 'write empty string then read empty string' do
        assert_write_and_read AMFValueType.string, '', @stream
      end

      should 'write string then read same string' do
        assert_write_and_read AMFValueType.string, 'hoge', @stream
      end

      should 'write same string twice then read same strings' do
        pos = @stream.position
        @writer.write_value AMFValue.new(AMFValueType.string, 'hoge')
        @writer.write_value AMFValue.new(AMFValueType.string, 'hoge')
        @stream.position = pos
        assert_equal_amfvalue AMFValue.new(AMFValueType.string, 'hoge'), @reader.read_value
        assert_equal_amfvalue AMFValue.new(AMFValueType.string, 'hoge'), @reader.read_value
      end

      should 'write different strings then read these strings' do
        pos = @stream.position
        @writer.write_value AMFValue.new(AMFValueType.string, 'hoge')
        @writer.write_value AMFValue.new(AMFValueType.string, 'ふが')
        @stream.position = pos
        reader = AMF3Reader.new(@stream)
        assert_equal_amfvalue AMFValue.new(AMFValueType.string, 'hoge'), @reader.read_value
        assert_equal_amfvalue AMFValue.new(AMFValueType.string, 'ふが'), @reader.read_value
      end

      should 'write long string then read same string' do
        value = ('long ' * 65536)+'string'
        assert_write_and_read AMFValueType.string, value, @stream
      end

      def assert_invalid_reference_cause_invalid_data_exception(marker)
        pos = @stream.position
        @writer.write_marker marker
        @writer.WriteUI29(99<<1)
        @stream.position = pos
        assert_raise System::IO::InvalidDataException do
          @reader.read_value
        end
      end

      should 'raise InvalidDataException if read invalid string reference' do
        assert_invalid_reference_cause_invalid_data_exception(AMF3Marker.string)
      end

      should 'raise InvalidDataException if read invalid array reference' do
        assert_invalid_reference_cause_invalid_data_exception(AMF3Marker.array)
      end

      should 'raise InvalidDataException if read invalid date reference' do
        assert_invalid_reference_cause_invalid_data_exception(AMF3Marker.date)
      end

      should 'raise InvalidDataException if read invalid byte array reference' do
        assert_invalid_reference_cause_invalid_data_exception(AMF3Marker.byte_array)
      end

      should 'raise InvalidDataException if read invalid xml document reference' do
        assert_invalid_reference_cause_invalid_data_exception(AMF3Marker.XMLDocument)
      end

      should 'raise InvalidDataException if read invalid xml reference' do
        assert_invalid_reference_cause_invalid_data_exception(AMF3Marker.XML)
      end

      should 'write nil then read nil' do
        assert_write_and_read AMFValueType.null, nil, @stream
      end

      def amf_value(val)
        case val
        when nil
          AMFValue.new
        when true, false
          AMFValue.new(val)
        when String
          AMFValue.new(AMFValueType.string, val)
        when Integer
          AMFValue.new(val)
        when Numeric
          AMFValue.new(val)
        when Hash
          AMFValue.new(dictionary(val))
        when Array
          AMFValue.new(System::Array[AMFValue].new(val.collect {|v| amf_value(v) }))
        else
          AMFValue.new(val)
        end
      end

      def dictionary(val)
        dic = System::Collections::Generic::Dictionary[System::String, AMFValue].new
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
        assert_write_and_read AMFValueType.ECMAArray, dic, @stream
      end

      def array(val)
        System::Array[AMFValue].new(val.collect {|v| amf_value(v) })
      end

      should 'write array then read same array' do
        ary = array([
          'hoge', 'foo',
          'fuga', 'bar',
          'puyo', 'baz',
          '42', 42.0,
        ])
        assert_write_and_read AMFValueType.strict_array, ary, @stream
      end

      def amf_object(val)
        AMFObject.new(dictionary(val))
      end

      should 'write object then read same object' do
        obj = amf_object(
          'hoge' => 'foo',
          'fuga' => 'bar',
          'puyo' => 'baz',
          '42' => 42.0,
        )
        assert_write_and_read AMFValueType.object, obj, @stream
      end

      should 'write object twice then read same objects' do
        obj = amf_object(
          'hoge' => 'foo',
          'fuga' => 'bar',
          'puyo' => 'baz',
          '42' => 42.0,
        )
        pos = @stream.position
        @writer.write_value AMFValue.new(AMFValueType.object, obj)
        @writer.write_value AMFValue.new(AMFValueType.object, obj)
        @stream.position = pos
        assert_equal_amfvalue AMFValue.new(AMFValueType.object, obj), @reader.read_value
        assert_equal_amfvalue AMFValue.new(AMFValueType.object, obj), @reader.read_value
      end

      should 'write date then read same date' do
        val = System::DateTime.now
        assert_write_and_read AMFValueType.date, val, @stream
      end

      should 'write xmldocument then read same xmldocument' do
        doc = <<-XML
        <hoge>
          <fuga>foo bar</fuga>
        </hoge>
        XML
        assert_write_and_read AMFValueType.XMLDocument, doc, @stream
      end

      should 'write xml then read same xml' do
        doc = <<-XML
        <hoge>
          <fuga>foo bar</fuga>
        </hoge>
        XML
        assert_write_and_read AMFValueType.XML, doc, @stream
      end

      should 'raise InvalidDataException if read unknown marker' do
        pos = @stream.position
        @writer.write_marker 42
        @stream.position = pos
        assert_raise System::IO::InvalidDataException do
          @reader.read_value
        end
      end

    end
  end

end

