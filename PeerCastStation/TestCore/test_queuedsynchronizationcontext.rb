
require 'test/unit'

$: << File.join(File.dirname(__FILE__), '..', 'PeerCastStation.Core', 'bin', 'Debug')
require 'PeerCastStation.Core.dll'

class TestQueuedSynchronizationContext < Test::Unit::TestCase
  QSyncContext = PeerCastStation::Core::QueuedSynchronizationContext
  
  def test_construct
    ctx = nil
    assert_nothing_raised do
      ctx = QSyncContext.new
    end
    assert(ctx.is_empty)
  end
  
  def test_post
    ctx = QSyncContext.new
    ctx.post(proc {|s| puts s }, 'foo')
    assert(!ctx.is_empty)
  end
  
  def test_process
    ctx = QSyncContext.new
    assert(!ctx.process)
    value = nil
    ctx.post(proc {|s| value = s }, 'foo')
    assert_nil(value)
    assert(!ctx.is_empty)
    assert(ctx.process)
    assert_equal('foo', value)
    assert(ctx.is_empty)
    assert(!ctx.process)
  end
  
  def test_process_all
    ctx = QSyncContext.new
    assert(!ctx.process)
    value = []
    ctx.post(proc {|s| value << s }, 'foo')
    ctx.post(proc {|s| value << s }, 'bar')
    ctx.post(proc {|s| value << s }, 'baz')
    assert(!ctx.is_empty)
    ctx.process_all
    assert(ctx.is_empty)
    assert_equal(['foo', 'bar', 'baz'], value)
  end
  
  def test_send
    ctx = QSyncContext.new
    main_th = Thread.current
    t = Thread.new {
      value = nil
      ctx.send(proc {|s|
        assert_equal(main_th, Thread.current)
        value = s
      }, 'foo')
      assert_equal('foo', value)
    }
    sleep(1)
    ctx.process
  end
end

