package com.viral32111.servermonitor

/**
 * Converts bytes to an appropriate size (KiB, MiB, GiB, etc.).
 * @param bytes The number of bytes.
 * @property amount The bytes converted to their appropriate size.
 * @property suffix The suffix for the appropriate size.
 */
class Size( bytes: Long ) {

	// Use 1024, not 1000
	companion object {
		private const val KIBIBYTE = 1024.0;
		private const val MEBIBYTE = KIBIBYTE * 1024.0;
		private const val GIBIBYTE = MEBIBYTE * 1024.0;
		private const val TEBIBYTE = GIBIBYTE * 1024.0;
	}

	val amount: Double
	val suffix: String

	// https://stackoverflow.com/a/13539881
	init {
		val kibibytes: Double = bytes / KIBIBYTE
		val mebibytes: Double = bytes / MEBIBYTE
		val gibibytes: Double = bytes / GIBIBYTE
		val tebibyte: Double = bytes / TEBIBYTE

		if ( tebibyte >= 1.0 ) {
			amount = tebibyte
			suffix = "TiB"
		} else if ( gibibytes >= 1.0 ) {
			amount = gibibytes
			suffix = "GiB"
		} else if ( mebibytes >= 1.0 ) {
			amount = mebibytes
			suffix = "MiB"
		} else if ( kibibytes >= 1.0 ) {
			amount = kibibytes
			suffix = "KiB"
		} else {
			amount = bytes.toDouble()
			suffix = "B"
		}
	}

}
