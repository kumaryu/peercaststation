using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PeerCastStation.Core;
using PeerCastStation.WPF.Commons;

namespace PeerCastStation.WPF.CoreSettings
{
  class ListViewModel<T> : ViewModelBase
  {
    private T[] items;
    public T[] Items
    {
      get { return items; }
      set { SetProperty("Items", ref items, value); }
    }

    private int selectedIndex;
    public int SelectedIndex
    {
      get { return selectedIndex; }
      set
      {
        SetProperty("SelectedIndex", ref selectedIndex, value, () =>
        {
          removeItem.OnCanExecuteChanged();
          SelectedItemChanged(this, new ItemEventArgs<T> { Item = SelectedItem });
        });
      }
    }
    internal T SelectedItem { get { return Items[SelectedIndex]; } }

    private readonly Command addItem;
    public Command AddItem { get { return addItem; } }
    private readonly Command removeItem;
    public Command RemoveItem { get { return removeItem; } }

    public ListViewModel()
    {
      addItem = new Command(
        () => ItemAdding(this, new EventArgs()));
      removeItem = new Command(
        () => ItemRemoving(this, new ItemEventArgs<T> { Item = SelectedItem }),
        () => SelectedItem != null);
    }

    public event ItemEventHandler<T> SelectedItemChanged = (sender, e) => { };
    public event EventHandler ItemAdding = (sender, e) => { };
    public event ItemEventHandler<T> ItemRemoving = (sender, e) => { };
  }

  delegate void ItemEventHandler<T>(object sender, ItemEventArgs<T> e);
  class ItemEventArgs<T> : EventArgs { public T Item { get; set; } }
}
