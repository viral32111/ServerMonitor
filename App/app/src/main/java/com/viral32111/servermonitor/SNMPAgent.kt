package com.viral32111.servermonitor

import com.google.gson.JsonObject

/**
 * Represents an SNMP agent on a network.
 * @param data The JSON object from the API representing the SNMP agent.
 */
class SNMPAgent( data: JsonObject ) {
	companion object {
		const val receivedTrapsCountWarningThreshold = 1
		const val receivedTrapsCountDangerThreshold = 10
	}

	val address: String
	val port: Int
	val name: String
	val description: String
	val location: String
	val contact: String
	val uptimeSeconds: Long
	val serviceCount: Int

	val receivedTrapsCount: Int
	// TODO: receivedTraps -> logs

	init {
		address = data.get( "address" ).asString
		port = data.get( "port" ).asInt
		name = data.get( "name" ).asString
		description = data.get( "description" ).asString
		location = data.get( "location" ).asString
		contact = data.get( "contact" ).asString
		uptimeSeconds = data.get( "uptimeSeconds" ).asDouble.toLong()
		serviceCount = data.get( "serviceCount" ).asInt

		val receivedTraps = data.get( "receivedTraps" ).asJsonObject
		receivedTrapsCount = receivedTraps.get( "count" ).asInt

		// TODO: supportedActions
		// TODO: logs
	}

	// TODO: getIssues() to return all the current issues (e.g., has received traps, etc.)

}
