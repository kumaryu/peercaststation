using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace PeerCastStation.WPF
{
  class ViewModelCollection<T>
    : ObservableCollection<T>,
      INotifyItemChanged<T>
    where T : INotifyPropertyChanged
  {
    public event NotifyItemChangedEventHandler<T>? ItemChanged;

    public ViewModelCollection(IEnumerable<T> items)
      : base(items)
    {
      foreach (var item in this) {
        if (item==null) continue;
        item.PropertyChanged += OnItemPropertyChanged;
      }
    }

    public ViewModelCollection(IList<T> items)
      : base(items)
    {
      foreach (var item in this) {
        if (item==null) continue;
        item.PropertyChanged += OnItemPropertyChanged;
      }
    }

    public ViewModelCollection()
      : base()
    {
    }

    protected override void ClearItems()
    {
      var items = this.ToArray();
      base.ClearItems();
      foreach (var item in items) {
        if (item==null) continue;
        item.PropertyChanged -= OnItemPropertyChanged;
      }
    }

    protected override void InsertItem(int index, T item)
    {
      base.InsertItem(index, item);
      if (item==null) return;
      item.PropertyChanged += OnItemPropertyChanged;
    }

    protected override void RemoveItem(int index)
    {
      var item = Items[index];
      base.RemoveItem(index);
      if (item==null) return;
      item.PropertyChanged -= OnItemPropertyChanged;
    }

    protected override void SetItem(int index, T item)
    {
      var orig = Items[index];
      base.SetItem(index, item);
      if (orig!=null) {
        orig.PropertyChanged -= OnItemPropertyChanged;
      }
      if (item!=null) {
        item.PropertyChanged += OnItemPropertyChanged;
      }
    }

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
      if (sender is T item) {
        ItemChanged?.Invoke(this, new NotifyItemChangedEventArgs<T>(item));
      }
    }

  }

}
