
HEAT = 'c:\Program Files\WiX Toolset v3.9\bin\heat.exe'

Projects = %W(
PeerCastStation.Core
PeerCastStation.ASF
PeerCastStation.FLV
PeerCastStation.MKV
PeerCastStation.HTTP
PeerCastStation.PCP
PeerCastStation.UI
PeerCastStation.UI.HTTP
PeerCastStation.WPF
)

Projects.each do |proj|
  args = %W(
    #{HEAT}
    project
    #{File.join('..', proj, proj + '.csproj')}
    -configuration Release
    -platform AnyCPU
    -ag
    -cg #{proj}
    -pog Binaries
    -pog Satellites
    -pog Content
    -out #{proj}.wxs
    -directoryid INSTALLFOLDER
    -nologo
  )
  p system(*args)
end

