package com.viral32111.servermonitor

import com.google.gson.JsonObject

/**
 * Represents a service on a server.
 * @param data The JSON object from the API representing the service.
 */
class Service( data: JsonObject ) {
	val runLevel: String
	val serviceName: String
	val displayName: String
	val description: String
	val statusCode: Int
	val exitCode: Int
	val uptimeSeconds: Double

	// TODO: supportedActions
	// TODO: logs

	init {
		runLevel = data.get( "level" ).asString
		serviceName = data.get( "service" ).asString
		displayName = data.get( "name" ).asString
		description = data.get( "description" ).asString
		statusCode = data.get( "statusCode" ).asInt
		exitCode = data.get( "exitCode" ).asInt
		uptimeSeconds = data.get( "uptimeSeconds" ).asDouble

		// TODO: supportedActions
		// TODO: logs
	}

	/**
	 * Checks if this service is running.
	 * @return True if this service is running, false otherwise.
	 */
	fun isRunning(): Boolean = statusCode == 1 && uptimeSeconds >= 0.0
}
