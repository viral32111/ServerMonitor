package com.viral32111.servermonitor

import android.content.Context
import android.util.Log
import com.android.volley.*
import com.android.volley.toolbox.JsonObjectRequest
import com.android.volley.toolbox.StringRequest
import com.android.volley.toolbox.Volley
import com.google.gson.JsonArray
import com.google.gson.JsonObject
import com.google.gson.JsonParseException
import com.google.gson.JsonParser
import com.google.gson.JsonSyntaxException
import com.viral32111.servermonitor.data.Server
import java.nio.charset.Charset
import java.util.*
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException
import kotlin.coroutines.suspendCoroutine

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

		// Sends a HTTP request (Callback)
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
						Log.e( Shared.logTag, "JSON response payload for API error code code '${ errorCode }': '${ payload }'" )
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
				Log.e( Shared.logTag, "HTTP response body for status code '${ statusCode }': '${ body }'" )

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

		/**
		 * Sends an authenticated HTTP request that expects a JSON response (coroutine style)
		 * @param method The HTTP method.
		 * @param url The target URL, using the HTTPS schema.
		 * @param username The user to authenticate as.
		 * @param password The password to authenticate with.
		 * @return The API response as a JSON object.
		 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
		 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
		 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
		 */
		private suspend fun sendRequest( method: Int, url: String, username: String, password: String ): JsonObject? = suspendCoroutine { continuation ->

			// Create the request to the given URL
			val httpRequest = object: StringRequest( method, url, { responseBody ->

				// Attempt to parse as JSON
				try {
					val payload = JsonParser.parseString( responseBody ).asJsonObject

					// Check the custom error code
					val errorCode = payload.get( "errorCode" )?.asInt
					if ( errorCode == ErrorCode.Success.code ) {
						continuation.resume( payload.get( "data" )?.asJsonObject )
					} else {
						Log.e( Shared.logTag, "JSON response payload for API error code code '${ errorCode }': '${ payload }'" )
						continuation.resumeWithException( APIException( "Unexpected response code '${ errorCode }' for '${ url }'", null, null, errorCode ) )
					}

				} catch ( exception: JsonParseException ) {
					continuation.resumeWithException( exception )
				} catch ( exception: JsonSyntaxException ) {
					continuation.resumeWithException( exception )
				}

			}, { error ->

				// Get useful response data to pass to our callback
				val statusCode = error.networkResponse?.statusCode
				val body = error.networkResponse?.data?.toString( Charset.defaultCharset() )
				Log.e( Shared.logTag, "HTTP response body for status code '${ statusCode }': '${ body }'" )

				// Attempt to pass our custom error code to the callback, if the JSON parse fails then just resume with the original exception and a null custom error code
				if ( body != null ) {
					try {
						continuation.resumeWithException( APIException( error.message, error, statusCode, JsonParser.parseString( body )?.asJsonObject?.get( "errorCode" )?.asInt ) )
					} catch ( exception: JsonParseException ) {
						continuation.resumeWithException( APIException( error.message, error, statusCode, null ) )
					} catch ( exception: JsonSyntaxException ) {
						continuation.resumeWithException( APIException( error.message, error, statusCode, null ) )
					}
				} else {
					continuation.resumeWithException( APIException( error.message, error, statusCode, null ) )
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

		// TODO: Update anything calling these to use the suspendables instead
		fun getHello( baseUrl: String, username: String, password: String, successCallback: ( data: JsonObject? ) -> Unit, errorCallback: ( error: VolleyError, statusCode: Int?, errorCode: Int? ) -> Unit ) = sendRequest( Request.Method.GET, "${ baseUrl }/hello", username, password, successCallback, errorCallback )
		fun getServers( baseUrl: String, username: String, password: String, successCallback: ( data: JsonObject? ) -> Unit, errorCallback: ( error: VolleyError, statusCode: Int?, errorCode: Int? ) -> Unit ) = sendRequest( Request.Method.GET, "${ baseUrl }/servers", username, password, successCallback, errorCallback )

		/**
		 * Fetches information about an instance (`GET /hello`).
		 * @param baseUrl The base URL of the API, using the HTTPS schema.
		 * @param username The user to authenticate as.
		 * @param password The password to authenticate with.
		 * @return A JSON object containing the instance information.
		 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
		 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
		 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
		 */
		suspend fun getHello( baseUrl: String, username: String, password: String ) = sendRequest( Request.Method.GET, "${ baseUrl }/hello", username, password )

		/**
		 * Fetches basic information for every server (`GET /servers`).
		 * @param baseUrl The base URL of the API, using the HTTPS schema.
		 * @param username The user to authenticate as.
		 * @param password The password to authenticate with.
		 * @return A JSON array of JSON objects representing a server.
		 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
		 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
		 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
		 */
		suspend fun getServers( baseUrl: String, username: String, password: String ): JsonArray? = sendRequest( Request.Method.GET, "${ baseUrl }/servers", username, password )?.get( "servers" )?.asJsonArray
		suspend fun getServersImproved( baseUrl: String, username: String, password: String ): Array<Server>? = sendRequest( Request.Method.GET, "${ baseUrl }/servers", username, password )
			?.get( "servers" )?.asJsonArray
			?.filter { jsonElement -> jsonElement.isJsonObject }
			?.map { jsonElement -> Server( jsonElement.asJsonObject ) } // .apply { if ( isOnline() ) updateFromAPI( baseUrl, username, password ) }
			?.toTypedArray()

		/**
		 * Fetches all data for a single server (`GET /server`)
		 * @param baseUrl The base URL of the API, using the HTTPS schema.
		 * @param username The user to authenticate as.
		 * @param password The password to authenticate with.
		 * @param serverIdentifier The Base64-encoded unique identifier of a server.
		 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
		 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
		 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
		 */
		suspend fun getServer( baseUrl: String, username: String, password: String, serverIdentifier: String ) = sendRequest( Request.Method.GET, "${ baseUrl }/server?id=${ serverIdentifier }", username, password )

		/**
		 * Sends an action to a server (`POST /server`).
		 * @param baseUrl The base URL of the API, using the HTTPS schema.
		 * @param username The user to authenticate as.
		 * @param password The password to authenticate with.
		 * @param serverIdentifier The Base64-encoded unique identifier of a server.
		 * @param actionName The name of the action to execute (e.g., shutdown, reboot).
		 * @return A JSON object containing the instance information.
		 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
		 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
		 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
		 */
		suspend fun postServer( baseUrl: String, username: String, password: String, serverIdentifier: String, actionName: String ) = sendRequest( Request.Method.POST, "${ baseUrl }/server?id=${ serverIdentifier }&action=${ actionName }", username, password )

		/**
		 * Sends an action to a service on a server (`POST /service`).
		 * @param baseUrl The base URL of the API, using the HTTPS schema.
		 * @param username The user to authenticate as.
		 * @param password The password to authenticate with.
		 * @param serverIdentifier The Base64-encoded unique identifier of a server.
		 * @param serviceName The name of the service to target (e.g., sshd).
		 * @param actionName The name of the action to execute (e.g., shutdown, reboot).
		 * @return A JSON object containing the instance information.
		 * @throws APIException Any sort of error, such as non-success HTTP status code, network connectivity, etc.
		 * @throws JsonParseException An error parsing the HTTP response body as JSON, when successful.
		 * @throws JsonSyntaxException An error parsing the HTTP response body as JSON, when successful.
		 */
		suspend fun postService( baseUrl: String, username: String, password: String, serverIdentifier: String, serviceName: String, actionName: String ) = sendRequest( Request.Method.POST, "${ baseUrl }/service?id=${ serverIdentifier }&name=${ serviceName }&action=${ actionName }", username, password )

	}

}

// Custom exception type for coroutines
class APIException(
	override val message: String?,
	val volleyError: VolleyError?,
	val httpStatusCode: Int?,
	val apiErrorCode: Int?
): Exception( message )
