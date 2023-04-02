package com.viral32111.servermonitor

import android.content.Context
import android.text.Html
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.FrameLayout
import android.widget.TextView
import androidx.recyclerview.widget.RecyclerView
import kotlin.math.roundToLong

class DrivePartitionAdapter(
	private val partitions: Array<Partition>,
	private val context: Context
): RecyclerView.Adapter<DrivePartitionAdapter.ViewHolder>() {

	// Holds all the UI
	class ViewHolder( view: View ) : RecyclerView.ViewHolder( view ) {
		val frameLayout: FrameLayout
		val textView: TextView

		init {
			Log.d( Shared.logTag, "Initialising new drive partition view holder..." )

			frameLayout = view.findViewById( R.id.drivePartitionFrameLayout )
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

		val usedBytes = partition.totalBytes - partition.freeBytes
		Log.d( Shared.logTag, "Drive Partition Used: '${ usedBytes }' bytes" )

		val total = Size( partition.totalBytes )
		val used = Size( usedBytes )
		Log.d( Shared.logTag, "Drive Partition Total: '${ total.amount }' '${ total.suffix }', Drive Partition Used: '${ used.amount }' '${ used.suffix }'" )

		val usage = ( usedBytes.toDouble() / partition.totalBytes.toDouble() ) * 100.0
		Log.d( Shared.logTag, "Drive Partition Usage: '${ usage }'" )

		viewHolder.textView.text = Html.fromHtml( String.format( context.getString( R.string.serverTextViewDrivesPartition ),
			partition.name,
			createColorText( roundValueOrDefault( used.amount, used.suffix ), colorForValue( context, usedBytes, ( partition.totalBytes / 2.0f ).roundToLong(), ( partition.totalBytes / 1.25f ).roundToLong() ) ),
			createColorText( roundValueOrDefault( total.amount, total.suffix ), colorAsNeutral( context, partition.totalBytes ) ),
			createColorText( roundValueOrDefault( usage, Shared.percentSymbol ), colorForValue( context, usage, 75.0, 90.0 ) ),
			partition.mountpoint
		), Html.FROM_HTML_MODE_LEGACY )

	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = partitions.size

}
