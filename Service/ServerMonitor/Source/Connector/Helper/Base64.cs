using System;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;

namespace ServerMonitor.Connector.Helper {
	
	// Helper class to encapsulate Base64 encoding & decoding
	public static class Base64 {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Connector/Helper/Base64" );

		// Converts an array of JSON objects to a JSON array - https://9to5answer.com/how-to-achieve-base64-url-safe-encoding-in-c
		public static string EncodeURLSafe( string text ) => Convert.ToBase64String( Encoding.UTF8.GetBytes( text ) ).TrimEnd( '=' ).Replace( "+", "-" ).Replace( "/", "_" );
		public static string DecodeURLSafe( string text ) => Encoding.UTF8.GetString( Convert.FromBase64String( text.Replace( "-", "+" ).Replace( "_", "/" ) + new string( '=', ( 4 - text.Length % 4 ) % 4 ) ) );

	}

}
