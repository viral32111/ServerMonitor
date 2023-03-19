package com.viral32111.servermonitor

import com.google.gson.JsonObject

/**
 * Represents a network interface on a server.
 * @param data The JSON object from the API representing the network interface.
 */
class NetworkInterface( data: JsonObject ) {
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
}
