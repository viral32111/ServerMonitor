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
import androidx.recyclerview.widget.RecyclerView
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout
import androidx.work.WorkManager
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
import com.viral32111.servermonitor.data.Server
import com.viral32111.servermonitor.data.Service
import com.viral32111.servermonitor.helper.API
import com.viral32111.servermonitor.helper.APIException
import com.viral32111.servermonitor.helper.ProgressBarAnimation
import com.viral32111.servermonitor.helper.Settings
import com.viral32111.servermonitor.helper.TimeSpan
import com.viral32111.servermonitor.helper.createHTMLColoredText
import com.viral32111.servermonitor.helper.createProgressDialog
import com.viral32111.servermonitor.helper.setTextFromHTML
import com.viral32111.servermonitor.helper.showBriefMessage
import com.viral32111.servermonitor.helper.showConfirmDialog
import com.viral32111.servermonitor.helper.showInformationDialog
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class ServiceActivity : AppCompatActivity() {

	// UI
	private var materialToolbar: MaterialToolbar? = null
	private lateinit var swipeRefreshLayout: SwipeRefreshLayout
	private lateinit var refreshProgressBar: ProgressBar
	private lateinit var statusTextView: TextView
	private lateinit var actionStartStopButton: Button
	private lateinit var actionRestartButton: Button
	private lateinit var informationServiceNameTextView: TextView
	private lateinit var informationDisplayNameTextView: TextView
	private lateinit var informationDescriptionTextView: TextView
	private lateinit var informationRunLevelTextView: TextView
	private lateinit var logsStatusTextView: TextView
	private lateinit var logsRecyclerView: RecyclerView

	// Misc
	private lateinit var progressBarAnimation: ProgressBarAnimation
	private lateinit var settings: Settings

	// Data from previous activity
	private lateinit var serverIdentifier: String
	private lateinit var serviceName: String

	// Contact information
	private var contactName: String? = null
	private var contactMethods: Array<String>? = null

	// Runs when the activity is created...
	override fun onCreate( savedInstanceState: Bundle? ) {

		// Run default action & display the relevant layout file
		super.onCreate( savedInstanceState )
		setContentView(R.layout.activity_service)
		Log.d(Shared.logTag, "Creating activity..." )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView(R.layout.action_bar)
		Log.d(Shared.logTag, "Switched to Material Toolbar" )

		// Set the title on the toolbar
		materialToolbar = supportActionBar?.customView?.findViewById(R.id.actionBarMaterialToolbar)
		materialToolbar?.title = getString(R.string.serviceActionBarTitle)
		materialToolbar?.isTitleCentered = true
		Log.d(Shared.logTag, "Set Material Toolbar title to '${ materialToolbar?.title }' (${ materialToolbar?.isTitleCentered })" )

		// Enable the back button on the toolbar
		materialToolbar?.navigationIcon = AppCompatResources.getDrawable( this,
			R.drawable.arrow_back
		)
		materialToolbar?.setNavigationOnClickListener {
			Log.d(Shared.logTag, "Navigation back button pressed. Returning to previous activity..." )
			finish()
			overridePendingTransition(R.anim.slide_in_from_left, R.anim.slide_out_to_right)
		}

		// When an item on the action bar menu is pressed...
		materialToolbar?.setOnMenuItemClickListener { menuItem ->

			// Settings
			if ( menuItem.title?.equals( getString(R.string.actionBarMenuSettings) ) == true ) {
				Log.d(Shared.logTag, "Opening Settings activity..." )

				startActivity( Intent( this, SettingsActivity::class.java ) )
				overridePendingTransition(R.anim.slide_in_from_right, R.anim.slide_out_to_left)

			// Logout
			} else if ( menuItem.title?.equals( getString(R.string.actionBarMenuLogout) ) == true ) {
				Log.d(Shared.logTag, "Logout menu item pressed, showing confirmation..." )

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
			} else if ( menuItem.title?.equals( getString(R.string.actionBarMenuAbout) ) == true ) {
				Log.d(Shared.logTag, "Showing information about app dialog..." )

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
		swipeRefreshLayout = findViewById(R.id.serviceSwipeRefreshLayout)
		refreshProgressBar = findViewById(R.id.serviceRefreshProgressBar)
		statusTextView = findViewById(R.id.serviceStatusTextView)
		actionStartStopButton = findViewById(R.id.serviceActionStartStopButton)
		actionRestartButton = findViewById(R.id.serviceActionRestartButton)
		informationServiceNameTextView = findViewById(R.id.serviceInformationServiceNameTextView)
		informationDisplayNameTextView = findViewById(R.id.serviceInformationDisplayNameTextView)
		informationDescriptionTextView = findViewById(R.id.serviceInformationDescriptionTextView)
		informationRunLevelTextView = findViewById(R.id.serviceInformationRunLevelTextView)
		logsStatusTextView = findViewById(R.id.serviceLogsStatusTextView)
		logsRecyclerView = findViewById(R.id.serviceLogsRecyclerView)

		// Get the settings
		settings = Settings( getSharedPreferences( Shared.sharedPreferencesName, MODE_PRIVATE ) )
		Log.d(Shared.logTag, "Got settings ('${ settings.instanceUrl }', '${ settings.credentialsUsername }', '${ settings.credentialsPassword }')" )

		// Return to the setup activity if we aren't setup yet
		if ( !settings.isSetup() ) {
			Log.d(Shared.logTag, "Not setup yet, returning to Setup activity..." )
			startActivity( Intent( this, SetupActivity::class.java ) )
			overridePendingTransition(R.anim.slide_in_from_left, R.anim.slide_out_to_right)
			return
		}

		// Return to the previous activity if we were not given all the required data
		val serverIdentifier = intent.extras?.getString( "serverIdentifier" )
		val serviceName = intent.extras?.getString( "serviceName" )
		Log.d(Shared.logTag, "Server Identifier: '${ serverIdentifier }', Service Name: '${ serviceName }'" )
		if ( serverIdentifier.isNullOrBlank() || serviceName.isNullOrBlank() ) {
			Log.w(Shared.logTag, "No server identifier and/or service name passed to activity?! Returning to previous activity..." )
			finish()
			overridePendingTransition(R.anim.slide_in_from_left, R.anim.slide_out_to_right)
			return
		}
		this.serverIdentifier = serverIdentifier
		this.serviceName = serviceName

		// Set the title on the toolbar (will be overridden later when we have the display name for the service)
		materialToolbar?.title = serviceName.uppercase()
		Log.d(Shared.logTag, "Set Material Toolbar title to '${ serviceName.uppercase() }'" )

		// Create the animation for the automatic refresh countdown progress bar - https://stackoverflow.com/a/18015071
		progressBarAnimation = ProgressBarAnimation( refreshProgressBar, refreshProgressBar.progress.toFloat(), refreshProgressBar.max.toFloat() )
		progressBarAnimation.interpolator = LinearInterpolator() // We want linear, not accelerate-decelerate interpolation

		// Store this activity for later use in showing snackbar messages
		val activity = this

		// Event listeners for the automatic refresh countdown progress bar animation - https://medium.com/android-news/handsome-codes-with-kotlin-6e183db4c7e5
		progressBarAnimation.setAnimationListener( object : Animation.AnimationListener {

			// When the animation starts...
			override fun onAnimationStart( animation: Animation? ) {
				Log.d(Shared.logTag, "Automatic refresh countdown progress bar animation started" )
			}

			// When the animation finishes or is manually cleared...
			override fun onAnimationEnd( animation: Animation? ) {
				Log.d(Shared.logTag, "Automatic refresh countdown progress bar animation ended (${ animation?.hasEnded() }, ${ animation?.hasStarted() }, ${ refreshProgressBar.progress }, ${ refreshProgressBar.isAnimating })" )

				// Don't refresh if we've been manually cleared
				if ( refreshProgressBar.progress == 0 ) return

				// Show refreshing spinner
				swipeRefreshLayout.isRefreshing = true

				CoroutineScope( Dispatchers.Main ).launch {
					withContext( Dispatchers.IO ) {

						// Fetch the server
						try {
							val server = Server( API.getServer(
								settings.instanceUrl!!,
								settings.credentialsUsername!!,
								settings.credentialsPassword!!,
								serverIdentifier
							)!!, true )
							Log.d(Shared.logTag, "Fetched server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') from API" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

								// Get this service
								val service = server.findService( serviceName )
								if ( service != null ) {

									// Update the UI
									updateUI( service )

									// Start the progress bar animation
									if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )

								} else {
									Log.e(Shared.logTag, "Service '${ serviceName }' does not exist? Returning to previous activity..." )
									finish()
									overridePendingTransition(
										R.anim.slide_in_from_left,
										R.anim.slide_out_to_right
									)
								}
							}

						} catch ( exception: APIException) {
							Log.e(Shared.logTag, "Failed to fetch server '${ serverIdentifier }' from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

								when ( exception.volleyError ) {

									// Bad authentication
									is AuthFailureError -> when ( exception.apiErrorCode ) {
										ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serviceToastServerAuthenticationUnknownUser )
										ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serviceToastServerAuthenticationIncorrectPassword )
										else -> showBriefMessage( activity, R.string.serviceToastServerAuthenticationFailure )
									}

									// HTTP 4xx
									is ClientError -> when ( exception.httpStatusCode ) {
										404 -> showBriefMessage( activity, R.string.serviceToastServerNotFound )
										else -> showBriefMessage( activity, R.string.serviceToastServerClientFailure )
									}

									// HTTP 5xx
									is ServerError -> when ( exception.httpStatusCode ) {
										502 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable )
										503 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable )
										504 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable )
										530 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable ) // Cloudflare
										else -> showBriefMessage( activity, R.string.serviceToastServerServerFailure )
									}

									// No Internet connection, malformed domain
									is NoConnectionError -> showBriefMessage( activity, R.string.serviceToastServerNoConnection )
									is NetworkError -> showBriefMessage( activity, R.string.serviceToastServerNoConnection )

									// Connection timed out
									is TimeoutError -> showBriefMessage( activity, R.string.serviceToastServerTimeout )

									// ¯\_(ツ)_/¯
									else -> showBriefMessage( activity, R.string.serviceToastServerFailure )

								}
							}
						} catch ( exception: JsonParseException) {
							Log.e(Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serviceToastServerParseFailure )
							}
						} catch ( exception: JsonSyntaxException) {
							Log.e(Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serviceToastServerParseFailure )
							}
						} catch ( exception: NullPointerException ) {
							Log.e(Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serviceToastServerNull )
							}
						}

					}
				}
			}

			// When the animation repeats...
			override fun onAnimationRepeat( animation: Animation? ) {
				Log.d(Shared.logTag, "Automatic refresh countdown progress bar animation repeated" )
			}

		} )

		// When we're swiped down to refresh...
		swipeRefreshLayout.setOnRefreshListener {
			Log.d(Shared.logTag, "Swipe refreshed!" )

			// Stop the automatic refresh countdown progress bar, thus calling the animation callback
			if ( settings.automaticRefresh ) {
				refreshProgressBar.clearAnimation()

			// If automatic refresh is disabled, then do the refresh manually...
			} else {
				CoroutineScope( Dispatchers.Main ).launch {
					withContext( Dispatchers.IO ) {

						// Fetch the server
						try {
							val server = Server( API.getServer(
								settings.instanceUrl!!,
								settings.credentialsUsername!!,
								settings.credentialsPassword!!,
								serverIdentifier
							)!!, true )
							Log.d(Shared.logTag, "Fetched server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') from API" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

								// Get this service
								val service = server.findService( serviceName )
								if ( service != null ) {

									// Update the UI
									updateUI( service )

									// Start the progress bar animation
									if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )

								} else {
									Log.e(Shared.logTag, "Service '${ serviceName }' does not exist? Returning to previous activity..." )
									finish()
									overridePendingTransition(
										R.anim.slide_in_from_left,
										R.anim.slide_out_to_right
									)
								}
							}

						} catch ( exception: APIException) {
							Log.e(Shared.logTag, "Failed to fetch server '${ serverIdentifier }' from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

								when ( exception.volleyError ) {

									// Bad authentication
									is AuthFailureError -> when ( exception.apiErrorCode ) {
										ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serviceToastServerAuthenticationUnknownUser )
										ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serviceToastServerAuthenticationIncorrectPassword )
										else -> showBriefMessage( activity, R.string.serviceToastServerAuthenticationFailure )
									}

									// HTTP 4xx
									is ClientError -> when ( exception.httpStatusCode ) {
										404 -> showBriefMessage( activity, R.string.serviceToastServerNotFound )
										else -> showBriefMessage( activity, R.string.serviceToastServerClientFailure )
									}

									// HTTP 5xx
									is ServerError -> when ( exception.httpStatusCode ) {
										502 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable )
										503 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable )
										504 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable )
										530 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable ) // Cloudflare
										else -> showBriefMessage( activity, R.string.serviceToastServerServerFailure )
									}

									// No Internet connection, malformed domain
									is NoConnectionError -> showBriefMessage( activity, R.string.serviceToastServerNoConnection )
									is NetworkError -> showBriefMessage( activity, R.string.serviceToastServerNoConnection )

									// Connection timed out
									is TimeoutError -> showBriefMessage( activity, R.string.serviceToastServerTimeout )

									// ¯\_(ツ)_/¯
									else -> showBriefMessage( activity, R.string.serviceToastServerFailure )

								}
							}
						} catch ( exception: JsonParseException) {
							Log.e(Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serviceToastServerParseFailure )
							}
						} catch ( exception: JsonSyntaxException) {
							Log.e(Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serviceToastServerParseFailure )
							}
						} catch ( exception: NullPointerException ) {
							Log.e(Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
								showBriefMessage( activity, R.string.serviceToastServerNull )
							}
						}

					}
				}
			}
		}

		// When the start/stop action button is pressed...
		actionStartStopButton.setOnClickListener {
			if ( actionStartStopButton.text == getString( R.string.serviceButtonStartAction ) ) {
				Log.d( Shared.logTag, "Start service button pressed!" )
				executeServiceAction( activity, "start" )
			} else if ( actionStartStopButton.text == getString( R.string.serviceButtonStopAction ) ) {
				Log.d( Shared.logTag, "Stop service button pressed!" )
				executeServiceAction( activity, "stop" )
			}
		}

		// When the restart action button is pressed...
		actionRestartButton.setOnClickListener {
			Log.d( Shared.logTag, "Restart service button pressed!" )
			executeServiceAction( activity, "restart" )
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
			overridePendingTransition(R.anim.slide_in_from_left, R.anim.slide_out_to_right)
		}
	}

	// When the activity is closed...
	override fun onStop() {
		super.onStop()
		Log.d( Shared.logTag, "Stopped service activity" )

		// Remove all observers for the always on-going notification worker
		//WorkManager.getInstance( applicationContext ).getWorkInfosForUniqueWorkLiveData( UpdateWorker.NAME ).removeObservers( this )
		//Log.d( Shared.logTag, "Removed all observers for the always on-going notification worker" )

		// Cancel all pending HTTP requests
		//API.cancelQueue()
	}

	override fun onPause() {
		super.onPause()
		Log.d( Shared.logTag, "Paused service activity" )

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
		Log.d( Shared.logTag, "Resumed service activity" )

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
					val hello = API.getHello(
						settings.instanceUrl!!,
						settings.credentialsUsername!!,
						settings.credentialsPassword!!
					)!!

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
								530 -> showBriefMessage( activity, R.string.toastInstanceTestUnavailable ) // Cloudflare
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
						showBriefMessage( activity, R.string.serviceToastServerParseFailure )
					}
				} catch ( exception: JsonSyntaxException ) {
					Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						showBriefMessage( activity, R.string.serviceToastServerParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						showBriefMessage( activity, R.string.serviceToastServerNull )
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

						// Get this service
						val service = server.findService( serviceName )
						if ( service != null ) {
							Log.d( Shared.logTag, "Got service '${ service.serviceName }' ('${ service.displayName }', '${ service.description }')" )

							// Update the UI
							updateUI( service )

							// Start the progress bar animation
							if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )

						} else {
							Log.e( Shared.logTag, "Service '${ serviceName }' does not exist? Returning to previous activity..." )
							finish()
							overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
						}
					}

				} catch ( exception: APIException) {
					Log.e( Shared.logTag, "Failed to fetch server '${ serverIdentifier }' from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

						when ( exception.volleyError ) {

							// Bad authentication
							is AuthFailureError -> when ( exception.apiErrorCode ) {
								ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serviceToastServerAuthenticationUnknownUser )
								ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serviceToastServerAuthenticationIncorrectPassword )
								else -> showBriefMessage( activity, R.string.serviceToastServerAuthenticationFailure )
							}

							// HTTP 4xx
							is ClientError -> when ( exception.httpStatusCode ) {
								404 -> showBriefMessage( activity, R.string.serviceToastServerNotFound )
								else -> showBriefMessage( activity, R.string.serviceToastServerClientFailure )
							}

							// HTTP 5xx
							is ServerError -> when ( exception.httpStatusCode ) {
								502 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable )
								503 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable )
								504 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable )
								530 -> showBriefMessage( activity, R.string.serviceToastServerUnavailable ) // Cloudflare
								else -> showBriefMessage( activity, R.string.serviceToastServerServerFailure )
							}

							// No Internet connection, malformed domain
							is NoConnectionError -> showBriefMessage( activity, R.string.serviceToastServerNoConnection )
							is NetworkError -> showBriefMessage( activity, R.string.serviceToastServerNoConnection )

							// Connection timed out
							is TimeoutError -> showBriefMessage( activity, R.string.serviceToastServerTimeout )

							// ¯\_(ツ)_/¯
							else -> showBriefMessage( activity, R.string.serviceToastServerFailure )

						}
					}
				} catch ( exception: JsonParseException) {
					Log.e( Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

						showBriefMessage( activity, R.string.serviceToastServerParseFailure )
					}
				} catch ( exception: JsonSyntaxException) {
					Log.e( Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

						showBriefMessage( activity, R.string.serviceToastServerParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

						showBriefMessage( activity, R.string.serviceToastServerNull )
					}
				}

			}
		}
	}

	// Updates the UI with the given service
	private fun updateUI( service: Service) {

		// Set the title on the toolbar
		materialToolbar?.title = service.displayName.uppercase()
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ service.displayName.uppercase() }'" )

		// Update the action buttons
		if ( service.isRunning() ) {
			actionStartStopButton.text = getString(R.string.serviceButtonStopAction)
			actionStartStopButton.setBackgroundColor( getColor(R.color.stopActionButton) )
			actionRestartButton.isEnabled = service.isRestartActionSupported()
		} else {
			actionStartStopButton.text = getString(R.string.serviceButtonStartAction)
			actionStartStopButton.setBackgroundColor( getColor(R.color.startActionButton) )
			actionRestartButton.isEnabled = service.isRestartActionSupported()
		}
		actionStartStopButton.isEnabled = service.isStartActionSupported() || service.isStopActionSupported()

		// Get the status
		val statusText = service.getStatusText()
		val statusColor = service.getStatusColor( statusText )

		// Update the status
		val uptimeText = TimeSpan( service.uptimeSeconds.toLong() ).toString( true )
		statusTextView.setTextColor( getColor( R.color.black ) )
		statusTextView.setTextFromHTML( getString( R.string.serviceTextViewStatusGood ).format(
			createHTMLColoredText( statusText, statusColor ),
			createHTMLColoredText(
				uptimeText.ifBlank { getString(R.string.serverTextViewServicesServiceStatusUptimeUnknown) },
				if ( uptimeText.isNotBlank() ) R.color.black else R.color.statusDead
			)
		) )

		// Update the name, description & run level
		informationServiceNameTextView.setTextColor( getColor( R.color.black ) )
		informationServiceNameTextView.text = getString( R.string.serviceTextViewInformationServiceName ).format( service.serviceName )
		informationDisplayNameTextView.setTextColor( getColor( R.color.black ) )
		informationDisplayNameTextView.text = getString( R.string.serviceTextViewInformationDisplayName ).format( service.displayName )
		informationDescriptionTextView.setTextColor( getColor( R.color.black ) )
		informationDescriptionTextView.text = getString( R.string.serviceTextViewInformationDescription ).format( service.description )
		informationRunLevelTextView.setTextColor( getColor( R.color.black ) )
		informationRunLevelTextView.text = getString( R.string.serviceTextViewInformationRunLevel ).format( service.runLevel )

		// TODO: Update the logs

	}

	// Executes an action on the server
	private fun executeServiceAction( activity: Activity, actionName: String ) {
		CoroutineScope( Dispatchers.Main ).launch {

			// Show progress dialog
			val progressDialog = createProgressDialog( activity, R.string.serviceDialogProgressActionExecuteTitle, R.string.serviceDialogProgressActionExecuteMessage ) {
				API.cancelQueue()
				showBriefMessage( activity, R.string.serviceDialogProgressActionExecuteCancel )
			}
			progressDialog.show()

			withContext( Dispatchers.IO ) {

				// Try to execute the action
				try {
					val action = API.postService( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!!, serverIdentifier, serviceName, actionName )
					val exitCode = action?.get( "exitCode" )?.asInt
					var outputText = action?.get( "outputText" )?.asString?.trim()
					var errorText = action?.get( "errorText" )?.asString?.trim()

					if ( outputText.isNullOrBlank() ) outputText = "N/A"
					if ( errorText.isNullOrBlank() ) errorText = "N/A"

					Log.d(Shared.logTag, "Executed action '${ actionName }' for service '${ serviceName }' on server '${ serverIdentifier }': '${ outputText }', '${ errorText }' (Exit Code: '${ exitCode }')" )

					withContext( Dispatchers.Main ) {
						progressDialog.dismiss()

						if ( exitCode == 0 ) showInformationDialog( activity, R.string.serviceDialogActionExecuteTitle, getString( R.string.serviceDialogActionExecuteMessageSuccess ).format( outputText, errorText ) )
						else showInformationDialog( activity, R.string.serviceDialogActionExecuteTitle, getString( R.string.serviceDialogActionExecuteMessageFailure ).format( exitCode, errorText, outputText ) )
					}

				} catch ( exception: APIException ) {
					Log.e(Shared.logTag, "Failed to execute action '${ actionName }' on API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

					withContext( Dispatchers.Main ) {
						progressDialog.dismiss()

						when ( exception.volleyError ) {

							// Bad authentication
							is AuthFailureError -> when ( exception.apiErrorCode ) {
								ErrorCode.UnknownUser.code -> showBriefMessage( activity, R.string.serviceToastActionAuthenticationUnknownUser )
								ErrorCode.IncorrectPassword.code -> showBriefMessage( activity, R.string.serviceToastActionAuthenticationIncorrectPassword )
								else -> showBriefMessage( activity, R.string.serviceToastActionAuthenticationFailure )
							}

							// HTTP 4xx
							is ClientError -> when ( exception.apiErrorCode ) {
								ErrorCode.InvalidParameter.code -> showBriefMessage( activity, R.string.serviceToastActionInvalidParameter )
								ErrorCode.UnknownAction.code -> showBriefMessage( activity, R.string.serviceToastActionUnknownAction )
								ErrorCode.ActionNotExecutable.code -> showBriefMessage( activity, R.string.serviceToastActionActionNotExecutable )
								ErrorCode.ActionServerUnknown.code -> showBriefMessage( activity, R.string.serviceToastActionActionServerUnknown )
								else -> when ( exception.httpStatusCode ) {
									404 -> showBriefMessage( activity, R.string.serviceToastActionNotFound )
									else -> showBriefMessage( activity, R.string.serviceToastActionClientFailure )
								}
							}

							// HTTP 5xx
							is ServerError -> when ( exception.apiErrorCode ) {
								ErrorCode.ActionServerOffline.code -> showBriefMessage( activity, R.string.serviceToastActionOffline )
								else -> when ( exception.httpStatusCode ) {
									502 -> showBriefMessage( activity, R.string.serviceToastActionUnavailable )
									503 -> showBriefMessage( activity, R.string.serviceToastActionUnavailable )
									504 -> showBriefMessage( activity, R.string.serviceToastActionUnavailable )
									530 -> showBriefMessage( activity, R.string.serviceToastActionUnavailable ) // Cloudflare
									else -> showBriefMessage( activity, R.string.serviceToastActionServerFailure )
								}
							}

							// No Internet connection, malformed domain
							is NoConnectionError -> showBriefMessage( activity, R.string.serviceToastActionNoConnection )
							is NetworkError -> showBriefMessage( activity, R.string.serviceToastActionNoConnection )

							// Connection timed out
							is TimeoutError -> showBriefMessage( activity, R.string.serviceToastActionTimeout )

							// ¯\_(ツ)_/¯
							else -> showBriefMessage( activity, R.string.serviceToastActionFailure )

						}
					}
				} catch ( exception: JsonParseException ) {
					Log.e( Shared.logTag, "Failed to parse execute server action API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						progressDialog.dismiss()
						showBriefMessage( activity, R.string.serviceToastActionParseFailure )
					}
				} catch ( exception: JsonSyntaxException ) {
					Log.e( Shared.logTag, "Failed to parse execute server action API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						progressDialog.dismiss()
						showBriefMessage( activity, R.string.serviceToastActionParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in execute server action API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						progressDialog.dismiss()
						showBriefMessage( activity, R.string.serviceToastActionNull )
					}
				}

			}
		}
	}

}
