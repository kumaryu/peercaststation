using System;
using System.Collections.Generic;
using System.Linq;

namespace PeerCastStation.UI.HTTP
{
  public class ObjectIdRegistry
  {
    private struct ObjectReference
    {
      private WeakReference reference;
      public ObjectReference(object obj)
      {
        reference = new WeakReference(obj);
      }

      public bool IsAlive {
        get { return reference.IsAlive; }
      }

      public object? Object {
        get {
          try {
            return reference.Target;
          }
          catch (InvalidOperationException) {
            return null;
          }
        }
      }

      public override bool Equals(object? other)
      {
        if (other==null) return false;
        if (other.GetType()!=this.GetType()) return false;
        var obj = this.Object;
        if (obj==null) return false;
        var x = ((ObjectReference)other).Object;
        if (x==null) return false;
        return obj.Equals(x);
      }

      public override int GetHashCode()
      {
        var obj = this.Object;
        if (obj==null) return 0;
        return obj.GetHashCode();
      }
    }

    private Dictionary<ObjectReference, int> objToId = new Dictionary<ObjectReference, int>();
    private HashSet<int> idSet = new HashSet<int>();
    private Random rand = new Random();
    private object locker = new object();

    private void Cleanup()
    {
      lock (locker) {
        var dead_list = objToId.Where(kv => !kv.Key.IsAlive).ToArray();
        foreach (var kv in dead_list) {
          objToId.Remove(kv.Key);
          idSet.Remove(kv.Value);
        }
      }
    }

    private int AllocateId()
    {
      lock (locker) {
        Cleanup();
        var id = rand.Next();
        while (!idSet.Add(id)) {
          id = rand.Next();
        }
        return id;
      }
    }

    public int GetId(object obj)
    {
      lock (locker) {
        var reference = new ObjectReference(obj);
        int id;
        if (objToId.TryGetValue(reference, out id)) {
          return id;
        }
        else {
          var new_id = AllocateId();
          objToId.Add(reference, new_id);
          return new_id;
        }
      }
    }

  }

}
