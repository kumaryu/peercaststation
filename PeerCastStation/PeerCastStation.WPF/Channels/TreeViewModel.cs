using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.Channels
{
  class TreeViewModel : ViewModelBase
  {
    private string text = "";
    public string Text
    {
      get { return text; }
      set { SetProperty("Text", ref text, value); }
    }

    private readonly ObservableCollection<TreeViewModel> children
      = new ObservableCollection<TreeViewModel>();
    public ObservableCollection<TreeViewModel> Children
    {
      get { return children; }
    }
  }
}
