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
