package com.viral32111.servermonitor

/**
 * Converts hertz to an appropriate notation (kHz, MHz, GHz, etc.), similar to the Size class.
 * @param hertz The number of hertz.
 * @property amount The hertz converted to its appropriate notation.
 * @property suffix The suffix for the appropriate notation.
 */
class Frequency( hertz: Long ) {

	// Use 1000 (SI), not 1024
	companion object {
		private const val KILOHERTZ = 1000.0;
		private const val MEGAHERTZ = KILOHERTZ * 1000.0;
		private const val GIGAHERTZ = MEGAHERTZ * 1000.0;
	}

	val amount: Double
	val suffix: String

	init {
		val kilohertz: Double = hertz / KILOHERTZ
		val megahertz: Double = hertz / MEGAHERTZ
		val gigahertz: Double = hertz / GIGAHERTZ

		if ( gigahertz >= 1.0 ) {
			amount = gigahertz
			suffix = "GHz"
		} else if ( megahertz >= 1.0 ) {
			amount = megahertz
			suffix = "MHz"
		} else if ( kilohertz >= 1.0 ) {
			amount = kilohertz
			suffix = "kHz"
		} else {
			amount = hertz.toDouble()
			suffix = "Hz"
		}
	}

}
