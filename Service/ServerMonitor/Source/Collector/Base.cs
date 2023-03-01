using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ServerMonitor.Collector {

	// Common base class for all collectors...
	public class Base {

		// Take configuration in constructor so it can be passed to the update methods
		private readonly Config configuration;
		public Base( Config config ) {
			configuration = config;
		}

		// Calls the appropriate update function depending on the operating system...
		public virtual void Update() {
			if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) ) UpdateOnWindows( configuration );
			else if ( RuntimeInformation.IsOSPlatform( OSPlatform.Linux ) ) UpdateOnLinux( configuration );
			else throw new PlatformNotSupportedException( "Unsupported operating system" );
		}

		// Override these for updating for Windows & Linux respectively...
		[ SupportedOSPlatform( "windows" ) ]
		public virtual void UpdateOnWindows( Config configuration ) => throw new PlatformNotSupportedException( "Windows-specific updating is not supported" );

		[ SupportedOSPlatform( "linux" ) ]
		public virtual void UpdateOnLinux( Config configuration ) => throw new PlatformNotSupportedException( "Linux-specific updating is not supported" );

	}

}
