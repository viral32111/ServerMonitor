package com.viral32111.servermonitor.adapter

import android.content.Context
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView
import com.viral32111.servermonitor.*
import com.viral32111.servermonitor.data.DockerContainer
import com.viral32111.servermonitor.helper.TimeSpan

class DockerContainerAdapter(
	private val containers: Array<DockerContainer>,
	private val context: Context
): RecyclerView.Adapter<DockerContainerAdapter.ViewHolder>() {

	// Holds all the UI
	class ViewHolder( view: View ) : RecyclerView.ViewHolder( view ) {
		val nameTextView: TextView
		val statusTextView: TextView
		val imageTextView: TextView

		init {
			Log.d(Shared.logTag, "Initialising new Docker container view holder..." )

			// Get relevant UI
			nameTextView = view.findViewById(R.id.dockerContainerNameTextView)
			statusTextView = view.findViewById(R.id.dockerContainerStatusTextView)
			imageTextView = view.findViewById(R.id.dockerContainerImageTextView)
		}
	}

	// Creates new views - called by the layout manager
	override fun onCreateViewHolder( viewGroup: ViewGroup, viewType: Int ): ViewHolder {
		Log.d(Shared.logTag, "Creating new Docker container view..." )
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.fragment_docker_container, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder(viewHolder: ViewHolder, index: Int ) {
		val container = containers[ index ]
		Log.d(Shared.logTag, "Replacing view for Docker container '${ container.name }' ('${ container.id }', '${ container.image }')..." )

		// Get the status & health
		val statusText = container.getStatusText()
		val statusColor = container.getStatusColor( statusText )
		val healthText = container.getHealthText()
		val healthColor = container.getHealthColor( healthText )

		// Update the name text
		viewHolder.nameTextView.text = context.getString(R.string.serverTextViewDockerContainerName).format( container.name, container.getShortIdentifier() )

		// Update the status text
		val uptimeText = TimeSpan( container.uptimeSeconds ).toString( false )
		viewHolder.statusTextView.setTextFromHTML( context.getString(R.string.serverTextViewDockerContainerStatus).format(
			context.createHTMLColoredText( statusText, statusColor ),
			context.createHTMLColoredText( healthText, healthColor ),
			context.createHTMLColoredText(
				uptimeText.ifBlank { context.getString(R.string.serverTextViewDockerContainerStatusUptimeUnknown) },
				if ( uptimeText.isNotBlank() ) R.color.black else R.color.statusDead
			)
		) )

		// Update the image text - grey if the image is old (deleted)
		viewHolder.imageTextView.text = container.image
		if ( container.isImageOld() ) viewHolder.imageTextView.setTextColor( context.getColor(R.color.statusDead) )

	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = containers.size

}
