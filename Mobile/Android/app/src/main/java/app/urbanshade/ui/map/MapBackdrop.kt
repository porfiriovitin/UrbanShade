package app.urbanshade.ui.map

import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import app.urbanshade.R
import app.urbanshade.ui.components.map.ClimateMap
import app.urbanshade.ui.components.map.EnvironmentalLayer
import app.urbanshade.ui.components.map.MapFitRequest
import app.urbanshade.ui.components.map.MapMarker
import app.urbanshade.ui.components.map.MapRoute
import app.urbanshade.ui.components.map.TerrainTreatment

/**
 * Shared map backdrop for both Admin and User profiles.
 * Employs the georeferenced satellite image, matching bounds, and selected environmental layer.
 * When [layer] is null, the heatmap is disabled and only the pure satellite image is rendered.
 * Optionally renders walking routes, origin/destination markers, and handles bounding box framing.
 */
@Composable
internal fun MapBackdrop(
    modifier: Modifier = Modifier,
    layer: EnvironmentalLayer? = EnvironmentalLayer.Temperature,
    showHeatmap: Boolean = (layer != null),
    routes: List<MapRoute> = emptyList(),
    selectedRouteId: String? = null,
    markers: List<MapMarker> = emptyList(),
    fitRequest: MapFitRequest? = null
) {
    ClimateMap(
        imageResId = R.drawable.map_satellite_sjc,
        bounds = SjcMapBounds,
        points = DemoClimatePoints,
        modifier = modifier,
        layer = layer ?: EnvironmentalLayer.Temperature,
        showMarkers = false,
        showLabels = false,
        showLegend = false,
        showHeatmap = showHeatmap && layer != null,
        routes = routes,
        selectedRouteId = selectedRouteId,
        markers = markers,
        fitRequest = fitRequest,
        terrainTreatment = TerrainTreatment()
    )
}
