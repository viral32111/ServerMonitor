using System;

namespace ServerMonitor.Collector {

	public static class Collector {
		
		public static void HandleCommand( string extraConfigurationFilePath ) {
			Console.WriteLine( "Collector!" );

			Config configuration = Configuration.Load( extraConfigurationFilePath );
			Console.WriteLine( "Loaded configuration. Test = {0}", configuration.Test );
		}

	}

}
