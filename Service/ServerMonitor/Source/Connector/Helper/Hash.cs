using System;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;

namespace ServerMonitor.Connector.Helper {
	
	// Helper class to encapsulate hashing
	public static class Hash {

		// Create the logger for this file
		private static readonly ILogger logger = Logging.CreateLogger( "Connector/Helper/Hash" );

		// Secure random number generator for generating hash salts
		private static readonly RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();

		// Hashes text using the PBKDF2 algorithm and returns it in our custom format for storing hashed passwords
		public static string PBKDF2( string text, int iterationCount = 1000, byte[]? saltBytes = null ) {

			// Generate fresh salt if none was provided
			if ( saltBytes == null ) {
				saltBytes = new byte[ 16 ];
				randomNumberGenerator.GetBytes( saltBytes );
			}

			// Securely hash the text - https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes?view=net-7.0
			using ( Rfc2898DeriveBytes pbkdf2 = new( text, saltBytes, iterationCount, HashAlgorithmName.SHA512 ) ) {
				byte[] hashBytes = pbkdf2.GetBytes( 512 / 8 );

				// Return the hash in the custom format, with the bytes converted to hexadecimal - https://stackoverflow.com/a/311179
				return $"PBKDF2-{ iterationCount }-{ Convert.ToHexString( saltBytes ).ToLower() }-{ Convert.ToHexString( hashBytes ).ToLower() }";
			}

		}

		// Hashes text using the SHA1 algorithm - https://stackoverflow.com/a/26558102
		public static string SHA1( string text ) => Convert.ToHexString( System.Security.Cryptography.SHA1.Create().ComputeHash( System.Text.Encoding.UTF8.GetBytes( text ) ) ).ToLower();

	}

}
