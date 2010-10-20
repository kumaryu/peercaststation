
require "socket"

ss = TCPServer.open(1234)
loop do
  Thread.new(ss.accept) do |s|
    while s.gets do
      s.write($_)
    end
    s.close
  end
end

