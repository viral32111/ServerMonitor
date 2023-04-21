package com.viral32111.servermonitor.adapter

import android.content.Context
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.Button
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView
import com.viral32111.servermonitor.R
import com.viral32111.servermonitor.Shared
import com.viral32111.servermonitor.data.Service
import com.viral32111.servermonitor.helper.TimeSpan
import com.viral32111.servermonitor.helper.createHTMLColoredText
import com.viral32111.servermonitor.helper.setTextFromHTML

class ServiceAdapter(
	private val services: Array<Service>,
	private val context: Context,
	private val onClickListener: ( service: Service) -> Unit
): RecyclerView.Adapter<ServiceAdapter.ViewHolder>() {

	// Holds all the UI
	class ViewHolder( view: View ) : RecyclerView.ViewHolder( view ) {
		val nameTextView: TextView
		val statusTextView: TextView
		val manageButton: Button

		init {
			Log.d(Shared.logTag, "Initialising new service view holder..." )

			// Get relevant UI
			nameTextView = view.findViewById(R.id.serviceNameTextView)
			statusTextView = view.findViewById(R.id.serviceStatusTextView)
			manageButton = view.findViewById(R.id.serviceManageButton)
		}
	}

	// Creates new views - called by the layout manager
	override fun onCreateViewHolder( viewGroup: ViewGroup, viewType: Int ): ViewHolder {
		Log.d(Shared.logTag, "Creating new service view..." )
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.fragment_service, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder(viewHolder: ViewHolder, index: Int ) {
		val service = services[ index ]
		Log.d(Shared.logTag, "Replacing view for service '${ service.serviceName }' ('${ service.displayName }', '${ service.description }')..." )

		// Forward button click events
		viewHolder.manageButton.setOnClickListener {
			Log.d(Shared.logTag, "Service '${ service.serviceName }' ('${ service.displayName }', '${ service.description }') pressed" )
			onClickListener.invoke( service )
		}

		// Get the status
		val statusText = service.getStatusText()
		val statusColor = service.getStatusColor( statusText )

		// Update the name text
		viewHolder.nameTextView.text = service.displayName

		// Update the status text
		val uptimeText = TimeSpan( service.uptimeSeconds.toLong() ).toString( false )
		viewHolder.statusTextView.setTextFromHTML( context.getString(R.string.serverTextViewServicesServiceStatus).format(
			context.createHTMLColoredText( statusText, statusColor ),
			context.createHTMLColoredText(
				uptimeText.ifBlank { context.getString(R.string.serverTextViewServicesServiceStatusUptimeUnknown) },
				if ( uptimeText.isNotBlank() ) R.color.black else R.color.statusDead
			)
		) )
	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = services.size

}
