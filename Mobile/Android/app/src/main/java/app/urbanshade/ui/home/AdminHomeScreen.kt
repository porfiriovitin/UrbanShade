package app.urbanshade.ui.home

import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.LocalLifecycleOwner
import androidx.lifecycle.repeatOnLifecycle
import app.urbanshade.R
import app.urbanshade.data.ClimateAlerts
import app.urbanshade.data.ClimateFixtures
import app.urbanshade.ui.components.*
import app.urbanshade.ui.sensors.DemoSensors
import app.urbanshade.ui.sensors.SensorCard
import app.urbanshade.ui.sensors.SensorStatus
import app.urbanshade.ui.theme.UrbanHeat
import app.urbanshade.ui.theme.UrbanMotion
import app.urbanshade.ui.theme.UrbanShadeTheme
import app.urbanshade.ui.theme.UrbanSpacing
import java.util.Locale
import kotlinx.coroutines.delay

@Composable
internal fun AdminHomeScreen(onNavigate: (NavigationDestination) -> Unit = {}) {
    val colors = MaterialTheme.colorScheme
    val locations = remember { ClimateFixtures.getAllLocations() }
    val date = remember(locations) {
        locations.map { location -> location.dailyRecords.map { it.dateIso }.toSet() }
            .reduce { common, dates -> common.intersect(dates) }.max()
    }
    val records = remember(locations, date) {
        locations.map { location -> location.dailyRecords.first { it.dateIso == date } }
    }
    val hours = records.first().samples.map { it.hourLabel }
    val temperatures = hours.map { hour ->
        records.map { record -> record.samples.first { it.hourLabel == hour }.currentTemperature }.average()
    }
    val alerts = remember(date) { ClimateAlerts.getAllAlerts().filter { it.dateIso == date } }
    val hottestLocation = locations[records.indices.maxBy { records[it].maxTemperature }]
    var sensorHead by remember { mutableIntStateOf(2) }
    var alertIndex by remember(date) {
        mutableIntStateOf(alerts.indexOfFirst { it.locationId == hottestLocation.id && it.id.endsWith("_heat") }.coerceAtLeast(0))
    }
    val active = LocalNavigationActive.current
    val lifecycleOwner = LocalLifecycleOwner.current
    LaunchedEffect(active, lifecycleOwner, date) {
        if (!active) return@LaunchedEffect
        lifecycleOwner.lifecycle.repeatOnLifecycle(Lifecycle.State.RESUMED) {
            delay(2_500)
            while (true) {
                sensorHead = (sensorHead + 1) % DemoSensors.size
                delay(400)
                if (alerts.isNotEmpty()) alertIndex = (alertIndex + 1) % alerts.size
                delay(2_100)
            }
        }
    }
    val sensors = List(3) { offset -> DemoSensors[(sensorHead - offset + DemoSensors.size) % DemoSensors.size] }
    val visibleAlerts = List(minOf(3, alerts.size)) { offset -> alerts[(alertIndex - offset + alerts.size) % alerts.size] }

    Column(Modifier.fillMaxSize().background(colors.background)) {
        LazyColumn(Modifier.weight(1f).fillMaxWidth(), contentPadding = PaddingValues(bottom = UrbanSpacing.Section),
            verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
            item(key = "header") {
                Column {
                    HomeBrandHeader(onNavigate)
                    Column(Modifier.padding(horizontal = UrbanSpacing.Page), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                        Text(stringResource(R.string.admin_home_brand), style = MaterialTheme.typography.labelSmall,
                            color = colors.onSurfaceVariant)
                        Text(stringResource(R.string.admin_home_title), style = MaterialTheme.typography.displayLarge,
                            color = colors.secondary, modifier = Modifier.semantics { heading() })
                        Text(stringResource(R.string.admin_home_subtitle), style = MaterialTheme.typography.titleLarge,
                            color = colors.onSurfaceVariant)
                    }
                }
            }
            item(key = "indicators") {
                Column(Modifier.padding(start = UrbanSpacing.Page, end = UrbanSpacing.Page, top = UrbanSpacing.Small),
                    verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
                        IndicatorCard(stringResource(R.string.admin_home_active_sensors),
                            "${DemoSensors.count { it.status == SensorStatus.Active }}/${DemoSensors.size}",
                            Modifier.weight(1f), contentPadding = 16.dp, singleLine = false)
                        IndicatorCard(stringResource(R.string.admin_home_alert_count), alerts.size.toString(),
                            Modifier.weight(1f), contentPadding = 16.dp, singleLine = false)
                    }
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
                        IndicatorCard(stringResource(R.string.admin_home_average_temperature),
                            ClimateFixtures.formatTemperature(records.flatMap { it.samples }.map { it.currentTemperature }.average()),
                            Modifier.weight(1f), contentPadding = 16.dp, singleLine = false)
                        IndicatorCard(stringResource(R.string.admin_home_average_cover),
                            String.format(Locale.forLanguageTag("pt-BR"), "%.0f%%", locations.map { it.currentTreeCoverPercent }.average()),
                            Modifier.weight(1f), contentPadding = 16.dp, singleLine = false)
                    }
                }
            }
            item(key = "temperature-chart") {
                HourlySeriesChart(stringResource(R.string.admin_home_chart), hours, temperatures, UrbanHeat,
                    modifier = Modifier.padding(start = UrbanSpacing.Page, end = UrbanSpacing.Page, top = UrbanSpacing.Small),
                    yMin = 20.0, yMax = 44.0, unit = "°C",
                    accessibleDescription = stringResource(R.string.admin_home_chart_description,
                        hours.zip(temperatures).joinToString("; ") { (hour, value) -> "$hour: ${ClimateFixtures.formatTemperature(value)}" }))
            }
            item(key = "telemetry-heading") {
                Row(Modifier.fillMaxWidth().padding(start = UrbanSpacing.Page, end = UrbanSpacing.Page, top = UrbanSpacing.Small),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    Text(stringResource(R.string.admin_home_telemetry), Modifier.weight(1f).semantics { heading() },
                        style = MaterialTheme.typography.titleLarge, color = colors.secondary)
                    TextButton(onClick = { onNavigate(NavigationDestination.Sensors) }) {
                        Text(stringResource(R.string.admin_home_all_sensors))
                    }
                }
            }
            items(sensors, key = { "sensor/${it.id}" }) { sensor ->
                val location = when (sensor.location) {
                    R.string.map_jardim -> ClimateFixtures.JardimLocation
                    R.string.map_confianca -> ClimateFixtures.ConfiancaLocation
                    else -> null
                }
                val sample = location?.dailyRecords?.firstOrNull { it.dateIso == date }?.samples?.lastOrNull()
                val reading = if (sensor.status != SensorStatus.Offline && sample != null) {
                    stringResource(R.string.admin_home_local_reading, sample.hourLabel,
                        ClimateFixtures.formatTemperature(sample.currentTemperature), sample.humidityPercent)
                } else null
                SensorCard(sensor, reading = reading, modifier = Modifier
                    .animateItem(fadeInSpec = tween(UrbanMotion.Detail), placementSpec = tween(UrbanMotion.Detail),
                        fadeOutSpec = tween(UrbanMotion.Transition))
                    .padding(horizontal = UrbanSpacing.Page))
            }
            if (visibleAlerts.isNotEmpty()) {
                item(key = "alerts-heading") {
                    Text(stringResource(R.string.admin_home_featured_alert), style = MaterialTheme.typography.titleLarge,
                        color = colors.secondary, modifier = Modifier
                            .padding(start = UrbanSpacing.Page, end = UrbanSpacing.Page, top = UrbanSpacing.Small)
                            .semantics { heading() })
                }
                items(visibleAlerts, key = { "alert/${it.id}" }) { alert ->
                    AlertCard(alert.message, Modifier
                        .animateItem(fadeInSpec = tween(UrbanMotion.Detail), placementSpec = tween(UrbanMotion.Detail),
                            fadeOutSpec = tween(UrbanMotion.Transition))
                        .padding(horizontal = UrbanSpacing.Page)
                        .clickable(role = Role.Button, onClickLabel = stringResource(R.string.navigation_alerts)) {
                            onNavigate(NavigationDestination.Alerts)
                        })
                }
            }
        }
        UrbanShadeNavigationBar(selected = NavigationDestination.Home, onNavigate = onNavigate)
    }
}

@Preview(widthDp = 412, heightDp = 917)
@Composable
private fun AdminHomePreview() {
    UrbanShadeTheme(true) {
        androidx.compose.runtime.CompositionLocalProvider(LocalAdminNavigation provides true) { AdminHomeScreen() }
    }
}
