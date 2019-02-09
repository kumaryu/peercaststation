using System;
using System.Collections.Generic;

namespace PeerCastStation.WPF
{
  internal class NamedValue<T>
  {
    public string Name { get; private set; }
    public T Value { get; private set; }
    public NamedValue(string name, T value)
    {
      Name = name;
      Value = value;
    }
  }

  internal class NamedValueList<T>
    : List<NamedValue<T>>
  {
    public void Add(string name, T value)
    {
      Add(new NamedValue<T>(name, value));
    }
  }

}
