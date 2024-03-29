package com.viral32111.servermonitor.activity

import android.annotation.SuppressLint
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
import androidx.work.*
import com.android.volley.*
import com.google.android.material.appbar.MaterialToolbar
import com.google.gson.JsonParseException
import com.google.gson.JsonSyntaxException
import com.viral32111.servermonitor.*
import com.viral32111.servermonitor.R
import com.viral32111.servermonitor.adapter.ServerAdapter
import com.viral32111.servermonitor.data.Server
import com.viral32111.servermonitor.database.initialiseDatabase
import com.viral32111.servermonitor.helper.*
import kotlinx.coroutines.*
import java.time.ZoneOffset
import java.time.ZonedDateTime
import java.util.*

class ServersActivity : AppCompatActivity() {

	// UI
	private lateinit var swipeRefreshLayout: SwipeRefreshLayout
	private lateinit var refreshProgressBar: ProgressBar
	private lateinit var recyclerView: RecyclerView
	private lateinit var statusTitleTextView: TextView
	private lateinit var statusDescriptionTextView: TextView

	// Misc
	private lateinit var progressBarAnimation: ProgressBarAnimation
	private lateinit var settings: Settings
	private var hasAutomaticRefreshFailed = false

	// Contact information
	private var contactName: String? = null
	private var contactMethods: Array<String>? = null

	// Runs when the activity is created...
	@SuppressLint( "InflateParams" ) // We intend to pass null to our layout inflater
	override fun onCreate( savedInstanceState: Bundle? ) {

		// Run default action & display the relevant layout file
		super.onCreate( savedInstanceState )
		setContentView( R.layout.activity_servers )
		Log.d( Shared.logTag, "Creating activity..." )

		// Switch to the custom Material Toolbar
		supportActionBar?.displayOptions = ActionBar.DISPLAY_SHOW_CUSTOM
		supportActionBar?.setCustomView( R.layout.action_bar )
		Log.d( Shared.logTag, "Switched to Material Toolbar" )

		// Set the title on the toolbar
		val materialToolbar = supportActionBar?.customView?.findViewById<MaterialToolbar>( R.id.actionBarMaterialToolbar )
		materialToolbar?.title = getString( R.string.serversActionBarTitle )
		materialToolbar?.isTitleCentered = true
		Log.d( Shared.logTag, "Set Material Toolbar title to '${ materialToolbar?.title }' (${ materialToolbar?.isTitleCentered })" )

		// Get all the UI
		swipeRefreshLayout = findViewById( R.id.serversSwipeRefreshLayout )
		refreshProgressBar = findViewById( R.id.serversRefreshProgressBar )
		recyclerView = findViewById( R.id.serversRecyclerView )
		statusTitleTextView = findViewById( R.id.serversStatusTitleTextView )
		statusDescriptionTextView = findViewById( R.id.serversStatusDescriptionTextView )

		// Get the settings
		settings = Settings( getSharedPreferences( Shared.sharedPreferencesName, MODE_PRIVATE ) )
		Log.d( Shared.logTag, "Got settings ('${ settings.instanceUrl }', '${ settings.credentialsUsername }', '${ settings.credentialsPassword }')" )

		// Initialise our RESTful API class
		API.initializeQueue( applicationContext )

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

					// Stop all workers
					WorkManager.getInstance( applicationContext ).cancelAllWork()
					Log.d( Shared.logTag, "Cancelled all existing workers" )

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

		// Return to the setup activity if we aren't setup yet
		if ( !settings.isSetup() ) {
			Log.d( Shared.logTag, "Not setup yet, returning to Setup activity..." )
			startActivity( Intent( this, SetupActivity::class.java ) )
			overridePendingTransition( R.anim.slide_in_from_left, R.anim.slide_out_to_right )
			return
		}

		// Create a linear layout manager for the recycler view
		val linearLayoutManager = LinearLayoutManager( this, LinearLayoutManager.VERTICAL, false )
		recyclerView.layoutManager = linearLayoutManager

		// Set the divider between servers in the recycler view - https://stackoverflow.com/q/40528012
		val dividerItemDecoration = DividerItemDecoration( this, linearLayoutManager.orientation )
		dividerItemDecoration.setDrawable( ContextCompat.getDrawable( this, R.drawable.shape_server_divider )!! )
		recyclerView.addItemDecoration( dividerItemDecoration )

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

						// Fetch the servers
						try {
							val servers = fetchServers( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!! )

							// Fetch the number of issues today
							val database = initialiseDatabase( applicationContext )
							val issuesTodayCount = database.issueHistory().fetchAfterStartedAtDate( ZonedDateTime.now( ZoneOffset.UTC ).toLocalDate().atStartOfDay().toInstant( ZoneOffset.UTC ).toEpochMilli() ).count()
							Log.d( Shared.logTag, "There have been $issuesTodayCount issue(s) today" )

							// Update the UI
							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0

								hasAutomaticRefreshFailed = false

								updateUI( servers, issuesTodayCount )
							}

						} catch ( exception: APIException ) {
							Log.e( Shared.logTag, "Failed to fetch servers from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0

								hasAutomaticRefreshFailed = true

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
										530 -> showBriefMessage( activity, R.string.serversToastServersUnavailable ) // Cloudflare
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

								hasAutomaticRefreshFailed = true

								showBriefMessage( activity, R.string.serversToastServersParseFailure )
							}
						} catch ( exception: JsonSyntaxException ) {
							Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0

								hasAutomaticRefreshFailed = true

								showBriefMessage( activity, R.string.serversToastServersParseFailure )
							}
						} catch ( exception: NullPointerException ) {
							Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0

								hasAutomaticRefreshFailed = true

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
			if ( settings.automaticRefresh && !hasAutomaticRefreshFailed ) {
				refreshProgressBar.clearAnimation()

			// If automatic refresh is disabled or failed, then do the refresh manually...
			} else {
				CoroutineScope( Dispatchers.Main ).launch {
					withContext( Dispatchers.IO ) {

						// Fetch the servers
						try {
							val servers = fetchServers( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!! )

							// Fetch the number of issues today
							val database = initialiseDatabase( applicationContext )
							val issuesTodayCount = database.issueHistory().fetchAfterStartedAtDate( ZonedDateTime.now( ZoneOffset.UTC ).toLocalDate().atStartOfDay().toInstant( ZoneOffset.UTC ).toEpochMilli() ).count()
							Log.d( Shared.logTag, "There have been $issuesTodayCount issue(s) today" )

							// Update the UI
							withContext( Dispatchers.Main ) {
								swipeRefreshLayout.isRefreshing = false
								refreshProgressBar.progress = 0
								updateUI( servers, issuesTodayCount )
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
										530 -> showBriefMessage( activity, R.string.serversToastServersUnavailable ) // Cloudflare
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

		// Register the observer for the always on-going notification worker
		//UpdateWorker.observe( this, this )
		//Log.d( Shared.logTag, "Registered observer for always on-going notification worker" )

	}

	// When the activity is closed...
	override fun onStop() {
		super.onStop()
		Log.d( Shared.logTag, "Stopped servers activity" )

		// Cancel all pending HTTP requests
		//API.cancelQueue()

		// Remove all observers for the always on-going notification worker
		//WorkManager.getInstance( applicationContext ).getWorkInfosForUniqueWorkLiveData( UpdateWorker.NAME ).removeObservers( this )
		//Log.d( Shared.logTag, "Removed all observers for the always on-going notification worker" )
	}

	// Stop the automatic refresh countdown progress bar when the activity is changed/app is minimised
	override fun onPause() {
		super.onPause()
		Log.d( Shared.logTag, "Paused servers activity" )

		// Stop any current refreshing
		swipeRefreshLayout.isRefreshing = false

		// Stop the automatic refresh countdown progress bar
		if ( settings.automaticRefresh ) {
			refreshProgressBar.progress = 0 // Reset progress to prevent automatic refresh
			refreshProgressBar.clearAnimation() // Will call the animation end callback
		}
	}

	// When the activity is opened/app is brought to foreground...
	override fun onResume() {
		super.onResume()
		Log.d( Shared.logTag, "Resumed servers activity" )

		// Reload settings in case they have changed
		settings.read()
		Log.d( Shared.logTag, "Reloaded settings (Automatic Refresh: '${ settings.automaticRefresh }', Automatic Refresh Interval: '${ settings.automaticRefreshInterval }')" )

		// Re-setup the worker as the automatic refresh interval may have changed
		val baseUrl = settings.instanceUrl
		val credentialsUsername = settings.credentialsUsername
		val credentialsPassword = settings.credentialsPassword
		if ( !baseUrl.isNullOrBlank() && !credentialsUsername.isNullOrBlank() && !credentialsPassword.isNullOrBlank() ) {
			UpdateWorker.setup( applicationContext, this, baseUrl, credentialsUsername, credentialsPassword, settings.automaticRefreshInterval, settings.notificationWhenIssueArises, shouldEnqueue = settings.notificationAlwaysOngoing )
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

				} catch ( exception: APIException ) {
					Log.e( Shared.logTag, "Failed to fetch contact information from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

					withContext( Dispatchers.Main ) {
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
						showBriefMessage( activity, R.string.serversToastServersParseFailure )
					}
				} catch ( exception: JsonSyntaxException ) {
					Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						showBriefMessage( activity, R.string.serversToastServersParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						showBriefMessage( activity, R.string.serversToastServersNull )
					}
				}

				/******************************************************************************/

				// Fetch the servers
				try {
					val servers = fetchServers( settings.instanceUrl!!, settings.credentialsUsername!!, settings.credentialsPassword!! )

					// Fetch the number of issues today
					val database = initialiseDatabase( applicationContext )
					val issuesTodayCount = database.issueHistory().fetchAfterStartedAtDate( ZonedDateTime.now( ZoneOffset.UTC ).toLocalDate().atStartOfDay().toInstant( ZoneOffset.UTC ).toEpochMilli() ).count()
					Log.d( Shared.logTag, "There have been $issuesTodayCount issue(s) today" )

					// Update the UI
					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

						updateUI( servers, issuesTodayCount )
					}

				} catch ( exception: APIException ) {
					Log.e( Shared.logTag, "Failed to fetch servers from API due to '${ exception.message }' (Volley Error: '${ exception.volleyError }', HTTP Status Code: '${ exception.httpStatusCode }', API Error Code: '${ exception.apiErrorCode }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0

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
								530 -> showBriefMessage( activity, R.string.serversToastServersUnavailable ) // Cloudflare
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
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						showBriefMessage( activity, R.string.serversToastServersParseFailure )
					}
				} catch ( exception: JsonSyntaxException ) {
					Log.e( Shared.logTag, "Failed to parse fetch servers API response as JSON due to '${ exception.message }'" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						showBriefMessage( activity, R.string.serversToastServersParseFailure )
					}
				} catch ( exception: NullPointerException ) {
					Log.e( Shared.logTag, "Encountered null property value in fetch servers API response ('${ exception.message }')" )

					withContext( Dispatchers.Main ) {
						swipeRefreshLayout.isRefreshing = false
						if ( settings.automaticRefresh ) refreshProgressBar.progress = 0
						showBriefMessage( activity, R.string.serversToastServersNull )
					}
				}

			}
		}

	}

	// Update the UI with the given servers
	private fun updateUI( servers: Array<Server>, issuesToday: Int? ) {

		// Set the overall status based on if there are there issues with any of the servers, if there are servers available to scrape...
		if ( servers.isNotEmpty() ) {
			if ( servers.any { server -> server.areThereIssues() } ) {
				statusTitleTextView.text = getString( R.string.serversTextViewStatusTitleBad )
				statusTitleTextView.setTextColor( getColor( R.color.statusBad ) )
				statusDescriptionTextView.text = getString( R.string.serversTextViewStatusDescriptionBad ).format( issuesToday?.coerceAtLeast( 1 ) ?: 1 )
			} else {
				statusTitleTextView.text = getString( R.string.serversTextViewStatusTitleGood )
				statusTitleTextView.setTextColor( getColor( R.color.statusGood ) )
				statusDescriptionTextView.text = getString( R.string.serversTextViewStatusDescriptionGood )
			}
		} else {
			statusTitleTextView.text = getString( R.string.serversTextViewStatusTitleDead )
			statusTitleTextView.setTextColor( getColor( R.color.statusDead ) )
			statusDescriptionTextView.text = getString( R.string.serversTextViewStatusDescriptionEmpty )
		}

		// Create the adapter for the recycler view - https://www.geeksforgeeks.org/android-pull-to-refresh-with-recyclerview-in-kotlin/
		val serverAdapter = ServerAdapter( servers, applicationContext ) { server -> onServerPressed( server ) }
		recyclerView.adapter = serverAdapter

		// Update the recycler view, stop loading & restart automatic refresh countdown progress bar animation
		serverAdapter.notifyItemRangeChanged( 0, servers.size ) // IDE doesn't like .notifyDataSetChanged()
		swipeRefreshLayout.isRefreshing = false
		if ( settings.automaticRefresh ) refreshProgressBar.startAnimation( progressBarAnimation )

	}

	// Switch to the server activity when an online server is pressed...
	private fun onServerPressed( server: Server ) {
		if ( server.isOnline() ) {
			Log.d( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') is online, switching to server activity..." )

			val intent = Intent( this, ServerActivity::class.java )
			intent.putExtra( "serverIdentifier", server.identifier )
			intent.putExtra( "fromServersActivity", true )

			startActivity( intent )
			overridePendingTransition( R.anim.slide_in_from_right, R.anim.slide_out_to_left )
		} else {
			Log.w( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') is offline, not switching to server activity" )
			showBriefMessage( this, R.string.serversToastOfflineServerPress )
		}
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
		Log.d( Shared.logTag, "Fetched '${ servers.size() }' servers from API ('${ servers }')" )

		// Convert the JSON array to a list of servers - https://www.geeksforgeeks.org/kotlin-list-arraylist/
		val serverList = ArrayList<Server>()
		for ( arrayItem in servers ) {
			val server = Server( arrayItem.asJsonObject )

			if ( server.isOnline() ) {
				Log.d( Shared.logTag, "Server '${ server.hostName }' ('${ server.identifier }', '${ server.jobName }', '${ server.instanceAddress }') is online, fetching metrics..." )

				server.updateFromAPI( instanceUrl, credentialsUsername, credentialsPassword )
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
