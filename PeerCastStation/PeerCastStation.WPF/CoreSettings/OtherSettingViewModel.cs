using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.CoreSettings
{
  class OtherSettingViewModel : ViewModelBase
  {
    private int maxRelays;
    public int MaxRelays
    {
      get { return maxRelays; }
      set
      {
        SetProperty("MaxRelays", ref maxRelays, value,
          applyOthers.OnCanExecuteChanged);
      }
    }

    private int maxRelaysPerChannel;
    public int MaxRelaysPerChannel
    {
      get { return maxRelaysPerChannel; }
      set
      {
        SetProperty("MaxRelaysPerChannel", ref maxRelaysPerChannel, value,
          applyOthers.OnCanExecuteChanged);
      }
    }

    private int maxDirects;
    public int MaxDirects
    {
      get { return maxDirects; }
      set
      {
        SetProperty("MaxDirects", ref maxDirects, value,
          applyOthers.OnCanExecuteChanged);
      }
    }

    private int maxDirectsPerChannel;
    public int MaxDirectsPerChannel
    {
      get { return maxDirectsPerChannel; }
      set
      {
        SetProperty("MaxDirectsPerChannel", ref maxDirectsPerChannel, value,
          applyOthers.OnCanExecuteChanged);
      }
    }

    private int maxUpstreamRate;
    public int MaxUpstreamRate
    {
      get { return maxUpstreamRate; }
      set
      {
        SetProperty("MaxUpstreamRate", ref maxUpstreamRate, value,
          applyOthers.OnCanExecuteChanged);
      }
    }

    private readonly Command applyOthers;
    public Command ApplyOthers { get { return applyOthers; } }

    public OtherSettingViewModel(AccessController accessController)
    {
      applyOthers = new Command(
        () => WriteTo(accessController),
        () => IsChanged(accessController));

      ReadFrom(accessController);
    }

    public void ReadFrom(AccessController from)
    {
      MaxRelays = from.MaxRelays;
      MaxRelaysPerChannel = from.MaxRelaysPerChannel;
      MaxDirects = from.MaxPlays;
      MaxDirectsPerChannel = from.MaxPlaysPerChannel;
      MaxUpstreamRate = from.MaxUpstreamRate;
    }

    public bool IsChanged(AccessController ctrler)
    {
      if (ctrler.MaxRelays != MaxRelays ||
          ctrler.MaxPlays != MaxDirects ||
          ctrler.MaxRelaysPerChannel != MaxRelaysPerChannel ||
          ctrler.MaxPlaysPerChannel != MaxDirectsPerChannel ||
          ctrler.MaxUpstreamRate != MaxUpstreamRate)
        return true;
      else
        return false;
    }

    public void WriteTo(AccessController to)
    {
      to.MaxRelays = MaxRelays;
      to.MaxPlays = MaxDirects;
      to.MaxRelaysPerChannel = MaxRelaysPerChannel;
      to.MaxPlaysPerChannel = MaxDirectsPerChannel;
      to.MaxUpstreamRate = MaxUpstreamRate;
      applyOthers.OnCanExecuteChanged();
    }
  }
}
