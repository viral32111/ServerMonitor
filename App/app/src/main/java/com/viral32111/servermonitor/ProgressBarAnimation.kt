package com.viral32111.servermonitor

import android.view.animation.Animation
import android.view.animation.Transformation
import android.widget.ProgressBar

// https://stackoverflow.com/a/18015071

class ProgressBarAnimation(
	private val progressBar: ProgressBar,
	private val from: Float,
	private val to: Float
): Animation() {
	override fun applyTransformation( interpolatedTime: Float, transformation: Transformation? ) {
		super.applyTransformation( interpolatedTime, transformation )
		progressBar.progress = ( from + ( to - from ) * interpolatedTime ).toInt()
	}
}
