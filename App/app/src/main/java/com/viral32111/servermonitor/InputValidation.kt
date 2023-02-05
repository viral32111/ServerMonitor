package com.viral32111.servermonitor

import java.net.MalformedURLException
import java.net.URL

// Checks if a URL is valid by parsing it using the URL class, and checking if it is secure
fun validateInstanceUrl( instanceUrl: String ): Boolean {
	return try {
		URL( instanceUrl ).protocol == "https"
	} catch ( exception: MalformedURLException ) {
		false
	}
}

// Checks if a username is valid using regular expressions by requiring alphanumeric characters & to be between 3 and 30 characters
fun validateCredentialsUsername( username: String ): Boolean {
	return Regex( "^[A-Za-z0-9_]{3,30}$" ).matches( username )
}

// Checks if a password is valid using regular expressions by requiring 1 uppercase letter, 1 lowercase letter, 1 symbol, 1 number and minimum length of 8 characters
// Modified version of https://stackoverflow.com/a/5142164
fun validateCredentialsPassword( password: String ): Boolean {
	return Regex( "^(?=.*[A-Z])(?=.*[!\"£\$%^&*(_+-={}\\[\\];'#:@~,./<>?|`¬)])(?=.*[0-9])(?=.*[a-z]).{8,}\$" ).matches( password )
}
