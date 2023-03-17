package com.viral32111.servermonitor

import java.math.BigInteger

// Converts seconds into days, hours, minutes & seconds.
class TimeSpan( totalSeconds: Long ) {
	val Days: Long;
	val Hours: Long;
	val Minutes: Long;
	val Seconds: Long;

	init {
		var remainder = totalSeconds

		Days = remainder / 86400
		remainder %= 86400

		Hours = remainder / 3600
		remainder %= 3600

		Minutes = remainder / 60
		remainder %= 60

		Seconds = remainder
	}

	fun toString( includeSeconds: Boolean ): String {
		val parts = ArrayList<String>()

		if ( Days > 0L ) parts.add( "${ Days } day${ if ( Days != 1L ) "s" else "" }" )
		if ( Hours > 0L ) parts.add( "${ Hours } day${ if ( Days != 1L ) "s" else "" }" )
		if ( Minutes > 0L ) parts.add( "${ Minutes } minute${ if ( Minutes != 1L ) "s" else "" }" )
		if ( includeSeconds && Seconds > 0L ) parts.add( "${ Seconds } second${ if ( Seconds != 1L ) "s" else "" }" )

		return parts.joinToString( ", " )
	}
}
