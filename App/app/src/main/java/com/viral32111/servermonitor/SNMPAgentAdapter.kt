package com.viral32111.servermonitor

import android.content.Context
import android.text.Html
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView

class SNMPAgentAdapter(
	private val agents: Array<SNMPAgent>,
	private val context: Context
): RecyclerView.Adapter<SNMPAgentAdapter.ViewHolder>() {

	// Holds all the UI
	class ViewHolder( view: View ) : RecyclerView.ViewHolder( view ) {
		val nameTextView: TextView
		val statusTextView: TextView
		val descriptionTextView: TextView
		val locationTextView: TextView
		val contactTextView: TextView
		val servicesTextView: TextView
		val trapsTextView: TextView

		init {
			Log.d( Shared.logTag, "Initialising new SNMP agent view holder..." )

			// Get relevant UI
			nameTextView = view.findViewById( R.id.snmpAgentNameTextView )
			statusTextView = view.findViewById( R.id.snmpAgentStatusTextView )
			descriptionTextView = view.findViewById( R.id.snmpAgentDescriptionTextView )
			locationTextView = view.findViewById( R.id.snmpAgentLocationTextView )
			contactTextView = view.findViewById( R.id.snmpAgentContactTextView )
			servicesTextView = view.findViewById( R.id.snmpAgentServicesTextView )
			trapsTextView = view.findViewById( R.id.snmpAgentTrapsTextView )
		}
	}

	// Creates new views - called by the layout manager
	override fun onCreateViewHolder( viewGroup: ViewGroup, viewType: Int ): ViewHolder {
		Log.d( Shared.logTag, "Creating new SNMP agent view..." )
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.fragment_snmp_agent, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder( viewHolder: ViewHolder, index: Int ) {
		val agent = agents[ index ]
		Log.d( Shared.logTag, "Replacing view for SNMP agent '${ agent.name }' ('${ agent.address }', '${ agent.port }')..." )

		// Update the name & description
		viewHolder.nameTextView.text = String.format( context.getString( R.string.serverTextViewSNMPAgentName ), agent.name, agent.address, agent.port )
		viewHolder.descriptionTextView.text = agent.description

		// Update the status text
		val uptimeText = TimeSpan( agent.uptimeSeconds ).toString( false )
		viewHolder.statusTextView.text = Html.fromHtml( String.format( context.getString( R.string.serverTextViewSNMPAgentStatus ),
			createColorText( "Online", context.getColor( R.color.statusGood ) ),
			createColorText(
				uptimeText.ifBlank { "an unknown duration" },
				context.getColor( if ( uptimeText.isNotBlank() ) R.color.black else R.color.statusDead )
			)
		), Html.FROM_HTML_MODE_LEGACY )

		// Update the location & contact
		viewHolder.locationTextView.text = String.format( context.getString( R.string.serverTextViewSNMPAgentLocation ), agent.location )
		viewHolder.contactTextView.text = String.format( context.getString( R.string.serverTextViewSNMPAgentContact ), agent.contact )

		// Update the running services count & received traps count
		viewHolder.servicesTextView.text = String.format( context.getString( R.string.serverTextViewSNMPAgentServices ), agent.serviceCount )
		viewHolder.trapsTextView.text = Html.fromHtml( String.format( context.getString( R.string.serverTextViewSNMPAgentTraps ),
			createColorText( agent.receivedTrapsCount.toString(), colorForValue( context, agent.receivedTrapsCount, 1, 10 ) )
		), Html.FROM_HTML_MODE_LEGACY )

	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = agents.size

}
