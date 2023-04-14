package com.viral32111.servermonitor

import com.google.gson.JsonObject

/**
 * Represents a network interface on a server.
 * @param data The JSON object from the API representing the network interface.
 */
class NetworkInterface( data: JsonObject ) {
	companion object { // No idea what the thresholds should be, so guesstimate
		const val transmitRateWarningThreshold = 1024L * 1024L // 1 MiB
		const val transmitRateDangerThreshold = transmitRateWarningThreshold * 10L // 10 MiB
		const val receiveRateWarningThreshold = 1024L * 1024L // 1 MiB
		const val receiveRateDangerThreshold = receiveRateWarningThreshold * 10L // 10 MiB
		const val totalRateWarningThreshold = transmitRateWarningThreshold + receiveRateWarningThreshold
		const val totalRateDangerThreshold = transmitRateDangerThreshold + receiveRateDangerThreshold

		const val transmitWarningThreshold = 1024L * 1024L * 1024L // 1 GiB
		const val transmitDangerThreshold = transmitWarningThreshold * 10L // 10 GiB
		const val receiveWarningThreshold = 1024L * 1024L * 1024L // 1 GiB
		const val receiveDangerThreshold = receiveWarningThreshold * 10 // 10 GiB
		const val totalWarningThreshold = 1024L * 1024L * 1024L
		const val totalDangerThreshold = totalWarningThreshold * 1024L
	}

	val name: String
	val totalBytesSent: Long
	val rateBytesSent: Long
	val totalBytesReceived: Long
	val rateBytesReceived: Long

	init {
		name = data.get( "name" ).asString

		val bytesSent = data.get( "bytesSent" ).asJsonObject
		totalBytesSent = bytesSent.get( "total" ).asLong
		rateBytesSent = bytesSent.get( "rate" ).asDouble.toLong()

		val bytesReceived = data.get( "bytesReceived" ).asJsonObject
		totalBytesReceived = bytesReceived.get( "total" ).asLong
		rateBytesReceived = bytesReceived.get( "rate" ).asDouble.toLong()
	}

	// Checks if there are any issues - transmit/receive too high
	fun areThereIssues() = rateBytesSent >= transmitRateDangerThreshold || rateBytesReceived >= receiveRateDangerThreshold || totalBytesSent >= transmitDangerThreshold || totalBytesReceived >= receiveDangerThreshold

}
