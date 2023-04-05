package com.viral32111.servermonitor

import android.content.Context
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

	var isStartActionSupported: Boolean? = null
	var isStopActionSupported: Boolean? = null
	var isRestartActionSupported: Boolean? = null

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
		isStartActionSupported = supportedActions.get( "start" ).asBoolean
		isStopActionSupported = supportedActions.get( "stop" ).asBoolean
		isRestartActionSupported = supportedActions.get( "restart" ).asBoolean

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
	* @param context The application context, for fetching a color resource.
	* @return The appropriate color for the status.
	*/
	fun getStatusColor( context: Context): Int = context.getColor( when ( getStatusText() ) {
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
	} )
}
