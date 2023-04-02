package com.viral32111.servermonitor

class Shared {
	companion object {
		const val sharedPreferencesName = "com.viral32111.ServerMonitor.Settings"
		const val httpRequestQueueTag = "ServerMonitor"
		const val logTag = "ServerMonitor"
		const val percentSymbol = "&#x0025;" // Unicode for percent symbol, required for literal use inside string formatting - https://stackoverflow.com/a/16273262
	}
}
