using System;

namespace PeerCastStation.Core
{
  public enum NotificationMessageType
  {
    Normal,
    Info,
    Warning,
    Error,
  }

  public class NotificationMessage
  {
    public string Title   { get; private set; }
    public string Message { get; private set; }
    public NotificationMessageType Type { get; private set; }
    public object? Data   { get; private set; }
    public NotificationMessage(string title, string message, NotificationMessageType type, object? data=null)
    {
      this.Title   = title;
      this.Message = message;
      this.Type    = type;
      this.Data    = data;
    }

    public override bool Equals(object? obj)
    {
      if (obj==null) return false;
      if (this==obj) return true;
      if (this.GetType()!=obj.GetType()) return false;
      var x = (NotificationMessage)obj;
      return
        Object.Equals(this.Title, x.Title) &&
        Object.Equals(this.Message, x.Message) &&
        Object.Equals(this.Type, x.Type) &&
        Object.Equals(this.Data, x.Data);
    }

    public override int GetHashCode()
    {
      return
        (this.Title==null   ? 0 : this.Title.GetHashCode()) +
        (this.Message==null ? 0 : this.Message.GetHashCode()) +
        (this.Type.GetHashCode()) +
        (this.Data==null    ? 0 : this.Data.GetHashCode());
    }
  }
}
