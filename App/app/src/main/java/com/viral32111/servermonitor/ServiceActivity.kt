package com.viral32111.servermonitor

import android.content.Context
import android.content.Intent
import android.os.Bundle
import android.util.Log
import android.view.View
import android.view.animation.Animation
import android.view.animation.LinearInterpolator
import android.widget.ProgressBar
import androidx.activity.OnBackPressedCallback
import androidx.appcompat.app.ActionBar
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.content.res.AppCompatResources
import androidx.swiperefreshlayout.widget.SwipeRefreshLayout
import com.android.volley.*
import com.google.android.material.appbar.MaterialToolbar
import com.google.gson.JsonParseException
import com.google.gson.JsonSyntaxException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class ServiceActivity : AppCompatActivity() {

	// UI
	private var materialToolbar: MaterialToolbar? = null
	private lateinit var swipeRefreshLayout: SwipeRefreshLayout
	private lateinit var refreshProgressBar: ProgressBar

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
		setContentView( R.layout.activity_service )
		Log.d( Shared.logTag, "Creating activity..." )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar )
		Log.d( Shared.logTag, "Switched to Material Toolbar" )

		// Set the title on the toolbar
		materialToolbar = supportActionBar?.customView?.findViewById( R.id.actionBarMaterialToolbar )
		materialToolbar?.title = getString( R.string.serviceActionBarTitle )
		materialToolbar?.isTitleCentered = true
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ materialToolbar?.title }' (${ materialToolbar?.isTitleCentered })" )

		// Enable the back button on the toolbar
		materialToolbar?.navigationIcon = AppCompatResources.getDrawable( this, R.drawable.ic_baseline_arrow_back_24 )
		materialToolbar?.setNavigationOnClickListener {
			Log.d( Shared.logTag, "Navigation back button pressed. Returning to previous activity..." )
			finish()
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
		}

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
		swipeRefreshLayout = findViewById( R.id.serviceSwipeRefreshLayout )
		refreshProgressBar = findViewById( R.id.serviceRefreshProgressBar )

		// Get the settings
		settings = Settings( getSharedPreferences( Shared.sharedPreferencesName, Context.MODE_PRIVATE ) )
		Log.d( Shared.logTag, "Got settings ('${ settings.instanceUrl }', '${ settings.credentialsUsername }', '${ settings.credentialsPassword }')" )

		// Return to the setup activity if we aren't setup yet
		if ( !settings.isSetup() ) {
			Log.d( Shared.logTag, "Not setup yet, returning to Setup activity..." )
			startActivity( Intent( this, SetupActivity::class.java ) )
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
			return
		}

		// Return to the previous activity if we were not given all the required data
		val serverIdentifier = intent.extras?.getString( "serverIdentifier" )
		val serviceName = intent.extras?.getString( "serviceName" )
		Log.d( Shared.logTag, "Server Identifier: '${ serverIdentifier }', Service Name: '${ serviceName }'" )
		if ( serverIdentifier.isNullOrBlank() || serviceName.isNullOrBlank() ) {
			Log.w( Shared.logTag, "No server identifier and/or service name passed to activity?! Returning to previous activity..." )
			finish()
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
			return
		}
		this.serverIdentifier = serverIdentifier
		this.serviceName = serviceName

		// Set the title on the toolbar (will be overridden later when we have the display name for the service)
		materialToolbar?.title = serviceName.uppercase()
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ serviceName.uppercase() }'" )

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

				CoroutineScope( Dispatchers.Main ).launch {
					withContext( Dispatchers.IO ) {

						// Fetch the server
						try {
							val server = Server( API.getServer( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!!, serverIdentifier )!!, true )
							Log.d( Shared.logTag, "Fetched server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') from API" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

								// Get this service
								val service = server.services?.find { service -> service.serviceName == serviceName }
								if ( service != null ) {

									// Update the UI & enable user input
									updateUI( service )
									enableInputs( true )

									// Start the progress bar animation
									if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )

								} else {
									Log.e( Shared.logTag, "Service '${ serviceName }' does not exist? Returning to previous activity..." )
									finish()
									overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
								}
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
								enableInputs( true )
								showBriefMessage( activity, R.string.serviceToastServerParseFailure )
							}
						} catch ( exception: JsonSyntaxException) {
							Log.e( Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
								enableInputs( true )
								showBriefMessage( activity, R.string.serviceToastServerParseFailure )
							}
						} catch ( exception: NullPointerException ) {
							Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
								enableInputs( true )
								showBriefMessage( activity, R.string.serviceToastServerNull )
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
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

								// Get this service
								val service = server.services?.find { service -> service.serviceName == serviceName }
								if ( service != null ) {

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

						} catch ( exception: APIException ) {
							Log.e( Shared.logTag, "Failed to fetch server '${ serverIdentifier }' from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
								enableInputs( true )

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
		Log.d( Shared.logTag, "Stopped service activity" )

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
						enableInputs( true )
						showBriefMessage( activity, R.string.serviceToastServerParseFailure )
					}
				} catch ( exception: JsonSyntaxException ) {
					Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						enableInputs( true )
						showBriefMessage( activity, R.string.serviceToastServerParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						enableInputs( true )
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
						val service = server.services?.find { service -> service.serviceName == serviceName }
						if ( service != null ) {
							Log.d( Shared.logTag, "Got service '${ service.serviceName }' ('${ service.displayName }', '${ service.description }')" )

							// Update the UI & enable user input
							updateUI( service )
							enableInputs( true )

							// Start the progress bar animation
							if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )

						} else {
							Log.e( Shared.logTag, "Service '${ serviceName }' does not exist? Returning to previous activity..." )
							finish()
							overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
						}
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
						enableInputs( true )
						showBriefMessage( activity, R.string.serviceToastServerParseFailure )
					}
				} catch ( exception: JsonSyntaxException) {
					Log.e( Shared.logTag, "Failed to parse fetch server API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						enableInputs( true )
						showBriefMessage( activity, R.string.serviceToastServerParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						enableInputs( true )
						showBriefMessage( activity, R.string.serviceToastServerNull )
					}
				}

			}
		}
	}

	// Updates the UI with the given service
	private fun updateUI( service: Service ) {

		// Set the title on the toolbar
		materialToolbar?.title = service.displayName.uppercase()
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ service.displayName.uppercase() }'" )

		// TODO

	}

	// Enables/disables user input
	private fun enableInputs( shouldEnable: Boolean ) {
		// TODO
	}

}
