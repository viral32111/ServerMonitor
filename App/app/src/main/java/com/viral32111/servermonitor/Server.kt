package com.viral32111.servermonitor

import com.google.gson.JsonObject
import kotlin.math.round

// Holds data about a server
class Server( data: JsonObject ) {
	val Identifier: String
	val JobName: String
	val InstanceAddress: String
	val LastScrape: Long
	val HostName: String
	val OperatingSystem: String
	val Architecture: String
	val Version: String
	val UptimeSeconds: Long

	// Decode the JSON object from the GET /servers array
	init {
		Identifier = data.get( "identifier" ).asString
		JobName = data.get( "jobName" ).asString
		InstanceAddress = data.get( "instanceAddress" ).asString
		LastScrape = data.get( "lastScrape" ).asLong
		HostName = data.get( "hostName" ).asString
		OperatingSystem = data.get( "operatingSystem" ).asString
		Architecture = data.get( "architecture" ).asString
		Version = data.get( "version" ).asString
		UptimeSeconds = round( data.get( "uptimeSeconds" ).asDouble ).toLong()

		/*
		Log.d( Shared.logTag, "Identifier: '${ Identifier }'" )
		Log.d( Shared.logTag, "Job Name: '${ JobName }'" )
		Log.d( Shared.logTag, "Instance Address: '${ InstanceAddress }'" )
		Log.d( Shared.logTag, "Last Scrape: '${ LastScrape }'" )
		Log.d( Shared.logTag, "Host Name: '${ HostName }'" )
		Log.d( Shared.logTag, "Operating System: '${ OperatingSystem }'" )
		Log.d( Shared.logTag, "Architecture: '${ Architecture }'" )
		Log.d( Shared.logTag, "Version: '${ Version }'" )
		Log.d( Shared.logTag, "Uptime: '${ UptimeSeconds }' seconds" )
		*/
	}

	// TODO: API call for GET /server, populate more properties with data
}
