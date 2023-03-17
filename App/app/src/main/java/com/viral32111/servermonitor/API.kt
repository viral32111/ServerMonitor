package com.viral32111.servermonitor

import android.content.Context
import android.util.Log
import com.android.volley.*
import com.android.volley.toolbox.JsonObjectRequest
import com.android.volley.toolbox.Volley
import com.google.gson.JsonObject
import com.google.gson.JsonParseException
import com.google.gson.JsonParser
import com.google.gson.JsonSyntaxException
import java.nio.charset.Charset
import java.util.*

class API {

	// Define static properties & methods - https://medium.com/swlh/kotlin-basics-of-companion-objects-a8422c96779b
	companion object {

		// HTTP request queue
		private lateinit var requestQueue: RequestQueue

		// Initialise the HTTP request queue - https://google.github.io/volley/simple.html#use-newrequestqueue
		fun initializeQueue( context: Context ) {
			requestQueue = Volley.newRequestQueue( context )
			Log.d( Shared.logTag, "Initialised HTTP request queue" )
		}

		// Cancels all HTTP requests in the queue - https://google.github.io/volley/simple.html#cancel-a-request
		fun cancelQueue() {
			Log.d( Shared.logTag, "Cancelling all HTTP requests in the queue..." )
			requestQueue.cancelAll( Shared.httpRequestQueueTag )
		}

		// Encode credentials as Base64 for use in HTTP Basic Authorization - https://developer.mozilla.org/en-US/docs/Web/HTTP/Authentication, https://developer.android.com/reference/kotlin/java/util/Base64
		private fun encodeCredentials( username: String, password: String ): String {
			return Base64.getEncoder().encodeToString( "${ username }:${ password }".toByteArray() )
		}

		// Sends a HTTP request
		private fun sendRequest( method: Int, url: String, username: String, password: String, successCallback: ( data: JsonObject? ) -> Unit, errorCallback: ( error: VolleyError, statusCode: Int?, errorCode: Int? ) -> Unit ) {

			// Create the request to the given URL
			val httpRequest = object: JsonObjectRequest( method, url, null, { _payload ->

				// Attempt to check the custom error code
				try {
					val payload = JsonParser.parseString( _payload.toString() ).asJsonObject // Convert Java JSON to Google GSON

					val errorCode = payload.get( "errorCode" )?.asInt
					if ( errorCode == ErrorCode.Success.code ) {
						successCallback.invoke( payload.get( "data" )?.asJsonObject )
					} else {
						errorCallback.invoke( VolleyError( "Received non-success code '${ errorCode }' from server" ), null, errorCode )
					}
				} catch ( exception: JsonParseException ) {
					errorCallback.invoke( VolleyError( exception.message, ParseError( exception ) ), null, null )
				} catch ( exception: JsonSyntaxException ) {
					errorCallback.invoke( VolleyError( exception.message, ParseError( exception ) ), null, null )
				}

			}, { error ->

				// Get useful response data to pass to our callback
				val statusCode = error.networkResponse?.statusCode
				val body = error.networkResponse?.data?.toString( Charset.defaultCharset() )

				// Attempt to pass our custom error code to the callback
				if ( body != null ) {
					try {
						errorCallback.invoke( error, statusCode, JsonParser.parseString( body )?.asJsonObject?.get( "errorCode" )?.asInt )
					} catch ( exception: JsonParseException ) {
						errorCallback.invoke( error, statusCode, null )
					} catch ( exception: JsonSyntaxException ) {
						errorCallback.invoke( error, statusCode, null )
					}
				} else {
					errorCallback.invoke( error, statusCode, null )
				}

			} ) {
				// Override the request headers - https://stackoverflow.com/a/53141982
				override fun getHeaders(): MutableMap<String, String> {
					return hashMapOf(
						"Accept" to "application/json, */*", // Expect a JSON response
						"Authorization" to "Basic ${ encodeCredentials( username, password ) }" // Authentication
					)
				}
			}

			// Disable automatic retrying on failure
			httpRequest.retryPolicy = DefaultRetryPolicy( DefaultRetryPolicy.DEFAULT_TIMEOUT_MS, 0, DefaultRetryPolicy.DEFAULT_BACKOFF_MULT )

			// Send the request
			httpRequest.tag = Shared.httpRequestQueueTag
			requestQueue.add( httpRequest )
			Log.d( Shared.logTag, "Sending HTTP request to URL '${ url }' (Method: '${ requestMethodToName( method ) }', Username: '${ username }', Password: '${ password }')..." )

		}

		// Convenience methods for each routes
		fun getHello( baseUrl: String, username: String, password: String, successCallback: ( data: JsonObject? ) -> Unit, errorCallback: ( error: VolleyError, statusCode: Int?, errorCode: Int? ) -> Unit ) {
			sendRequest( Request.Method.GET, "${ baseUrl }/hello", username, password, successCallback, errorCallback )
		}
		fun getServers( baseUrl: String, username: String, password: String, successCallback: ( data: JsonObject? ) -> Unit, errorCallback: ( error: VolleyError, statusCode: Int?, errorCode: Int? ) -> Unit ) {
			sendRequest( Request.Method.GET, "${ baseUrl }/servers", username, password, successCallback, errorCallback )
		}

		// TODO: Update these to take necessary parameters
		fun getServer( baseUrl: String, username: String, password: String, successCallback: ( data: JsonObject? ) -> Unit, errorCallback: ( error: VolleyError, statusCode: Int?, errorCode: Int? ) -> Unit ) {
			sendRequest( Request.Method.GET, "${ baseUrl }/server", username, password, successCallback, errorCallback )
		}
		fun postServer( baseUrl: String, username: String, password: String, successCallback: ( data: JsonObject? ) -> Unit, errorCallback: ( error: VolleyError, statusCode: Int?, errorCode: Int? ) -> Unit ) {
			sendRequest( Request.Method.POST, "${ baseUrl }/server", username, password, successCallback, errorCallback )
		}
		fun postService( baseUrl: String, username: String, password: String, successCallback: ( data: JsonObject? ) -> Unit, errorCallback: ( error: VolleyError, statusCode: Int?, errorCode: Int? ) -> Unit ) {
			sendRequest( Request.Method.POST, "${ baseUrl }/service", username, password, successCallback, errorCallback )
		}

	}

}
