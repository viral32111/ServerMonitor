package com.viral32111.servermonitor

import android.app.Activity
import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.text.Html
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
import com.android.volley.*
import com.google.android.material.appbar.MaterialToolbar
import com.google.gson.JsonParseException
import com.google.gson.JsonSyntaxException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import kotlin.math.roundToLong

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

				showInformationDialog(
					this,
					R.string.dialogInformationAboutTitle,
					String.format( "%s\n\n%s", getString( R.string.dialogInformationAboutMessage ), contactInformation )
				)
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

		// Default to unknown for all resources - the text is already grey as defined in layout, so no need to call createColorText()
		resourcesProcessorTextView.text = String.format( getString( R.string.serverTextViewResourcesDataProcessor ), String.format( getString( R.string.serverTextViewResourcesDataProcessorValue ), "0%", "0Hz", "0℃" ) )
		resourcesMemoryTextView.text = String.format( getString( R.string.serverTextViewResourcesDataMemory ), String.format( getString( R.string.serverTextViewResourcesDataMemoryValue ), "0B", "0B", "0%" ) )
		resourcesSwapTextView.text = String.format( getString( R.string.serverTextViewResourcesDataSwap ), "Swap", String.format( getString( R.string.serverTextViewResourcesDataSwapValue ), "0B", "0B", "0%" ) ) // Assume name is swap, probably fine as most servers are Linux
		resourcesNetworkTextView.text = String.format( getString( R.string.serverTextViewResourcesDataNetwork ), String.format( getString( R.string.serverTextViewResourcesDataNetworkValue ), "0B/s", "0B/s" ) )
		resourcesDriveTextView.text = String.format( getString( R.string.serverTextViewResourcesDataDrive ), String.format( getString( R.string.serverTextViewResourcesDataDriveValue ), "0B/s", "0B/s" ) )
		resourcesPowerTextView.text = String.format( getString( R.string.serverTextViewResourcesDataPower ), String.format( getString( R.string.serverTextViewResourcesDataPowerValue ), "0W", "0W" ) )
		resourcesFansTextView.text =String.format( getString( R.string.serverTextViewResourcesDataFans ), String.format( getString( R.string.serverTextViewResourcesDataFansValue ), "0RPM" ) )

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

		// Return to the previous activity if we were not given a server identifier
		val serverIdentifier = intent.extras?.getString( "serverIdentifier" )
		Log.d( Shared.logTag, "Server identifier: '${ serverIdentifier }'" )
		if ( serverIdentifier.isNullOrBlank() ) {
			Log.w( Shared.logTag, "No server identifier passed to activity?! Returning to previous activity..." )
			finish()
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
			return
		}
		this.serverIdentifier = serverIdentifier

		// Enable the back button on the toolbar if we came from the servers activity
		if ( intent.extras?.getBoolean( "fromServersActivity" ) == true ) {
			materialToolbar?.navigationIcon = AppCompatResources.getDrawable( this, R.drawable.ic_baseline_arrow_back_24 )
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
				} catch ( exception: JsonParseException) {
					Log.e( Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						showBriefMessage( activity, R.string.serverToastServerParseFailure )
					}
				} catch ( exception: JsonSyntaxException) {
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
		actionShutdownButton.isEnabled = server.isShutdownActionSupported == true
		actionRebootButton.isEnabled = server.isRebootActionSupported == true

		// Set the overall status - https://stackoverflow.com/a/37899914
		statusTextView.setTextColor( getColor( if ( server.isOnline() ) R.color.black else R.color.statusDead ) )
		statusTextView.text = Html.fromHtml( String.format(
			getString( R.string.serverTextViewStatusGood ),
			"<strong>" + createColorText( if ( server.isOnline() ) "ONLINE" else "OFFLINE", getColor( if ( server.isOnline() ) R.color.statusGood else R.color.statusDead ) ) + "</strong>",
			TimeSpan( server.uptimeSeconds ).toString( true )
		), Html.FROM_HTML_MODE_LEGACY )

		// TODO: One big if statement for server.isOnline() instead a bunch of small ones, like in ServerAdapter

		// Set the processor resource data
		if ( server.isOnline() ) {
			val processorFrequency = Frequency( server.processorFrequency?.roundToLong()?.times( 1000L * 1000L ) ?: -1L ) // Value from API is already in MHz

			resourcesProcessorTextView.setTextColor( getColor( R.color.black ) )
			resourcesProcessorTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.black ) )
			resourcesProcessorTextView.text = Html.fromHtml( String.format( getString( R.string.serverTextViewResourcesDataProcessor, String.format(
				getString( R.string.serverTextViewResourcesDataProcessorValue ),
				createColorText( roundValueOrDefault( server.processorUsage, Shared.percentSymbol ), colorForValue( applicationContext, server.processorUsage, Server.processorUsageWarningThreshold, Server.processorUsageDangerThreshold ) ),
				createColorText( roundValueOrDefault( processorFrequency.amount, processorFrequency.suffix ), colorAsNeutral( applicationContext, server.processorFrequency ) ),
				createColorText( roundValueOrDefault( server.processorTemperature, Shared.degreesCelsiusSymbol ), colorForValue( applicationContext, server.processorTemperature, Server.processorTemperatureWarningThreshold, Server.processorTemperatureDangerThreshold ) )
			) ) ), Html.FROM_HTML_MODE_LEGACY )
		} else {
			resourcesProcessorTextView.setTextColor( getColor( R.color.statusDead ) )
			resourcesProcessorTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			resourcesProcessorTextView.text = String.format( getString( R.string.serverTextViewResourcesDataProcessor ), createColorText( "Unknown", getColor( R.color.statusDead ) ) )
		}

		// Set the memory resource data
		if ( server.isOnline() ) {
			val memoryTotalBytes = server.memoryTotalBytes ?: -1L
			val memoryFreeBytes = server.memoryFreeBytes ?: -1L
			val memoryUsedBytes = memoryTotalBytes - memoryFreeBytes
			Log.d( Shared.logTag, "Memory Total: '${ memoryTotalBytes }' bytes, Memory Free: '${ memoryFreeBytes }' bytes, Memory Used: '${ memoryUsedBytes }' bytes" )

			val memoryUsed = Size( memoryUsedBytes )
			val memoryTotal = Size( memoryTotalBytes )
			Log.d( Shared.logTag, "Memory Total: '${ memoryTotal.amount }' '${ memoryTotal.suffix }', Memory Used: '${ memoryUsed.amount }' '${ memoryUsed.suffix }'" )

			val memoryUsage = server.getMemoryUsage()
			Log.d( Shared.logTag, "Memory Usage: '${ memoryUsage }'" )

			resourcesMemoryTextView.setTextColor( getColor( R.color.black ) )
			resourcesMemoryTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.black ) )
			resourcesMemoryTextView.text = Html.fromHtml( String.format( getString( R.string.serverTextViewResourcesDataMemory, String.format(
				getString( R.string.serverTextViewResourcesDataMemoryValue ),
				createColorText( roundValueOrDefault( memoryUsed.amount, memoryUsed.suffix ), colorForValue( applicationContext, memoryUsedBytes, Server.memoryUsedWarningThreshold( memoryTotalBytes ), Server.memoryUsedDangerThreshold( memoryTotalBytes ) ) ),
				createColorText( roundValueOrDefault( memoryTotal.amount, memoryTotal.suffix ), colorAsNeutral( applicationContext, memoryTotalBytes ) ),
				createColorText( roundValueOrDefault( memoryUsage, Shared.percentSymbol ), colorForValue( applicationContext, memoryUsage, Server.memoryUsageWarningThreshold, Server.memoryUsageDangerThreshold ) )
			) ) ), Html.FROM_HTML_MODE_LEGACY )
		} else {
			resourcesMemoryTextView.setTextColor( getColor( R.color.statusDead ) )
			resourcesMemoryTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			resourcesMemoryTextView.text = String.format( getString( R.string.serverTextViewResourcesDataMemory ), createColorText( "Unknown", getColor( R.color.statusDead ) ) )
		}

		// Set the swap/page-file resource data
		val swapName = if ( server.operatingSystem.contains( "Microsoft Windows" ) ) "Page" else "Swap"
		if ( server.isOnline() ) {
			val swapTotalBytes = server.swapTotalBytes ?: -1L
			val swapFreeBytes = server.swapFreeBytes ?: -1L
			val swapUsedBytes = if ( swapTotalBytes >= 0L && swapFreeBytes >= 0L ) swapTotalBytes - swapFreeBytes else -1L
			Log.d( Shared.logTag, "Swap Total: '${ swapTotalBytes }' bytes, Swap Free: '${ swapFreeBytes }' bytes, Swap Used: '${ swapUsedBytes }' bytes" )

			val swapUsed = Size( swapUsedBytes )
			val swapTotal = Size( swapTotalBytes )
			Log.d( Shared.logTag, "Swap Total: '${ swapTotal.amount }' '${ swapTotal.suffix }', Swap Used: '${ swapUsed.amount }' '${ swapUsed.suffix }'" )

			val swapUsage = server.getSwapUsage()
			Log.d( Shared.logTag, "Swap Usage: '${ swapUsage }'" )

			resourcesSwapTextView.setTextColor( getColor( R.color.black ) )
			resourcesSwapTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.black ) )
			resourcesSwapTextView.text = Html.fromHtml(
				String.format( getString( R.string.serverTextViewResourcesDataSwap ),
					swapName,
					String.format( getString( R.string.serverTextViewResourcesDataSwapValue ),
						createColorText( roundValueOrDefault( swapUsed.amount, swapUsed.suffix ), colorForValue( applicationContext, swapUsedBytes, Server.swapUsedWarningThreshold( swapTotalBytes ), Server.swapUsedDangerThreshold( swapTotalBytes ) ) ),
						createColorText( roundValueOrDefault( swapTotal.amount, swapTotal.suffix ), colorAsNeutral( applicationContext, swapTotalBytes ) ),
						createColorText( roundValueOrDefault( swapUsage, Shared.percentSymbol ), colorForValue( applicationContext, swapUsage, Server.swapUsageWarningThreshold, Server.swapUsageDangerThreshold ) )
					)
				), Html.FROM_HTML_MODE_LEGACY )
		} else {
			resourcesSwapTextView.setTextColor( getColor( R.color.statusDead ) )
			resourcesSwapTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			resourcesSwapTextView.text = String.format( getString( R.string.serverTextViewResourcesDataSwap ), swapName, createColorText( "Unknown", getColor( R.color.statusDead ) ) )
		}

		// Set the network resource data (this is a total of all interfaces)
		if ( server.isOnline() ) {
			val networkTransmitRateBytes = server.networkInterfaces?.fold( 0L ) { total, networkInterface -> total + networkInterface.rateBytesSent } ?: -1L
			val networkReceiveRateBytes = server.networkInterfaces?.fold( 0L ) { total, networkInterface -> total + networkInterface.rateBytesReceived } ?: -1L
			Log.d( Shared.logTag, "Network Transmit Rate: '${ networkTransmitRateBytes }' bytes, Network Receive Rate: '${ networkReceiveRateBytes }' bytes" )

			val networkTransmitRate = Size( networkTransmitRateBytes )
			val networkReceiveRate = Size( networkReceiveRateBytes )
			Log.d( Shared.logTag, "Network Transmit Rate: '${ networkTransmitRate.amount }' '${ networkTransmitRate.suffix }', Network Receive Rate: '${ networkReceiveRate.amount }' '${ networkReceiveRate.suffix }'" )

			resourcesNetworkTextView.setTextColor( getColor( R.color.black ) )
			resourcesNetworkTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.black ) )
			resourcesNetworkTextView.text = Html.fromHtml( String.format( getString( R.string.serverTextViewResourcesDataNetwork ), String.format( getString( R.string.serverTextViewResourcesDataNetworkValue ),
				createColorText( roundValueOrDefault( networkTransmitRate.amount, networkTransmitRate.suffix + "/s" ), colorForValue( applicationContext, networkTransmitRateBytes, Server.networkTransmitRateWarningThreshold, Server.networkTransmitRateDangerThreshold ) ),
				createColorText( roundValueOrDefault( networkReceiveRate.amount, networkReceiveRate.suffix + "/s" ), colorForValue( applicationContext, networkReceiveRateBytes, Server.networkReceiveRateWarningThreshold, Server.networkReceiveRateDangerThreshold ) )
			) ), Html.FROM_HTML_MODE_LEGACY )
		} else {
			resourcesNetworkTextView.setTextColor( getColor( R.color.statusDead ) )
			resourcesNetworkTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			resourcesNetworkTextView.text = String.format( getString( R.string.serverTextViewResourcesDataNetwork ), createColorText( "Unknown", getColor( R.color.statusDead ) ) )
		}

		// Set the drive resource data (this is a total of all drives)
		if ( server.isOnline() ) {
			val driveReadRateBytes = server.drives?.fold( 0L ) { total, drive -> total + drive.rateBytesRead } ?: -1L
			val driveWriteRateBytes = server.drives?.fold( 0L ) { total, drive -> total + drive.rateBytesWritten } ?: -1L
			Log.d( Shared.logTag, "Drive Read Rate: '${ driveReadRateBytes }' bytes, Drive Write Rate: '${ driveWriteRateBytes }' bytes" )

			val driveReadRate = Size( driveReadRateBytes )
			val driveWriteRate = Size( driveWriteRateBytes )
			Log.d( Shared.logTag, "Drive Read Rate: '${ driveReadRate.amount }' '${ driveReadRate.suffix }', Drive Write Rate: '${ driveWriteRate.amount }' '${ driveWriteRate.suffix }'" )

			resourcesDriveTextView.setTextColor( getColor( R.color.black ) )
			resourcesDriveTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.black ) )
			resourcesDriveTextView.text = Html.fromHtml( String.format( getString( R.string.serverTextViewResourcesDataDrive ), String.format( getString( R.string.serverTextViewResourcesDataDriveValue ),
				createColorText( roundValueOrDefault( driveReadRate.amount, driveReadRate.suffix + "/s" ), colorForValue( applicationContext, driveReadRateBytes, Server.driveReadRateWarningThreshold, Server.driveReadRateDangerThreshold ) ),
				createColorText( roundValueOrDefault( driveWriteRate.amount, driveWriteRate.suffix + "/s" ), colorForValue( applicationContext, driveWriteRateBytes, Server.driveWriteRateWarningThreshold, Server.driveWriteRateDangerThreshold ) )
			) ), Html.FROM_HTML_MODE_LEGACY )
		} else {
			resourcesDriveTextView.setTextColor( getColor( R.color.statusDead ) )
			resourcesDriveTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			resourcesDriveTextView.text = String.format( getString( R.string.serverTextViewResourcesDataDrive ), createColorText( "Unknown", getColor( R.color.statusDead ) ) )
		}

		// TODO: Power resource data

		// TODO: Fans resource data

		// Network interfaces
		if ( server.isOnline() ) {
			val networkInterfaces = server.networkInterfaces?.reversedArray()
			if ( networkInterfaces != null && networkInterfaces.isNotEmpty() ) {
				networkStatusTextView.visibility = View.GONE
				networkRecyclerView.visibility = View.VISIBLE

				val networkInterfacesAdapter = NetworkInterfaceAdapter( networkInterfaces, applicationContext )
				networkRecyclerView.adapter = networkInterfacesAdapter
				networkInterfacesAdapter.notifyItemRangeChanged( 0, networkInterfaces.size )
			} else {
				networkRecyclerView.visibility = View.GONE

				networkStatusTextView.visibility = View.VISIBLE
				networkStatusTextView.setTextColor( getColor( R.color.statusDead ) )
				networkStatusTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
				networkStatusTextView.text = getString( R.string.serverTextViewNetworkEmpty )
			}
		} else {
			networkRecyclerView.visibility = View.GONE

			networkStatusTextView.visibility = View.VISIBLE
			networkStatusTextView.setTextColor( getColor( R.color.statusDead ) )
			networkStatusTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			networkStatusTextView.text = getString( R.string.serverTextViewNetworkUnknown )
		}

		// Drives
		if ( server.isOnline() ) {
			val drives = server.drives
			if ( drives != null && drives.isNotEmpty() ) {
				drivesStatusTextView.visibility = View.GONE
				drivesRecyclerView.visibility = View.VISIBLE

				val drivesAdapter = DriveAdapter( drives, applicationContext )
				drivesRecyclerView.adapter = drivesAdapter
				drivesAdapter.notifyItemRangeChanged( 0, drives.size )
			} else {
				drivesRecyclerView.visibility = View.GONE

				drivesStatusTextView.visibility = View.VISIBLE
				drivesStatusTextView.setTextColor( getColor( R.color.statusDead ) )
				drivesStatusTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
				drivesStatusTextView.text = getString( R.string.serverTextViewDrivesEmpty )
			}
		} else {
			drivesRecyclerView.visibility = View.GONE

			drivesStatusTextView.visibility = View.VISIBLE
			drivesStatusTextView.setTextColor( getColor( R.color.statusDead ) )
			drivesStatusTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			drivesStatusTextView.text = getString( R.string.serverTextViewDrivesUnknown )
		}

		// Services
		if ( server.isOnline() ) {
			val services = server.services?.let { sortServices( it ) }

			if ( services != null && services.isNotEmpty() ) {
				servicesStatusTextView.visibility = View.GONE
				servicesRecyclerView.visibility = View.VISIBLE

				val servicesAdapter = ServiceAdapter( services, applicationContext ) { service -> onServiceManagePressed( service ) }
				servicesRecyclerView.adapter = servicesAdapter
				servicesAdapter.notifyItemRangeChanged( 0, services.size )
			} else {
				servicesRecyclerView.visibility = View.GONE

				servicesStatusTextView.visibility = View.VISIBLE
				servicesStatusTextView.setTextColor( getColor( R.color.statusDead ) )
				servicesStatusTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
				servicesStatusTextView.text = getString( R.string.serverTextViewServicesEmpty )
			}
		} else {
			servicesRecyclerView.visibility = View.GONE

			servicesStatusTextView.visibility = View.VISIBLE
			servicesStatusTextView.setTextColor( getColor( R.color.statusDead ) )
			servicesStatusTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			servicesStatusTextView.text = getString( R.string.serverTextViewServicesUnknown )
		}

		// Docker containers
		if ( server.isOnline() ) {
			val dockerContainers = server.dockerContainers

			if ( dockerContainers != null && dockerContainers.isNotEmpty() ) {
				dockerStatusTextView.visibility = View.GONE
				dockerRecyclerView.visibility = View.VISIBLE

				val dockerContainersAdapter = DockerContainerAdapter( dockerContainers, applicationContext )
				dockerRecyclerView.adapter = dockerContainersAdapter
				dockerContainersAdapter.notifyItemRangeChanged( 0, dockerContainers.size )
			} else {
				dockerRecyclerView.visibility = View.GONE

				dockerStatusTextView.visibility = View.VISIBLE
				dockerStatusTextView.setTextColor( getColor( R.color.statusDead ) )
				dockerStatusTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
				dockerStatusTextView.text = getString( R.string.serverTextViewDockerEmpty )
			}
		} else {
			dockerRecyclerView.visibility = View.GONE

			dockerStatusTextView.visibility = View.VISIBLE
			dockerStatusTextView.setTextColor( getColor( R.color.statusDead ) )
			dockerStatusTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			dockerStatusTextView.text = getString( R.string.serverTextViewDockerUnknown )
		}

		// SNMP
		if ( server.isOnline() ) {
			snmpTitleTextView.text = Html.fromHtml( String.format( getString( R.string.serverTextViewSNMPTitleCommunity ), "<em>${ server.snmpCommunity }</em>" ), Html.FROM_HTML_MODE_LEGACY )

			val snmpAgents = server.snmpAgents

			if ( snmpAgents != null && snmpAgents.isNotEmpty() ) {
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
		} else {
			snmpTitleTextView.text = getString( R.string.serverTextViewSNMPTitle )

			snmpRecyclerView.visibility = View.GONE

			snmpStatusTextView.visibility = View.VISIBLE
			snmpStatusTextView.setTextColor( getColor( R.color.statusDead ) )
			snmpStatusTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			snmpStatusTextView.text = getString( R.string.serverTextViewSNMPUnknown )
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

	// Sorts the services before showing them - https://stackoverflow.com/a/59402330
	private fun sortServices( services: Array<Service> ): Array<Service> {

		// Convert fixed array to list
		val servicesCopy = arrayListOf<Service>().apply { addAll( services ) }

		// Sort by name in alphabetical order - https://stackoverflow.com/a/53354117
		servicesCopy.sortBy { it.displayName }

		// Sort by status code - groups up services with the same status (running, stopped, etc.)
		servicesCopy.sortWith( Comparator { service1: Service, service2: Service ->
			return@Comparator service1.statusCode - service2.statusCode
		} )

		// Sort by familiar services - groups up commonly used/recognised services
		servicesCopy.sortWith( compareBy { it.serviceName in arrayOf(

			// Windows
			"Schedule", "EventLog",
			"pla", // Performance Logs & Alerts
			"VBoxService", // VirtualBox Guest Additions
			"wuauserv", // Windows Update
			"W32Time", // Windows Time
			"mpssvc", // Windows Defender Firewall
			"TermService", // Remote Desktop Services
			"Cloudflared",
			"Dhcp", "DHCPServer",
			"Dnscache", "DNS",
			"SNMP", "SNMPTRAP",

			// Linux
			"apparmor",
			"thermald",
			"snapd", "unattended-upgrades",
			"lvm2-monitor", "lvm",
			"cloudflared",
			"ssh", "sshd", "ssh-agent",
			"docker", "dockerd", "containerd",

		) } )

		// Reverse the order - moves familiar & running services to the top
		servicesCopy.reverse()

		// Convert back to fixed array before returning
		return servicesCopy.toTypedArray()

	}

	// Executes an action on the server
	private fun executeServerAction( activity: Activity, actionName: String ) {
		CoroutineScope( Dispatchers.Main ).launch {

			// Show progress dialog
			val progressDialog = createProgressDialog( activity, R.string.serverDialogProgressActionExecuteTitle, R.string.serverDialogProgressActionExecuteMessage ) {
				API.cancelQueue()
				showBriefMessage( activity, R.string.serverDialogProgressActionExecuteCancel )
			}
			progressDialog.show()

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

						if ( exitCode == 0 ) showInformationDialog( activity, R.string.serverDialogActionExecuteTitle, String.format( getString( R.string.serverDialogActionExecuteMessageSuccess, outputText, errorText ) ) )
						else showInformationDialog( activity, R.string.serverDialogActionExecuteTitle, String.format( getString( R.string.serverDialogActionExecuteMessageFailure, exitCode, errorText, outputText ) ) )
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
