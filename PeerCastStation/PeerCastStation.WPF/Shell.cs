using System;
using System.Runtime.InteropServices;

namespace PeerCastStation.WPF
{
	internal static class Shell
	{
		public enum KnownFolder {
			Downloads,
		}
		private static Guid FOLDERID_Downloads = new Guid("{374DE290-123F-4565-9164-39C4925E467B}");

		[DllImport("Shell32.dll", PreserveSig=false)]
		private static extern void SHGetKnownFolderPath(ref Guid refid, uint flags, IntPtr htoken, out IntPtr path);
		public static string GetKnownFolder(KnownFolder folder)
		{
			Guid id;
			switch (folder) {
			case KnownFolder.Downloads:
			default:
				id = FOLDERID_Downloads;
				break;
			}
			IntPtr path_ptr;
			SHGetKnownFolderPath(ref id, 0, IntPtr.Zero, out path_ptr);
			var path = Marshal.PtrToStringUni(path_ptr);
			Marshal.FreeCoTaskMem(path_ptr);
			return path;
		}
	}
}
