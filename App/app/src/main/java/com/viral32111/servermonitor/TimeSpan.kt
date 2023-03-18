package com.viral32111.servermonitor

// Converts seconds into days, hours, minutes & seconds.
class TimeSpan( totalSeconds: Long ) {
	private val days: Long
	private val hours: Long
	private val minutes: Long
	private val seconds: Long

	// https://www.geeksforgeeks.org/converting-seconds-into-days-hours-minutes-and-seconds/
	init {
		var remainder = totalSeconds

		days = remainder / 86400
		remainder %= 86400

		hours = remainder / 3600
		remainder %= 3600

		minutes = remainder / 60
		remainder %= 60

		seconds = remainder
	}

	// Creates a human-readable string
	fun toString( includeSeconds: Boolean ): String {
		val parts = ArrayList<String>()

		if ( days > 0L ) parts.add( "$days day${ if ( days != 1L ) "s" else "" }" )
		if ( hours > 0L ) parts.add( "$hours hour${ if ( hours != 1L ) "s" else "" }" )
		if ( minutes > 0L ) parts.add( "$minutes minute${ if ( minutes != 1L ) "s" else "" }" )
		if ( includeSeconds && seconds > 0L ) parts.add( "$seconds second${ if ( seconds != 1L ) "s" else "" }" )

		return parts.joinToString( ", " )
	}
}
