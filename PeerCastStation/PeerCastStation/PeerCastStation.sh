#!/usr/bin/env bash

DOTNET=dotnet 
VERSION=6.0
ENTRYPOINT="$(cd $(dirname $0);pwd)/PeerCastStation.dll"

has_command() {
  command -v "$1" > /dev/null 2>&1
  return $?
}

has_dotnet() {
  dotnet=$1
  if has_command "$dotnet"; then
    "$dotnet" --list-runtimes | grep "Microsoft.NETCore.App $VERSION" > /dev/null 2>&1
    return $?
  else
    return 1
  fi
}

download() {
  url=$1
  if has_command curl; then
    curl -Ls "$url"
  elif has_command wget; then
    wget -qO- "$url"
  else
    echo "curl or wget are required to download dotnet." >&2
    return 1
  fi
}

start_process() {
  dotnet=$1
  shift
  $dotnet $ENTRYPOINT $*
}

if has_dotnet dotnet; then
  start_process dotnet $*
elif has_dotnet ~/.dotnet/dotnet; then
  start_process ~/.dotnet/dotnet $*
else
  download https://dot.net/v1/dotnet-install.sh | bash -s -- -c $VERSION --runtime dotnet
  if has_dotnet ~/.dotnet/dotnet; then
    start_process ~/.dotnet/dotnet $*
  fi
fi

