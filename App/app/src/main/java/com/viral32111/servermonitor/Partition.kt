package com.viral32111.servermonitor

import com.google.gson.JsonObject

/**
 * Represents a drive of a drive.
 * @param data The JSON object on the drive representing the partition.
 */
class Partition( data: JsonObject ) {
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
