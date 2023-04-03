package com.viral32111.servermonitor

import android.content.Context
import android.text.Html
import android.util.Log
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.TextView
import androidx.appcompat.content.res.AppCompatResources
import androidx.recyclerview.widget.RecyclerView

class NetworkInterfaceAdapter(
	private val networkInterfaces: Array<NetworkInterface>,
	private val context: Context
): RecyclerView.Adapter<NetworkInterfaceAdapter.ViewHolder>() {

	// Holds all the UI
	class ViewHolder( view: View ) : RecyclerView.ViewHolder( view ) {
		val textView: TextView
		val transmitTextView: TextView
		val receiveTextView: TextView

		init {
			Log.d( Shared.logTag, "Initialising new network interface view holder..." )

			// Get relevant UI
			textView = view.findViewById( R.id.networkInterfaceTextView )
			transmitTextView = view.findViewById( R.id.networkInterfaceTransmitTextView )
			receiveTextView = view.findViewById( R.id.networkInterfaceReceiveTextView )
		}
	}

	// Creates new views - called by the layout manager
	override fun onCreateViewHolder( viewGroup: ViewGroup, viewType: Int ): ViewHolder {
		Log.d( Shared.logTag, "Creating new network interface view..." )
		return ViewHolder( LayoutInflater.from( viewGroup.context ).inflate( R.layout.fragment_network_interface, viewGroup, false ) )
	}

	// Replaces the contents of a view - called by the layout manager
	override fun onBindViewHolder( viewHolder: ViewHolder, index: Int ) {
		val networkInterface = networkInterfaces[ index ]
		Log.d( Shared.logTag, "Replacing view for network interface '${ networkInterface.name }'..." )

		// Change icon depending on type of interface (e.g., Ethernet, Wi-Fi, etc.)
		if ( networkInterface.name.startsWith( "enp" ) || networkInterface.name.startsWith( "eth" ) ) {
			viewHolder.textView.setCompoundDrawablesWithIntrinsicBounds( AppCompatResources.getDrawable( context, R.drawable.cable ), null, null, null )
		} else if ( networkInterface.name.startsWith( "wlo" ) || networkInterface.name.startsWith( "wlan" ) ) {
			viewHolder.textView.setCompoundDrawablesWithIntrinsicBounds( AppCompatResources.getDrawable( context, R.drawable.wifi ), null, null, null )
		} else if ( networkInterface.name.startsWith( "lo" ) ) {
			viewHolder.textView.setCompoundDrawablesWithIntrinsicBounds( AppCompatResources.getDrawable( context, R.drawable.laps ), null, null, null )

			// No point in showing separate values for loopback, as they'll always be the same
			viewHolder.transmitTextView.visibility = View.GONE
			viewHolder.receiveTextView.visibility = View.GONE
		}

		// Set tint color to black as the above icon changes make it white
		viewHolder.textView.compoundDrawables[ 0 ].setTint( context.getColor( R.color.black ) )

		// Get the current rate & total bytes sent through this interface
		val totalRateBytes = networkInterface.rateBytesSent + networkInterface.rateBytesReceived
		val totalBytes = networkInterface.totalBytesSent + networkInterface.totalBytesReceived
		val totalRate = Size( totalRateBytes )
		val total = Size( totalBytes )

		// Update the interface text
		viewHolder.textView.text = Html.fromHtml( String.format( context.getString( R.string.serverTextViewNetworkInterface ),
			networkInterface.name,
			createColorText( roundValueOrDefault( totalRate.amount, totalRate.suffix + "/s" ), colorForValue( context, totalRateBytes, 1024L * 1024L, 1024L * 1024L * 10L ) ), // No idea what the thresholds should be, so guesstimate
			createColorText( roundValueOrDefault( total.amount, total.suffix ), colorForValue( context, totalBytes, 1024L * 1024L * 1024L, 1024L * 1024L * 1024L * 1024L ) ), // No idea what the thresholds should be, so guesstimate
		), Html.FROM_HTML_MODE_LEGACY )

		// Get the current rate & total transmitted bytes for this interface
		val transmitRateBytes = networkInterface.rateBytesSent
		val transmitBytes = networkInterface.totalBytesSent
		val transmitRate = Size( transmitRateBytes )
		val transmit = Size( transmitBytes )

		// Update the transmit text
		viewHolder.transmitTextView.text = Html.fromHtml( String.format( context.getString( R.string.serverTextViewNetworkInterfaceTransmit ),
			createColorText( roundValueOrDefault( transmitRate.amount, transmitRate.suffix + "/s" ), colorForValue( context, transmitRateBytes, 1024L * 1024L, 1024L * 1024L * 10L ) ), // No idea what the thresholds should be, so guesstimate
			createColorText( roundValueOrDefault( transmit.amount, transmit.suffix ), colorForValue( context, transmitBytes, 1024L * 1024L * 1024L, 1024L * 1024L * 1024L * 1024L ) ), // No idea what the thresholds should be, so guesstimate
		), Html.FROM_HTML_MODE_LEGACY )

		// Get the current rate & total received bytes for this interface
		val receiveRateBytes = networkInterface.rateBytesReceived
		val receiveBytes = networkInterface.totalBytesReceived
		val receiveRate = Size( receiveRateBytes )
		val receive = Size( receiveBytes )

		// Update the receive text
		viewHolder.receiveTextView.text = Html.fromHtml( String.format( context.getString( R.string.serverTextViewNetworkInterfaceReceive ),
			createColorText( roundValueOrDefault( receiveRate.amount, receiveRate.suffix + "/s" ), colorForValue( context, receiveRateBytes, 1024L * 1024L, 1024L * 1024L * 10L ) ), // No idea what the thresholds should be, so guesstimate
			createColorText( roundValueOrDefault( receive.amount, receive.suffix ), colorForValue( context, receiveBytes, 1024L * 1024L * 1024L, 1024L * 1024L * 1024L * 1024L ) ), // No idea what the thresholds should be, so guesstimate
		), Html.FROM_HTML_MODE_LEGACY )

	}

	// Returns the number of views - called by the layout manager
	override fun getItemCount() = networkInterfaces.size

}
