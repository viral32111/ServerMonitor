package com.viral32111.servermonitor

import com.google.gson.JsonObject
import kotlin.math.roundToLong

/**
 * Represents a drive of a drive.
 * @param data The JSON object on the drive representing the partition.
 */
class Partition( data: JsonObject ) {
	companion object {
		fun usedBytesWarningThreshold( totalBytes: Long ) = ( totalBytes / 1.33 ).roundToLong() // 75%
		fun usedBytesDangerThreshold( totalBytes: Long ) = ( totalBytes / 1.1 ).roundToLong() // 90%

		const val usageWarningThreshold = 75.0
		const val usageDangerThreshold = 90.0
	}

	val name: String
	val mountpoint: String
	val totalBytes: Long
	val freeBytes: Long

	init {
		name = data.get( "name" ).asString
		mountpoint = data.get( "mountpoint" ).asString
		totalBytes = data.get( "totalBytes" ).asLong
		freeBytes = data.get( "freeBytes" ).asLong
	}
}
