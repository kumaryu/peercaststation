#coding: utf-8

require 'optparse'
require 'time'
require 'yaml'

BASE = File.dirname(__FILE__)
date = Time.now
url  = nil

opt = OptionParser.new
opt.on('--dev') do
  url = 'http://www.pecastation.org/files/appcast-dev.xml'
end
opt.on('--stable') do
  url = 'http://www.pecastation.org/files/appcast.xml'
end
opt.on('--date=DATE') do |v|
  date = Time.parse(v)
end
opt.parse!(ARGV)

if ARGV.size<1 then
  $stderr.puts "#{$0} [--dev|--stable] [--date=DATE] VERSION"
  exit 1
end
version = ARGV[0]

if url.nil? then
  url = 'http://www.pecastation.org/files/appcast.xml'
  md = /^(\d+)\.(\d+)/.match(version)
  if md and md[2].to_i%2==1 then
    url = 'http://www.pecastation.org/files/appcast-dev.xml'
  end
end

def replace_files(pattern, code='utf-8', &block)
  Dir.glob(pattern) do |fn|
    src = File.open(fn, "r:#{code}") {|f| f.read }
    File.open(fn, "w:#{code}") do |f|
      src.lines.each do |line|
        block.call(f, line)
      end
    end
  end
end

def replace_setting(project, name, value)
  replace_files(File.join(BASE, project, '/app.config')) do |f, line|
    case line
    when %r;(\s*)<add\s*key="#{name}"\s*value=".*"\s*/>(\s*);
      f.puts %Q(#{$1}<add key="#{name}" value="#{value}"/>#{$2})
    else
      f.puts line
    end
  end
end

def replace_yaml(file, &block)
  doc = YAML.load_file(File.join(BASE, 'appveyor.yml'))
  block.call(doc)
  File.open(file, 'w:utf-8') do |f|
    YAML.dump(doc, f)
  end
end

replace_files(File.join(BASE, '**/AssemblyInfo.cs')) do |f, line|
  case line
  when /\[assembly: AssemblyFileVersion\("\S+"\)\]/
    f.puts %Q/[assembly: AssemblyFileVersion("#{version}")]/
  when /\[assembly: AssemblyInformationalVersion\("\S+"\)\]/
    f.puts %Q/[assembly: AssemblyInformationalVersion("#{version}")]/
  else
    f.puts line
  end
end

replace_setting('PeerCastStation/PeerCastStation', 'AgentName', "PeerCastStation/#{version}")
replace_setting('PeerCastStation/PeerCastStation', 'UpdateURL', url)
replace_setting('PeerCastStation/PeerCastStation', 'CurrentVersion', "#{date.year}-#{date.month}-#{date.day}")
replace_setting('PeerCastStation/PecaStationd', 'AgentName', "PeerCastStation/#{version}")
replace_setting('PeerCastStation/PecaStationd', 'UpdateURL', url)
replace_setting('PeerCastStation/PecaStationd', 'CurrentVersion', "#{date.year}-#{date.month}-#{date.day}")

replace_files(File.join(BASE, 'PeerCastStation/PeerCastStation.PCP/PCPVersion.cs')) do |f, line|
  case line
  when /(\s*public static readonly short  ServantVersionEXNumber = )\d+(;\s*)/
    vernum = version.split('.').join
    f.puts %Q/#{$1}#{vernum[0,3]}#{$2}/
  else
    f.puts line
  end
end

replace_yaml(File.join(BASE, 'appveyor.yml')) do |doc|
  doc['version'] = version.split('.')[0,3].join('.') + '.{build}'
end

