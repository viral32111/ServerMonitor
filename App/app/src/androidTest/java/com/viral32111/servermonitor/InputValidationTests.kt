package com.viral32111.servermonitor

import android.content.res.Resources
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import org.junit.Test
import org.junit.Assert.*
import org.junit.runner.RunWith

@RunWith( AndroidJUnit4::class )
class InputValidationTests {

	// Instance URL
	@Test
	fun testInstanceUrlValidation() {
		val appContext = InstrumentationRegistry.getInstrumentation().targetContext

		// Works with root, paths & subdomains
		assertTrue( "Root", validateInstanceUrl( "https://example.com" ) )
		assertTrue( "Path", validateInstanceUrl( "https://example.org/my-instance" ) )
		assertTrue( "Subdomain", validateInstanceUrl( "https://example-tunnel.trycloudflare.com/instance" ) )

		// Our examples must work!
		assertTrue( "Setup activity hint", validateInstanceUrl( appContext.getString( R.string.setupEditTextInstanceUrlHint ) ) )
		assertTrue( "Settings activity hint", validateInstanceUrl( appContext.getString( R.string.settingsEditTextInstanceUrlHint ) ) )

		// Insecure connections are bad
		assertFalse( "Insecure", validateInstanceUrl( "http://example.net/example" ) )
	}

	// Username
	@Test
	fun testUsernameValidation() {
		val appContext = InstrumentationRegistry.getInstrumentation().targetContext

		// Capitalisation, numbers & underscores are good
		assertTrue( "All lowercase", validateCredentialsUsername( "alice" ) )
		assertTrue( "Uppercase & numbers", validateCredentialsUsername( "Bob23" ) )
		assertTrue( "Lowercase & underscore", validateCredentialsUsername( "john_doe" ) )

		// Our examples must work!
		assertTrue( "Setup activity hint", validateCredentialsUsername( appContext.getString( R.string.setupEditTextCredentialsUsernameHint ) ) )
		assertTrue( "Settings activity hint", validateCredentialsUsername( appContext.getString( R.string.settingsEditTextCredentialsUsernameHint ) ) )

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
		val appContext = InstrumentationRegistry.getInstrumentation().targetContext

		// Needs lowercase, uppercase, symbol, and number
		assertTrue( "Mixed #1", validateCredentialsPassword( "Hello-123" ) )
		assertTrue( "Mixed #2", validateCredentialsPassword( "P4ssw0rd!" ) )
		assertTrue( "Mixed #3", validateCredentialsPassword( "HeyWorld@987" ) )

		// Our examples must work!
		assertTrue( "Setup activity hint", validateCredentialsPassword( appContext.getString( R.string.setupEditTextCredentialsPasswordHint ) ) )
		assertTrue( "Settings activity hint", validateCredentialsPassword( appContext.getString( R.string.settingsEditTextCredentialsPasswordHint ) ) )

		// Too short
		assertFalse( "Too short", validateCredentialsPassword( "Puppies" ) )

		// No lowercase, uppercase, symbol, or number
		assertFalse( "All lowercase", validateCredentialsPassword( "password" ) )
		assertFalse( "All uppercase", validateCredentialsPassword( "PASSWORD" ) )
		assertFalse( "All numbers", validateCredentialsPassword( "12345678" ) )
		assertFalse( "All symbols", validateCredentialsPassword( "@!£$%^&*()-=[]#:" ) )

	}

}
