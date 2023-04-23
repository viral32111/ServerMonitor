package com.viral32111.servermonitor.adapter

import android.content.Context
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.constraintlayout.widget.ConstraintLayout
import androidx.recyclerview.widget.RecyclerView
import com.viral32111.servermonitor.R
import com.viral32111.servermonitor.Shared
import com.viral32111.servermonitor.data.Drive
import com.viral32111.servermonitor.data.NetworkInterface
import com.viral32111.servermonitor.data.Server
import com.viral32111.servermonitor.helper.Size
import com.viral32111.servermonitor.helper.TimeSpan
import com.viral32111.servermonitor.helper.atLeastRoundInt
import com.viral32111.servermonitor.helper.atLeastRoundLong
import com.viral32111.servermonitor.helper.getAppropriateColor

// Custom recycler view adapter for the server list - https://developer.android.com/develop/ui/views/layout/recyclerview, https://stackoverflow.com/a/54847887
class ServerAdapter(
	private val servers: Array<Server>,
	private val context: Context,
	private val onServerClickListener: ( server: Server) -> Unit
): RecyclerView.Adapter<ServerAdapter.ViewHolder>() {

	// Holds all the UI
	class ViewHolder( view: View ) : RecyclerView.ViewHolder( view ) {
		val constraintLayout: ConstraintLayout
		val titleTextView: TextView
		val statusTextView: TextView
		val processorTextView: TextView
		val memoryTextView: TextView
		val temperatureTextView: TextView
		val serviceTextView: TextView
		val networkTextView: TextView
		val diskTextView: TextView
		val uptimeTextView: TextView

		init {
			Log.d( Shared.logTag, "Initialising new view holder..." )

			// Get relevant UI
			constraintLayout = view.findViewById( R.id.serverConstraintLayout )
			titleTextView = view.findViewById( R.id.serverTitleTextView )
			statusTextView = view.findViewById( R.id.serverStatusTextView )
			processorTextView = view.findViewById( R.id.serverProcessorUsageTextView )
			memoryTextView = view.findViewById( R.id.serverMemoryUsageTextView )
			temperatureTextView = view.findViewById( R.id.serverTemperatureValueTextView )
			serviceTextView = view.findViewById( R.id.serverServicesCountTextView )
			networkTextView = view.findViewById( R.id.serverNetworkUsageTextView )
			diskTextView = view.findViewById( R.id.serverDiskUsageTextView )
			uptimeTextView = view.findViewById( R.id.serverUptimeTextView )
		}
	}

	// Creates new views - called by the layout manager
	override fun onCreateViewHolder( viewGroup: ViewGroup, viewType: Int ): ViewHolder {
		Log.d( Shared.logTag, "Creating new server view..." )
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.fragment_server, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder( viewHolder: ViewHolder, index: Int ) {
		val server = servers[ index ]
		Log.d( Shared.logTag, "Replacing view for server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }')..." )

		// Forward click events - https://stackoverflow.com/a/49969478
		viewHolder.constraintLayout.setOnClickListener {
			Log.d( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') pressed" )
			onServerClickListener.invoke( server )
		}

		// Check if there are any issues with this server
		val areThereIssues = server.areThereIssues()

		// Set the title to the server's name & color depending on if there are any issues
		viewHolder.titleTextView.text = server.hostName.uppercase()
		viewHolder.titleTextView.setTextColor( context.getColor( if ( areThereIssues ) R.color.statusBad else R.color.black ) )

		// Is the server running?
		if ( server.isOnline() ) {

			// Status
			viewHolder.statusTextView.text = context.getString( R.string.serversTextViewServerStatusOnline )
			viewHolder.statusTextView.setTextColor( context.getColor( if ( areThereIssues ) R.color.statusBad else R.color.statusGood ) )

			// Processor Usage
			val processorUsage = server.getProcessorUsage()
			viewHolder.processorTextView.text = context.getString( R.string.serversTextViewServerProcessorUsage ).format( processorUsage.atLeastRoundInt( 0 ) )
			viewHolder.processorTextView.setTextColor( context.getColor( processorUsage.getAppropriateColor( Server.processorUsageWarningThreshold, Server.processorUsageDangerThreshold ) ) )

			// Memory Used
			val memoryTotalBytes = server.getMemoryTotal()
			val memoryFreeBytes = server.getMemoryFree()
			val memoryUsedBytes = server.getMemoryUsed( memoryFreeBytes, memoryTotalBytes )
			val memoryUsed = Size( memoryUsedBytes )
			viewHolder.memoryTextView.text = context.getString( R.string.serversTextViewServerMemoryUsage ).format( memoryUsed.amount.atLeastRoundLong( 0 ), memoryUsed.suffix.first() )
			viewHolder.memoryTextView.setTextColor( context.getColor( memoryUsedBytes.getAppropriateColor( Server.memoryUsedWarningThreshold( memoryTotalBytes ), Server.memoryUsedDangerThreshold( memoryTotalBytes ) ) ) )

			// Processor Temperature
			val processorTemperature = server.getProcessorTemperature()
			viewHolder.temperatureTextView.text = context.getString( R.string.serversTextViewServerTemperatureValue ).format( processorTemperature.atLeastRoundInt( 0 ) )
			viewHolder.temperatureTextView.setTextColor( context.getColor( processorTemperature.getAppropriateColor( Server.processorTemperatureWarningThreshold, Server.processorTemperatureDangerThreshold ) ) )

			// Running Services Count
			val runningServices = server.getRunningServices()
			viewHolder.serviceTextView.text = context.getString( R.string.serversTextViewServerServicesCount ).format( runningServices.size )
			viewHolder.serviceTextView.setTextColor( context.getColor( if ( server.areThereIssuesWithServices() ) R.color.statusBad else R.color.statusGood ) )

			// Network I/O
			val networkTotalRateBytes = server.getNetworkTotalRate()
			val networkTotalRate = Size( networkTotalRateBytes )
			viewHolder.networkTextView.text = context.getString( R.string.serversTextViewServerNetworkUsage ).format( networkTotalRate.amount.atLeastRoundLong( 0 ), networkTotalRate.suffix.first() )
			viewHolder.networkTextView.setTextColor( context.getColor( if ( server.areThereIssuesWithNetworkInterfaces() ) R.color.statusBad else networkTotalRateBytes.getAppropriateColor( NetworkInterface.totalRateWarningThreshold, NetworkInterface.totalRateDangerThreshold ) ) )

			// Drive I/O
			val driveTotalRateBytes = server.getDriveTotalRate()
			val driveTotalRate = Size( driveTotalRateBytes )
			viewHolder.diskTextView.text = context.getString( R.string.serversTextViewServerNetworkUsage ).format( driveTotalRate.amount.atLeastRoundLong( 0 ), driveTotalRate.suffix.first() )
			viewHolder.diskTextView.setTextColor( context.getColor( if ( server.areThereIssuesWithDrives() ) R.color.statusBad else driveTotalRateBytes.getAppropriateColor( Drive.totalRateWarningThreshold, Drive.totalRateDangerThreshold ) ) )

			// Uptime as days, hours & minutes
			viewHolder.uptimeTextView.text = context.getString( R.string.serversTextViewServerUptime ).format( TimeSpan( server.uptimeSeconds ).toString( false ) )

		// The server is offline...
		} else {

			// Status
			viewHolder.statusTextView.text = context.getString(R.string.serversTextViewServerStatusOffline)
			viewHolder.statusTextView.setTextColor( context.getColor( R.color.statusDead ) )

			// Processor Usage
			viewHolder.processorTextView.text = context.getString( R.string.serversTextViewServerProcessorUsage).format( 0 )
			viewHolder.processorTextView.setTextColor( context.getColor( R.color.statusDead ) )

			// Memory Used
			viewHolder.memoryTextView.text = context.getString( R.string.serversTextViewServerMemoryUsage ).format( 0, "B" )
			viewHolder.memoryTextView.setTextColor( context.getColor( R.color.statusDead ) )

			// Processor Temperature
			viewHolder.temperatureTextView.text = context.getString( R.string.serversTextViewServerTemperatureValue).format( 0 )
			viewHolder.temperatureTextView.setTextColor( context.getColor( R.color.statusDead ) )

			// Running Services Count
			viewHolder.serviceTextView.text = context.getString( R.string.serversTextViewServerServicesCount ).format( 0 )
			viewHolder.serviceTextView.setTextColor( context.getColor( R.color.statusDead ) )

			// Network I/O
			viewHolder.networkTextView.text = context.getString( R.string.serversTextViewServerNetworkUsage ).format( 0, "B" )
			viewHolder.networkTextView.setTextColor( context.getColor( R.color.statusDead ) )

			// Disk I/O
			viewHolder.diskTextView.text = context.getString( R.string.serversTextViewServerDiskUsage ).format( 0, "B" )
			viewHolder.diskTextView.setTextColor( context.getColor( R.color.statusDead ) )

			// Uptime
			viewHolder.uptimeTextView.text = context.getString( R.string.serversTextViewServerUptimeOffline )

		}
	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = servers.size

}
