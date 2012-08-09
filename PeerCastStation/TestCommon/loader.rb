
require 'test/unit'

module TestLoader
  def self.run(args=ARGV)
    args.select {|arg| (/^-/=~arg).nil? }.each {|file| load file }
    if defined?(Test::Unit::AutoRunner) then
      return Test::Unit::AutoRunner.run
    else
      exit_code = MiniTest::Unit.new.run(args)
      ::MiniTest::Unit.class_eval do
        define_method(:run) do |a|
        end
      end
      return (exit_code || 0)==0
    end
  end
end

exit TestLoader.run(ARGV) if $0==__FILE__

