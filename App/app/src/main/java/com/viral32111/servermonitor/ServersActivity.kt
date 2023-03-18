package com.viral32111.servermonitor

import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.view.View
import android.view.animation.Animation
import android.view.animation.LinearInterpolator
import android.widget.ProgressBar
import android.widget.TextView
import androidx.appcompat.app.ActionBar
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.recyclerview.widget.DividerItemDecoration
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout
import com.android.volley.*
import com.google.android.material.appbar.MaterialToolbar
import com.google.gson.JsonParseException
import com.google.gson.JsonSyntaxException
import kotlinx.coroutines.*

class ServersActivity : AppCompatActivity() {

	// UI
	private lateinit var swipeRefreshLayout: SwipeRefreshLayout
	private lateinit var recyclerView: RecyclerView
	private lateinit var statusTitleTextView: TextView
	private lateinit var statusDescriptionTextView: TextView
	private lateinit var refreshProgressBar: ProgressBar

	// Misc
	private lateinit var progressBarAnimation: ProgressBarAnimation
	private lateinit var settings: Settings

	// Runs when the activity is created...
	@SuppressLint( "InflateParams" ) // We intend to pass null to our layout inflater
	override fun onCreate(savedInstanceState: Bundle? ) {

		// Run default action & display the relevant layout file
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_servers )
		Log.d( Shared.logTag, "Creating activity..." )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar )
		Log.d( Shared.logTag, "Switched to Material Toolbar" )

		// Get all UI controls
		val materialToolbar = supportActionBar?.customView?.findViewById<MaterialToolbar>( R.id.actionBarMaterialToolbar )
		Log.d( Shared.logTag, "Got UI controls" )

		// Set the title on the toolbar
		materialToolbar?.title = getString( R.string.serversActionBarTitle )
		materialToolbar?.isTitleCentered = true
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ materialToolbar?.title }' (${ materialToolbar?.isTitleCentered })" )

		// Open settings when its action bar menu item is clicked
		materialToolbar?.setOnMenuItemClickListener { menuItem ->
			if ( menuItem.title?.equals( getString( R.string.action_bar_menu_settings ) ) == true ) {
				Log.d( Shared.logTag, "Opening Settings activity..." )
				startActivity( Intent( this, SettingsActivity::class.java ) )
				overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
			}

			return@setOnMenuItemClickListener true
		}

		// Get all the UI
		swipeRefreshLayout = findViewById( R.id.serversSwipeRefreshLayout )
		recyclerView = findViewById( R.id.serversRecyclerView )
		statusTitleTextView = findViewById( R.id.serversStatusTitleTextView )
		statusDescriptionTextView = findViewById( R.id.serversStatusDescriptionTextView )
		refreshProgressBar = findViewById( R.id.serversRefreshProgressBar )

		// Get the settings
		settings = Settings( getSharedPreferences( Shared.sharedPreferencesName, Context.MODE_PRIVATE ) )
		Log.d( Shared.logTag, "Got settings ('${ settings.instanceUrl }', '${ settings.credentialsUsername }', '${ settings.credentialsPassword }')" )

		// Switch to the servers activity if we aren't servers yet
		if ( !settings.isSetup() ) {
			Log.d( Shared.logTag, "Not setup yet, switching to servers activity..." )
			startActivity( Intent( this, SetupActivity::class.java ) )
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
			return
		}

		// Create a linear layout manager for the recycler view
		val linearLayoutManager = LinearLayoutManager( this, LinearLayoutManager.VERTICAL, false )
		recyclerView.layoutManager = linearLayoutManager

		// Set the divider between servers in the recycler view - https://stackoverflow.com/q/40528012
		val dividerItemDecoration = DividerItemDecoration( this, linearLayoutManager.orientation )
		dividerItemDecoration.setDrawable( ContextCompat.getDrawable( this, R.drawable.shape_divider )!! )
		recyclerView.addItemDecoration( dividerItemDecoration )

		// Create the animation for the automatic refresh countdown progress bar - https://stackoverflow.com/a/18015071
		progressBarAnimation = ProgressBarAnimation( refreshProgressBar, refreshProgressBar.progress.toFloat(), refreshProgressBar.max.toFloat() )
		progressBarAnimation.interpolator = LinearInterpolator() // We want linear, not accelerate-decelerate interpolation

		// Event listeners for the automatic refresh countdown progress bar animation - https://medium.com/android-news/handsome-codes-with-kotlin-6e183db4c7e5
		progressBarAnimation.setAnimationListener( object : Animation.AnimationListener {

			// When the animation starts...
			override fun onAnimationStart( animation: Animation? ) {
				Log.d( Shared.logTag, "Animation started" )
			}

			// When the animation finishes...
			override fun onAnimationEnd( animation: Animation? ) {
				Log.d( Shared.logTag, "Animation ended" )

				// Show refreshing spinner
				swipeRefreshLayout.isRefreshing = true

				CoroutineScope( Dispatchers.Main ).launch { // Begin coroutine context (on the UI thread)...
					withContext( Dispatchers.IO ) { // Run on network thread...

						// Fetch the servers
						try {
							val servers = fetchServers( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!! )

							// Update the UI
							withContext( Dispatchers.Main ) {
								updateUI( servers )
							}

						} catch ( exception: APIException ) {
							Log.e( Shared.logTag, "V Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
						} catch ( exception: JsonParseException ) {
							Log.e( Shared.logTag, "JP Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
						} catch ( exception: JsonSyntaxException ) {
							Log.e( Shared.logTag, "JS Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
						} catch ( exception: NullPointerException ) {
							Log.e( Shared.logTag, "NP Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
						}

					}
				}

				/*
				// Fetch the servers...
				fetchServers( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!!, { servers ->

					// Update the overall status
					statusTitleTextView.text = getString( R.string.serversTextViewStatusTitleGood )
					statusTitleTextView.setTextColor( getColor( R.color.statusGood ) )
					statusDescriptionTextView.text = getString( R.string.serversTextViewStatusDescriptionGood )

					// Create a new adapter for the recycler view
					val serverAdapter = ServerAdapter( servers, applicationContext ) { server ->
						Log.d( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') pressed" )
					}
					recyclerView.swapAdapter( serverAdapter, false )

					// Update the recycler view, restart automatic refresh countdown progress bar & hide refreshing spinner
					serverAdapter.notifyItemRangeChanged( 0, servers.size )
					if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )
					swipeRefreshLayout.isRefreshing = false

				}, {

				   // Hide refreshing spinner
				   swipeRefreshLayout.isRefreshing = false

				}, false )
				*/
			}

			// When the animation repeats...
			override fun onAnimationRepeat( animation: Animation? ) {
				Log.d( Shared.logTag, "Animation repeated" )
			}

		} )

		// When we're swiped down to refresh...
		swipeRefreshLayout.setOnRefreshListener {
			Log.d( Shared.logTag, "Swipe refreshed!" )

			// Stop the automatic refresh countdown progress bar
			if ( settings.automaticRefresh ) {
				refreshProgressBar.clearAnimation()
				refreshProgressBar.progress = 0
			}

			CoroutineScope( Dispatchers.Main ).launch {
				withContext( Dispatchers.IO ) {

					// Fetch the servers
					try {
						val servers = fetchServers( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!! )

						// Update the UI
						withContext( Dispatchers.Main ) {
							updateUI( servers )
						}

					} catch ( exception: APIException ) {
						Log.e( Shared.logTag, "V Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
					} catch ( exception: JsonParseException ) {
						Log.e( Shared.logTag, "JP Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
					} catch ( exception: JsonSyntaxException ) {
						Log.e( Shared.logTag, "JS Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
					} catch ( exception: NullPointerException ) {
						Log.e( Shared.logTag, "NP Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
					}

				}
			}

			/*
			// Fetch the servers...
			fetchServers( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!!, { servers ->

				// Update the overall status
				statusTitleTextView.text = getString( R.string.serversTextViewStatusTitleGood )
				statusTitleTextView.setTextColor( getColor( R.color.statusGood ) )
				statusDescriptionTextView.text = getString( R.string.serversTextViewStatusDescriptionGood )

				// Create a new adapter for the recycler view
				val serverAdapter = ServerAdapter( servers, this ) { server ->
					Log.d( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') pressed" )
				}
				recyclerView.swapAdapter( serverAdapter, false )

				// Update the recycler view, stop loading & restart automatic refresh countdown progress bar animation
				serverAdapter.notifyItemRangeChanged( 0, servers.size )
				swipeRefreshLayout.isRefreshing = false
				if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )

			}, {

			   // Hide refresh spinner
				swipeRefreshLayout.isRefreshing = false

			}, false )
			*/

		}

	}

	// Cancel pending HTTP requests when the activity is closed
	override fun onStop() {
		super.onStop()
		API.cancelQueue()
	}

	// Stop the automatic refresh countdown progress bar when the activity is changed/app is minimised
	override fun onPause() {
		super.onPause()
		Log.d( Shared.logTag, "Paused servers activity" )

		// Stop the automatic refresh countdown progress bar
		if ( settings.automaticRefresh ) {
			refreshProgressBar.clearAnimation()
			refreshProgressBar.progress = 0
		}
	}

	// When the activity is opened/app is brought to foreground...
	override fun onResume() {
		super.onResume()
		Log.d( Shared.logTag, "Resumed servers activity" )

		// Reload settings in case they have changed
		settings.read()
		Log.d( Shared.logTag, "Reloaded settings (Automatic Refresh: '${ settings.automaticRefresh }', Automatic Refresh Interval: '${ settings.automaticRefreshInterval }')" )

		// Set the progress bar animation duration to the automatic refresh interval
		progressBarAnimation.duration = settings.automaticRefreshInterval * 1000L // Convert seconds to milliseconds

		// Toggle the countdown progress bar depending on the automatic refresh setting
		refreshProgressBar.isEnabled = settings.automaticRefresh
		refreshProgressBar.visibility = if ( settings.automaticRefresh ) View.VISIBLE else View.GONE

		// Show refreshing spinner
		swipeRefreshLayout.isRefreshing = true

		CoroutineScope( Dispatchers.Main ).launch {
			withContext( Dispatchers.IO ) {

				// Fetch the servers
				try {
					val servers = fetchServers( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!! )

					// Update the UI
					withContext( Dispatchers.Main ) {
						updateUI( servers )
					}

				} catch ( exception: APIException ) {
					Log.e( Shared.logTag, "V Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
				} catch ( exception: JsonParseException ) {
					Log.e( Shared.logTag, "JP Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
				} catch ( exception: JsonSyntaxException ) {
					Log.e( Shared.logTag, "JS Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "NP Exception (onAnimationEnd)!!! ${ exception.message }, ${ exception }" )
				}

			}
		}

	}

	private fun updateUI( servers: Array<Server> ) {

		// Set the overall status
		statusTitleTextView.text = getString( R.string.serversTextViewStatusTitleGood )
		statusTitleTextView.setTextColor( getColor( R.color.statusGood ) )
		statusDescriptionTextView.text = getString( R.string.serversTextViewStatusDescriptionGood )

		// Create the adapter for the recycler view - https://www.geeksforgeeks.org/android-pull-to-refresh-with-recyclerview-in-kotlin/
		val serverAdapter = ServerAdapter( servers, applicationContext ) { server -> onServerPressed( server ) }
		recyclerView.adapter = serverAdapter

		// Update the recycler view, stop loading & restart automatic refresh countdown progress bar animation
		serverAdapter.notifyItemRangeChanged( 0, servers.size ) // IDE doesn't like .notifyDataSetChanged()
		swipeRefreshLayout.isRefreshing = false
		if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )

	}

	private fun onServerPressed( server: Server ) {
		Log.d( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') pressed" )
	}

	// Fetches the servers
	private fun fetchServers( instanceUrl: String, credentialsUsername: String, credentialsPassword: String, successCallback: ( servers: Array<Server> ) -> Unit, errorCallback: () -> Unit, useProgressDialog: Boolean = false ) {

		// Create a progress dialog
		val progressDialog = createProgressDialog( this, R.string.serversDialogProgressServersTitle, R.string.serversDialogProgressServersMessage ) {
			API.cancelQueue()
			showBriefMessage( this, R.string.serversToastServersCancel )
		}

		// Fetch the servers
		API.getServers( instanceUrl, credentialsUsername, credentialsPassword, { serversData ->

			// Hide the progress dialog
			if ( useProgressDialog ) progressDialog.dismiss()

			// Get the array
			val servers = serversData?.get( "servers" )?.asJsonArray
			Log.d( Shared.logTag, "Got '${ servers?.size() }' servers from API ('${ servers.toString() }')" )

			if ( servers != null ) {
				// https://www.geeksforgeeks.org/kotlin-list-arraylist/
				val serverList = ArrayList<Server>()
				for ( arrayItem in servers ) {
					val server = Server( arrayItem.asJsonObject )

					if ( server.isOnline() ) {
						Log.d( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') is online, fetching metrics..." )

						// TODO: Wait for all of these because the foor loop finishes before these return
						server.fetchMetrics( this, instanceUrl, credentialsUsername, credentialsPassword, {
							Log.d( Shared.logTag, "Metrics fetched for server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }')" )
							serverList.add( server )

						}, {
							Log.d( Shared.logTag, "Failed to fetch metrics for server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }')" )
						} )

					} else {
						Log.d( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') is offline, not fetching metrics..." )
						serverList.add( server )
					}
				}

				//successCallback.invoke( serverList.toTypedArray() )

			} else {
				Log.e( Shared.logTag, "Servers array from API is null?!" )
				errorCallback.invoke()
				showBriefMessage( this, R.string.serversToastServersNull )
			}

		}, { error, statusCode, errorCode ->
			Log.e( Shared.logTag, "Failed to get servers from API due to '${ error }' (Status Code: '${ statusCode }', Error Code: '${ errorCode }')" )

			// Hide the progress dialog
			if ( useProgressDialog ) progressDialog.dismiss()

			// Run the custom callback
			errorCallback.invoke()

			when ( error ) {

				// Bad authentication
				is AuthFailureError -> when ( errorCode ) {
					ErrorCode.UnknownUser.code -> showBriefMessage( this, R.string.serversToastServersAuthenticationUnknownUser )
					ErrorCode.IncorrectPassword.code -> showBriefMessage( this, R.string.serversToastServersAuthenticationIncorrectPassword )
					else -> showBriefMessage( this, R.string.serversToastServersAuthenticationFailure )
				}

				// HTTP 4xx
				is ClientError -> when ( statusCode ) {
					404 -> showBriefMessage( this, R.string.serversToastServersNotFound )
					else -> showBriefMessage( this, R.string.serversToastServersClientFailure )
				}

				// HTTP 5xx
				is ServerError -> when ( statusCode ) {
					502 -> showBriefMessage( this, R.string.serversToastServersUnavailable )
					503 -> showBriefMessage( this, R.string.serversToastServersUnavailable )
					504 -> showBriefMessage( this, R.string.serversToastServersUnavailable )
					else -> showBriefMessage( this, R.string.serversToastServersServerFailure )
				}

				// No Internet connection, malformed domain
				is NoConnectionError -> showBriefMessage( this, R.string.serversToastServersNoConnection )
				is NetworkError -> showBriefMessage( this, R.string.serversToastServersNoConnection )

				// Connection timed out
				is TimeoutError -> showBriefMessage( this, R.string.serversToastServersTimeout )

				// Couldn't parse as JSON
				is ParseError -> showBriefMessage( this, R.string.serversToastServersParseFailure )

				// ¯\_(ツ)_/¯
				else -> showBriefMessage( this, R.string.serversToastServersFailure )

			}
		} )

		// Show the progress dialog
		if ( useProgressDialog ) progressDialog.show()

	}

	/**
	 * Fetches all of the servers as an array.
	 * @param instanceUrl The URL to the connector instance, using the HTTPS schema.
	 * @param credentialsUsername The user to authenticate as.
	 * @param credentialsPassword The password to authenticate with.
	 * @return An array of servers.
	 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
	 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
	 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
	 * @throws NullPointerException The API response contained an unexpected null property.
	 */
	private suspend fun fetchServers( instanceUrl: String, credentialsUsername: String, credentialsPassword: String ): Array<Server> {

		// Fetch the servers, will throw a null pointer exception if null
		val servers = API.getServers( instanceUrl, credentialsUsername, credentialsPassword )!!
		Log.d( Shared.logTag, "Got '${ servers.size() }' servers from API ('${ servers }')" )

		// Convert the JSON array to a list of servers - https://www.geeksforgeeks.org/kotlin-list-arraylist/
		val serverList = ArrayList<Server>()
		for ( arrayItem in servers ) {
			val server = Server( arrayItem.asJsonObject ) // TODO: Ensure this is a JSON object

			if ( server.isOnline() ) {
				Log.d( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') is online, fetching metrics..." )

				server.update( this, instanceUrl, credentialsUsername, credentialsPassword )
				Log.d( Shared.logTag, "Metrics fetched for server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }')" )

				serverList.add( server )
			} else {
				Log.d( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') is offline, not fetching metrics..." )
				serverList.add( server )
			}
		}

		// Convert the list to a fixed array before returning
		return serverList.toTypedArray()

	}

}
