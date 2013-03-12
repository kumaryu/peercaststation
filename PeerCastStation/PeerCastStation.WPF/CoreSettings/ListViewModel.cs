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

    private readonly Command addItem;
    public Command AddItem { get { return addItem; } }
    private readonly Command removeItem;
    public Command RemoveItem { get { return removeItem; } }

    internal ListViewModel()
    {
      addItem = new Command(
        () => ItemAdding(this, new EventArgs()));
      removeItem = new Command(
        () => ItemRemoving(this, new ItemEventArgs<T> { Item = SelectedItem }),
        () => SelectedItem != null);
    }

    internal event ItemEventHandler<T> SelectedItemChanged = (sender, e) => { };
    internal event EventHandler ItemAdding = (sender, e) => { };
    internal event ItemEventHandler<T> ItemRemoving = (sender, e) => { };
  }

  delegate void ItemEventHandler<T>(object sender, ItemEventArgs<T> e);
  class ItemEventArgs<T> : EventArgs { public T Item { get; set; } }
}
