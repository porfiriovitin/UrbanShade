package app.urbanshade.ui.map

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Slider
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableFloatStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import app.urbanshade.R
import app.urbanshade.ui.components.map.ClimateMap
import app.urbanshade.ui.components.map.EnvironmentalLayer
import app.urbanshade.ui.theme.UrbanShadeTheme

@Composable
internal fun ClimateMapDemo(modifier: Modifier = Modifier) {
    var selectedLayer by rememberSaveable { mutableStateOf(EnvironmentalLayer.Temperature) }
    var coolNight by rememberSaveable { mutableStateOf(false) }
    var limitRadius by rememberSaveable { mutableStateOf(false) }
    var temperatureAdjustment by rememberSaveable { mutableFloatStateOf(0f) }
    val points = remember(coolNight, temperatureAdjustment) {
        DemoClimatePoints.map {
            it.copy(temperatureCelsius = (it.temperatureCelsius + temperatureAdjustment - if (coolNight) 9.5f else 0f).coerceIn(10f, 40f))
        }
    }
    Surface(modifier.fillMaxSize()) {
        Column(
            Modifier.verticalScroll(rememberScrollState()).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Text(stringResource(R.string.climate_map_demo_title), style = MaterialTheme.typography.titleMedium)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                EnvironmentalLayer.entries.forEach { layer ->
                    FilterChip(
                        selected = selectedLayer == layer,
                        onClick = { selectedLayer = layer },
                        label = { Text(stringResource(layer.labelResId)) }
                    )
                }
            }
            if (selectedLayer == EnvironmentalLayer.Temperature) {
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    FilterChip(
                        selected = !coolNight,
                        onClick = { coolNight = false },
                        label = { Text(stringResource(R.string.climate_map_demo_warm)) }
                    )
                    FilterChip(
                        selected = coolNight,
                        onClick = { coolNight = true },
                        label = { Text(stringResource(R.string.climate_map_demo_cool)) }
                    )
                }
                Text(
                    stringResource(R.string.climate_map_demo_adjustment, temperatureAdjustment),
                    style = MaterialTheme.typography.bodySmall
                )
                Slider(temperatureAdjustment, { temperatureAdjustment = it }, valueRange = -8f..8f)
            }
            FilterChip(
                selected = limitRadius,
                onClick = { limitRadius = !limitRadius },
                label = { Text(stringResource(R.string.climate_map_demo_radius)) }
            )
            ClimateMap(
                imageResId = R.drawable.map_satellite_sjc,
                bounds = SjcMapBounds,
                points = points,
                layer = selectedLayer,
                modifier = Modifier.fillMaxWidth(),
                radiusDegrees = if (limitRadius) 0.012 else null,
                showLegend = false
            )
        }
    }
}

@Preview(showBackground = true, widthDp = 412, heightDp = 780)
@Composable
private fun ClimateMapDemoPreview() {
    UrbanShadeTheme(darkTheme = true) { ClimateMapDemo() }
}
