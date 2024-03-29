package com.viral32111.servermonitor.data

import android.util.Log
import com.google.gson.JsonObject
import com.google.gson.JsonParseException
import com.google.gson.JsonSyntaxException
import com.viral32111.servermonitor.Shared
import com.viral32111.servermonitor.helper.API
import com.viral32111.servermonitor.helper.APIException
import com.viral32111.servermonitor.helper.atLeastRoundInt
import com.viral32111.servermonitor.helper.toArrayList
import kotlin.math.round
import kotlin.math.roundToLong

// Holds data about a server
class Server( data: JsonObject, extended: Boolean = false ) {
	companion object {

		// Processor
		const val processorUsageWarningThreshold = 50.0f
		const val processorUsageDangerThreshold = 80.0f
		const val processorTemperatureWarningThreshold = 60.0f
		const val processorTemperatureDangerThreshold = 90.0f

		// Memory
		fun memoryUsedWarningThreshold( totalBytes: Long ): Long = ( totalBytes / 2.0 ).roundToLong() // 50%
		fun memoryUsedDangerThreshold( totalBytes: Long ): Long = ( totalBytes / 1.25 ).roundToLong() // 80%
		const val memoryUsageWarningThreshold = 50.0f
		const val memoryUsageDangerThreshold = 80.0f

		// Swap
		fun swapUsedWarningThreshold( totalBytes: Long ): Long = ( totalBytes / 1.4 ).roundToLong() // 70%
		fun swapUsedDangerThreshold( totalBytes: Long ): Long = ( totalBytes / 1.1 ).roundToLong() // 90%
		const val swapUsageWarningThreshold = 70.0f
		const val swapUsageDangerThreshold = 90.0f
	}

	var identifier: String
	var jobName: String
	var instanceAddress: String
	private var lastScrape: Long
	var hostName: String
	private var operatingSystem: String
	private var architecture: String
	var version: String
	var uptimeSeconds: Long

	private var actionShutdownSupported: Boolean? = null
	private var actionRebootSupported: Boolean? = null

	private var processorUsage: Float? = null
	private var processorFrequency: Float? = null
	private var processorTemperature: Float? = null

	private var memoryTotalBytes: Long? = null
	private var memoryFreeBytes: Long? = null
	private var swapTotalBytes: Long? = null
	private var swapFreeBytes: Long? = null

	private var drives: Array<Drive>? = null
	private var networkInterfaces: Array<NetworkInterface>? = null

	private var services: Array<Service>? = null
	private var dockerContainers: Array<DockerContainer>? = null

	var snmpCommunity: String? = null
	private var snmpAgents: Array<SNMPAgent>? = null

	// Deserialize the JSON object
	init {
		identifier = data.get( "identifier" ).asString
		jobName = data.get( "jobName" ).asString
		instanceAddress = data.get( "instanceAddress" ).asString
		lastScrape = data.get( "lastScrape" ).asLong
		hostName = data.get( "hostName" ).asString
		operatingSystem = data.get( "operatingSystem" ).asString
		architecture = data.get( "architecture" ).asString
		version = data.get( "version" ).asString
		uptimeSeconds = round( data.get( "uptimeSeconds" ).asDouble ).toLong()

		if ( extended ) updateUsingAPIData( data )
	}

	// Checks if the server is online or offline
	fun isOnline(): Boolean = uptimeSeconds >= 0

	// Checks if this server is running Windows
	fun isOperatingSystemWindows() = this.operatingSystem.lowercase().contains( "microsoft windows" )

	/******************************************************/

	// Gets the processor usage/frequency/temperature
	fun getProcessorUsage() = this.processorUsage?.coerceIn( 0.0f, 100.0f ) ?: -1.0f // Clamp to be safe
	fun getProcessorFrequency() = this.processorFrequency?.times( 1000.0f * 1000.0f )?.atLeastRoundInt( 0 ) ?: -1 // Convert to Hz from MHz returned by API
	fun getProcessorTemperature() = this.processorTemperature ?: -1.0f

	/******************************************************/

	// Gets the total/free memory in bytes
	fun getMemoryTotal() = this.memoryTotalBytes ?: -1L
	fun getMemoryFree() = this.memoryFreeBytes ?: -1L

	// Gets the used memory in bytes and as a percentage - these accept free/long bytes as the properties may have changed since the last call
	fun getMemoryUsed( freeBytes: Long? = this.getMemoryFree(), totalBytes: Long? = this.getMemoryTotal() ): Long = getBytesUsed( freeBytes, totalBytes )
	fun getMemoryUsage( freeBytes: Long? = this.getMemoryFree(), totalBytes: Long? = this.getMemoryTotal() ): Float = getBytesUsage( freeBytes, totalBytes )

	/******************************************************/

	// Gets the total/free swap in bytes
	fun getSwapTotal() = this.swapTotalBytes ?: -1L
	fun getSwapFree() = this.swapFreeBytes ?: -1L

	// Gets the used swap in bytes and as a percentage - these accept free/long bytes as the properties may have changed since the last call
	fun getSwapUsed( freeBytes: Long? = this.getSwapFree(), totalBytes: Long? = this.getSwapTotal() ): Long = getBytesUsed( freeBytes, totalBytes )
	fun getSwapUsage( freeBytes: Long? = this.getSwapFree(), totalBytes: Long? = this.getSwapTotal() ): Float = getBytesUsage( freeBytes, totalBytes )

	/******************************************************/

	// Gets all the network interfaces
	fun getNetworkInterfaces(): Array<NetworkInterface> = this.networkInterfaces?.reversedArray() ?: emptyArray()

	// Gets the total network transmit/receive rate in bytes
	fun getNetworkTotalTransmitRate() = this.networkInterfaces?.fold( 0L ) { total, networkInterface -> total + networkInterface.rateBytesSent } ?: -1L
	fun getNetworkTotalReceiveRate() = this.networkInterfaces?.fold( 0L ) { total, networkInterface -> total + networkInterface.rateBytesReceived } ?: -1L
	fun getNetworkTotalRate() = this.networkInterfaces?.fold( 0L ) { total, networkInterface -> total + networkInterface.rateBytesSent + networkInterface.rateBytesReceived } ?: -1L

	/******************************************************/

	// Gets all the drives
	fun getDrives(): Array<Drive> = this.drives?.reversedArray() ?: emptyArray()

	// Gets the total drive read/write rate in bytes
	fun getDriveTotalReadRate() = this.drives?.fold( 0L ) { total, drive -> total + drive.rateBytesRead } ?: -1L
	fun getDriveTotalWriteRate() = this.drives?.fold( 0L ) { total, drive -> total + drive.rateBytesWritten } ?: -1L
	fun getDriveTotalRate() = this.drives?.fold( 0L ) { total, drive -> total + drive.rateBytesRead + drive.rateBytesWritten } ?: -1L

	/******************************************************/

	// Calculates the number of used bytes
	private fun getBytesUsed( freeBytes: Long?, totalBytes: Long? ): Long {
		if ( freeBytes == null || totalBytes == null ) return -1L // Not fetched yet
		if ( freeBytes < 0L || totalBytes < 0L ) return -1L // No metrics yet

		return ( totalBytes - freeBytes ).coerceAtLeast( 0 ) // Clamp to be safe
	}

	// Calculates the percentage of used bytes
	private fun getBytesUsage( freeBytes: Long?, totalBytes: Long? ): Float {
		if ( freeBytes == null || totalBytes == null ) return -1.0f // Not fetched yet
		if ( freeBytes < 0L || totalBytes < 0L ) return -1.0f // No metrics yet

		val usedBytes = getBytesUsed( freeBytes, totalBytes )
		val usagePercentage = ( usedBytes.toDouble() / totalBytes.toDouble() ) * 100.0f

		return usagePercentage.toFloat().coerceIn( 0.0f, 100.0f ) // Clamp to be safe
	}

	/******************************************************/

	// Checks if actions are supported
	fun isShutdownActionSupported() = this.actionShutdownSupported == true
	fun isRebootActionSupported() = this.actionRebootSupported == true

	/******************************************************/

	// Gets all the services
	fun getServices(): Array<Service> = this.services ?: emptyArray()

	// Attempts to find a given service
	fun findService( name: String ): Service? = this.getServices().find { service -> service.serviceName == name }

	// Gets a list of only the services that are running
	fun getRunningServices(): Array<Service> = this.getServices().filter { it.isRunning() }.toTypedArray()

	// Gets all the services but in a better order - https://stackoverflow.com/a/59402330
	fun getSortedServices(): Array<Service> {

		// Convert fixed array to list
		val services = this.getServices().toArrayList()

		// Sort by name in alphabetical order - https://stackoverflow.com/a/53354117
		services.sortBy { it.displayName }

		// Sort by status code - groups up services with the same status (running, stopped, etc.)
		services.sortWith( Comparator { service1: Service, service2: Service ->
			return@Comparator service1.statusCode - service2.statusCode
		} )

		// Sort by familiar services - groups up commonly used/recognised services
		services.sortWith( compareBy { it.serviceName in arrayOf(

			// Windows
			"Schedule", "EventLog",
			"pla", // Performance Logs & Alerts
			"VBoxService", // VirtualBox Guest Additions
			"wuauserv", // Windows Update
			"W32Time", // Windows Time
			"mpssvc", // Windows Defender Firewall
			"TermService", // Remote Desktop Services
			"Cloudflared",
			"Dhcp", "DHCPServer",
			"Dnscache", "DNS",
			"SNMP", "SNMPTRAP",

			// Linux
			"apparmor",
			"thermald",
			"snapd", "unattended-upgrades",
			"lvm2-monitor", "lvm",
			"cloudflared",
			"ssh", "sshd", "ssh-agent",
			"docker", "dockerd", "containerd"

		) } )

		// Reverse the order - moves familiar & running services to the top
		services.reverse()

		// Convert back to fixed array before returning
		return services.toTypedArray()

	}

	/******************************************************/

	// Gets all the Docker containers
	fun getDockerContainers() = this.dockerContainers?.reversedArray() ?: emptyArray()

	/******************************************************/

	// Gets all the SNMP agents
	fun getSNMPAgents() = this.snmpAgents?.reversedArray() ?: emptyArray()

	/******************************************************/

	// Checks if there are any issues for each component
	private fun areThereIssuesWithProcessor(): Boolean = this.getProcessorUsage() >= processorUsageDangerThreshold || this.getProcessorTemperature() >= processorTemperatureDangerThreshold
	private fun areThereIssuesWithMemory(): Boolean {
		val totalBytes = this.getMemoryTotal()
		val freeBytes = this.getMemoryFree()

		if ( freeBytes < 0L || totalBytes < 0L ) return false // No metrics yet?

		return this.getMemoryUsed( freeBytes, totalBytes ) >= memoryUsedDangerThreshold( totalBytes ) || this.getMemoryUsage( freeBytes, totalBytes ) >= memoryUsageDangerThreshold
	}
	private fun areThereIssuesWithSwap(): Boolean {
		val totalBytes = this.getSwapTotal()
		val freeBytes = this.getSwapFree()

		if ( freeBytes < 0L || totalBytes < 0L ) return false // No metrics yet?

		return this.getSwapUsed( freeBytes, totalBytes ) >= swapUsedDangerThreshold( totalBytes ) || this.getSwapUsage( freeBytes, totalBytes ) >= swapUsageDangerThreshold
	}
	fun areThereIssuesWithNetworkInterfaces(): Boolean = this.getNetworkInterfaces().any { networkInterface -> networkInterface.areThereIssues() }
	fun areThereIssuesWithDrives(): Boolean = this.getDrives().any { drive -> drive.areThereIssues() }
	fun areThereIssuesWithServices(): Boolean {
		val servicesWithIssues = this.getServices().filter { service -> service.areThereIssues() }
		Log.w( Shared.logTag, "Issues with services: '${ servicesWithIssues.joinToString( ", " ) { service -> "${ service.serviceName } (${ service.displayName })" } }'" )
		return servicesWithIssues.isNotEmpty()
	}
	private fun areThereIssuesWithDockerContainers(): Boolean = this.getDockerContainers().any { dockerContainer -> dockerContainer.areThereIssues() }
	private fun areThereIssuesWithSNMPAgents(): Boolean = this.getSNMPAgents().any { snmpAgent -> snmpAgent.areThereIssues() }

	// Checks if there are any issues with any components
	fun areThereIssues(): Boolean {
		if ( !this.isOnline() ) {
			Log.w( Shared.logTag, "Skipping issue check as server '$hostName' is offline..." )
			return false
		}

		val areThereProcessorIssues = areThereIssuesWithProcessor()
		val areThereMemoryIssues = areThereIssuesWithMemory()
		val areThereSwapIssues = areThereIssuesWithSwap()
		val areThereNetworkInterfaceIssues = areThereIssuesWithNetworkInterfaces()
		val areThereDriveIssues = areThereIssuesWithDrives()
		val areThereServiceIssues = areThereIssuesWithServices()
		val areThereDockerContainerIssues = areThereIssuesWithDockerContainers()
		val areThereSNMPAgentIssues = areThereIssuesWithSNMPAgents()

		/*
		Log.wtf( Shared.logTag, "Processor issues? $areThereProcessorIssues" )
		Log.wtf( Shared.logTag, "Memory issues? $areThereMemoryIssues" )
		Log.wtf( Shared.logTag, "Swap issues? $areThereSwapIssues" )
		Log.wtf( Shared.logTag, "Network interface issues? $areThereNetworkInterfaceIssues" )
		Log.wtf( Shared.logTag, "Drive issues? $areThereDriveIssues" )
		Log.wtf( Shared.logTag, "Service issues? $areThereServiceIssues" )
		Log.wtf( Shared.logTag, "Docker container issues? $areThereDockerContainerIssues" )
		Log.wtf( Shared.logTag, "SNMP agent issues? $areThereSNMPAgentIssues" )
		*/

		return areThereProcessorIssues || areThereMemoryIssues || areThereSwapIssues || areThereNetworkInterfaceIssues || areThereDriveIssues || areThereServiceIssues || areThereDockerContainerIssues || areThereSNMPAgentIssues
	}

	/******************************************************/

	/**
	 * Updates this server's data.
	 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
	 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
	 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
	 * @throws NullPointerException The API response contained an unexpected null property.
	 */
	private fun updateUsingAPIData( data: JsonObject ) {

		// Update basic information
		identifier = data.get( "identifier" ).asString
		jobName = data.get( "jobName" ).asString
		instanceAddress = data.get( "instanceAddress" ).asString
		lastScrape = data.get( "lastScrape" ).asLong
		hostName = data.get( "hostName" ).asString
		operatingSystem = data.get( "operatingSystem" ).asString
		architecture = data.get( "architecture" ).asString
		version = data.get( "version" ).asString
		uptimeSeconds = round( data.get( "uptimeSeconds" ).asDouble ).toLong()

		val supportedActions = data.get( "supportedActions" ).asJsonObject
		actionShutdownSupported = supportedActions.get( "shutdown" ).asBoolean
		actionRebootSupported = supportedActions.get( "reboot" ).asBoolean

		val resources = data.get( "resources" ).asJsonObject

		// Update processor metrics
		val processor = resources.get( "processor" ).asJsonObject
		processorUsage = processor.get( "usage" ).asFloat
		processorFrequency = processor.get( "frequency" ).asFloat
		processorTemperature = processor.get( "temperature" ).asFloat

		// Update memory metrics
		val memory = resources.get( "memory" ).asJsonObject
		memoryTotalBytes = memory.get( "totalBytes" ).asLong
		memoryFreeBytes = memory.get( "freeBytes" ).asLong

		// Update swap/page-file metrics
		val swap = memory.get( "swap" ).asJsonObject
		swapTotalBytes = swap.get( "totalBytes" ).asLong
		swapFreeBytes = swap.get( "freeBytes" ).asLong

		// Drives
		val drivesList = ArrayList<Drive>()
		for ( drive in resources.get( "drives" ).asJsonArray.reversed() ) drivesList.add( Drive( drive.asJsonObject ) )
		drives = drivesList.toTypedArray()

		// Network Interfaces
		val netInterfacesList = ArrayList<NetworkInterface>()
		for ( netInterface in resources.get( "networkInterfaces" ).asJsonArray ) netInterfacesList.add( NetworkInterface( netInterface.asJsonObject ) )
		networkInterfaces = netInterfacesList.toTypedArray()

		// Services
		val servicesList = ArrayList<Service>()
		for ( service in data.get( "services" ).asJsonArray ) servicesList.add( Service( service.asJsonObject ) )
		services = servicesList.toTypedArray()

		// Docker containers
		val dockerContainersList = ArrayList<DockerContainer>()
		for ( dockerContainer in data.get( "dockerContainers" ).asJsonArray ) dockerContainersList.add( DockerContainer( dockerContainer.asJsonObject ) )
		dockerContainers = dockerContainersList.toTypedArray()

		// SNMP
		val snmp = data.get( "snmp" ).asJsonObject
		snmpCommunity = snmp.get( "community" ).asString
		val snmpAgentsList = ArrayList<SNMPAgent>()
		for ( snmpAgent in snmp.get( "agents" ).asJsonArray ) snmpAgentsList.add( SNMPAgent( snmpAgent.asJsonObject ) )
		snmpAgents = snmpAgentsList.toTypedArray()

	}

	/**
	 * Updates this server's data.
	 * @param instanceUrl The URL to the connector instance, using the HTTPS schema.
	 * @param credentialsUsername The user to authenticate as.
	 * @param credentialsPassword The password to authenticate with.
	 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
	 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
	 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
	 * @throws NullPointerException The API response contained an unexpected null property.
	 */
	suspend fun updateFromAPI( instanceUrl: String, credentialsUsername: String, credentialsPassword: String ) {

		// Fetch the server, will throw a null pointer exception if null
		val data = API.getServer( instanceUrl, credentialsUsername, credentialsPassword, identifier )!!
		Log.d( Shared.logTag, "Fetched server '${ hostName }' ('${ identifier }', '${ jobName }', '${ instanceAddress }') from API" )

		// Set the properties
		updateUsingAPIData( data )

	}

}
