package com.viral32111.servermonitor

import com.google.gson.JsonObject
import kotlin.math.roundToLong

/**
 * Represents a drive on a server.
 * @param data The JSON object from the API representing the drive.
 */
class Drive( data: JsonObject ) {
	companion object { // No idea what the thresholds should be, so guesstimate
		const val writeRateWarningThreshold = 1024L * 1024L // 1 MiB
		const val writeRateDangerThreshold = 1024L * 1024L * 10L // 10 MiB

		const val readRateWarningThreshold = 1024L * 1024L // 1 MiB
		const val readRateDangerThreshold = 1024L * 1024L * 10L // 10 MiB

		const val totalRateWarningThreshold = readRateWarningThreshold + writeRateWarningThreshold
		const val totalRateDangerThreshold = readRateDangerThreshold + writeRateDangerThreshold

		const val healthWarningThreshold = 99
		const val healthDangerThreshold = 90
	}

	val name: String
	val health: Int
	private val totalBytesRead: Long // Unused
	val rateBytesRead: Long
	private val totalBytesWritten: Long // Unused
	val rateBytesWritten: Long
	private val partitions: Array<Partition>

	init {
		name = data.get( "name" ).asString
		health = data.get( "health" ).asInt

		val bytesRead = data.get( "bytesRead" ).asJsonObject
		totalBytesRead = bytesRead.get( "total" ).asLong
		rateBytesRead = bytesRead.get( "rate" ).asDouble.toLong()

		val bytesWritten = data.get( "bytesWritten" ).asJsonObject
		totalBytesWritten = bytesWritten.get( "total" ).asLong
		rateBytesWritten = bytesWritten.get( "rate" ).asDouble.toLong()

		val partitionsList = ArrayList<Partition>()
		for ( partition in data.get( "partitions" ).asJsonArray ) partitionsList.add( Partition( ( partition.asJsonObject ) ) )
		partitions = partitionsList.toTypedArray()
	}

	// Gets the partitions
	fun getPartitions() = this.partitions.reversedArray()

}
