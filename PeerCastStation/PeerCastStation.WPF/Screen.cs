using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;

namespace PeerCastStation.WPF
{
  internal class Screen
  {
    public bool IsPrimary { get; }
    public Rect PhysicalDisplayArea { get; }
    public Rect PhysicalWorkingArea { get; }
    public int DpiX { get; }
    public int DpiY { get; }
    public Rect DisplayArea {
      get {
        return new Rect(
          PhysicalDisplayArea.X * 96.0 / DpiX,
          PhysicalDisplayArea.Y * 96.0 / DpiY,
          PhysicalDisplayArea.Width * 96.0 / DpiX,
          PhysicalDisplayArea.Height * 96.0 / DpiY
        );
      }
    }
    public Rect WorkingArea {
      get {
        return new Rect(
          PhysicalWorkingArea.X * 96.0 / DpiX,
          PhysicalWorkingArea.Y * 96.0 / DpiY,
          PhysicalWorkingArea.Width * 96.0 / DpiX,
          PhysicalWorkingArea.Height * 96.0 / DpiY
        );
      }
    }

    private Screen(IntPtr monitor)
    {
      var info = new User32.MONITORINFO() { cbSize = Marshal.SizeOf<User32.MONITORINFO>() };
      if (User32.GetMonitorInfoW(monitor, ref info)) {
        PhysicalDisplayArea = info.rcMonitor.ToRect();
        PhysicalWorkingArea = info.rcWork.ToRect();
        IsPrimary = (info.dwFlags & User32.MONITORINFOF_PRIMARY)!=0;
      }
      uint dpix = 96, dpiy = 96;
      try {
        Shcore.GetDpiForMonitor(monitor, Shcore.MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, ref dpix, ref dpiy);
      }
      catch (DllNotFoundException) {
      }
      catch (EntryPointNotFoundException) {
      }
      DpiX = (int)dpix;
      DpiY = (int)dpiy;
    }

    public Rect GetWorkingAreaWithDpi(int dpi)
    {
      var area = PhysicalWorkingArea;
      area.Scale(96.0 / dpi, 96.0 / dpi);
      return area;
    }

    public static IList<Screen> GetAllScreen()
    {
      var screens = new List<Screen>();
      User32.EnumDisplayMonitors(
        IntPtr.Zero,
        IntPtr.Zero,
        (IntPtr hMonitor, IntPtr hdcMonitor, ref User32.RECT lprcMonitor, IntPtr dwData) => {
          screens.Add(new Screen(hMonitor));
          return true;
        },
        IntPtr.Zero
      );
      return screens;
    }

    public static int GetDpiForWindow(System.Windows.Interop.WindowInteropHelper hwnd)
    {
      if (hwnd.Handle!=IntPtr.Zero) {
        try {
          return (int)User32.GetDpiForWindow(hwnd.Handle);
        }
        catch (DllNotFoundException) {
          return 96;
        }
        catch (EntryPointNotFoundException) {
          return 96;
        }
      }
      else {
        throw new InvalidOperationException();
      }
    }

    public static int GetDpiForWindow(Window window)
    {
      return GetDpiForWindow(new System.Windows.Interop.WindowInteropHelper(window));
    }

    private static class User32 {
      [StructLayout(LayoutKind.Sequential)]
      public struct RECT
      {
        public int left;
        public int top;
        public int right;
        public int bottom;
        public Rect ToRect()
        {
          return new Rect(left, top, right-left, bottom-top);
        }
      }

      public delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

      [DllImport("User32.dll", CallingConvention=CallingConvention.Winapi, PreserveSig=true, ExactSpelling=true)]
      public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);

      public const int MONITORINFOF_PRIMARY = 1;
      [StructLayout(LayoutKind.Sequential)]
      public struct MONITORINFO {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
      }

      [DllImport("User32.dll", CallingConvention=CallingConvention.Winapi, PreserveSig=true, ExactSpelling=true)]
      public static extern bool GetMonitorInfoW(IntPtr hMonitor, ref MONITORINFO lpmi);

      [DllImport("User32.dll", CallingConvention=CallingConvention.Winapi, PreserveSig=true, ExactSpelling=true)]
      public static extern uint GetDpiForWindow(IntPtr hwnd);
    }

    private static class Shcore {
      public enum MONITOR_DPI_TYPE {
        MDT_EFFECTIVE_DPI = 0,
        MDT_ANGULAR_DPI = 1,
        MDT_RAW_DPI = 2,
        MDT_DEFAULT = 0,
      }

      [DllImport("Shcore.dll", CallingConvention=CallingConvention.Winapi, PreserveSig=false)]
      public static extern void GetDpiForMonitor(IntPtr hmonitor, MONITOR_DPI_TYPE dpiType, ref uint dpiX, ref uint dpiY);
    }

  }

}
