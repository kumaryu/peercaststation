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
using PeerCastStation.Core;

namespace PeerCastStation.WPF.CoreSettings
{
  static class OutputListenerExtensions
  {
    internal static bool? GetFromLocalOutputAccepts(
      this OutputListener self, OutputStreamType target)
    {
      if (self == null)
        return null;
      return self.LocalOutputAccepts.Contains(target);
    }

    internal static void SetToLocalOutputAccepts(
      this OutputListener self, OutputStreamType target, bool? value)
    {
      if (self == null)
        return;
      if (value == true)
        self.LocalOutputAccepts |= target;
      else
        self.LocalOutputAccepts &= ~target;
    }

    internal static bool? GetFromGlobalOutputAccepts(
      this OutputListener self, OutputStreamType target)
    {
      if (self == null)
        return null;
      return self.GlobalOutputAccepts.Contains(target);
    }

    internal static void SetToGlobalOutputAccepts(
      this OutputListener self, OutputStreamType target, bool? value)
    {
      if (self == null)
        return;
      if (value == true)
        self.GlobalOutputAccepts |= target;
      else
        self.GlobalOutputAccepts &= ~target;
    }

    private static bool Contains(this OutputStreamType self, OutputStreamType other)
    {
      return (self & other) != 0;
    }
  }
}
