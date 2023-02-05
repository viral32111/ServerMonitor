package com.viral32111.servermonitor

import androidx.test.platform.app.InstrumentationRegistry
import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.Assert.*

@RunWith(AndroidJUnit4::class)
class AppContext {

	// Context
	@Test
	fun testAppContext() {
		val appContext = InstrumentationRegistry.getInstrumentation().targetContext
		assertEquals( "com.viral32111.servermonitor", appContext.packageName )
	}

}
