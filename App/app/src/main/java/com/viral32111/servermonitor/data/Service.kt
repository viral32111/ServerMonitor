package com.viral32111.servermonitor.data

import com.google.gson.JsonObject
import com.viral32111.servermonitor.R

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
	private val exitCode: Int
	val uptimeSeconds: Double

	private var actionStartSupported: Boolean? = null
	private var actionStopSupported: Boolean? = null
	private var actionRestartSupported: Boolean? = null

	// TODO: logs

	init {
		runLevel = data.get( "level" ).asString
		serviceName = data.get( "service" ).asString
		displayName = data.get( "name" ).asString
		description = data.get( "description" ).asString
		statusCode = data.get( "statusCode" ).asInt
		exitCode = data.get( "exitCode" ).asInt
		uptimeSeconds = data.get( "uptimeSeconds" ).asDouble

		val supportedActions = data.get( "supportedActions" ).asJsonObject
		actionStartSupported = supportedActions.get( "start" ).asBoolean
		actionStopSupported = supportedActions.get( "stop" ).asBoolean
		actionRestartSupported = supportedActions.get( "restart" ).asBoolean

		// TODO: logs
	}

	/**
	 * Checks if this service is running.
	 * @return True if this service is running, false otherwise.
	 */
	fun isRunning(): Boolean = statusCode == 1 && uptimeSeconds >= 0.0

	/**
	 * Gets the text representing the status of this service.
	 * @return The text representing the status.
	 */
	fun getStatusText(): String = when ( this.statusCode ) {
		0 -> "Stopped" // Linux: inactive, Windows: Stopped
		1 -> "Running" // Linux: active, Windows: Running
		2 -> "Starting" // Linux: activating, Windows: StartPending
		3 -> "Stopping" // Windows: StopPending
		4 -> "Restarting" // Linux: reloading
		5 -> "Failing" // Linux: failed
		6 -> "Finished" // Linux: exited
		7 -> "Continuing" // Windows: ContinuePending
		8 -> "Pausing" // Windows: PausePending
		9 -> "Paused" // Windows: Paused
		else -> "Unknown"
	}

	/**
	* Gets the appropriate color for the status of this service.
	* @return The appropriate color for the status.
	*/
	fun getStatusColor( statusText: String = this.getStatusText() ) = when ( statusText ) {
		"Stopped" -> R.color.statusNeutral
		"Running" -> R.color.statusGood
		"Starting" -> R.color.statusWarning
		"Stopping" -> R.color.statusWarning
		"Restarting" -> R.color.statusWarning
		"Failing" -> R.color.statusBad
		"Finished" -> R.color.statusNeutral
		"Continuing" -> R.color.statusWarning
		"Pausing" -> R.color.statusWarning
		"Paused" -> R.color.statusWarning
		else -> R.color.statusDead
	}

	// Checks if actions are supported
	fun isStartActionSupported() = this.actionStartSupported == true
	fun isStopActionSupported() = this.actionStopSupported == true
	fun isRestartActionSupported() = this.actionRestartSupported == true

	// Checks if there are any issues - exited/failed, error exit code
	fun areThereIssues(): Boolean {
		if ( this.getStatusText() == "Unknown" ) return false // We have no idea
		if ( this.statusCode != 0 && this.statusCode != 1 ) return this.exitCode != 0
		return this.statusCode == 4 || this.statusCode == 5
	}

}
