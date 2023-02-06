using System;

namespace ServerMonitor.Connector {

	public static class Connector {
		
		public static void HandleCommand( string extraConfigurationFilePath ) {
			Console.WriteLine( "Connector!" );

			Config configuration = Configuration.Load( extraConfigurationFilePath );
			Console.WriteLine( "Loaded configuration. Test = {0}", configuration.Test );
		}

	}

}
