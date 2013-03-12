// PeerCastStation, a P2P streaming servent.
// Copyright (C) 2013 PROGRE (djyayutto@gmail.com)
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.ChannelLists.ChannelInfos
{
  class TrackViewModel : ViewModelBase
  {
    private bool isReadOnly;
    public bool IsReadOnly
    {
      get { return isReadOnly; }
      set { SetProperty("IsReadOnly", ref isReadOnly, value); }
    }

    private string title = "";
    public string Title
    {
      get { return title; }
      set { SetProperty("Title", ref title, value); }
    }

    private string album = "";
    public string Album
    {
      get { return album; }
      set { SetProperty("Album", ref album, value); }
    }

    private string artist = "";
    public string Artist
    {
      get { return artist; }
      set { SetProperty("Artist", ref artist, value); }
    }

    private string genre = "";
    public string Genre
    {
      get { return genre; }
      set { SetProperty("Genre", ref genre, value); }
    }

    private string url = "";
    public string Url
    {
      get { return url; }
      set { SetProperty("Url", ref url, value); }
    }
  }
}
