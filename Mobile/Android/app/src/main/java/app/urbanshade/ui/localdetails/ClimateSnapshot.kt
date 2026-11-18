package app.urbanshade.ui.localdetails

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.unit.dp
import app.urbanshade.R
import app.urbanshade.data.DailyClimateRecord
import app.urbanshade.data.LocationClimateData
import app.urbanshade.ui.components.map.ClimateMap
import app.urbanshade.ui.components.map.ClimatePoint
import app.urbanshade.ui.components.map.EnvironmentalLayer
import app.urbanshade.ui.components.map.TerrainTreatment
import app.urbanshade.ui.map.DemoClimatePoints
import app.urbanshade.ui.map.EnvironmentalLayerSelector
import app.urbanshade.ui.map.SjcMapBounds

internal fun snapshotPoints(
    location: LocationClimateData,
    record: DailyClimateRecord,
    proposed: Boolean
): List<ClimatePoint> {
    val sample = record.samples.maxBy { it.currentTemperature }
    return DemoClimatePoints.map { point ->
        point.copy(
            name = location.name,
            temperatureCelsius = (if (proposed) sample.proposedTemperature else sample.currentTemperature).toFloat(),
            treeCoverPercent = (if (proposed) location.proposedTreeCoverPercent else location.currentTreeCoverPercent).toFloat(),
            relativeHumidityPercent = sample.humidityPercent.toFloat()
        )
    }
}

@Composable
internal fun ClimateSnapshot(
    location: LocationClimateData,
    record: DailyClimateRecord,
    proposed: Boolean
) {
    var selectedLayer by rememberSaveable { mutableStateOf<EnvironmentalLayer?>(EnvironmentalLayer.Temperature) }
    val points = remember(location, record, proposed) { snapshotPoints(location, record, proposed) }
    val layer = selectedLayer ?: EnvironmentalLayer.Temperature
    Box(Modifier.fillMaxWidth().height(350.dp).clip(RoundedCornerShape(25.dp))) {
        ClimateMap(
            imageResId = R.drawable.map_satellite_sjc,
            bounds = SjcMapBounds,
            points = points,
            modifier = Modifier.fillMaxSize(),
            layer = layer,
            valueMin = if (layer == EnvironmentalLayer.Temperature) 18f else layer.valueMin,
            valueMax = if (layer == EnvironmentalLayer.Temperature) 42f else layer.valueMax,
            showMarkers = false,
            showLabels = false,
            showLegend = false,
            showHeatmap = selectedLayer != null,
            gesturesEnabled = false,
            terrainTreatment = TerrainTreatment(
                locationId = location.id,
                treeCoverGain = if (proposed) (location.proposedTreeCoverPercent - location.currentTreeCoverPercent).toFloat() else 0f,
                coolingCelsius = if (proposed) location.peakReductionCelsius.toFloat() else 0f
            )
        )
        EnvironmentalLayerSelector(
            selectedLayer = selectedLayer,
            onLayerSelected = { selectedLayer = it },
            modifier = Modifier.align(Alignment.CenterEnd).padding(12.dp)
        )
    }
}
