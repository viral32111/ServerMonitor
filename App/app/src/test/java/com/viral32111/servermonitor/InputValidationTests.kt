package com.viral32111.servermonitor

import org.junit.Test
import org.junit.Assert.*

class InputValidationTests {

	@Test
	fun testInstanceUrlValidation() {
		assertTrue( validateInstanceUrl( "https://example.com" ) )
		assertTrue( validateInstanceUrl( "https://example.org/my-instance" ) )
		assertTrue( validateInstanceUrl( "https://example-tunnel.trycloudflare.com/instance" ) )

		assertFalse( validateInstanceUrl( "http://example.net/example" ) )
	}

	// TODO: Test username validation
	// TODO: Test password validation

}
