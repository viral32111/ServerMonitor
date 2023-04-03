package com.viral32111.servermonitor

import android.content.Context
import android.text.Html
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView

class ServiceAdapter(
		private val services: Array<Service>,
		private val context: Context,
		private val onClickListener: ( service: Service ) -> Unit
	): RecyclerView.Adapter<ServiceAdapter.ViewHolder>() {

	// Holds all the UI
	class ViewHolder( view: View ) : RecyclerView.ViewHolder( view ) {
		val nameTextView: TextView
		val statusTextView: TextView
		val manageButton: Button

		init {
			Log.d( Shared.logTag, "Initialising new service view holder..." )

			nameTextView = view.findViewById( R.id.serviceNameTextView )
			statusTextView = view.findViewById( R.id.serviceStatusTextView )
			manageButton = view.findViewById( R.id.serviceManageButton )
		}
	}

	// Creates new views - called by the layout manager
	override fun onCreateViewHolder( viewGroup: ViewGroup, viewType: Int ): ViewHolder {
		Log.d( Shared.logTag, "Creating new service view..." )
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.fragment_service, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder( viewHolder: ViewHolder, index: Int ) {
		val service = services[ index ]
		Log.d( Shared.logTag, "Replacing view for service '${ service.displayName }' ('${ service.serviceName }', '${ service.description }')..." )

		// Forward button click events
		viewHolder.manageButton.setOnClickListener {
			Log.d( Shared.logTag, "Service '${ service.displayName }' ('${ service.serviceName }', '${ service.description }') pressed" )
			onClickListener.invoke( service )
		}

		// Convert the status code to text
		val statusText = when ( service.statusCode ) {
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

		// Get an appropriate color for the status
		val statusColor = context.getColor( when ( statusText ) {
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

		// Update the name text
		viewHolder.nameTextView.text = service.displayName

		// Update the uptime text
		val uptimeSeconds = service.uptimeSeconds.toLong()
		val uptimeText = TimeSpan( uptimeSeconds ).toString( false )
		viewHolder.statusTextView.text = Html.fromHtml( String.format( context.getString( R.string.serverTextViewServicesServiceStatus ),
			createColorText( statusText, statusColor ),
			createColorText(
				uptimeText.ifBlank { "an unknown duration" },
				context.getColor( if ( uptimeText.isNotBlank() ) R.color.black else R.color.statusDead )
			)
		), Html.FROM_HTML_MODE_LEGACY )
	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = services.size

}
