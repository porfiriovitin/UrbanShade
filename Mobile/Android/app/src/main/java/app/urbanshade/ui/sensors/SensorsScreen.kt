package app.urbanshade.ui.sensors

import androidx.annotation.StringRes
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import app.urbanshade.ui.theme.UrbanSpacing
import app.urbanshade.R
import app.urbanshade.ui.components.*
import app.urbanshade.ui.theme.UrbanShadeTheme
import app.urbanshade.ui.theme.UrbanSuccess
import app.urbanshade.ui.theme.UrbanWarning

internal enum class SensorStatus { Active, Offline, Attention }
internal data class SensorItem(val id: String, @param:StringRes val location: Int, val status: SensorStatus = SensorStatus.Active)

internal val DemoSensors = listOf(
    SensorItem("US-001", R.string.map_jardim),
    SensorItem("US-002", R.string.map_confianca),
    SensorItem("US-003", R.string.map_jardim, SensorStatus.Offline),
    SensorItem("US-004", R.string.location_center),
    SensorItem("US-005", R.string.location_center, SensorStatus.Attention),
    SensorItem("US-006", R.string.map_confianca),
    SensorItem("US-007", R.string.map_jardim),
    SensorItem("US-008", R.string.location_center, SensorStatus.Offline),
    SensorItem("US-009", R.string.map_confianca),
    SensorItem("US-010", R.string.map_jardim)
)

@Composable
internal fun SensorsScreen(onBack: () -> Unit = {}, onNavigate: (NavigationDestination) -> Unit = {}) {
    MonitoringPage(stringResource(R.string.navigation_sensors), stringResource(R.string.sensors_description),
        stringResource(R.string.sensors_add), NavigationDestination.Sensors, onBack, onNavigate) {
        items(DemoSensors, key = { it.id }) { SensorCard(it) }
    }
}

@Composable
internal fun SensorCard(sensor: SensorItem, modifier: Modifier = Modifier, reading: String? = null) {
    val colors = MaterialTheme.colorScheme
    val (label, tint) = when (sensor.status) {
        SensorStatus.Active -> R.string.sensor_active to UrbanSuccess
        SensorStatus.Offline -> R.string.sensor_offline to colors.onSurfaceVariant
        SensorStatus.Attention -> R.string.sensor_attention to UrbanWarning
    }
    Row(modifier.fillMaxWidth().background(colors.surface, RoundedCornerShape(12.dp)).padding(UrbanSpacing.Medium),
        verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
        FigmaGroupIcon(R.drawable.figma_sensor_active, null, Modifier.size(24.dp),
            widthFraction = 20.8f / 24, heightFraction = 14.4871f / 24, verticalOffset = (-0.82).dp)
        Column(Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Text(sensor.id, style = MaterialTheme.typography.titleMedium, color = colors.onSurface)
            Text(stringResource(sensor.location), Modifier.heightIn(min = 24.dp),
                style = MaterialTheme.typography.bodyMedium, color = colors.onSurfaceVariant)
            reading?.let {
                Text(it, style = MaterialTheme.typography.bodyMedium, color = colors.onSurfaceVariant)
            }
        }
        Text(stringResource(label), style = MaterialTheme.typography.labelSmall, color = tint)
    }
}

@Preview(widthDp = 412, heightDp = 917)
@Composable
private fun SensorsPreview() { UrbanShadeTheme(true) { SensorsScreen() } }
