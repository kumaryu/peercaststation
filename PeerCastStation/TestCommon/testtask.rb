
require 'rake/testtask'

class PeerCastTestTask < Rake::TestTask
  if not self.instance_methods.include?(:run_code) then
    def define
      lib_path = @libs.join(File::PATH_SEPARATOR)
      desc "Run tests" + (@name==:test ? "" : " for #{@name}")
      task @name do
        RakeFileUtils.verbose(@verbose) do
          @ruby_opts.unshift( "-I\"#{lib_path}\"" )
          @ruby_opts.unshift( "-w" ) if @warning
          ruby @ruby_opts.join(" ") +
            " \"#{run_code}\" " +
            file_list.collect { |fn| "\"#{fn}\"" }.join(' ') +
            " #{option_list}"
        end
      end
      self
    end
  end

  def run_code
    'TestCommon/loader.rb'
  end
end

