package app.urbanshade.ui.components.map

import android.graphics.Bitmap
import kotlin.math.roundToInt
import kotlin.math.sqrt

internal object HeatmapGenerator {
    const val DefaultGridWidth = 72
    const val DefaultTemperatureMin = 10f
    const val DefaultTemperatureMax = 40f
    // ~80% opacity for vibrant layer colors
    const val DefaultAlpha = 204
    private const val DistanceEpsilonSquared = 1e-10

    /** Classifies a value into one of [numBands] equal intervals between [valueMin] and [valueMax]. */
    fun classifyBand(value: Float, valueMin: Float, valueMax: Float, numBands: Int = 5): Int {
        require(value.isFinite() && valueMin.isFinite() && valueMax.isFinite())
        require(valueMax > valueMin && (valueMax - valueMin).isFinite())
        require(numBands > 0)
        val fraction = ((value - valueMin) / (valueMax - valueMin)).coerceIn(0f, 1f)
        val index = (fraction * numBands).toInt()
        return index.coerceAtMost(numBands - 1)
    }

    /** Maps a numeric reading to its band color with light edge smoothing between bands. */
    fun bandColor(
        value: Float,
        palette: HeatmapPalette,
        valueMin: Float = DefaultTemperatureMin,
        valueMax: Float = DefaultTemperatureMax,
        alpha: Int = DefaultAlpha
    ): Int {
        require(value.isFinite() && valueMin.isFinite() && valueMax.isFinite())
        require(valueMax > valueMin && (valueMax - valueMin).isFinite())
        require(alpha in 0..255)
        val fraction = ((value - valueMin) / (valueMax - valueMin)).coerceIn(0f, 1f)
        val bands = palette.bands
        val numBands = bands.size
        if (numBands == 0) return 0
        if (numBands == 1) return withAlpha(bands[0], alpha)

        val scaled = fraction * numBands
        val bandIndex = scaled.toInt().coerceIn(0, numBands - 1)
        val baseColor = bands[bandIndex]

        val smoothDelta = 0.025f * numBands
        val upperBoundaryDistance = scaled - (bandIndex + 1)
        val lowerBoundaryDistance = scaled - bandIndex

        val finalColor = when {
            upperBoundaryDistance > -smoothDelta && bandIndex < numBands - 1 -> {
                val t = (upperBoundaryDistance + smoothDelta) / (2 * smoothDelta)
                val progress = t * t * (3 - 2 * t)
                interpolateRgb(baseColor, bands[bandIndex + 1], progress)
            }
            lowerBoundaryDistance < smoothDelta && bandIndex > 0 -> {
                val t = (lowerBoundaryDistance + smoothDelta) / (2 * smoothDelta)
                val progress = t * t * (3 - 2 * t)
                interpolateRgb(bands[bandIndex - 1], baseColor, progress)
            }
            else -> baseColor
        }
        return withAlpha(finalColor, alpha)
    }

    /** Compatibility helper for thermal color queries. */
    fun temperatureColor(
        temperature: Float,
        palette: HeatmapPalette,
        temperatureMin: Float = DefaultTemperatureMin,
        temperatureMax: Float = DefaultTemperatureMax,
        alpha: Int = DefaultAlpha
    ): Int = bandColor(temperature, palette, temperatureMin, temperatureMax, alpha)

    fun generateBitmap(
        points: List<ClimatePoint>,
        bounds: MapBounds,
        palette: HeatmapPalette,
        gridWidth: Int = DefaultGridWidth,
        gridHeight: Int = (gridWidth / bounds.aspectRatio).roundToInt().coerceAtLeast(1),
        valueMin: Float = DefaultTemperatureMin,
        valueMax: Float = DefaultTemperatureMax,
        radiusDegrees: Double? = null,
        alpha: Int = DefaultAlpha,
        valueSelector: (ClimatePoint) -> Float = { it.temperatureCelsius },
        temperatureMin: Float = valueMin,
        temperatureMax: Float = valueMax
    ): Bitmap = Bitmap.createBitmap(
        generatePixels(
            points, bounds, palette, gridWidth, gridHeight,
            valueMin, valueMax, radiusDegrees, alpha, valueSelector,
            temperatureMin, temperatureMax
        ),
        gridWidth,
        gridHeight,
        Bitmap.Config.ARGB_8888
    )

    /** IDW (power 2), classified into five bands. Empty inputs produce a transparent layer. */
    fun generatePixels(
        points: List<ClimatePoint>,
        bounds: MapBounds,
        palette: HeatmapPalette,
        gridWidth: Int = DefaultGridWidth,
        gridHeight: Int = (gridWidth / bounds.aspectRatio).roundToInt().coerceAtLeast(1),
        valueMin: Float = DefaultTemperatureMin,
        valueMax: Float = DefaultTemperatureMax,
        radiusDegrees: Double? = null,
        alpha: Int = DefaultAlpha,
        valueSelector: (ClimatePoint) -> Float = { it.temperatureCelsius },
        temperatureMin: Float = valueMin,
        temperatureMax: Float = valueMax
    ): IntArray {
        val effectiveMin = if (temperatureMin != valueMin) temperatureMin else valueMin
        val effectiveMax = if (temperatureMax != valueMax) temperatureMax else valueMax
        require(gridWidth > 0 && gridHeight > 0 && gridWidth.toLong() * gridHeight <= Int.MAX_VALUE)
        require(effectiveMin.isFinite() && effectiveMax.isFinite())
        require(effectiveMax > effectiveMin && (effectiveMax - effectiveMin).isFinite())
        require(radiusDegrees == null || (radiusDegrees.isFinite() && radiusDegrees > 0))
        require(alpha in 0..255)
        val pixels = IntArray(gridWidth * gridHeight)
        if (points.isEmpty()) return pixels

        for (y in 0 until gridHeight) {
            val latitude = bounds.latitudeMax - (y + 0.5) / gridHeight * (bounds.latitudeMax - bounds.latitudeMin)
            for (x in 0 until gridWidth) {
                val longitude = bounds.longitudeMin + (x + 0.5) / gridWidth * (bounds.longitudeMax - bounds.longitudeMin)
                val value = readingAt(points, latitude, longitude, valueSelector)
                val pixelAlpha = if (radiusDegrees == null) alpha else {
                    val nearestDistanceSquared = points.minOf {
                        val dy = latitude - it.latitude
                        val dx = longitude - it.longitude
                        dx * dx + dy * dy
                    }
                    (alpha * (1 - sqrt(nearestDistanceSquared) / radiusDegrees).coerceIn(0.0, 1.0)).roundToInt()
                }
                pixels[y * gridWidth + x] = if (pixelAlpha == 0) 0 else {
                    bandColor(value, palette, effectiveMin, effectiveMax, pixelAlpha)
                }
            }
        }
        return pixels
    }

    internal fun readingAt(
        points: List<ClimatePoint>,
        latitude: Double,
        longitude: Double,
        valueSelector: (ClimatePoint) -> Float
    ): Float {
        require(points.isNotEmpty())
        var weighted = 0.0
        var totalWeight = 0.0
        for (point in points) {
            val dy = latitude - point.latitude
            val dx = longitude - point.longitude
            val distance = dx * dx + dy * dy
            if (distance < DistanceEpsilonSquared) return valueSelector(point)
            val weight = 1.0 / distance
            weighted += weight * valueSelector(point)
            totalWeight += weight
        }
        return (weighted / totalWeight).toFloat()
    }

    private fun withAlpha(color: Int, alpha: Int): Int = (alpha shl 24) or (color and 0x00FFFFFF)

    private fun interpolateRgb(from: Int, to: Int, progress: Float): Int {
        fun channel(shift: Int): Int {
            val f = (from ushr shift) and 0xFF
            val t = (to ushr shift) and 0xFF
            return (f + (t - f) * progress).roundToInt().coerceIn(0, 255)
        }
        return (channel(16) shl 16) or (channel(8) shl 8) or channel(0)
    }
}
