FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0-alpine AS build
WORKDIR /src
COPY . .
WORKDIR "/src/PeerCastStation"
RUN dotnet restore "PeerCastStation.csproj" -p:"Platform=Any CPU" -p:PeerCastUseGUI=false
RUN dotnet build "PeerCastStation.csproj" -c Release -o /app/build -p:"Platform=Any CPU" -p:PeerCastUseGUI=false

FROM build AS test
WORKDIR /src
RUN apk add bash
CMD ["dotnet", "test", "PeerCastStation.Test/PeerCastStation.Test.fsproj", "-v", "n", "-c", "Release", "-o", "/app/build", "-p:Platform=Any CPU", "-p:PeerCastUseGUI=false"]

FROM build AS publish
RUN dotnet publish "PeerCastStation.csproj" --no-self-contained -r any -c Release -o /app/publish -p:"Platform=Any CPU" -p:PeerCastUseGUI=false

FROM base AS final
VOLUME ["/data"]
EXPOSE 7144
WORKDIR /app
COPY --from=publish /app/publish .
RUN apk add bash
ENTRYPOINT ["dotnet", "PeerCastStation.dll", "-s", "/data/PecaSettings.xml"]
