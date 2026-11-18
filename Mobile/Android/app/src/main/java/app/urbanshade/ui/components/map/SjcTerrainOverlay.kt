package app.urbanshade.ui.components.map

import android.content.res.Resources
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import app.urbanshade.R
import kotlin.math.floor
import kotlin.math.roundToInt
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive

internal data class TerrainTreatment(
    val locationId: String? = null,
    val treeCoverGain: Float = 0f,
    val coolingCelsius: Float = 0f
) {
    init {
        require(treeCoverGain.isFinite() && treeCoverGain in 0f..100f)
        require(coolingCelsius.isFinite() && coolingCelsius >= 0f)
    }
}

/** Terrain masks calibrated to the bundled satellite, independent of climate readings. */
internal object SjcTerrainOverlay {
    private const val Size = 1024
    private data class Masks(
        val canopy: FloatArray,
        val urban: FloatArray,
        val planting: Map<String, FloatArray>
    )
    @Volatile private var cachedMasks: Masks? = null

    suspend fun generate(
        resources: Resources,
        bounds: MapBounds,
        points: List<ClimatePoint>,
        layer: EnvironmentalLayer,
        valueMin: Float,
        valueMax: Float,
        treatment: TerrainTreatment
    ): Bitmap {
        require(points.isNotEmpty() && valueMax > valueMin)
        val masks = masks(resources)
        val planting = masks.planting[treatment.locationId]
        val gain = (treatment.treeCoverGain / 16f).coerceIn(0f, 1f)
        val cooling = (treatment.coolingCelsius / 5f).coerceIn(0f, 0.8f)
        // Interpolate the existing readings on a coarse grid; terrain retains canopy detail.
        val gridSize = 72
        val field = FloatArray(gridSize * gridSize) { i ->
            val x = (i % gridSize).toDouble() / (gridSize - 1)
            val y = (i / gridSize).toDouble() / (gridSize - 1)
            HeatmapGenerator.readingAt(
                points,
                bounds.latitudeMax - y * (bounds.latitudeMax - bounds.latitudeMin),
                bounds.longitudeMin + x * (bounds.longitudeMax - bounds.longitudeMin)
            ) { it.valueFor(layer) }
        }
        val planted = planting?.let { blur(it, 22) }
        val pixels = IntArray(Size * Size)
        for (y in 0 until Size) {
            currentCoroutineContext().ensureActive()
            for (x in 0 until Size) {
                val i = y * Size + x
                val reading = sample(field, gridSize, x, y)
                val level = ((reading - valueMin) / (valueMax - valueMin)).coerceIn(0f, 1f)
                val canopy = masks.canopy[i]
                if (layer == EnvironmentalLayer.TreeCover) {
                    val existing = canopy * (0.72f + 0.28f * level)
                    val added = (planting?.get(i) ?: 0f) * gain
                    val coverage = maxOf(existing, added).coerceIn(0f, 1f)
                    pixels[i] = color(0xFF58B879.toInt(), 0xFF168049.toInt(), coverage, 0.60f * coverage)
                } else {
                    val localCooling = 1f - ((planted?.get(i) ?: 0f) * 6f).coerceIn(0f, 1f) * cooling
                    val heat = smooth(0.30f, 0.98f, level) * masks.urban[i] *
                        (1f - 0.96f * canopy) * localCooling
                    pixels[i] = if (heat < 0.005f) 0 else {
                        val tone = level * (0.70f + 0.30f * masks.urban[i])
                        val warm = color(0xFFF2BE60.toInt(), 0xFFEC8A42.toInt(), (tone - 0.40f) / 0.35f, 1f)
                        color(warm, 0xFFD95342.toInt(), (tone - 0.75f) / 0.25f, 0.75f * heat)
                    }
                }
            }
        }
        return Bitmap.createBitmap(pixels, Size, Size, Bitmap.Config.ARGB_8888)
    }

    private fun masks(resources: Resources): Masks = cachedMasks ?: synchronized(this) {
        cachedMasks ?: buildMasks(resources).also { cachedMasks = it }
    }

    private fun buildMasks(resources: Resources): Masks {
        val source = requireNotNull(BitmapFactory.decodeResource(resources, R.drawable.map_satellite_sjc))
        val scaled = Bitmap.createScaledBitmap(source, Size, Size, true)
        val pixels = IntArray(Size * Size)
        scaled.getPixels(pixels, 0, Size, 0, 0, Size, Size)
        if (scaled !== source) scaled.recycle()
        source.recycle()
        val canopy = FloatArray(pixels.size)
        val urban = FloatArray(pixels.size)
        val plantingAllowed = FloatArray(pixels.size)
        val hsv = FloatArray(3)
        for (y in 0 until Size) {
            for (x in 0 until Size) {
                val i = y * Size + x
                val pixel = pixels[i]
                Color.colorToHSV(pixel, hsv)
                val r = Color.red(pixel).toFloat()
                val g = Color.green(pixel).toFloat()
                val b = Color.blue(pixel).toFloat()
                val nx = x.toFloat() / Size
                val ny = y.toFloat() / Size
                // Open eastern fields and the construction site are not urban heat islands.
                val builtArea = nx < urbanEdge(ny) && !(nx < 0.16f && ny < 0.24f)
                val water = nx in 0.808f..0.875f && ny in 0.59f..0.64f
                val pitchX = (nx - 0.428f) / 0.022f
                val pitchY = (ny - 0.431f) / 0.018f
                val sportsPitch = pitchX * pitchX + pitchY * pitchY < 1f
                val green = smooth(1f, 13f, g - r) * smooth(3f, 20f, g - b) *
                    smooth(0.13f, 0.30f, hsv[1]) * (1f - smooth(0.62f, 0.85f, hsv[2]))
                canopy[i] = if (water || sportsPitch || hsv[0] !in 65f..170f) 0f else green
                val roof = hsv[2] > 0.38f && (hsv[1] < 0.28f || hsv[0] in 8f..45f)
                urban[i] = if (builtArea && roof && !water) 1f - canopy[i] else 0f
                // Planting paths are traced along verges; exclude bright/terracotta roofs and water.
                plantingAllowed[i] = if (!water && hsv[2] in 0.12f..0.64f &&
                    !(hsv[0] in 8f..45f && hsv[1] > 0.27f)) 1f else 0f
            }
        }
        val canopySoft = blur(canopy, 1)
        val density = blur(blur(urban, 14), 14)
        for (i in density.indices) density[i] = smooth(0.22f, 0.80f, density[i])
        // shortcut: fixed planting corridors for this offline image; retrace when its geography changes.
        val corridors = mapOf(
            "jardim" to listOf(
                floatArrayOf(0.248f, 0.624f, 0.247f, 0.708f, 0.239f, 0.785f, 0.240f, 0.820f),
                floatArrayOf(0.264f, 0.817f, 0.370f, 0.820f, 0.472f, 0.831f, 0.580f, 0.838f),
                floatArrayOf(0.692f, 0.716f, 0.706f, 0.769f, 0.720f, 0.816f)
            ),
            "confianca" to listOf(
                floatArrayOf(0.168f, 0.343f, 0.184f, 0.377f, 0.194f, 0.412f),
                floatArrayOf(0.226f, 0.432f, 0.284f, 0.441f, 0.321f, 0.454f),
                floatArrayOf(0.759f, 0.460f, 0.773f, 0.496f, 0.781f, 0.533f)
            )
        ).mapValues { (_, paths) ->
            val bitmap = Bitmap.createBitmap(Size, Size, Bitmap.Config.ARGB_8888)
            val canvas = Canvas(bitmap)
            val paint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
                color = Color.WHITE
                style = Paint.Style.STROKE
                strokeWidth = 7f
                strokeCap = Paint.Cap.ROUND
                strokeJoin = Paint.Join.ROUND
            }
            paths.forEach { coordinates ->
                val path = Path().apply {
                    moveTo(coordinates[0] * Size, coordinates[1] * Size)
                    for (i in 2 until coordinates.size step 2) lineTo(coordinates[i] * Size, coordinates[i + 1] * Size)
                }
                canvas.drawPath(path, paint)
            }
            bitmap.getPixels(pixels, 0, Size, 0, 0, Size, Size)
            bitmap.recycle()
            val mask = FloatArray(pixels.size) { Color.alpha(pixels[it]) / 255f * plantingAllowed[it] }
            blur(mask, 1)
        }
        return Masks(canopySoft, density, corridors)
    }

    private fun urbanEdge(y: Float): Float = when {
        y < 0.22f -> 0.62f
        y < 0.46f -> 0.62f + (y - 0.22f) * 0.70f
        y < 0.80f -> 0.79f
        else -> 0.79f - (y - 0.80f) * 0.45f
    }

    /** Two sliding-window passes, linear in image size rather than blur radius. */
    private fun blur(input: FloatArray, radius: Int): FloatArray {
        val horizontal = FloatArray(input.size)
        val output = FloatArray(input.size)
        val diameter = 2 * radius + 1
        for (y in 0 until Size) {
            var sum = 0f
            val row = y * Size
            for (dx in -radius..radius) sum += input[row + dx.coerceIn(0, Size - 1)]
            for (x in 0 until Size) {
                horizontal[row + x] = sum / diameter
                sum += input[row + (x + radius + 1).coerceAtMost(Size - 1)] - input[row + (x - radius).coerceAtLeast(0)]
            }
        }
        for (x in 0 until Size) {
            var sum = 0f
            for (dy in -radius..radius) sum += horizontal[dy.coerceIn(0, Size - 1) * Size + x]
            for (y in 0 until Size) {
                output[y * Size + x] = (sum / diameter).coerceIn(0f, 1f)
                sum += horizontal[(y + radius + 1).coerceAtMost(Size - 1) * Size + x] -
                    horizontal[(y - radius).coerceAtLeast(0) * Size + x]
            }
        }
        return output
    }

    private fun sample(field: FloatArray, width: Int, x: Int, y: Int): Float {
        val gx = x.toFloat() / (Size - 1) * (width - 1)
        val gy = y.toFloat() / (Size - 1) * (width - 1)
        val x0 = floor(gx).toInt()
        val y0 = floor(gy).toInt()
        val x1 = (x0 + 1).coerceAtMost(width - 1)
        val y1 = (y0 + 1).coerceAtMost(width - 1)
        val top = field[y0 * width + x0] * (1 - gx + x0) + field[y0 * width + x1] * (gx - x0)
        val bottom = field[y1 * width + x0] * (1 - gx + x0) + field[y1 * width + x1] * (gx - x0)
        return top * (1 - gy + y0) + bottom * (gy - y0)
    }

    private fun smooth(low: Float, high: Float, value: Float): Float {
        val t = ((value - low) / (high - low)).coerceIn(0f, 1f)
        return t * t * (3 - 2 * t)
    }

    private fun color(from: Int, to: Int, progress: Float, opacity: Float): Int {
        val t = progress.coerceIn(0f, 1f)
        fun channel(shift: Int): Int {
            val a = (from ushr shift) and 255
            val b = (to ushr shift) and 255
            return (a + (b - a) * t).roundToInt()
        }
        return Color.argb((255 * opacity.coerceIn(0f, 1f)).roundToInt(), channel(16), channel(8), channel(0))
    }
}
