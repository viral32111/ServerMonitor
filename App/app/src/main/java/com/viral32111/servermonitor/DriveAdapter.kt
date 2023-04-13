package com.viral32111.servermonitor

import android.content.Context
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView

class DriveAdapter(
	private val drives: Array<Drive>,
	private val context: Context
): RecyclerView.Adapter<DriveAdapter.ViewHolder>() {

	// Holds all the UI
	class ViewHolder( view: View ) : RecyclerView.ViewHolder( view ) {
		val textView: TextView
		val partitionsRecyclerView: RecyclerView

		init {
			Log.d( Shared.logTag, "Initialising new drive view holder..." )

			// Get relevant UI
			textView = view.findViewById( R.id.driveTextView )
			partitionsRecyclerView = view.findViewById( R.id.drivePartitionsRecyclerView )

			// Create a linear layout manager for the recycler view
			partitionsRecyclerView.layoutManager = LinearLayoutManager( view.context, LinearLayoutManager.VERTICAL, false )
		}
	}

	// Creates new views - called by the layout manager
	override fun onCreateViewHolder( viewGroup: ViewGroup, viewType: Int ): ViewHolder {
		Log.d( Shared.logTag, "Creating new drive view..." )
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.fragment_drive, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder( viewHolder: ViewHolder, index: Int ) {
		val drive = drives[ index ]
		Log.d( Shared.logTag, "Replacing view for drive '${ drive.name }'..." )

		// Drive name & S.M.A.R.T health
		viewHolder.textView.setTextFromHTML( context.getString( R.string.serverTextViewDrivesDrive ).format(
			drive.name,
			context.createHTMLColoredText( drive.health.coerceAtLeast( 0 ).toString().suffixWith( Shared.percentSymbol ), drive.health.getAppropriateColorReverse( Drive.healthWarningThreshold, Drive.healthDangerThreshold ) )
		) )

		// Partitions - we assume there will always be some as a drive is pointless without one
		val partitions = drive.getPartitions()
		viewHolder.partitionsRecyclerView.adapter = DrivePartitionAdapter( partitions, context )
		viewHolder.partitionsRecyclerView.adapter?.notifyItemRangeChanged( 0, partitions.size )

	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = drives.size

}
