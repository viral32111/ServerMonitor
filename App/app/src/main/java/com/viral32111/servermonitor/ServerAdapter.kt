package com.viral32111.servermonitor

import android.content.Context
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.constraintlayout.widget.ConstraintLayout
import androidx.recyclerview.widget.RecyclerView
import kotlin.math.roundToInt
import kotlin.math.roundToLong

// Custom recycler view adapter for the server list - https://developer.android.com/develop/ui/views/layout/recyclerview, https://stackoverflow.com/a/54847887
class ServerAdapter( private val servers: Array<Server>, private val context: Context, private val onServerClickListener: ( server: Server ) -> Unit ): RecyclerView.Adapter<ServerAdapter.ViewHolder>() {

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

		// Set the title to the server's name
		viewHolder.titleTextView.text = server.hostName.uppercase()

		// The server is online...
		if ( server.isOnline() ) {
			viewHolder.statusTextView.text = context.getString( R.string.serversTextViewServerStatusOnline )
			viewHolder.statusTextView.setTextColor( context.getColor( R.color.statusGood ) )

			// Processor Usage
			val processorUsage = server.getProcessorUsage()
			viewHolder.processorTextView.text = String.format( context.getString( R.string.serversTextViewServerProcessorUsage ), processorUsage.atLeastInt( 0 ) )
			viewHolder.processorTextView.setTextColor( colorForValue( context, processorUsage, Server.processorUsageWarningThreshold, Server.processorUsageDangerThreshold ) )

			// Memory Used
			val memoryTotalBytes = server.getMemoryTotal()
			val memoryFreeBytes = server.getMemoryFree()
			val memoryUsedBytes = server.getMemoryUsed( memoryTotalBytes, memoryFreeBytes )
			val memoryUsed = Size( memoryUsedBytes )
			viewHolder.memoryTextView.text = String.format( context.getString( R.string.serversTextViewServerMemoryUsage ), memoryUsed.amount.atLeastLong( 0 ), memoryUsed.suffix.first() )
			viewHolder.memoryTextView.setTextColor( colorForValue( context, memoryUsedBytes, Server.memoryUsedWarningThreshold( memoryTotalBytes ), Server.memoryUsedDangerThreshold( memoryTotalBytes ) ) )

			// Processor Temperature
			val processorTemperature = server.getProcessorTemperature()
			viewHolder.temperatureTextView.text = String.format( context.getString( R.string.serversTextViewServerTemperatureValue ), processorTemperature.atLeastInt( 0 ) )
			viewHolder.temperatureTextView.setTextColor( colorForValue( context, processorTemperature, Server.processorTemperatureWarningThreshold, Server.processorTemperatureDangerThreshold ) )

			// Running Services Count
			val runningServices = server.getRunningServices()
			viewHolder.serviceTextView.text = String.format( context.getString( R.string.serversTextViewServerServicesCount ), runningServices.size )
			viewHolder.serviceTextView.setTextColor( context.getColor( R.color.statusGood ) ) // Not really possible for this to have warning/danger

			// Network I/O
			val networkTotalRateBytes = server.getNetworkTotalRate()
			val networkTotalRate = Size( networkTotalRateBytes )
			viewHolder.networkTextView.text = String.format( context.getString( R.string.serversTextViewServerNetworkUsage ), networkTotalRate.amount.atLeastLong( 0 ), networkTotalRate.suffix.first() )
			viewHolder.networkTextView.setTextColor( colorForValue( context, networkTotalRateBytes, Server.networkTotalRateWarningThreshold, Server.networkTotalRateDangerThreshold ) )

			// Disk I/O
			val driveTotalRateBytes = server.getDriveTotalRate()
			val driveTotalRate = Size( driveTotalRateBytes )
			viewHolder.networkTextView.text = String.format( context.getString( R.string.serversTextViewServerNetworkUsage ), driveTotalRate.amount.atLeastLong( 0 ), driveTotalRate.suffix.first() )
			viewHolder.networkTextView.setTextColor( colorForValue( context, networkTotalRateBytes, Server.driveTotalRateWarningThreshold, Server.driveTotalRateDangerThreshold ) )


			if ( server.drives != null ) {
				val rate = Size( server.drives!!.fold( 0 ) { total, drive -> total + drive.rateBytesRead + drive.rateBytesWritten } )
				viewHolder.diskTextView.text = String.format( context.getString( R.string.serversTextViewServerDiskUsage ), rate.amount.roundToLong().coerceAtLeast( 0 ), rate.suffix.first() )
				viewHolder.diskTextView.setTextColor( context.getColor( R.color.statusGood ) )
			} else {
				viewHolder.diskTextView.text = String.format( context.getString( R.string.serversTextViewServerDiskUsage ), 0, "B" )
				viewHolder.diskTextView.setTextColor( context.getColor( R.color.statusDead ) )
			}

			// Format the uptime into days, hours & minutes
			viewHolder.uptimeTextView.text = String.format( context.getString( R.string.serversTextViewServerUptime ), TimeSpan( server.uptimeSeconds ).toString( false ) )

		// The server is offline...
		} else {
			viewHolder.statusTextView.text = context.getString( R.string.serversTextViewServerStatusOffline )
			viewHolder.statusTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.processorTextView.text = String.format( context.getString( R.string.serversTextViewServerProcessorUsage ), 0 )
			viewHolder.processorTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.memoryTextView.text = String.format( context.getString( R.string.serversTextViewServerMemoryUsage ), 0, "B" )
			viewHolder.memoryTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.temperatureTextView.text = String.format( context.getString( R.string.serversTextViewServerTemperatureValue ), 0 )
			viewHolder.temperatureTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.serviceTextView.text = String.format( context.getString( R.string.serversTextViewServerServicesCount ), 0 )
			viewHolder.serviceTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.networkTextView.text = String.format( context.getString( R.string.serversTextViewServerNetworkUsage ), 0, "B" )
			viewHolder.networkTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.diskTextView.text = String.format( context.getString( R.string.serversTextViewServerDiskUsage ), 0, "B" )
			viewHolder.diskTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.uptimeTextView.text = context.getString( R.string.serversTextViewServerUptimeOffline )
		}
	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = servers.size

}
