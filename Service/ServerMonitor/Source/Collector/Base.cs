using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ServerMonitor.Collector {

	// Common base class for all collectors...
	public class Base {

		// Calls the appropriate update function depending on the operating system...
		public virtual void Update() {
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) UpdateOnWindows();
			else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) UpdateOnLinux();
			else throw new PlatformNotSupportedException( "Unsupported operating system" );
		}

		// Override these for updating for Windows & Linux respectively...
		[ SupportedOSPlatform( "windows" ) ]
		public virtual void UpdateOnWindows() => throw new PlatformNotSupportedException( "Windows-specific updating is not supported" );

		[ SupportedOSPlatform( "linux" ) ]
		public virtual void UpdateOnLinux() => throw new PlatformNotSupportedException( "Linux-specific updating is not supported" );

	}

}
