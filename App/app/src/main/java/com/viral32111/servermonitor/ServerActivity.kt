package com.viral32111.servermonitor

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
	private lateinit var snmpTitleTextView: TextView
	private lateinit var refreshProgressBar: ProgressBar

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
		snmpTitleTextView = findViewById( R.id.serverSNMPTitleTextView )
		refreshProgressBar = findViewById( R.id.serverRefreshProgressBar )

		// Default to unknown for all resources - the text is already grey as defined in layout, so no need to call createColorText()
		resourcesProcessorTextView.text = String.format( getString( R.string.serverTextViewResourcesDataProcessor ), "Unknown" )
		resourcesMemoryTextView.text = String.format( getString( R.string.serverTextViewResourcesDataMemory ), "Unknown" )
		resourcesSwapTextView.text = String.format( getString( R.string.serverTextViewResourcesDataSwap ), "Swap", "Unknown" ) // Assume name is swap, probably fine as most servers are Linux
		resourcesNetworkTextView.text = String.format( getString( R.string.serverTextViewResourcesDataNetwork ), "Unknown" )
		resourcesDriveTextView.text = String.format( getString( R.string.serverTextViewResourcesDataDrive ), "Unknown" )
		resourcesPowerTextView.text = String.format( getString( R.string.serverTextViewResourcesDataPower ), "Unknown" )
		resourcesFansTextView.text = String.format( getString( R.string.serverTextViewResourcesDataFans ), "Unknown" )

		// TODO: Default to nothing available for drives, services, Docker containers & SNMP agents

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

		// Create a linear layout manager for the drives recycler view
		val linearLayoutManager = LinearLayoutManager( this, LinearLayoutManager.VERTICAL, false )
		drivesRecyclerView.layoutManager = linearLayoutManager

		// Set the divider between drives in its recycler view
		val dividerItemDecoration = DividerItemDecoration( this, linearLayoutManager.orientation )
		dividerItemDecoration.setDrawable( ContextCompat.getDrawable( this, R.drawable.shape_drive_divider )!! )
		drivesRecyclerView.addItemDecoration( dividerItemDecoration )

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

				// Show refreshing spinner & disable user input
				swipeRefreshLayout.isRefreshing = true
				enableInputs( false )

				CoroutineScope( Dispatchers.Main ).launch { // Begin coroutine context (on the UI thread)...
					withContext( Dispatchers.IO ) { // Run on network thread...

						// Fetch the server
						try {
							val server = Server( API.getServer( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!!, serverIdentifier )!!, true )
							Log.d( Shared.logTag, "Fetched server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') from API" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0

								// Update the UI & enable user input
								updateUI( server )
								enableInputs( true )

								// Start the progress bar animation
								if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )
							}

						} catch ( exception: APIException ) {
							Log.e( Shared.logTag, "Failed to fetch servers from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								enableInputs( true )

								when ( exception.volleyError ) {

									// Bad authentication
									is AuthFailureError -> when ( exception.apiErrorCode ) {
										ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serversToastServersAuthenticationUnknownUser )
										ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serversToastServersAuthenticationIncorrectPassword )
										else -> showBriefMessage( activity, R.string.serversToastServersAuthenticationFailure )
									}

									// HTTP 4xx
									is ClientError -> when ( exception.httpStatusCode ) {
										404 -> showBriefMessage( activity, R.string.serversToastServersNotFound )
										else -> showBriefMessage( activity, R.string.serversToastServersClientFailure )
									}

									// HTTP 5xx
									is ServerError -> when ( exception.httpStatusCode ) {
										502 -> showBriefMessage( activity, R.string.serversToastServersUnavailable )
										503 -> showBriefMessage( activity, R.string.serversToastServersUnavailable )
										504 -> showBriefMessage( activity, R.string.serversToastServersUnavailable )
										530 -> showBriefMessage( activity, R.string.serverToastServerUnavailable ) // Cloudflare
										else -> showBriefMessage( activity, R.string.serversToastServersServerFailure )
									}

									// No Internet connection, malformed domain
									is NoConnectionError -> showBriefMessage( activity, R.string.serversToastServersNoConnection )
									is NetworkError -> showBriefMessage( activity, R.string.serversToastServersNoConnection )

									// Connection timed out
									is TimeoutError -> showBriefMessage( activity, R.string.serversToastServersTimeout )

									// ¯\_(ツ)_/¯
									else -> showBriefMessage( activity, R.string.serversToastServersFailure )

								}
							}
						} catch ( exception: JsonParseException ) {
							Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								enableInputs( true )
								showBriefMessage( activity, R.string.serversToastServersParseFailure )
							}
						} catch ( exception: JsonSyntaxException ) {
							Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								enableInputs( true )
								showBriefMessage( activity, R.string.serversToastServersParseFailure )
							}
						} catch ( exception: NullPointerException ) {
							Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								enableInputs( true )
								showBriefMessage( activity, R.string.serversToastServersNull )
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

						// Fetch the servers
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
										ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serversToastServersAuthenticationUnknownUser )
										ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serversToastServersAuthenticationIncorrectPassword )
										else -> showBriefMessage( activity, R.string.serversToastServersAuthenticationFailure )
									}

									// HTTP 4xx
									is ClientError -> when ( exception.httpStatusCode ) {
										404 -> showBriefMessage( activity, R.string.serversToastServersNotFound )
										else -> showBriefMessage( activity, R.string.serversToastServersClientFailure )
									}

									// HTTP 5xx
									is ServerError -> when ( exception.httpStatusCode ) {
										502 -> showBriefMessage( activity, R.string.serversToastServersUnavailable )
										503 -> showBriefMessage( activity, R.string.serversToastServersUnavailable )
										504 -> showBriefMessage( activity, R.string.serversToastServersUnavailable )
										530 -> showBriefMessage( activity, R.string.serverToastServerUnavailable ) // Cloudflare
										else -> showBriefMessage( activity, R.string.serversToastServersServerFailure )
									}

									// No Internet connection, malformed domain
									is NoConnectionError -> showBriefMessage( activity, R.string.serversToastServersNoConnection )
									is NetworkError -> showBriefMessage( activity, R.string.serversToastServersNoConnection )

									// Connection timed out
									is TimeoutError -> showBriefMessage( activity, R.string.serversToastServersTimeout )

									// ¯\_(ツ)_/¯
									else -> showBriefMessage( activity, R.string.serversToastServersFailure )

								}
							}
						} catch ( exception: JsonParseException ) {
							Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serversToastServersParseFailure )
							}
						} catch ( exception: JsonSyntaxException ) {
							Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serversToastServersParseFailure )
							}
						} catch ( exception: NullPointerException ) {
							Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serversToastServersNull )
							}
						}

					}
				}
			}
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

		// Show refreshing spinner & disable user input
		swipeRefreshLayout.isRefreshing = true
		enableInputs( false )

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
						enableInputs( true )

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
						enableInputs( true )
						showBriefMessage( activity, R.string.serversToastServersParseFailure )
					}
				} catch ( exception: JsonSyntaxException ) {
					Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						enableInputs( true )
						showBriefMessage( activity, R.string.serversToastServersParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						enableInputs( true )
						showBriefMessage( activity, R.string.serversToastServersNull )
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

						// Update the UI & enable user input
						updateUI( server )
						enableInputs( true )

						// Start the progress bar animation
						if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )
					}

				} catch ( exception: APIException ) {
					Log.e( Shared.logTag, "Failed to fetch server '${ serverIdentifier }' from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						enableInputs( true )

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
						enableInputs( true )
						showBriefMessage( activity, R.string.serverToastServerParseFailure )
					}
				} catch ( exception: JsonSyntaxException) {
					Log.e( Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						enableInputs( true )
						showBriefMessage( activity, R.string.serverToastServerParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						enableInputs( true )
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

		// Set the overall status - https://stackoverflow.com/a/37899914
		statusTextView.setTextColor( getColor( if ( server.isOnline() ) R.color.black else R.color.statusDead ) )
		statusTextView.text = Html.fromHtml( String.format(
			getString( R.string.serverTextViewStatusGood ),
			"<strong><span style=\"color: #${ getColor( if ( server.isOnline() ) R.color.statusGood else R.color.statusDead ).toString( 16 ) }\">${ if ( server.isOnline() ) "ONLINE" else "OFFLINE" }</span></strong>",
			TimeSpan( server.uptimeSeconds ).toString( true )
		), Html.FROM_HTML_MODE_LEGACY )

		// Set the processor resource data
		if ( server.isOnline() ) {
			val processorFrequency = Frequency( server.processorFrequency?.roundToLong()?.times( 1000L * 1000L ) ?: -1L ) // Value from API is already in MHz

			resourcesProcessorTextView.setTextColor( getColor( R.color.black ) )
			resourcesProcessorTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.black ) )
			resourcesProcessorTextView.text = Html.fromHtml( String.format( getString( R.string.serverTextViewResourcesDataProcessor, String.format(
				getString( R.string.serverTextViewResourcesDataProcessorValue ),
				createColorText( roundValueOrDefault( server.processorUsage, Shared.percentSymbol ), colorForValue( applicationContext, server.processorUsage, 50.0f, 80.0f ) ),
				createColorText( roundValueOrDefault( processorFrequency.amount, processorFrequency.suffix ), colorAsNeutral( applicationContext, server.processorFrequency ) ),
				createColorText( roundValueOrDefault( server.processorTemperature, "℃" ), colorForValue( applicationContext, server.processorTemperature, 60.0f, 90.0f ) )
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

			val memoryUsage = ( memoryUsedBytes.toDouble() / memoryTotalBytes.toDouble() ) * 100.0
			Log.d( Shared.logTag, "Memory Usage: '${ memoryUsage }'" )

			resourcesMemoryTextView.setTextColor( getColor( R.color.black ) )
			resourcesMemoryTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.black ) )
			resourcesMemoryTextView.text = Html.fromHtml( String.format( getString( R.string.serverTextViewResourcesDataMemory, String.format(
				getString( R.string.serverTextViewResourcesDataMemoryValue ),
				createColorText( roundValueOrDefault( memoryUsed.amount, memoryUsed.suffix ), colorForValue( applicationContext, memoryUsedBytes.toFloat(), memoryTotalBytes / 2.0f, memoryTotalBytes / 1.25f ) ),
				createColorText( roundValueOrDefault( memoryTotal.amount, memoryTotal.suffix ), colorAsNeutral( applicationContext, memoryTotalBytes ) ),
				createColorText( roundValueOrDefault( memoryUsage, Shared.percentSymbol ), colorForValue( applicationContext, memoryUsage, 50.0, 80.0 ) )
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

			val swapUsage = if ( swapTotalBytes >= 0L && swapFreeBytes >= 0L ) ( swapUsedBytes.toDouble() / swapTotalBytes.toDouble() ) * 100.0 else -1.0
			Log.d( Shared.logTag, "Swap Usage: '${ swapUsage }'" )

			resourcesSwapTextView.setTextColor( getColor( R.color.black ) )
			resourcesSwapTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.black ) )
			resourcesSwapTextView.text = Html.fromHtml(
				String.format( getString( R.string.serverTextViewResourcesDataSwap ),
					swapName,
					String.format( getString( R.string.serverTextViewResourcesDataSwapValue ),
						createColorText( roundValueOrDefault( swapUsed.amount, swapUsed.suffix ), colorForValue( applicationContext, swapUsedBytes.toFloat(), swapTotalBytes / 2.0f, swapTotalBytes / 1.25f ) ),
						createColorText( roundValueOrDefault( swapTotal.amount, swapTotal.suffix ), colorAsNeutral( applicationContext, swapTotalBytes ) ),
						createColorText( roundValueOrDefault( swapUsage, Shared.percentSymbol ), colorForValue( applicationContext, swapUsage, 50.0, 80.0 ) )
					)
				), Html.FROM_HTML_MODE_LEGACY )
		} else {
			resourcesSwapTextView.setTextColor( getColor( R.color.statusDead ) )
			resourcesSwapTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			resourcesSwapTextView.text = String.format( getString( R.string.serverTextViewResourcesDataSwap ), swapName, createColorText( "Unknown", getColor( R.color.statusDead ) ) )
		}

		// Set the network resource data
		// TODO: Separate section for this with each interface individually, as this is just a total/overview
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
				createColorText( roundValueOrDefault( networkTransmitRate.amount, networkTransmitRate.suffix + "/s" ), colorForValue( applicationContext, networkTransmitRateBytes, 1024L * 1024L, 1024L * 1024L * 10L ) ), // No idea what the thresholds should be, so guestimate
				createColorText( roundValueOrDefault( networkReceiveRate.amount, networkReceiveRate.suffix + "/s" ), colorForValue( applicationContext, networkReceiveRateBytes, 1024L * 1024L, 1024L * 1024L * 10L ) ) // No idea what the thresholds should be, so guestimate
			) ), Html.FROM_HTML_MODE_LEGACY )
		} else {
			resourcesNetworkTextView.setTextColor( getColor( R.color.statusDead ) )
			resourcesNetworkTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			resourcesNetworkTextView.text = String.format( getString( R.string.serverTextViewResourcesDataNetwork ), createColorText( "Unknown", getColor( R.color.statusDead ) ) )
		}

		// Set the drive resource data (this is a total)
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
				createColorText( roundValueOrDefault( driveReadRate.amount, driveReadRate.suffix + "/s" ), colorForValue( applicationContext, driveReadRateBytes, 1024L * 1024L, 1024L * 1024L * 10L ) ), // No idea what the thresholds should be, so guestimate
				createColorText( roundValueOrDefault( driveWriteRate.amount, driveWriteRate.suffix + "/s" ), colorForValue( applicationContext, driveWriteRateBytes, 1024L * 1024L, 1024L * 1024L * 10L ) ) // No idea what the thresholds should be, so guestimate
			) ), Html.FROM_HTML_MODE_LEGACY )
		} else {
			resourcesDriveTextView.setTextColor( getColor( R.color.statusDead ) )
			resourcesDriveTextView.compoundDrawables[ 0 ].setTint( getColor( R.color.statusDead ) )
			resourcesDriveTextView.text = String.format( getString( R.string.serverTextViewResourcesDataDrive ), createColorText( "Unknown", getColor( R.color.statusDead ) ) )
		}

		// TODO: Power resource data

		// TODO: Fans resource data

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

		// SNMP
		if ( server.isOnline() ) {
			snmpTitleTextView.text = Html.fromHtml( String.format( getString( R.string.serverTextViewSNMPTitleCommunity ), "<em>${ server.snmpCommunity }</em>" ), Html.FROM_HTML_MODE_LEGACY )
		} else {
			snmpTitleTextView.text = getString( R.string.serverTextViewSNMPTitle )
		}

	}

	// Enable/disable user input
	private fun enableInputs( shouldEnable: Boolean ) {

		// Action buttons
		actionShutdownButton.isEnabled = shouldEnable
		actionRebootButton.isEnabled = shouldEnable

	}

}
