FROM mcr.microsoft.com/dotnet/runtime:3.1-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:3.1-alpine AS build
WORKDIR /src
COPY ["PeerCastStation/PeerCastStation.csproj", "PeerCastStation/"]
RUN dotnet restore "PeerCastStation/PeerCastStation.csproj"
COPY . .
WORKDIR "/src/PeerCastStation"
RUN dotnet build "PeerCastStation.csproj" -c Release -o /app/build -p:PeerCastUseGUI=false

FROM build AS publish
RUN dotnet publish "PeerCastStation.csproj" -c Release -o /app/publish -p:PeerCastUseGUI=false

FROM base AS final
VOLUME ["/data"]
EXPOSE 7144
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "PeerCastStation.dll", "-s", "/data/PeCaSettings.xml"]
