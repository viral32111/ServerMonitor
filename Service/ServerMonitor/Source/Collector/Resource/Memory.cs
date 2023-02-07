using System;
using System.Runtime.InteropServices;

/*
Memory systemMemory = new();
systemMemory.Update();
Console.WriteLine( systemMemory.TotalBytes, systemMemory.FreeBytes, systemMemory.UsedBytes );
*/

namespace ServerMonitor.Collector.Resource {
	public class Memory {
		public readonly int TotalBytes;
		public readonly int FreeBytes;
		public readonly int UsedBytes;

		public void Update() {
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) UpdateOnWindows();
			else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) UpdateOnLinux();
			else throw new Exception( "Unsupported operating system" );
		}

		private void UpdateOnWindows() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Windows" );

			// TODO: PerformanceCounter...

			throw new NotImplementedException();
		}

		private void UpdateOnLinux() {
			if ( !RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) throw new InvalidOperationException( "Method only available on Linux" );

			// TODO: /proc/meminfo

			throw new NotImplementedException();
		}
	}
}