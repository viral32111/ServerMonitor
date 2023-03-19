package com.viral32111.servermonitor

import com.google.gson.JsonObject

/**
 * Represents a service on a server.
 * @param data The JSON object from the API representing the service.
 */
class Service( data: JsonObject ) {
	var runLevel: String? = null
	var serviceName: String? = null
	var displayName: String? = null
	var description: String? = null
	var statusCode: Int? = null
	var exitCode: Int? = null
	var uptimeSeconds: Double? = null
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
	}

	/**
	 * Checks if this service is running.
	 * @return True if this service is running, false otherwise.
	 */
	fun isRunning(): Boolean = if ( statusCode != null && uptimeSeconds != null ) statusCode!! == 1 && uptimeSeconds!! >= 0.0 else false
}
