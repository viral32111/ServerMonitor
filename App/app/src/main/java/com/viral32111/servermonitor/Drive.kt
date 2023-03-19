package com.viral32111.servermonitor

import com.google.gson.JsonObject

/**
 * Represents a drive on a server.
 * @param data The JSON object from the API representing the drive.
 */
class Drive( data: JsonObject ) {
	val name: String
	val health: Int
	val totalBytesRead: Long
	val rateBytesRead: Long
	val totalBytesWritten: Long
	val rateBytesWritten: Long
	val partitions: Array<Partition>

	init {
		name = data.get( "name" ).asString
		health = data.get( "health" ).asInt

		val bytesRead = data.get( "bytesRead" ).asJsonObject
		totalBytesRead = bytesRead.get( "total" ).asLong
		rateBytesRead = bytesRead.get( "rate" ).asDouble.toLong()

		val bytesWritten = data.get( "bytesWritten" ).asJsonObject
		totalBytesWritten = bytesRead.get( "total" ).asLong
		rateBytesWritten = bytesRead.get( "rate" ).asDouble.toLong()

		val partitionsList = ArrayList<Partition>()
		for ( partition in data.get( "partitions" ).asJsonArray ) partitionsList.add( Partition( ( partition.asJsonObject ) ) )
		partitions = partitionsList.toTypedArray()
	}
}
