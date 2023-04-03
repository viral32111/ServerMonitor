package com.viral32111.servermonitor

import android.content.Context
import android.text.Html
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView

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
			Log.d( Shared.logTag, "Initialising new Docker container view holder..." )

			nameTextView = view.findViewById( R.id.dockerContainerNameTextView )
			statusTextView = view.findViewById( R.id.dockerContainerStatusTextView )
			imageTextView = view.findViewById( R.id.dockerContainerImageTextView )
		}
	}

	// Creates new views - called by the layout manager
	override fun onCreateViewHolder( viewGroup: ViewGroup, viewType: Int ): ViewHolder {
		Log.d( Shared.logTag, "Creating new Docker container view..." )
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.fragment_docker_container, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder( viewHolder: ViewHolder, index: Int ) {
		val container = containers[ index ]
		Log.d( Shared.logTag, "Replacing view for Docker container '${ container.name }' ('${ container.id }', '${ container.image }')..." )

		// Convert the status code to text
		val statusText = when ( container.statusCode ) {
			0 -> "Created"
			1 -> "Running"
			2 -> "Restarting"
			3 -> "Dead"
			4 -> "Exited"
			5 -> "Paused"
			6 -> "Removing"
			else -> "Unknown"
		}

		// Get an appropriate color for the status
		val statusColor = context.getColor( when ( statusText ) {
			"Created" -> R.color.statusNeutral
			"Running" -> R.color.statusGood
			"Restarting" -> R.color.statusWarning
			"Dead" -> R.color.statusBad
			"Exited" -> R.color.statusBad
			"Paused" -> R.color.statusNeutral
			"Removing" -> R.color.statusWarning
			else -> R.color.statusDead
		} )

		// Convert the health status code to text
		val healthText = when ( container.statusCode ) {
			0 -> "Unhealthy"
			1 -> "Healthy"
			else -> "Unknown"
		}

		// Get an appropriate color for the health status
		val healthColor = context.getColor( when ( healthText ) {
			"Unhealthy" -> R.color.statusBad
			"Healthy" -> R.color.statusGood
			else -> R.color.statusDead
		} )

		// Update the name text
		viewHolder.nameTextView.text = String.format( context.getString( R.string.serverTextViewDockerContainerName ), container.name, container.id.substring( 0, 12 ) )

		// Update the status text
		val uptimeText = TimeSpan( container.uptimeSeconds ).toString( false )
		viewHolder.statusTextView.text = Html.fromHtml( String.format( context.getString( R.string.serverTextViewDockerContainerStatus ),
			createColorText( statusText, statusColor ),
			createColorText( healthText, healthColor ),
			createColorText(
				uptimeText.ifBlank { "an unknown duration" },
				context.getColor( if ( uptimeText.isNotBlank() ) R.color.black else R.color.statusDead )
			)
		), Html.FROM_HTML_MODE_LEGACY )

		// Update the image text (appears as grey if the image is deleted)
		viewHolder.imageTextView.text = container.image
		if ( container.image.startsWith( "sha256:" ) ) viewHolder.imageTextView.setTextColor( context.getColor( R.color.statusDead ) )

	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = containers.size

}
