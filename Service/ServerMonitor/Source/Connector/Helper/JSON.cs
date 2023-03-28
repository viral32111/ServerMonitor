using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace ServerMonitor.Connector.Helper {
	
	// Helper class to encapsulate JSON manipulation
	public static class JSON {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Collector/Helper/JSON" );

		// Converts an array of JSON objects to a JSON array
		public static JsonArray CreateJsonArray( JsonObject[] jsonObjectArray ) => jsonObjectArray.Aggregate( new JsonArray(), ( array, jsonObject ) => {
			array.Add( jsonObject );
			return array;
		} );

		// Converts an array of JSON objects to a JSON array
		public static JsonArray CreateJsonArray( string[] array ) => array.Aggregate( new JsonArray(), ( array, str ) => {
			array.Add( str );
			return array;
		} );

	}

}
