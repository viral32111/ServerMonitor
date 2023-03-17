package com.viral32111.servermonitor

import android.content.Context
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.constraintlayout.widget.ConstraintLayout
import androidx.recyclerview.widget.RecyclerView

// https://developer.android.com/develop/ui/views/layout/recyclerview, https://stackoverflow.com/a/54847887

class ServerAdapter( private val servers: Array<Server>, private val context: Context, private val onServerClickListener: ( server: Server ) -> Unit ): RecyclerView.Adapter<ServerAdapter.ViewHolder>() {

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
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.server, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder( viewHolder: ViewHolder, index: Int ) {
		val server = servers[ index ]

		// https://stackoverflow.com/a/49969478
		viewHolder.constraintLayout.setOnClickListener {
			onServerClickListener.invoke( server )
		}

		viewHolder.titleTextView.text = server.HostName.uppercase()

		if ( server.UptimeSeconds >= 0 ) {
			viewHolder.statusTextView.text = context.getString( R.string.serversTextViewServerStatusOnline )
			viewHolder.statusTextView.setTextColor( context.getColor( R.color.statusGood ) )

			viewHolder.uptimeTextView.text = String.format( context.getString( R.string.serversTextViewServerUptime ), TimeSpan( server.UptimeSeconds ).toString( false ) )

			// TODO: Populate the other text views with server data
		} else {
			viewHolder.statusTextView.text = context.getString( R.string.serversTextViewServerStatusOffline )
			viewHolder.processorTextView.text = String.format( context.getString( R.string.serversTextViewServerProcessorUsage ), 0 )
			viewHolder.memoryTextView.text = String.format( context.getString( R.string.serversTextViewServerMemoryUsage ), 0, "K" )
			viewHolder.temperatureTextView.text = String.format( context.getString( R.string.serversTextViewServerTemperatureValue ), 0 )
			viewHolder.serviceTextView.text = String.format( context.getString( R.string.serversTextViewServerServicesCount ), 0 )
			viewHolder.networkTextView.text = String.format( context.getString( R.string.serversTextViewServerNetworkUsage ), 0, "K" )
			viewHolder.diskTextView.text = String.format( context.getString( R.string.serversTextViewServerDiskUsage ), 0, "K" )
			viewHolder.uptimeTextView.text = context.getString( R.string.serversTextViewServerUptimeOffline )
		}
	}

	// Returns the number of servers - called by the layout manager
	override fun getItemCount() = servers.size

}
