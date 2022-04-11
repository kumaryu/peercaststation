using System;
using System.Collections.Generic;

namespace PeerCastStation.Core
{
  public class NotificationMessageEventArgs
    : EventArgs
  {
    public NotificationMessage Message { get; private set; }
    public NotificationMessageEventArgs(NotificationMessage message)
    {
      Message = message;
    }
  }
  public delegate void NotificationMessageEventHandler(object sender, NotificationMessageEventArgs args);

  public abstract class PeerCastApplication
  {
    public static PeerCastApplication? Current { get; set; }
    public abstract PecaSettings Settings { get; }
    public abstract IEnumerable<IPlugin> Plugins { get; }
    public abstract PeerCast PeerCast { get; }
    public abstract string BasePath { get; }
    public abstract string[] Args { get; }
    public abstract void Stop(int exit_code, Action cleanupHandler);
    public void Stop(int exit_code)
    {
      Stop(exit_code, ()=> { });
    }
    public void Stop()
    {
      Stop(0);
    }

    public event NotificationMessageEventHandler? MessageNotified;
    public virtual void ShowNotificationMessage(NotificationMessage message)
    {
      MessageNotified?.Invoke(this, new NotificationMessageEventArgs(message));
    }

    public abstract void SaveSettings();
    public PeerCastApplication()
    {
      if (Current==null) Current = this;
    }
  }
}
