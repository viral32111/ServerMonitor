package com.viral32111.servermonitor.adapter

import android.content.Context
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView
import com.viral32111.servermonitor.R
import com.viral32111.servermonitor.Shared
import com.viral32111.servermonitor.data.SNMPAgent
import com.viral32111.servermonitor.helper.TimeSpan
import com.viral32111.servermonitor.helper.createHTMLColoredText
import com.viral32111.servermonitor.helper.getAppropriateColor
import com.viral32111.servermonitor.helper.setTextFromHTML

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
			Log.d(Shared.logTag, "Initialising new SNMP agent view holder..." )

			// Get relevant UI
			nameTextView = view.findViewById(R.id.snmpAgentNameTextView)
			statusTextView = view.findViewById(R.id.snmpAgentStatusTextView)
			descriptionTextView = view.findViewById(R.id.snmpAgentDescriptionTextView)
			locationTextView = view.findViewById(R.id.snmpAgentLocationTextView)
			contactTextView = view.findViewById(R.id.snmpAgentContactTextView)
			servicesTextView = view.findViewById(R.id.snmpAgentServicesTextView)
			trapsTextView = view.findViewById(R.id.snmpAgentTrapsTextView)
		}
	}

	// Creates new views - called by the layout manager
	override fun onCreateViewHolder( viewGroup: ViewGroup, viewType: Int ): ViewHolder {
		Log.d(Shared.logTag, "Creating new SNMP agent view..." )
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.fragment_snmp_agent, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder(viewHolder: ViewHolder, index: Int ) {
		val agent = agents[ index ]
		Log.d(Shared.logTag, "Replacing view for SNMP agent '${ agent.name }' ('${ agent.address }', '${ agent.port }')..." )

		// Update the name & description
		viewHolder.nameTextView.text = context.getString(R.string.serverTextViewSNMPAgentName).format( agent.name, agent.address, agent.port )
		viewHolder.descriptionTextView.text = agent.description

		// Update the status text
		val uptimeText = TimeSpan( agent.uptimeSeconds ).toString( false )
		viewHolder.statusTextView.setTextFromHTML( context.getString(R.string.serverTextViewSNMPAgentStatus).format(
			context.createHTMLColoredText( context.getString(R.string.serverTextViewSNMPAgentStatusOnline), context.getColor(
				R.color.statusGood
			) ),
			context.createHTMLColoredText(
				uptimeText.ifBlank { context.getString(R.string.serverTextViewSNMPAgentStatusUptimeUnknown) },
				if ( uptimeText.isNotBlank() ) R.color.black else R.color.statusDead
			)
		) )

		// Update the location & contact
		viewHolder.locationTextView.text = context.getString(R.string.serverTextViewSNMPAgentLocation).format( agent.location )
		viewHolder.contactTextView.text = context.getString(R.string.serverTextViewSNMPAgentContact).format( agent.contact )

		// Update the running services count & received traps count
		viewHolder.servicesTextView.text = context.getString(R.string.serverTextViewSNMPAgentServices).format( agent.serviceCount )
		viewHolder.trapsTextView.setTextFromHTML( context.getString(R.string.serverTextViewSNMPAgentTraps).format(
			context.createHTMLColoredText( agent.receivedTrapsCount.coerceAtLeast( 0 ).toString(), agent.receivedTrapsCount.getAppropriateColor( SNMPAgent.receivedTrapsCountWarningThreshold, SNMPAgent.receivedTrapsCountDangerThreshold ) )
		) )

	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = agents.size

}
