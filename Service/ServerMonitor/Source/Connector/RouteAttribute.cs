using System;

namespace ServerMonitor.Connector {

	// Attribute for providing data about a route request handler
	[ AttributeUsage( AttributeTargets.Method ) ]
	public class RouteAttribute : Attribute {
		public readonly string Method;
		public readonly string Path;

		public RouteAttribute( string method, string path ) => ( Method, Path ) = ( method, path );
	}

}
