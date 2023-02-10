using System;
using System.Runtime.InteropServices;

// TODO

namespace ServerMonitor.Collector.Resource {

	// Base class for other resource classes
	public abstract class Resource {

		// Calls the appropriate update function depending on the operating system...
		public virtual void Update() {
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) UpdateOnWindows();
			else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) UpdateOnLinux();
			else throw new Exception( "Unsupported operating system" );
		}

		// Override with code for updating for Windows & Linux respectively...
		public virtual void UpdateOnWindows() => throw new Exception( "Windows-specific updating is not supported" );
		public virtual void UpdateOnLinux() => throw new Exception( "Linux-specific updating is not supported" );

	}

}
