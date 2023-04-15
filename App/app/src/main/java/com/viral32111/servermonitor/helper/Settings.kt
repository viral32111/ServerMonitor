package com.viral32111.servermonitor.helper

import android.content.SharedPreferences
import android.util.Log
import com.viral32111.servermonitor.Shared

// Manages custom settings - https://developer.android.com/training/data-storage/shared-preferences
class Settings( private val sharedPreferences: SharedPreferences ) {

	// No defaults for these, they must be configured on setup
	var instanceUrl: String? = null
	var credentialsUsername: String? = null
	var credentialsPassword: String? = null

	// Defaults are the values
	var automaticRefresh: Boolean = true
	var automaticRefreshInterval: Int = 15 // Seconds
	var theme: String = "Light" // TODO: Set this to 0 for system theme, once dark theme is implemented
	var notificationAlwaysOngoing: Boolean = true
	var notificationWhenIssueArises: Boolean = true

	// Read values from persistent settings on initialisation
	init {
		read()
	}

	// Checks if we are setup yet
	fun isSetup(): Boolean {
		return !instanceUrl.isNullOrBlank() && !credentialsUsername.isNullOrBlank() || !credentialsPassword.isNullOrBlank()
	}

	// Save the values to shared preferences - https://developer.android.com/training/data-storage/shared-preferences#WriteSharedPreference
	fun save() {
		with ( sharedPreferences.edit() ) {
			putString( "instanceUrl", instanceUrl )
			putString( "credentialsUsername", credentialsUsername )
			putString( "credentialsPassword", credentialsPassword )
			putBoolean( "automaticRefresh", automaticRefresh )
			putInt( "automaticRefreshInterval", automaticRefreshInterval )
			putString( "theme", theme )
			putBoolean( "notificationAlwaysOngoing", notificationAlwaysOngoing )
			putBoolean( "notificationWhenIssueArises", notificationWhenIssueArises )
			apply()
		}

		Log.d(Shared.logTag, "Saved settings to shared preferences (URL: '${ instanceUrl }', Username: '${ credentialsUsername }', Password: '${ credentialsPassword }')" )
	}

	// Read the values from shared preferences, fallback to defaults - https://developer.android.com/training/data-storage/shared-preferences#ReadSharedPreference
	fun read() {
		instanceUrl = sharedPreferences.getString( "instanceUrl", null )
		credentialsUsername = sharedPreferences.getString( "credentialsUsername", null )
		credentialsPassword = sharedPreferences.getString( "credentialsPassword", null )

		automaticRefresh = sharedPreferences.getBoolean( "automaticRefresh", automaticRefresh )
		automaticRefreshInterval = sharedPreferences.getInt( "automaticRefreshInterval", automaticRefreshInterval )
		theme = sharedPreferences.getString( "theme", theme )!!
		notificationAlwaysOngoing = sharedPreferences.getBoolean( "notificationAlwaysOngoing", notificationAlwaysOngoing )
		notificationWhenIssueArises = sharedPreferences.getBoolean( "notificationWhenIssueArises", notificationWhenIssueArises )
	}
}
