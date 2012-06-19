# coding:utf-8
# PeerCastStation, a P2P streaming servent.
# Copyright (C) 2012 Ryuichi Sakamoto (kumaryu@kumaryu.net)
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
$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.FLV', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'
require 'PeerCastStation.FLV.dll'
require 'test/unit'
require 'shoulda/context'
PCSCore = PeerCastStation::Core unless defined?(PCSCore)
PCSFLV  = PeerCastStation::FLV unless defined?(PCSFLV)

class TC_FLVContentReaderFactory < Test::Unit::TestCase
  context 'コンストラクタ' do
    setup do
      @factory = PCSFLV::FLVContentReaderFactory.new
    end

    should 'IContentReaderFactoryを実装している' do
      assert_kind_of PCSCore::IContentReaderFactory, @factory
    end

    should 'NameがFLVっぽいのを返す' do
      assert_equal "Flash Video (FLV)", @factory.name
    end
  end

  context 'Create' do
    setup do
      @factory = PCSFLV::FLVContentReaderFactory.new
      @peercast = PCSCore::PeerCast.new
      @channel = PCSCore::Channel.new(
        @peercast,
        System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string), 
        System::Uri.new('http://127.0.0.1:8888/'))
    end
    
    teardown do
      @peercast.stop if @peercast
    end

    should 'IContentReaderを実装したインスタンスを返す' do
      reader = @factory.create(@channel)
      assert_kind_of PCSCore::IContentReader, reader
    end

    should 'NameがFLVっぽいのを返す' do
      reader = @factory.create(@channel)
      assert_equal "Flash Video (FLV)", reader.name
    end
  end
end

class TC_FLVContentReader < Test::Unit::TestCase
  def fixture(name)
    File.join(File.dirname(__FILE__), 'fixtures', name)
  end

  context 'Read' do
    setup do
      @peercast = PCSCore::PeerCast.new
      @channel = PCSCore::Channel.new(
        @peercast,
        System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string), 
        System::Uri.new('http://127.0.0.1:8888/'))
      @factory = PCSFLV::FLVContentReaderFactory.new
      @reader = @factory.create(@channel)
    end
    
    teardown do
      @peercast.stop if @peercast
    end

    should '空のストリームから読むとEndOfStreamExceptionを投げる' do
      stream = System::IO::MemoryStream.new
      assert_raises System::IO::EndOfStreamException do
        content = @reader.read(stream)
      end
    end

    context 'test.flv' do
      setup do
        @stream = System::IO::File.open_read(fixture('test.flv'))
      end

      teardown do
        @stream.close if @stream
      end

      should '例外を投げずに読み取れる' do
        assert_nothing_raised do
          contents = @reader.read(@stream)
        end
      end

      should '一度で出来る限り読み込み、二度目の読み込みはEndOfStreamException' do
        contents = @reader.read(@stream)
        assert_raises System::IO::EndOfStreamException do
          contents = @reader.read(@stream)
        end
      end

      should 'null以外を返す' do
        assert_not_nil @reader.read(@stream)
      end

      should 'ContentHeaderがnullでなく、Positionは0' do
        res = @reader.read(@stream)
        assert_not_nil res.ContentHeader
        assert_equal 0, res.ContentHeader.Position
      end

      should 'ChannelInfoに値が入っている' do
        res = @reader.read(@stream)
        assert_not_nil res.ChannelInfo
        assert_equal 'video/x-flv', res.ChannelInfo.MIMEType
        assert_equal '.flv', res.ChannelInfo.ContentExtension
        assert_equal 'FLV',  res.ChannelInfo.ContentType
        assert res.ChannelInfo.Bitrate!=0
      end

      should 'Contentsにいくらかのパケットが入っている' do
        res = @reader.read(@stream)
        assert_not_nil res.Contents
        assert 0<res.Contents.count
      end

      should 'ContentHeaderとContentsのパケットは連続している' do
        res = @reader.read(@stream)
        pos  = res.ContentHeader.Position
        pos += res.ContentHeader.Data.length
        res.Contents.each do |content|
          assert_equal content.Position, pos
          pos += content.Data.length
        end
      end
    end
  end

  context '少しづつRead' do
    setup do
      @peercast = PCSCore::PeerCast.new
      @channel = PCSCore::Channel.new(
        @peercast,
        System::Guid.new('9778E62BDC59DF56F9216D0387F80BF2'.to_clr_string), 
        System::Uri.new('http://127.0.0.1:8888/'))
      @factory = PCSFLV::FLVContentReaderFactory.new
      @reader = @factory.create(@channel)
      @stream = System::IO::MemoryStream.new
    end
    
    teardown do
      @stream.close if @stream
      @peercast.stop if @peercast
    end

    def copy_to(dst, src, len)
      bytes = System::Array[System::Byte].new(len)
      len = src.Read(bytes, 0, len)
      dst.Write(bytes, 0, len) if len>0
      len
    end

    def read_slow(&block)
      pos = @stream.position
      count = 1024
      begin
        res = nil
        loop do
          @stream.position = pos
          res = @reader.read(@stream)
          if res or
             res.ContentHeader.nil? and
             res.Contents.nil? and
             res.ChannelInfo.nil? then
            pos = @stream.position
            count = 1024
          else
            break
          end
        end
        block.call(res) if block
        pos = @stream.position
        count = 1024
      rescue System::IO::EndOfStreamException
        @stream.position = @stream.length
        if copy_to(@stream, @file, count)>0 then
          count = [count*2, 65536].min
          @stream.position = pos
          retry
        end
      end
      res
    end

    context 'test.flv' do
      setup do
        @file = System::IO::File.open_read(fixture('test.flv'))
      end

      teardown do
        @file.close if @file
      end

      should '読んだだけで足りなければEndOfStreamException' do
        copy_to(@stream, @file, 8)
        @stream.position = 0
        assert_raises System::IO::EndOfStreamException do
          contents = @reader.read(@stream)
        end
      end

      should '少しずつ読んでいってContentHeaderが読み込める' do
        header = nil
        read_slow do |res|
          assert_not_nil res.ContentHeader
          assert_equal 0, res.ContentHeader.Position
        end
      end

      should 'ヘッダを読み込んだあとも少しづつ読んでいくとContentが読み込める' do
        header = nil
        contents = []
        until header do 
          read_slow do |res|
            if res.ContentHeader then
              header = res.ContentHeader
            end
            if res.Contents then
              res.Contents.each do |content|
                contents << content
              end
            end
          end
        end

        while @file.position<@file.length do 
          read_slow do |res|
            res.Contents.each do |content|
              contents << content
            end
          end
        end
        pos  = header.Position
        pos += header.Data.length
        contents.each do |content|
          assert_equal pos, content.Position
          pos += content.Data.length
        end
      end

      should 'ContentHeaderまで読んだ時点でChannelInfoに値が入る' do
        res = read_slow 
        assert_not_nil res.ChannelInfo
        assert_equal 'video/x-flv', res.ChannelInfo.MIMEType
        assert_equal '.flv', res.ChannelInfo.ContentExtension
        assert_equal 'FLV',  res.ChannelInfo.ContentType
        assert_equal 0,      res.ChannelInfo.Bitrate
      end

      should '少しづつ読んでいくとChannelInfo.Bitrateが更新される' do
        header = read_slow

        infos = []
        while @file.position<@file.length do 
          read_slow do |res|
            infos << res.ChannelInfo if res.ChannelInfo
          end
        end
        assert_not_empty infos
        infos.each do |info|
          assert_not_equal 0, info.Bitrate
        end
      end
    end
  end
end
