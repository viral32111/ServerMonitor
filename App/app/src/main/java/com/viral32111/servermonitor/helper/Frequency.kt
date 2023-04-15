package com.viral32111.servermonitor.helper

/**
 * Converts hertz to an appropriate notation (kHz, MHz, GHz, etc.), similar to the Size class.
 * @param hertz The number of hertz.
 * @property amount The hertz converted to its appropriate notation.
 * @property suffix The suffix for the appropriate notation.
 */
class Frequency( hertz: Int ) {

	// Use 1000 (SI), not 1024
	companion object {
		private const val KILOHERTZ = 1000.0f
		private const val MEGAHERTZ = KILOHERTZ * 1000.0f
		private const val GIGAHERTZ = MEGAHERTZ * 1000.0f
	}

	val amount: Float
	val suffix: String

	init {
		val kilohertz: Float = hertz / KILOHERTZ
		val megahertz: Float = hertz / MEGAHERTZ
		val gigahertz: Float = hertz / GIGAHERTZ

		if ( hertz < 0 ) {
			amount = 0.0f
			suffix = "Hz"

		} else if ( gigahertz >= 1.0f ) {
			amount = gigahertz
			suffix = "GHz"
		} else if ( megahertz >= 1.0f ) {
			amount = megahertz
			suffix = "MHz"
		} else if ( kilohertz >= 1.0f ) {
			amount = kilohertz
			suffix = "kHz"
		} else {
			amount = hertz.toFloat() // Also pointless
			suffix = "Hz"
		}
	}

}
