package com.viral32111.servermonitor

import android.content.Context
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView

class DrivePartitionAdapter(
	private val partitions: Array<DrivePartition>,
	private val context: Context
): RecyclerView.Adapter<DrivePartitionAdapter.ViewHolder>() {

	// Holds all the UI
	class ViewHolder( view: View ) : RecyclerView.ViewHolder( view ) {
		val textView: TextView

		init {
			Log.d( Shared.logTag, "Initialising new drive partition view holder..." )

			// Get relevant UI
			textView = view.findViewById( R.id.drivePartitionTextView )
		}
	}

	// Creates new views - called by the layout manager
	override fun onCreateViewHolder( viewGroup: ViewGroup, viewType: Int ): ViewHolder {
		Log.d( Shared.logTag, "Creating new drive partition view..." )
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.fragment_drive_partition, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder( viewHolder: ViewHolder, index: Int ) {
		val partition = partitions[ index ]
		Log.d( Shared.logTag, "Replacing view for drive partition '${ partition.name }' ('${ partition.mountpoint }')..." )

		// Calculate the used bytes on this partition
		val usedBytes = partition.totalBytes - partition.freeBytes
		Log.d( Shared.logTag, "Drive Partition Used: '${ usedBytes }' bytes" )

		// Convert the total & used bytes on this partition to their appropriate notation
		val total = Size( partition.totalBytes )
		val used = Size( usedBytes )
		Log.d( Shared.logTag, "Drive Partition Total: '${ total.amount }' '${ total.suffix }', Drive Partition Used: '${ used.amount }' '${ used.suffix }'" )

		// Calculate the percentage of bytes used for this partition
		val usage = ( usedBytes.toDouble() / partition.totalBytes.toDouble() ) * 100.0
		Log.d( Shared.logTag, "Drive Partition Usage: '${ usage }'" )

		// Update the text
		viewHolder.textView.setTextFromHTML( context.getString( R.string.serverTextViewDrivesPartition ).format(
			partition.name,
			context.createHTMLColoredText( used.amount.atLeastRoundAsString( 0.0, 1 ).suffixWith( used.suffix ), usedBytes.getAppropriateColor( DrivePartition.usedBytesWarningThreshold( partition.totalBytes ), DrivePartition.usedBytesDangerThreshold( partition.totalBytes ) ) ),
			context.createHTMLColoredText( total.amount.atLeastRoundAsString( 0.0, 1 ).suffixWith( total.suffix ), partition.totalBytes.getAppropriateColor() ),
			context.createHTMLColoredText( usage.roundAsString( 1 ).suffixWith( Shared.percentSymbol ), usage.getAppropriateColor( DrivePartition.usageWarningThreshold, DrivePartition.usageDangerThreshold ) ),
			partition.mountpoint
		) )

	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = partitions.size

}
