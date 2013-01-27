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
    private IEnumerable<T> items;
    public IEnumerable<T> Items
    {
      get { return items; }
      set { SetProperty("Items", ref items, value); }
    }

    private T selectedItem;
    public T SelectedItem
    {
      get { return selectedItem; }
      set
      {
        SetProperty("SelectedItem", ref selectedItem, value, () =>
        {
          removeItem.OnCanExecuteChanged();
          SelectedItemChanged(this, new ItemEventArgs<T> { Item = value });
        });
      }
    }

    public bool IsItemSelected
    {
      get { return SelectedItem != null; }
    }

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
        () => IsItemSelected);
    }

    public event ItemEventHandler<T> SelectedItemChanged = (sender, e) => { };
    public event EventHandler ItemAdding = (sender, e) => { };
    public event ItemEventHandler<T> ItemRemoving = (sender, e) => { };
  }

  delegate void ItemEventHandler<T>(object sender, ItemEventArgs<T> e);
  class ItemEventArgs<T> : EventArgs { public T Item { get; set; } }
}
