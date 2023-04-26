package com.viral32111.servermonitor.activity

import android.app.Activity
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.view.View
import android.view.animation.Animation
import android.view.animation.LinearInterpolator
import android.widget.Button
import android.widget.ProgressBar
import android.widget.TextView
import androidx.activity.OnBackPressedCallback
import androidx.appcompat.app.ActionBar
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.content.res.AppCompatResources
import androidx.core.content.ContextCompat
import androidx.recyclerview.widget.DividerItemDecoration
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout
import com.android.volley.AuthFailureError
import com.android.volley.ClientError
import com.android.volley.NetworkError
import com.android.volley.NoConnectionError
import com.android.volley.ServerError
import com.android.volley.TimeoutError
import com.google.android.material.appbar.MaterialToolbar
import com.google.gson.JsonParseException
import com.google.gson.JsonSyntaxException
import com.viral32111.servermonitor.ErrorCode
import com.viral32111.servermonitor.R
import com.viral32111.servermonitor.Shared
import com.viral32111.servermonitor.UpdateWorker
import com.viral32111.servermonitor.adapter.DockerContainerAdapter
import com.viral32111.servermonitor.adapter.DriveAdapter
import com.viral32111.servermonitor.adapter.NetworkInterfaceAdapter
import com.viral32111.servermonitor.adapter.SNMPAgentAdapter
import com.viral32111.servermonitor.adapter.ServiceAdapter
import com.viral32111.servermonitor.data.Drive
import com.viral32111.servermonitor.data.NetworkInterface
import com.viral32111.servermonitor.data.Server
import com.viral32111.servermonitor.data.Service
import com.viral32111.servermonitor.helper.API
import com.viral32111.servermonitor.helper.APIException
import com.viral32111.servermonitor.helper.Frequency
import com.viral32111.servermonitor.helper.ProgressBarAnimation
import com.viral32111.servermonitor.helper.Settings
import com.viral32111.servermonitor.helper.Size
import com.viral32111.servermonitor.helper.TimeSpan
import com.viral32111.servermonitor.helper.asHTMLBold
import com.viral32111.servermonitor.helper.asHTMLItalic
import com.viral32111.servermonitor.helper.atLeastRoundAsString
import com.viral32111.servermonitor.helper.concat
import com.viral32111.servermonitor.helper.createHTMLColoredText
import com.viral32111.servermonitor.helper.createProgressDialog
import com.viral32111.servermonitor.helper.getAppropriateColor
import com.viral32111.servermonitor.helper.roundAsString
import com.viral32111.servermonitor.helper.setTextFromHTML
import com.viral32111.servermonitor.helper.setTextIconColor
import com.viral32111.servermonitor.helper.showBriefMessage
import com.viral32111.servermonitor.helper.showConfirmDialog
import com.viral32111.servermonitor.helper.showInformationDialog
import com.viral32111.servermonitor.helper.suffixWith
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class ServerActivity : AppCompatActivity() {

	// UI
	private var materialToolbar: MaterialToolbar? = null
	private lateinit var swipeRefreshLayout: SwipeRefreshLayout
	private lateinit var refreshProgressBar: ProgressBar
	private lateinit var statusTextView: TextView
	private lateinit var actionShutdownButton: Button
	private lateinit var actionRebootButton: Button
	private lateinit var resourcesProcessorTextView: TextView
	private lateinit var resourcesMemoryTextView: TextView
	private lateinit var resourcesSwapTextView: TextView
	private lateinit var resourcesNetworkTextView: TextView
	private lateinit var resourcesDriveTextView: TextView
	private lateinit var resourcesPowerTextView: TextView
	private lateinit var resourcesFansTextView: TextView
	private lateinit var drivesStatusTextView: TextView
	private lateinit var drivesRecyclerView: RecyclerView
	private lateinit var networkStatusTextView: TextView
	private lateinit var networkRecyclerView: RecyclerView
	private lateinit var servicesStatusTextView: TextView
	private lateinit var servicesRecyclerView: RecyclerView
	private lateinit var dockerStatusTextView: TextView
	private lateinit var dockerRecyclerView: RecyclerView
	private lateinit var snmpTitleTextView: TextView
	private lateinit var snmpStatusTextView: TextView
	private lateinit var snmpRecyclerView: RecyclerView

	// Misc
	private lateinit var progressBarAnimation: ProgressBarAnimation
	private lateinit var settings: Settings

	// Data from previous activity
	private lateinit var serverIdentifier: String

	// Contact information
	private var contactName: String? = null
	private var contactMethods: Array<String>? = null

	// Runs when the activity is created...
	override fun onCreate( savedInstanceState: Bundle? ) {

		// Run default action & display the relevant layout file
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_server )
		Log.d( Shared.logTag, "Creating activity..." )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar )
		Log.d( Shared.logTag, "Switched to Material Toolbar" )

		// Set the title on the toolbar
		materialToolbar = supportActionBar?.customView?.findViewById( R.id.actionBarMaterialToolbar )
		materialToolbar?.title = getString( R.string.serverActionBarTitle )
		materialToolbar?.isTitleCentered = true
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ materialToolbar?.title }' (${ materialToolbar?.isTitleCentered })" )

		// When an item on the action bar menu is pressed...
		materialToolbar?.setOnMenuItemClickListener { menuItem ->

			// Settings
			if ( menuItem.title?.equals( getString( R.string.actionBarMenuSettings ) ) == true ) {
				Log.d( Shared.logTag, "Opening Settings activity..." )

				startActivity( Intent( this, SettingsActivity::class.java ) )
				overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )

			// Logout
			} else if ( menuItem.title?.equals( getString( R.string.actionBarMenuLogout ) ) == true ) {
				Log.d( Shared.logTag, "Logout menu item pressed, showing confirmation..." )

				showConfirmDialog( this, R.string.dialogConfirmLogoutMessage, {
					Log.d( Shared.logTag, "Logout confirmed" )

					// Erase setup URL & credentials
					settings.instanceUrl = null
					settings.credentialsUsername = null
					settings.credentialsPassword = null
					settings.save()
					Log.d( Shared.logTag, "Erased stored credentials" )

					// Return to the setup activity
					Log.d( Shared.logTag, "Opening Setup activity..." )
					startActivity( Intent( this, SetupActivity::class.java ) )
					overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
					finish()
				}, {
					Log.d( Shared.logTag, "Logout aborted" )
				} )

			// About
			} else if ( menuItem.title?.equals( getString( R.string.actionBarMenuAbout ) ) == true ) {
				Log.d( Shared.logTag, "Showing information about app dialog..." )

				// Get the contact information, if it exists
				val contactInformation = if ( !contactName.isNullOrBlank() && contactMethods != null ) "Contact for ${ contactName }:\n${ contactMethods!!.joinToString( "\n" ) }" else  ""

				showInformationDialog( this, R.string.dialogInformationAboutTitle, String.format( "%s\n\n%s", getString( R.string.dialogInformationAboutMessage ), contactInformation ) )
			}

			return@setOnMenuItemClickListener true

		}

		// Get all the UI
		swipeRefreshLayout = findViewById( R.id.serverSwipeRefreshLayout )
		refreshProgressBar = findViewById( R.id.serverRefreshProgressBar )
		statusTextView = findViewById( R.id.serverStatusTextView )
		actionShutdownButton = findViewById( R.id.serverActionShutdownButton )
		actionRebootButton = findViewById( R.id.serverActionRebootButton )
		resourcesProcessorTextView = findViewById( R.id.serverResourcesDataProcessorTextView )
		resourcesMemoryTextView = findViewById( R.id.serverResourcesDataMemoryTextView )
		resourcesSwapTextView = findViewById( R.id.serverResourcesDataSwapTextView )
		resourcesNetworkTextView = findViewById( R.id.serverResourcesDataNetworkTextView )
		resourcesDriveTextView = findViewById( R.id.serverResourcesDataDriveTextView )
		resourcesPowerTextView = findViewById( R.id.serverResourcesDataPowerTextView )
		resourcesFansTextView = findViewById( R.id.serverResourcesDataFansTextView )
		drivesStatusTextView = findViewById( R.id.serverDrivesStatusTextView )
		drivesRecyclerView = findViewById( R.id.serverDrivesRecyclerView )
		networkStatusTextView = findViewById( R.id.serverNetworkStatusTextView )
		networkRecyclerView = findViewById( R.id.serverNetworkRecyclerView )
		servicesStatusTextView = findViewById( R.id.serverServicesStatusTextView )
		servicesRecyclerView = findViewById( R.id.serverServicesRecyclerView )
		dockerStatusTextView = findViewById( R.id.serverDockerStatusTextView )
		dockerRecyclerView = findViewById( R.id.serverDockerRecyclerView )
		snmpTitleTextView = findViewById( R.id.serverSNMPTitleTextView )
		snmpStatusTextView = findViewById( R.id.serverSNMPStatusTextView )
		snmpRecyclerView = findViewById( R.id.serverSNMPRecyclerView )

		// Default to unknown for all resources - the text is already grey as defined in layout, so no need color it here
		resourcesProcessorTextView.text = getString( R.string.serverTextViewResourcesProcessor ).format( "0%", "0Hz", "0℃" )
		resourcesMemoryTextView.text = getString( R.string.serverTextViewResourcesMemory ).format( "0B", "0B", "0%" )
		resourcesSwapTextView.text = getString( R.string.serverTextViewResourcesSwap ).format( "Swap", "0B", "0B", "0%" ) // Assume name is swap, probably fine as most servers are Linux
		resourcesNetworkTextView.text = getString( R.string.serverTextViewResourcesNetwork ).format( "0B/s", "0B/s" )
		resourcesDriveTextView.text = getString( R.string.serverTextViewResourcesDrive ).format( "0B/s", "0B/s" )
		resourcesPowerTextView.text = getString( R.string.serverTextViewResourcesPower ).format( "0W", "0W" )
		resourcesFansTextView.text = getString( R.string.serverTextViewResourcesFans ).format( "0RPM" )

		// Get the settings
		settings = Settings( getSharedPreferences( Shared.sharedPreferencesName, MODE_PRIVATE ) )
		Log.d( Shared.logTag, "Got settings ('${ settings.instanceUrl }', '${ settings.credentialsUsername }', '${ settings.credentialsPassword }')" )

		// Switch to the servers activity if we aren't servers yet
		if ( !settings.isSetup() ) {
			Log.d( Shared.logTag, "Not setup yet, switching to servers activity..." )

			startActivity( Intent( this, SetupActivity::class.java ) )
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )

			finish()

			return
		}

		// Return to the previous activity if we were not given a server identifier
		val serverIdentifier = intent.extras?.getString( "serverIdentifier" )
		Log.d( Shared.logTag, "Server identifier: '${ serverIdentifier }'" )
		if ( serverIdentifier.isNullOrBlank() ) {
			Log.w( Shared.logTag, "No server identifier passed to activity?! Returning to Servers activity..." )

			startActivity( Intent( this, ServersActivity::class.java ) )
			overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )

			finish()

			return
		}
		this.serverIdentifier = serverIdentifier

		// Enable the back button on the toolbar if we came from the servers activity
		if ( intent.extras?.getBoolean( "fromServersActivity" ) == true ) {
			materialToolbar?.navigationIcon = AppCompatResources.getDrawable( this, R.drawable.arrow_back )
			materialToolbar?.setNavigationOnClickListener {
				Log.d( Shared.logTag, "Navigation back button pressed. Returning to previous activity..." )

				finish()
				overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
			}
		}

		// Setup the drives recycler view
		val drivesLinearLayoutManager = LinearLayoutManager( this, LinearLayoutManager.VERTICAL, false )
		drivesRecyclerView.layoutManager = drivesLinearLayoutManager
		val drivesDividerItemDecoration = DividerItemDecoration( this, drivesLinearLayoutManager.orientation )
		drivesDividerItemDecoration.setDrawable( ContextCompat.getDrawable( this, R.drawable.shape_section_divider )!! )
		drivesRecyclerView.addItemDecoration( drivesDividerItemDecoration )

		// Setup the network interfaces recycler view
		val networkLinearLayoutManager = LinearLayoutManager( this, LinearLayoutManager.VERTICAL, false )
		networkRecyclerView.layoutManager = networkLinearLayoutManager
		val networkDividerItemDecoration = DividerItemDecoration( this, networkLinearLayoutManager.orientation )
		networkDividerItemDecoration.setDrawable( ContextCompat.getDrawable( this, R.drawable.shape_section_divider )!! )
		networkRecyclerView.addItemDecoration( networkDividerItemDecoration )

		// Setup the services recycler view
		val servicesLinearLayoutManager = LinearLayoutManager( this, LinearLayoutManager.VERTICAL, false )
		servicesRecyclerView.layoutManager = servicesLinearLayoutManager
		val servicesDividerItemDecoration = DividerItemDecoration( this, servicesLinearLayoutManager.orientation )
		servicesDividerItemDecoration.setDrawable( ContextCompat.getDrawable( this, R.drawable.shape_section_divider )!! )
		servicesRecyclerView.addItemDecoration( servicesDividerItemDecoration )

		// Setup the Docker containers recycler view
		val dockerLinearLayoutManager = LinearLayoutManager( this, LinearLayoutManager.VERTICAL, false )
		dockerRecyclerView.layoutManager = dockerLinearLayoutManager
		val dockerDividerItemDecoration = DividerItemDecoration( this, dockerLinearLayoutManager.orientation )
		dockerDividerItemDecoration.setDrawable( ContextCompat.getDrawable( this, R.drawable.shape_section_divider )!! )
		dockerRecyclerView.addItemDecoration( dockerDividerItemDecoration )

		// Setup the SNMP agents recycler view
		val snmpLinearLayoutManager = LinearLayoutManager( this, LinearLayoutManager.VERTICAL, false )
		snmpRecyclerView.layoutManager = snmpLinearLayoutManager
		val snmpDividerItemDecoration = DividerItemDecoration( this, dockerLinearLayoutManager.orientation )
		snmpDividerItemDecoration.setDrawable( ContextCompat.getDrawable( this, R.drawable.shape_section_divider )!! )
		snmpRecyclerView.addItemDecoration( dockerDividerItemDecoration )

		// Create the animation for the automatic refresh countdown progress bar - https://stackoverflow.com/a/18015071
		progressBarAnimation = ProgressBarAnimation( refreshProgressBar, refreshProgressBar.progress.toFloat(), refreshProgressBar.max.toFloat() )
		progressBarAnimation.interpolator = LinearInterpolator() // We want linear, not accelerate-decelerate interpolation

		// Store this activity for later use in showing snackbar messages
		val activity = this

		// Event listeners for the automatic refresh countdown progress bar animation - https://medium.com/android-news/handsome-codes-with-kotlin-6e183db4c7e5
		progressBarAnimation.setAnimationListener( object : Animation.AnimationListener {

			// When the animation starts...
			override fun onAnimationStart( animation: Animation? ) {
				Log.d( Shared.logTag, "Automatic refresh countdown progress bar animation started" )
			}

			// When the animation finishes or is manually cleared...
			override fun onAnimationEnd( animation: Animation? ) {
				Log.d( Shared.logTag, "Automatic refresh countdown progress bar animation ended (${ animation?.hasEnded() }, ${ animation?.hasStarted() }, ${ refreshProgressBar.progress }, ${ refreshProgressBar.isAnimating })" )

				// Don't refresh if we've been manually cleared
				if ( refreshProgressBar.progress == 0 ) return

				// Show refreshing spinner
				swipeRefreshLayout.isRefreshing = true

				CoroutineScope( Dispatchers.Main ).launch { // Begin coroutine context (on the UI thread)...
					withContext( Dispatchers.IO ) { // Run on network thread...

						// Fetch the server
						try {
							val server = Server( API.getServer( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!!, serverIdentifier )!!, true )
							Log.d( Shared.logTag, "Fetched server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') from API" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

								// Update the UI
								updateUI( server )

								// Start the progress bar animation
								if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )
							}

						} catch ( exception: APIException ) {
							Log.e( Shared.logTag, "Failed to fetch servers from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0

								when ( exception.volleyError ) {

									// Bad authentication
									is AuthFailureError -> when ( exception.apiErrorCode ) {
										ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serverToastServerAuthenticationUnknownUser )
										ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serverToastServerAuthenticationIncorrectPassword )
										else -> showBriefMessage( activity, R.string.serverToastServerAuthenticationFailure )
									}

									// HTTP 4xx
									is ClientError -> when ( exception.httpStatusCode ) {
										404 -> showBriefMessage( activity, R.string.serverToastServerNotFound )
										else -> showBriefMessage( activity, R.string.serverToastServerClientFailure )
									}

									// HTTP 5xx
									is ServerError -> when ( exception.httpStatusCode ) {
										502 -> showBriefMessage( activity, R.string.serverToastServerUnavailable )
										503 -> showBriefMessage( activity, R.string.serverToastServerUnavailable )
										504 -> showBriefMessage( activity, R.string.serverToastServerUnavailable )
										530 -> showBriefMessage( activity, R.string.serverToastServerUnavailable ) // Cloudflare
										else -> showBriefMessage( activity, R.string.serverToastServerServerFailure )
									}

									// No Internet connection, malformed domain
									is NoConnectionError -> showBriefMessage( activity, R.string.serverToastServerNoConnection )
									is NetworkError -> showBriefMessage( activity, R.string.serverToastServerNoConnection )

									// Connection timed out
									is TimeoutError -> showBriefMessage( activity, R.string.serverToastServerTimeout )

									// ¯\_(ツ)_/¯
									else -> showBriefMessage( activity, R.string.serverToastServerFailure )

								}
							}
						} catch ( exception: JsonParseException ) {
							Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serverToastServerParseFailure )
							}
						} catch ( exception: JsonSyntaxException ) {
							Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serverToastServerParseFailure )
							}
						} catch ( exception: NullPointerException ) {
							Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serverToastServerNull )
							}
						}

					}
				}
			}

			// When the animation repeats...
			override fun onAnimationRepeat( animation: Animation? ) {
				Log.d( Shared.logTag, "Automatic refresh countdown progress bar animation repeated" )
			}

		} )

		// When we're swiped down to refresh...
		swipeRefreshLayout.setOnRefreshListener {
			Log.d( Shared.logTag, "Swipe refreshed!" )

			// Stop the automatic refresh countdown progress bar, thus calling the animation callback
			if ( settings.automaticRefresh ) {
				refreshProgressBar.clearAnimation()

			// If automatic refresh is disabled, then do the refresh manually...
			} else {
				CoroutineScope( Dispatchers.Main ).launch {
					withContext( Dispatchers.IO ) {

						// Fetch the server
						try {
							val server = Server( API.getServer( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!!, serverIdentifier )!!, true )
							Log.d( Shared.logTag, "Fetched server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') from API" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0

								// Update the UI
								updateUI( server )

								// Start the progress bar animation
								if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )
							}

						} catch ( exception: APIException ) {
							Log.e( Shared.logTag, "Failed to fetch servers from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0

								when ( exception.volleyError ) {

									// Bad authentication
									is AuthFailureError -> when ( exception.apiErrorCode ) {
										ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serverToastServerAuthenticationUnknownUser )
										ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serverToastServerAuthenticationIncorrectPassword )
										else -> showBriefMessage( activity, R.string.serverToastServerAuthenticationFailure )
									}

									// HTTP 4xx
									is ClientError -> when ( exception.httpStatusCode ) {
										404 -> showBriefMessage( activity, R.string.serverToastServerNotFound )
										else -> showBriefMessage( activity, R.string.serverToastServerClientFailure )
									}

									// HTTP 5xx
									is ServerError -> when ( exception.httpStatusCode ) {
										502 -> showBriefMessage( activity, R.string.serverToastServerUnavailable )
										503 -> showBriefMessage( activity, R.string.serverToastServerUnavailable )
										504 -> showBriefMessage( activity, R.string.serverToastServerUnavailable )
										530 -> showBriefMessage( activity, R.string.serverToastServerUnavailable ) // Cloudflare
										else -> showBriefMessage( activity, R.string.serverToastServerServerFailure )
									}

									// No Internet connection, malformed domain
									is NoConnectionError -> showBriefMessage( activity, R.string.serverToastServerNoConnection )
									is NetworkError -> showBriefMessage( activity, R.string.serverToastServerNoConnection )

									// Connection timed out
									is TimeoutError -> showBriefMessage( activity, R.string.serverToastServerTimeout )

									// ¯\_(ツ)_/¯
									else -> showBriefMessage( activity, R.string.serverToastServerFailure )

								}
							}
						} catch ( exception: JsonParseException ) {
							Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serverToastServerParseFailure )
							}
						} catch ( exception: JsonSyntaxException ) {
							Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serverToastServerParseFailure )
							}
						} catch ( exception: NullPointerException ) {
							Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serverToastServerNull )
							}
						}

					}
				}
			}
		}

		// When the shutdown action button is pressed...
		actionShutdownButton.setOnClickListener {
			Log.d( Shared.logTag, "Shutdown server button pressed, sending API request..." )
			executeServerAction( activity, "shutdown" )
		}

		// When the reboot action button is pressed...
		actionRebootButton.setOnClickListener {
			Log.d( Shared.logTag, "Reboot server button pressed, sending API request..." )
			executeServerAction( activity, "reboot" )
		}

		// Register the back button pressed callback - https://medium.com/tech-takeaways/how-to-migrate-the-deprecated-onbackpressed-function-e66bb29fa2fd
		onBackPressedDispatcher.addCallback( this, onBackPressed )

		// Register the observer for the always on-going notification worker
		//UpdateWorker.observe( this, this )
		//Log.d( Shared.logTag, "Registered observer for always on-going notification worker" )

	}

	// Use custom animation when the back button is pressed - https://medium.com/tech-takeaways/how-to-migrate-the-deprecated-onbackpressed-function-e66bb29fa2fd
	private val onBackPressed: OnBackPressedCallback = object : OnBackPressedCallback( true ) {
		override fun handleOnBackPressed() {
			Log.d( Shared.logTag, "System back button pressed. Returning to previous activity..." )
			finish()
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
		}
	}

	// When the activity is closed...
	override fun onStop() {
		super.onStop()
		Log.d( Shared.logTag, "Stopped server activity" )

		// Remove all observers for the always on-going notification worker
		//WorkManager.getInstance( applicationContext ).getWorkInfosForUniqueWorkLiveData( UpdateWorker.NAME ).removeObservers( this )
		//Log.d( Shared.logTag, "Removed all observers for the always on-going notification worker" )

		// Cancel all pending HTTP requests
		//API.cancelQueue()
	}

	override fun onPause() {
		super.onPause()
		Log.d( Shared.logTag, "Paused server activity" )

		// Stop any current refreshing
		swipeRefreshLayout.isRefreshing = false

		// Stop the automatic refresh countdown progress bar
		if ( settings.automaticRefresh ) {
			refreshProgressBar.progress = 0 // Reset progress to prevent automatic refresh
			refreshProgressBar.clearAnimation() // Will call the animation end callback
		}
	}

	override fun onResume() {
		super.onResume()
		Log.d( Shared.logTag, "Resumed server activity" )

		// Reload settings in case they have changed
		settings.read()
		Log.d( Shared.logTag, "Reloaded settings (Automatic Refresh: '${ settings.automaticRefresh }', Automatic Refresh Interval: '${ settings.automaticRefreshInterval }')" )

		// Re-setup the worker as the automatic refresh interval may have changed
		val baseUrl = settings.instanceUrl
		val credentialsUsername = settings.credentialsUsername
		val credentialsPassword = settings.credentialsPassword
		if ( !baseUrl.isNullOrBlank() && !credentialsUsername.isNullOrBlank() && !credentialsPassword.isNullOrBlank() ) {
			UpdateWorker.setup( applicationContext, this, baseUrl, credentialsUsername, credentialsPassword, settings.automaticRefreshInterval, shouldEnqueue = settings.notificationAlwaysOngoing )
		} else {
			Log.wtf( Shared.logTag, "Base URL, username, or password (app is not setup) is null/blank after resuming?!" )
		}

		// Set the progress bar animation duration to the automatic refresh interval
		progressBarAnimation.duration = settings.automaticRefreshInterval * 1000L // Convert seconds to milliseconds

		// Toggle the countdown progress bar depending on the automatic refresh setting
		refreshProgressBar.isEnabled = settings.automaticRefresh
		refreshProgressBar.visibility = if ( settings.automaticRefresh ) View.VISIBLE else View.GONE

		// Show refreshing spinner
		swipeRefreshLayout.isRefreshing = true

		val activity = this
		CoroutineScope( Dispatchers.Main ).launch {
			withContext( Dispatchers.IO ) {

				// Fetch the contact information
				try {
					val hello = API.getHello( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!! )!!

					val contact = hello.get( "contact" ).asJsonObject!!
					contactName = contact.get( "name" ).asString!!

					val contactMethodsList = ArrayList<String>()
					for ( contactMethod in contact.get( "methods" ).asJsonArray!! ) contactMethodsList.add( contactMethod.asString!! )
					contactMethods = contactMethodsList.toTypedArray()

					Log.d( Shared.logTag, "Fetched contact information from API (Name: '${ contactName }', Methods: '${ contactMethods!!.joinToString( ", " ) }')" )

					// We don't enable user input & stop refreshing spinner here, as there's still another request to come

				} catch ( exception: APIException ) {
					Log.e( Shared.logTag, "Failed to fetch contact information from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false

						when ( exception.volleyError ) {

							// Bad authentication
							is AuthFailureError -> when ( exception.apiErrorCode ) {
								ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.toastInstanceTestAuthenticationUnknownUser )
								ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.toastInstanceTestAuthenticationIncorrectPassword )
								else -> showBriefMessage( activity, R.string.toastInstanceTestAuthenticationFailure )
							}

							// HTTP 4xx
							is ClientError -> when ( exception.httpStatusCode ) {
								404 -> showBriefMessage( activity, R.string.toastInstanceTestNotFound )
								else -> showBriefMessage( activity, R.string.toastInstanceTestClientFailure )
							}

							// HTTP 5xx
							is ServerError -> when ( exception.httpStatusCode ) {
								502 -> showBriefMessage( activity, R.string.toastInstanceTestUnavailable )
								503 -> showBriefMessage( activity, R.string.toastInstanceTestUnavailable )
								504 -> showBriefMessage( activity, R.string.toastInstanceTestUnavailable )
								530 -> showBriefMessage( activity, R.string.serverToastServerUnavailable ) // Cloudflare
								else -> showBriefMessage( activity, R.string.toastInstanceTestServerFailure )
							}

							// No Internet connection, malformed domain
							is NoConnectionError -> showBriefMessage( activity, R.string.toastInstanceTestNoConnection )
							is NetworkError -> showBriefMessage( activity, R.string.toastInstanceTestNoConnection )

							// Connection timed out
							is TimeoutError -> showBriefMessage( activity, R.string.toastInstanceTestTimeout )

							// ¯\_(ツ)_/¯
							else -> showBriefMessage( activity, R.string.toastInstanceTestFailure )

						}
					}
				} catch ( exception: JsonParseException ) {
					Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						showBriefMessage( activity, R.string.serverToastServerParseFailure )
					}
				} catch ( exception: JsonSyntaxException ) {
					Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						showBriefMessage( activity, R.string.serverToastServerParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						showBriefMessage( activity, R.string.serverToastServerNull )
					}
				}

				/******************************************************************************/

				// Fetch the server
				try {
					val server = Server( API.getServer( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!!, serverIdentifier )!!, true )
					Log.d( Shared.logTag, "Fetched server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') from API" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

						// Update the UI
						updateUI( server )

						// Start the progress bar animation
						if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )
					}

				} catch ( exception: APIException ) {
					Log.e( Shared.logTag, "Failed to fetch server '${ serverIdentifier }' from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

						when ( exception.volleyError ) {

							// Bad authentication
							is AuthFailureError -> when ( exception.apiErrorCode ) {
								ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serverToastServerAuthenticationUnknownUser )
								ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serverToastServerAuthenticationIncorrectPassword )
								else -> showBriefMessage( activity, R.string.serverToastServerAuthenticationFailure )
							}

							// HTTP 4xx
							is ClientError -> when ( exception.httpStatusCode ) {
								404 -> showBriefMessage( activity, R.string.serverToastServerNotFound )
								else -> showBriefMessage( activity, R.string.serverToastServerClientFailure )
							}

							// HTTP 5xx
							is ServerError -> when ( exception.httpStatusCode ) {
								502 -> showBriefMessage( activity, R.string.serverToastServerUnavailable )
								503 -> showBriefMessage( activity, R.string.serverToastServerUnavailable )
								504 -> showBriefMessage( activity, R.string.serverToastServerUnavailable )
								530 -> showBriefMessage( activity, R.string.serverToastServerUnavailable ) // Cloudflare
								else -> showBriefMessage( activity, R.string.serverToastServerServerFailure )
							}

							// No Internet connection, malformed domain
							is NoConnectionError -> showBriefMessage( activity, R.string.serverToastServerNoConnection )
							is NetworkError -> showBriefMessage( activity, R.string.serverToastServerNoConnection )

							// Connection timed out
							is TimeoutError -> showBriefMessage( activity, R.string.serverToastServerTimeout )

							// ¯\_(ツ)_/¯
							else -> showBriefMessage( activity, R.string.serverToastServerFailure )

						}
					}
				} catch ( exception: JsonParseException ) {
					Log.e( Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						showBriefMessage( activity, R.string.serverToastServerParseFailure )
					}
				} catch ( exception: JsonSyntaxException ) {
					Log.e( Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						showBriefMessage( activity, R.string.serverToastServerParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						showBriefMessage( activity, R.string.serverToastServerNull )
					}
				}

			}
		}
	}

	// Update the UI with the given server
	private fun updateUI( server: Server ) {

		// Set the title on the toolbar
		materialToolbar?.title = server.hostName.uppercase()
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ server.hostName.uppercase() }'" )

		// Enable/disable action buttons
		actionShutdownButton.isEnabled = server.isShutdownActionSupported()
		actionRebootButton.isEnabled = server.isRebootActionSupported()

		// Swap should be called page file if this server is running Windows
		val swapName = if ( server.isOperatingSystemWindows() ) "Page File" else "Swap"

		// Is the server running?
		if ( server.isOnline() ) {

			// Overall status
			statusTextView.setTextColor( getColor( R.color.black ) )
			statusTextView.setTextFromHTML( getString( R.string.serverTextViewStatus ).format(
				createHTMLColoredText( getString( R.string.serverTextViewStatusOnline ), R.color.statusGood ).asHTMLBold(),
				TimeSpan( server.uptimeSeconds ).toString( true )
			) )

			// Resources -> Processor
			val processorUsage = server.getProcessorUsage()
			val processorFrequencyHz = server.getProcessorFrequency()
			val processorTemperature = server.getProcessorTemperature()
			val processorFrequency = Frequency( processorFrequencyHz )
			resourcesProcessorTextView.setTextIconColor( getColor( R.color.black ) )
			resourcesProcessorTextView.setTextFromHTML( getString( R.string.serverTextViewResourcesProcessor ).format(
				applicationContext.createHTMLColoredText( processorUsage.atLeastRoundAsString( 0.0f, 1 ).suffixWith( Shared.percentSymbol ), processorUsage.getAppropriateColor( Server.processorUsageWarningThreshold, Server.processorUsageDangerThreshold ) ),
				applicationContext.createHTMLColoredText( processorFrequency.amount.atLeastRoundAsString( 0.0f, 1 ).suffixWith( processorFrequency.suffix ), processorFrequencyHz.getAppropriateColor() ),
				applicationContext.createHTMLColoredText( processorTemperature.atLeastRoundAsString( 0.0f, 1 ).suffixWith( Shared.degreesCelsiusSymbol ), processorTemperature.getAppropriateColor( Server.processorTemperatureWarningThreshold, Server.processorTemperatureDangerThreshold ) )
			) )

			// Resources -> Memory
			val memoryTotalBytes = server.getMemoryTotal()
			val memoryFreeBytes = server.getMemoryFree()
			val memoryUsedBytes = server.getMemoryUsed( memoryFreeBytes, memoryTotalBytes )
			val memoryTotal = Size( memoryTotalBytes )
			val memoryUsed = Size( memoryUsedBytes )
			val memoryUsage = server.getMemoryUsage( memoryFreeBytes, memoryTotalBytes )
			resourcesMemoryTextView.setTextIconColor( getColor( R.color.black ) )
			resourcesMemoryTextView.setTextFromHTML( getString( R.string.serverTextViewResourcesMemory ).format(
				applicationContext.createHTMLColoredText( memoryUsed.amount.atLeastRoundAsString( 0.0, 1 ).suffixWith( memoryUsed.suffix ), memoryUsedBytes.getAppropriateColor( Server.memoryUsedWarningThreshold( memoryTotalBytes ), Server.memoryUsedDangerThreshold( memoryTotalBytes ) ) ),
				applicationContext.createHTMLColoredText( memoryTotal.amount.atLeastRoundAsString( 0.0, 1 ).suffixWith( memoryTotal.suffix ), memoryTotalBytes.getAppropriateColor() ),
				applicationContext.createHTMLColoredText( memoryUsage.roundAsString( 1 ).suffixWith( Shared.percentSymbol ), memoryUsage.getAppropriateColor( Server.memoryUsageWarningThreshold, Server.memoryUsageDangerThreshold ) )
			) )

			// Resources -> Swap
			val swapTotalBytes = server.getSwapTotal()
			val swapFreeBytes = server.getSwapFree()
			val swapUsedBytes = server.getSwapUsed( swapFreeBytes, swapTotalBytes )
			val swapTotal = Size( swapTotalBytes )
			val swapUsed = Size( swapUsedBytes )
			val swapUsage = server.getSwapUsage( swapFreeBytes, swapTotalBytes )
			resourcesSwapTextView.setTextIconColor( getColor( R.color.black ) )
			resourcesSwapTextView.setTextFromHTML( getString( R.string.serverTextViewResourcesSwap ).format(
				swapName,
				applicationContext.createHTMLColoredText( swapUsed.amount.atLeastRoundAsString( 0.0, 1 ).suffixWith( swapUsed.suffix ), swapUsedBytes.getAppropriateColor( Server.swapUsedWarningThreshold( swapTotalBytes ), Server.swapUsedDangerThreshold( swapTotalBytes ) ) ),
				applicationContext.createHTMLColoredText( swapTotal.amount.atLeastRoundAsString( 0.0, 1 ).suffixWith( swapTotal.suffix ), swapTotalBytes.getAppropriateColor() ),
				applicationContext.createHTMLColoredText( swapUsage.roundAsString( 1 ).suffixWith( Shared.percentSymbol ), swapUsage.getAppropriateColor( Server.swapUsageWarningThreshold, Server.swapUsageDangerThreshold ) )
			) )

			// Resources -> Network I/O
			val networkTransmitRateBytes = server.getNetworkTotalTransmitRate()
			val networkReceiveRateBytes = server.getNetworkTotalReceiveRate()
			val networkTransmitRate = Size( networkTransmitRateBytes )
			val networkReceiveRate = Size( networkReceiveRateBytes )
			resourcesNetworkTextView.setTextIconColor( getColor( R.color.black ) )
			resourcesNetworkTextView.setTextFromHTML( getString( R.string.serverTextViewResourcesNetwork ).format(
				applicationContext.createHTMLColoredText( networkTransmitRate.amount.atLeastRoundAsString( 0.0, 1 ).suffixWith( networkTransmitRate.suffix.concat( "/s" ) ), networkTransmitRateBytes.getAppropriateColor( NetworkInterface.transmitRateWarningThreshold, NetworkInterface.transmitRateDangerThreshold ) ),
				applicationContext.createHTMLColoredText( networkReceiveRate.amount.atLeastRoundAsString( 0.0, 1 ).suffixWith( networkReceiveRate.suffix.concat( "/s" ) ), networkReceiveRateBytes.getAppropriateColor( NetworkInterface.receiveRateWarningThreshold, NetworkInterface.receiveRateDangerThreshold ) )
			) )

			// Resources -> Drive I/O
			val driveReadRateBytes = server.getDriveTotalReadRate()
			val driveWriteRateBytes = server.getDriveTotalWriteRate()
			val driveReadRate = Size( driveReadRateBytes )
			val driveWriteRate = Size( driveWriteRateBytes )
			resourcesDriveTextView.setTextIconColor( getColor( R.color.black ) )
			resourcesDriveTextView.setTextFromHTML( getString( R.string.serverTextViewResourcesDrive ).format(
				applicationContext.createHTMLColoredText( driveReadRate.amount.atLeastRoundAsString( 0.0, 1 ).suffixWith( driveReadRate.suffix.concat( "/s" ) ), driveReadRateBytes.getAppropriateColor( Drive.readRateWarningThreshold, Drive.readRateDangerThreshold ) ),
				applicationContext.createHTMLColoredText( driveWriteRate.amount.atLeastRoundAsString( 0.0, 1 ).suffixWith( driveWriteRate.suffix.concat( "/s" ) ), driveWriteRateBytes.getAppropriateColor( Drive.writeRateWarningThreshold, Drive.writeRateDangerThreshold ) )
			) )

			// TODO: Power & fans

			// Network Interfaces
			val networkInterfaces = server.getNetworkInterfaces()
			if ( networkInterfaces.isNotEmpty() ) {
				networkStatusTextView.visibility = View.GONE

				networkRecyclerView.adapter = NetworkInterfaceAdapter( networkInterfaces, applicationContext )
				networkRecyclerView.adapter?.notifyItemRangeChanged( 0, networkInterfaces.size )
				networkRecyclerView.visibility = View.VISIBLE
			} else {
				networkRecyclerView.visibility = View.GONE

				networkStatusTextView.setTextIconColor( getColor( R.color.statusDead ) )
				networkStatusTextView.text = getString( R.string.serverTextViewNetworkEmpty )
				networkStatusTextView.visibility = View.VISIBLE
			}

			// Drives
			val drives = server.getDrives()
			if ( drives.isNotEmpty() ) {
				drivesStatusTextView.visibility = View.GONE

				drivesRecyclerView.adapter = DriveAdapter( drives, applicationContext )
				drivesRecyclerView.adapter?.notifyItemRangeChanged( 0, drives.size )
				drivesRecyclerView.visibility = View.VISIBLE
			} else {
				drivesRecyclerView.visibility = View.GONE

				drivesStatusTextView.setTextIconColor( getColor( R.color.statusDead ) )
				drivesStatusTextView.text = getString( R.string.serverTextViewDrivesEmpty )
				drivesStatusTextView.visibility = View.VISIBLE
			}

			// Services
			val services = server.getSortedServices()
			if ( services.isNotEmpty() ) {
				servicesStatusTextView.visibility = View.GONE

				servicesRecyclerView.adapter = ServiceAdapter( services, applicationContext ) { service -> onServiceManagePressed( service ) }
				servicesRecyclerView.adapter?.notifyItemRangeChanged( 0, services.size )
				servicesRecyclerView.visibility = View.VISIBLE
			} else {
				servicesRecyclerView.visibility = View.GONE

				servicesStatusTextView.setTextIconColor( getColor( R.color.statusDead ) )
				servicesStatusTextView.text = getString( R.string.serverTextViewServicesEmpty )
				servicesStatusTextView.visibility = View.VISIBLE
			}

			// Docker Containers
			val dockerContainers = server.getDockerContainers()
			if ( dockerContainers.isNotEmpty() ) {
				dockerStatusTextView.visibility = View.GONE

				dockerRecyclerView.adapter = DockerContainerAdapter( dockerContainers, applicationContext )
				dockerRecyclerView.adapter?.notifyItemRangeChanged( 0, dockerContainers.size )
				dockerRecyclerView.visibility = View.VISIBLE
			} else {
				dockerRecyclerView.visibility = View.GONE

				dockerStatusTextView.setTextIconColor( getColor( R.color.statusDead ) )
				dockerStatusTextView.text = getString( R.string.serverTextViewDockerEmpty )
				dockerStatusTextView.visibility = View.VISIBLE
			}

			// SNMP Community
			val snmpCommunityName = server.snmpCommunity
			if ( snmpCommunityName != null ) {
				snmpTitleTextView.setTextFromHTML( getString( R.string.serverTextViewSNMPTitleCommunity ).format( snmpCommunityName.asHTMLItalic() ) )
			} else {
				snmpTitleTextView.text = getString( R.string.serverTextViewSNMPTitle )
			}

			// SNMP Agents
			val snmpAgents = server.getSNMPAgents()
			if ( snmpAgents.isNotEmpty() ) {
				snmpStatusTextView.visibility = View.GONE
				snmpRecyclerView.visibility = View.VISIBLE

				val snmpAgentsAdapter = SNMPAgentAdapter( snmpAgents, applicationContext )
				snmpRecyclerView.adapter = snmpAgentsAdapter
				snmpAgentsAdapter.notifyItemRangeChanged( 0, snmpAgents.size )
			} else {
				snmpRecyclerView.visibility = View.GONE

				snmpStatusTextView.visibility = View.VISIBLE
				snmpStatusTextView.setTextColor( getColor( R.color.statusDead ) )
				snmpStatusTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
				snmpStatusTextView.text = getString( R.string.serverTextViewSNMPEmpty )
			}

		// The server is not running...
		} else {

			// Overall status
			statusTextView.setTextColor( getColor( R.color.statusDead ) )
			statusTextView.setTextFromHTML( getString( R.string.serverTextViewStatus ).format(
				applicationContext.createHTMLColoredText( getString( R.string.serverTextViewStatusOffline ), getColor( R.color.statusDead ) ).asHTMLBold(),
				getString( R.string.serverTextViewStatusOfflineUptime )
			) )

			// Resources -> Processor
			resourcesProcessorTextView.setTextIconColor( getColor( R.color.statusDead ) )
			resourcesProcessorTextView.text = getString( R.string.serverTextViewResourcesProcessorUnknown )

			// Resources -> Memory
			resourcesMemoryTextView.setTextIconColor( getColor( R.color.statusDead ) )
			resourcesMemoryTextView.text = getString( R.string.serverTextViewResourcesMemoryUnknown )

			// Resources -> Swap
			resourcesSwapTextView.setTextIconColor( getColor( R.color.statusDead ) )
			resourcesSwapTextView.text = getString( R.string.serverTextViewResourcesSwapUnknown )

			// Resources -> Network I/O
			resourcesNetworkTextView.setTextIconColor( getColor( R.color.statusDead ) )
			resourcesNetworkTextView.text = getString( R.string.serverTextViewResourcesNetworkUnknown )

			// Resources -> Drive I/O
			resourcesDriveTextView.setTextColor( getColor( R.color.statusDead ) )
			resourcesDriveTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			resourcesDriveTextView.text = getString( R.string.serverTextViewResourcesDriveUnknown )

			// TODO: Power & fans

			// Network Interfaces
			networkRecyclerView.visibility = View.GONE
			networkStatusTextView.setTextIconColor( getColor( R.color.statusDead ) )
			networkStatusTextView.text = getString( R.string.serverTextViewNetworkUnknown )
			networkStatusTextView.visibility = View.VISIBLE

			// Drives
			drivesRecyclerView.visibility = View.GONE
			drivesStatusTextView.setTextIconColor( getColor( R.color.statusDead ) )
			drivesStatusTextView.text = getString( R.string.serverTextViewDrivesUnknown )
			drivesStatusTextView.visibility = View.VISIBLE

			// Services
			servicesRecyclerView.visibility = View.GONE
			servicesStatusTextView.setTextIconColor( getColor( R.color.statusDead ) )
			servicesStatusTextView.text = getString( R.string.serverTextViewServicesUnknown )
			servicesStatusTextView.visibility = View.VISIBLE

			// Docker Containers
			dockerRecyclerView.visibility = View.GONE
			dockerStatusTextView.setTextIconColor( getColor( R.color.statusDead ) )
			dockerStatusTextView.text = getString( R.string.serverTextViewDockerUnknown )
			dockerStatusTextView.visibility = View.VISIBLE

			// SNMP Community
			snmpTitleTextView.text = getString( R.string.serverTextViewSNMPTitle )

			// SNMP Agents
			snmpRecyclerView.visibility = View.GONE
			snmpStatusTextView.setTextIconColor( getColor( R.color.statusDead ) )
			snmpStatusTextView.text = getString( R.string.serverTextViewSNMPUnknown )
			snmpStatusTextView.visibility = View.VISIBLE

		}

	}

	// Runs when a service's manage button is pressed...
	private fun onServiceManagePressed( service: Service ) {
		Log.d( Shared.logTag, "Switching to Service activity..." )

		val intent = Intent( this, ServiceActivity::class.java )
		intent.putExtra( "serverIdentifier", serverIdentifier )
		intent.putExtra( "serviceName", service.serviceName )

		startActivity( intent )
		overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
	}

	// Executes an action on the server
	private fun executeServerAction( activity: Activity, actionName: String ) {
		CoroutineScope( Dispatchers.Main ).launch {

			// Show progress dialog
			val progressDialog = createProgressDialog( activity, R.string.serverDialogProgressActionExecuteTitle, R.string.serverDialogProgressActionExecuteMessage ) {
				API.cancelQueue()
				showBriefMessage( activity, R.string.serverDialogProgressActionExecuteCancel )
			}.apply { show() }

			withContext( Dispatchers.IO ) {

				// Try to execute the action
				try {
					val action = API.postServer( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!!, serverIdentifier, actionName )
					val exitCode = action?.get( "exitCode" )?.asInt
					var outputText = action?.get( "outputText" )?.asString?.trim()
					var errorText = action?.get( "errorText" )?.asString?.trim()

					if ( outputText.isNullOrBlank() ) outputText = "N/A"
					if ( errorText.isNullOrBlank() ) errorText = "N/A"

					Log.d( Shared.logTag, "Executed action '${ actionName }' on server '${ serverIdentifier }': '${ outputText }', '${ errorText }' (Exit Code: '${ exitCode }')" )

					withContext( Dispatchers.Main ) {
						progressDialog.dismiss()

						if ( exitCode == 0 ) showInformationDialog( activity, R.string.serverDialogActionExecuteTitle, getString( R.string.serverDialogActionExecuteMessageSuccess ).format( outputText, errorText ) )
						else showInformationDialog( activity, R.string.serverDialogActionExecuteTitle, getString( R.string.serverDialogActionExecuteMessageFailure ).format( exitCode, errorText, outputText ) )
					}

				} catch ( exception: APIException ) {
					Log.e( Shared.logTag, "Failed to execute action '${ actionName }' on API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

					withContext( Dispatchers.Main ) {
						progressDialog.dismiss()

						when ( exception.volleyError ) {

							// Bad authentication
							is AuthFailureError -> when ( exception.apiErrorCode ) {
								ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serverToastActionAuthenticationUnknownUser )
								ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serverToastActionAuthenticationIncorrectPassword )
								else -> showBriefMessage( activity, R.string.serverToastActionAuthenticationFailure )
							}

							// HTTP 4xx
							is ClientError -> when ( exception.apiErrorCode ) {
								ErrorCode.InvalidParameter.code -> showBriefMessage( activity, R.string.serverToastActionInvalidParameter )
								ErrorCode.UnknownAction.code -> showBriefMessage( activity, R.string.serverToastActionUnknownAction )
								ErrorCode.ActionNotExecutable.code -> showBriefMessage( activity, R.string.serverToastActionActionNotExecutable )
								ErrorCode.ActionServerUnknown.code -> showBriefMessage( activity, R.string.serverToastActionActionServerUnknown )
								else -> when ( exception.httpStatusCode ) {
									404 -> showBriefMessage( activity, R.string.serverToastActionNotFound )
									else -> showBriefMessage( activity, R.string.serverToastActionClientFailure )
								}
							}

							// HTTP 5xx
							is ServerError -> when ( exception.apiErrorCode ) {
								ErrorCode.ActionServerOffline.code -> showBriefMessage( activity, R.string.serverToastActionOffline )
								else -> when ( exception.httpStatusCode ) {
									502 -> showBriefMessage( activity, R.string.serverToastActionUnavailable )
									503 -> showBriefMessage( activity, R.string.serverToastActionUnavailable )
									504 -> showBriefMessage( activity, R.string.serverToastActionUnavailable )
									530 -> showBriefMessage( activity, R.string.serverToastActionUnavailable ) // Cloudflare
									else -> showBriefMessage( activity, R.string.serverToastActionServerFailure )
								}
							}

							// No Internet connection, malformed domain
							is NoConnectionError -> showBriefMessage( activity, R.string.serverToastActionNoConnection )
							is NetworkError -> showBriefMessage( activity, R.string.serverToastActionNoConnection )

							// Connection timed out
							is TimeoutError -> showBriefMessage( activity, R.string.serverToastActionTimeout )

							// ¯\_(ツ)_/¯
							else -> showBriefMessage( activity, R.string.serverToastActionFailure )

						}
					}
				} catch ( exception: JsonParseException ) {
					Log.e( Shared.logTag, "Failed to parse execute server action API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						progressDialog.dismiss()
						showBriefMessage( activity, R.string.serverToastActionParseFailure )
					}
				} catch ( exception: JsonSyntaxException ) {
					Log.e( Shared.logTag, "Failed to parse execute server action API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						progressDialog.dismiss()
						showBriefMessage( activity, R.string.serverToastActionParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in execute server action API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						progressDialog.dismiss()
						showBriefMessage( activity, R.string.serverToastActionNull )
					}
				}

			}
		}
	}

}
