package com.viral32111.servermonitor

import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.*
import org.junit.Test
import org.junit.runner.RunWith

@RunWith( AndroidJUnit4::class )
class InputValidationTests {

	// Instance URL
	@Test
	fun testInstanceUrlValidation() {

		// Works with root, paths & subdomains
		assertTrue( "Root", validateInstanceUrl( "https://example.com" ) )
		assertTrue( "Path", validateInstanceUrl( "https://example.org/my-instance" ) )
		assertTrue( "Subdomain", validateInstanceUrl( "https://example-tunnel.trycloudflare.com/instance" ) )

		// Our example must work!
		assertTrue( "Setup activity hint", validateInstanceUrl( "https://gateway.example.com/my-instance" ) )

		// Insecure connections are bad
		assertFalse( "Insecure", validateInstanceUrl( "http://example.net/example" ) )

	}

	// Username
	@Test
	fun testUsernameValidation() {

		// Capitalisation, numbers & underscores are good
		assertTrue( "All lowercase", validateCredentialsUsername( "alice" ) )
		assertTrue( "Uppercase & numbers", validateCredentialsUsername( "Bob23" ) )
		assertTrue( "Lowercase & underscore", validateCredentialsUsername( "john_doe" ) )

		// Our example must work!
		assertTrue( "Setup activity hint", validateCredentialsUsername( "John_Doe98" ) )

		// Too short/too long
		assertFalse( "Too short", validateCredentialsUsername( "ab" ) )
		assertFalse( "Too long", validateCredentialsUsername( "abcdefghijklmnopqrstuvwxyz1234567890" ) )

		// Other symbols are bad
		assertFalse( "At symbol", validateCredentialsUsername( "Alice@Somewhere" ) )
		assertFalse( "Equal symbols", validateCredentialsUsername( "=Bob23=" ) )
		assertFalse( "Backtick symbols", validateCredentialsUsername( "J`O`H`N_D`O`E" ) )

	}

	// Password
	@Test
	fun testPasswordValidation() {

		// Needs lowercase, uppercase, symbol, and number
		assertTrue( "Mixed #1", validateCredentialsPassword( "Hello-123" ) )
		assertTrue( "Mixed #2", validateCredentialsPassword( "P4ssw0rd!" ) )
		assertTrue( "Mixed #3", validateCredentialsPassword( "HeyWorld@987" ) )

		// Our example must work!
		assertTrue( "Setup activity hint", validateCredentialsPassword( "Sup3r_Go0d_P4ssw0rd!" ) )

		// Too short
		assertFalse( "Too short", validateCredentialsPassword( "Puppies" ) )

		// No lowercase, uppercase, symbol, or number
		assertFalse( "All lowercase", validateCredentialsPassword( "password" ) )
		assertFalse( "All uppercase", validateCredentialsPassword( "PASSWORD" ) )
		assertFalse( "All numbers", validateCredentialsPassword( "12345678" ) )
		assertFalse( "All symbols", validateCredentialsPassword( "@!£$%^&*()-=[]#:" ) )

	}

}
