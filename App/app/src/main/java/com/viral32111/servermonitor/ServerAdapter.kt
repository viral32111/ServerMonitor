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

		// Forward click events to the given listener - https://stackoverflow.com/a/49969478
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
			if ( server.processorUsage != null ) {
				viewHolder.processorTextView.text = String.format( context.getString( R.string.serversTextViewServerProcessorUsage ), server.processorUsage!!.toInt() )
				viewHolder.processorTextView.setTextColor( context.getColor( R.color.statusGood ) )
			} else {
				viewHolder.processorTextView.text = String.format( context.getString( R.string.serversTextViewServerProcessorUsage ), 0 )
				viewHolder.processorTextView.setTextColor( context.getColor( R.color.statusDead ) )
			}

			// Memory Used
			if ( server.memoryTotalBytes != null && server.memoryFreeBytes != null ) {
				val size = Size( server.memoryTotalBytes!! - server.memoryFreeBytes!! )
				viewHolder.memoryTextView.text = String.format( context.getString( R.string.serversTextViewServerMemoryUsage ), size.amount.roundToInt(), size.suffix.substring( 0, 1 ) )
				viewHolder.memoryTextView.setTextColor( context.getColor( R.color.statusGood ) )
			} else {
				viewHolder.memoryTextView.text = String.format( context.getString( R.string.serversTextViewServerMemoryUsage ), 0, "K" )
				viewHolder.memoryTextView.setTextColor( context.getColor( R.color.statusDead ) )
			}

			// Processor Temperature
			if ( server.processorTemperature != null && server.processorTemperature!! >= 0f ) {
				viewHolder.temperatureTextView.text = String.format( context.getString( R.string.serversTextViewServerTemperatureValue ), server.processorTemperature!!.toInt() )
				viewHolder.temperatureTextView.setTextColor( context.getColor( R.color.statusGood ) )
			} else {
				viewHolder.temperatureTextView.text = String.format( context.getString( R.string.serversTextViewServerTemperatureValue ), 0 )
				viewHolder.temperatureTextView.setTextColor( context.getColor( R.color.statusDead ) )
			}

			// Running Services Count
			if ( server.services != null ) {
				val runningServices = ArrayList<Service>()
				for ( service in server.services!! ) if ( service.isRunning() ) runningServices.add( service )

				viewHolder.serviceTextView.text = String.format( context.getString( R.string.serversTextViewServerServicesCount ), runningServices.size )
				viewHolder.serviceTextView.setTextColor( context.getColor( R.color.statusGood ) )
			} else {
				viewHolder.serviceTextView.text = String.format( context.getString( R.string.serversTextViewServerServicesCount ), server.services!!.size )
				viewHolder.serviceTextView.setTextColor( context.getColor( R.color.statusDead ) )
			}

			// Network I/O
			if ( server.networkInterfaces != null ) {
				val rate = Size( server.networkInterfaces!!.fold( 0 ) { total, networkInterface -> total + networkInterface.rateBytesSent + networkInterface.rateBytesReceived } ) // https://kotlinlang.org/docs/collection-aggregate.html#fold-and-reduce
				viewHolder.networkTextView.text = String.format( context.getString( R.string.serversTextViewServerNetworkUsage ), rate.amount.roundToLong().coerceAtLeast( 0 ), rate.suffix.substring( 0, 1 ) )
				viewHolder.networkTextView.setTextColor( context.getColor( R.color.statusGood ) )
			} else {
				viewHolder.networkTextView.text = String.format( context.getString( R.string.serversTextViewServerNetworkUsage ), 0, "K" )
				viewHolder.networkTextView.setTextColor( context.getColor( R.color.statusDead ) )
			}

			// Disk I/O
			if ( server.drives != null ) {
				val rate = Size( server.drives!!.fold( 0 ) { total, drive -> total + drive.rateBytesRead + drive.rateBytesWritten } )
				viewHolder.diskTextView.text = String.format( context.getString( R.string.serversTextViewServerDiskUsage ), rate.amount.roundToLong().coerceAtLeast( 0 ), rate.suffix.substring( 0, 1 ) )
				viewHolder.diskTextView.setTextColor( context.getColor( R.color.statusGood ) )
			} else {
				viewHolder.diskTextView.text = String.format( context.getString( R.string.serversTextViewServerDiskUsage ), 0, "K" )
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

			viewHolder.memoryTextView.text = String.format( context.getString( R.string.serversTextViewServerMemoryUsage ), 0, "K" )
			viewHolder.memoryTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.temperatureTextView.text = String.format( context.getString( R.string.serversTextViewServerTemperatureValue ), 0 )
			viewHolder.temperatureTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.serviceTextView.text = String.format( context.getString( R.string.serversTextViewServerServicesCount ), 0 )
			viewHolder.serviceTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.networkTextView.text = String.format( context.getString( R.string.serversTextViewServerNetworkUsage ), 0, "K" )
			viewHolder.networkTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.diskTextView.text = String.format( context.getString( R.string.serversTextViewServerDiskUsage ), 0, "K" )
			viewHolder.diskTextView.setTextColor( context.getColor( R.color.statusDead ) )

			viewHolder.uptimeTextView.text = context.getString( R.string.serversTextViewServerUptimeOffline )
		}
	}

	// Returns the number of servers - called by the layout manager
	override fun getItemCount() = servers.size

}
