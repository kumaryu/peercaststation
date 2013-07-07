# coding: utf-8
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
require 'test_core_common'
require 'shoulda/context'

module TestCore
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

  class TC_StreamConnection < Test::Unit::TestCase
    context 'コンストラクタ' do
      setup do
        @input  = MockStream.new
        @output = MockStream.new
        @obj = PeerCastStation::Core::StreamConnection.new(@input, @output)
      end

      teardown do
        @obj.Dispose
        @input.dispose
        @output.dispose
      end

      should 'InputStreamとOutputStreamを引数で指定したものにする' do
        assert_equal @input,  @obj.InputStream
        assert_equal @output, @obj.OutputStream
      end

      should 'タイムアウトの初期値はInfiniteにする' do
        assert_equal System::Threading::Timeout.infinite, @obj.ReceiveTimeout
        assert_equal System::Threading::Timeout.infinite, @obj.SendTimeout
      end

      should 'エラーの初期値はnullにする' do
        assert_nil @obj.ReceiveError
        assert_nil @obj.SendError
      end

      should '送受信レートの初期値は0にする' do
        assert_equal 0, @obj.ReceiveRate
        assert_equal 0, @obj.SendRate
      end

      should '受信待ちハンドルの初期値は未シグナルにする' do
        assert !@obj.ReceiveWaitHandle.WaitOne(0)
        assert_equal 0, @obj.SendRate
      end
    end

    context '受信' do
      setup do
        @input  = MockStream.new
        @output = MockStream.new
        @input.read_data = 'hoge' * 10
        @input.read_delay = 10
        @obj = PeerCastStation::Core::StreamConnection.new(@input, @output)
      end

      teardown do
        @obj.Dispose
        @input.dispose
        @output.dispose
      end

      should '受信したらReceiveWaitHandleがシグナル状態になる' do
        assert @obj.ReceiveWaitHandle.WaitOne(100)
      end

      should '受信後にRecvでデータを受信できる' do
        @obj.ReceiveWaitHandle.WaitOne(100)
        @obj.Recv(proc {|stream|
          buf = System::Array[System::Byte].new(1024)
          len = stream.Read(buf, 0, buf.Length)
          assert_equal 40, len
        })
      end

      should '受信データを全部処理してなければReceiveWaitHandleはシグナル状態になりっぱなし' do
        @obj.ReceiveWaitHandle.WaitOne(100)
        @obj.Recv(proc {|stream|
          buf = System::Array[System::Byte].new(10)
          stream.Read(buf, 0, buf.Length)
        })
        assert @obj.ReceiveWaitHandle.WaitOne(0)
      end

      should '受信データを全部処理したらReceiveWaitHandleは未シグナル状態に戻る' do
        @obj.ReceiveWaitHandle.WaitOne(100)
        @obj.Recv(proc {|stream|
          buf = System::Array[System::Byte].new(1024)
          len = stream.Read(buf, 0, buf.Length)
        })
        assert !@obj.ReceiveWaitHandle.WaitOne(0)
      end

      should '受信データ処理中にEndOfStreamExceptionを投げたらReceiveWaitHandleは未シグナル状態に戻る' do
        @obj.ReceiveWaitHandle.WaitOne(100)
        @obj.Recv(proc {|stream|
          raise System::IO::EndOfStreamException.new
        })
        assert !@obj.ReceiveWaitHandle.WaitOne(0)
      end

      should '0バイト受信したらReceiveErrorにEndOfStreamExceptionが入る' do
        @input.read_data = ''
        @obj.ReceiveWaitHandle.WaitOne(100)
        assert_kind_of System::IO::EndOfStreamException, @obj.ReceiveError
      end
    end

    context '送信' do
      setup do
        @input  = MockStream.new
        @output = MockStream.new
        @output.write_delay = 1
        @obj = PeerCastStation::Core::StreamConnection.new(@input, @output)
      end

      teardown do
        @obj.Dispose
        @input.dispose
        @output.dispose
      end

      should 'Sendでデータを送信する' do
        @obj.Send('hoge')
        sleep(0.1)
        assert_equal 'hoge', @output.write_data
      end

      should 'SendにActionを渡してストリームに書き込んだデータを送信する' do
        @obj.Send(proc {|stream|
          data = 'hoge'
          stream.Write(data, 0, data.bytesize)
        })
        sleep(0.1)
        assert_equal 'hoge', @output.write_data
      end
    end

    context 'Close' do
      setup do
        @input  = MockStream.new
        @output = MockStream.new
        @obj = PeerCastStation::Core::StreamConnection.new(@input, @output)
      end

      teardown do
        @obj.Dispose
        @input.dispose
        @output.dispose
      end

      should '閉じた時に基になったストリームも閉じる' do
        @obj.Close
        assert @input.disposed?
        assert @output.disposed?
      end

      should '閉じた時に送信待ちのデータは全部送信する' do
        @obj.Send('hoge')
        @obj.Send('hoge')
        @obj.Close
        assert_equal 'hogehoge', @output.write_data
      end
    end

    context '受信エラー' do
      setup do
        @input  = MockStream.new
        @input.read_delay = 10
        @input.read_exception = System::IO::IOException.new
        @output = MockStream.new
        @obj = PeerCastStation::Core::StreamConnection.new(@input, @output)
      end

      teardown do
        @obj.Dispose
        @input.dispose
        @output.dispose
      end

      should '受信時にエラーが出るとReceiveWaitHandleがシグナル状態になる' do
        sleep(0.1)
        assert @obj.ReceiveWaitHandle.WaitOne(0)
      end

      should '受信時にエラーが出るとReceiveErrorに格納される' do
        sleep(0.1)
        assert_equal @input.read_exception, @obj.ReceiveError
      end

      should '受信時にエラーが出ると受信済みデータを読み取ったあとにRecvでIOExceptionが投げられる' do
        sleep(0.1)
        assert_raise(System::IO::IOException) do
          @obj.Recv(proc {|stream|
            buf = System::Array[System::Byte].new(stream.Length)
            stream.Read(buf, 0, buf.Length)
          })
        end
      end

      should '受信時にエラーが出るとRecvでEndOfStreamExceptionが投げられた時にIOExceptionが投げられる' do
        sleep(0.1)
        assert_raise(System::IO::IOException) do
          @obj.Recv(proc {|stream|
            raise System::IO::EndOfStreamException.new
          })
        end
      end

    end

    context '送信エラー' do
      setup do
        @input  = MockStream.new
        @output = MockStream.new
        @output.write_delay = 10
        @output.write_exception = System::IO::IOException.new
        @obj = PeerCastStation::Core::StreamConnection.new(@input, @output)
      end

      teardown do
        @obj.Dispose
        @input.dispose
        @output.dispose
      end

      should '送信時にエラーが出るとSendErrorに格納される' do
        @obj.Send('hoge')
        sleep(0.1)
        assert_equal @output.write_exception, @obj.SendError
      end

      should 'エラー発生時はSendでIOExceptionが投げられる' do
        @obj.Send('hoge')
        sleep(0.1)
        assert_raise(System::IO::IOException) do
          @obj.Send('hoge')
        end
      end

    end

  end
end

