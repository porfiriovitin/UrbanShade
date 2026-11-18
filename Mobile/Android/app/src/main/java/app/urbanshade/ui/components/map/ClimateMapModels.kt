package app.urbanshade.ui.components.map

import androidx.annotation.StringRes
import androidx.compose.runtime.Immutable
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import app.urbanshade.R

/** RGB bands for an environmental layer; opacity is controlled separately. */
@Immutable
internal data class HeatmapPalette(val bands: List<Int>) {
    constructor(cold: Int, middle: Int, hot: Int) : this(listOf(cold, middle, hot))
    val cold: Int get() = bands.first()
    val middle: Int get() = bands[bands.size / 2]
    val hot: Int get() = bands.last()

    fun copy(cold: Int = this.cold, middle: Int = this.middle, hot: Int = this.hot): HeatmapPalette {
        if (bands.size == 3) return HeatmapPalette(cold, middle, hot)
        val mutable = bands.toMutableList()
        mutable[0] = cold
        mutable[mutable.size / 2] = middle
        mutable[mutable.size - 1] = hot
        return HeatmapPalette(mutable)
    }
}

/** Environmental layers supported by the map. */
internal enum class EnvironmentalLayer(
    @get:StringRes val labelResId: Int,
    @get:StringRes val shortLabelResId: Int,
    @get:StringRes val contentDescriptionResId: Int,
    val valueMin: Float,
    val valueMax: Float,
    val unit: String
) {
    Temperature(
        labelResId = R.string.climate_map_layer_temperature,
        shortLabelResId = R.string.climate_map_layer_temp_short,
        contentDescriptionResId = R.string.climate_map_layer_temp_desc,
        valueMin = 10f,
        valueMax = 40f,
        unit = "°C"
    ),
    TreeCover(
        labelResId = R.string.climate_map_layer_tree_cover,
        shortLabelResId = R.string.climate_map_layer_arb_short,
        contentDescriptionResId = R.string.climate_map_layer_arb_desc,
        valueMin = 0f,
        valueMax = 100f,
        unit = "%"
    ),
    Humidity(
        labelResId = R.string.climate_map_layer_humidity,
        shortLabelResId = R.string.climate_map_layer_umi_short,
        contentDescriptionResId = R.string.climate_map_layer_umi_desc,
        valueMin = 0f,
        valueMax = 100f,
        unit = "%"
    )
}

/** Bounds of a north-up, linearly projected (EPSG:4326) local image. */
@Immutable
internal data class MapBounds(
    val latitudeMin: Double,
    val latitudeMax: Double,
    val longitudeMin: Double,
    val longitudeMax: Double,
    val aspectRatio: Float = 1f
) {
    init {
        require(latitudeMin.isFinite() && latitudeMax.isFinite())
        require(longitudeMin.isFinite() && longitudeMax.isFinite())
        require(latitudeMin >= -90 && latitudeMax <= 90 && latitudeMin < latitudeMax)
        require(longitudeMin >= -180 && longitudeMax <= 180 && longitudeMin < longitudeMax)
        require(aspectRatio.isFinite() && aspectRatio > 0)
    }

    // Preserve out-of-bounds coordinates so external sensors aren't pinned to image edges.
    fun normalized(latitude: Double, longitude: Double): Offset = Offset(
        x = ((longitude - longitudeMin) / (longitudeMax - longitudeMin)).toFloat(),
        y = ((latitudeMax - latitude) / (latitudeMax - latitudeMin)).toFloat()
    )

    fun coordinates(x: Float, y: Float): Pair<Double, Double> =
        latitudeMax - y * (latitudeMax - latitudeMin) to
            longitudeMin + x * (longitudeMax - longitudeMin)
}

@Immutable
internal data class ClimatePoint(
    val id: String,
    val name: String,
    val latitude: Double,
    val longitude: Double,
    val temperatureCelsius: Float,
    val treeCoverPercent: Float = 0f,
    val relativeHumidityPercent: Float = 0f
) {
    init {
        require(latitude.isFinite() && latitude in -90.0..90.0)
        require(longitude.isFinite() && longitude in -180.0..180.0)
        require(temperatureCelsius.isFinite())
        require(treeCoverPercent.isFinite() && treeCoverPercent in 0f..100f)
        require(relativeHumidityPercent.isFinite() && relativeHumidityPercent in 0f..100f)
    }

    fun valueFor(layer: EnvironmentalLayer): Float = when (layer) {
        EnvironmentalLayer.Temperature -> temperatureCelsius
        EnvironmentalLayer.TreeCover -> treeCoverPercent
        EnvironmentalLayer.Humidity -> relativeHumidityPercent
    }
}

@Immutable
internal data class MapCoordinate(
    val latitude: Double,
    val longitude: Double
) {
    init {
        require(latitude.isFinite() && latitude in -90.0..90.0)
        require(longitude.isFinite() && longitude in -180.0..180.0)
    }
}

@Immutable
internal data class MapMarker(
    val id: String,
    val coordinate: MapCoordinate,
    val title: String,
    val isOrigin: Boolean = false
)

@Immutable
internal data class MapRoute(
    val id: String,
    val name: String,
    val durationMinutes: Int,
    val distanceMeters: Int,
    val description: String,
    val points: List<MapCoordinate>
) {
    val durationAndDistanceText: String
        get() = "$durationMinutes min · $distanceMeters m"
}

@Immutable
internal data class MapViewportPadding(
    val top: Dp = 24.dp,
    val bottom: Dp = 24.dp,
    val start: Dp = 24.dp,
    val end: Dp = 24.dp
)

@Immutable
internal data class MapFitRequest(
    val id: String,
    val coordinates: List<MapCoordinate>,
    val padding: MapViewportPadding = MapViewportPadding()
)

