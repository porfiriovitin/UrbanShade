package app.urbanshade.ui.components

import androidx.annotation.DrawableRes
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.selection.selectableGroup
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.compositionLocalOf
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ColorFilter
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import app.urbanshade.R
import app.urbanshade.ui.theme.UrbanMotion

internal enum class NavigationDestination { Home, Map, Alerts, Sensors, Profile }

internal val LocalAdminNavigation = compositionLocalOf { false }
internal val LocalNavigationActive = compositionLocalOf { true }

@Composable
internal fun UrbanShadeNavigationBar(
    selected: NavigationDestination,
    onNavigate: (NavigationDestination) -> Unit = {},
    modifier: Modifier = Modifier
) {
    val colors = MaterialTheme.colorScheme
    val destinations = if (LocalAdminNavigation.current) {
        listOf(NavigationDestination.Home, NavigationDestination.Map, NavigationDestination.Alerts, NavigationDestination.Sensors)
    } else {
        listOf(NavigationDestination.Home, NavigationDestination.Map, NavigationDestination.Alerts)
    }
    Row(modifier.fillMaxWidth().height(64.dp).background(colors.background).selectableGroup(),
        verticalAlignment = Alignment.CenterVertically) {
        destinations.forEach { destination ->
            val label = stringResource(when (destination) {
                NavigationDestination.Home -> R.string.navigation_home
                NavigationDestination.Map -> R.string.navigation_map
                NavigationDestination.Alerts -> R.string.navigation_alerts
                NavigationDestination.Sensors -> R.string.navigation_sensors
                NavigationDestination.Profile -> R.string.navigation_profile
            })
            val tint by animateColorAsState(if (selected == destination) colors.primary else colors.onSurfaceVariant,
                tween(UrbanMotion.Selection), label = "Navigation tint")
            Box(Modifier.weight(1f).fillMaxHeight().selectable(
                selected = selected == destination, role = Role.Tab,
                enabled = LocalNavigationActive.current,
                onClick = { onNavigate(destination) }
            ), Alignment.Center) {
                when (destination) {
                    NavigationDestination.Home -> Icon(painterResource(R.drawable.ic_home), label,
                        Modifier.size(28.dp), tint = tint)
                    NavigationDestination.Map -> Icon(
                        painterResource(if (selected == destination) R.drawable.map_pin_filled else R.drawable.map_pin_outline),
                        label, Modifier.size(28.dp), tint = tint)
                    NavigationDestination.Alerts -> FigmaGroupIcon(
                        R.drawable.figma_bell, label, Modifier.size(28.dp), tint,
                        16f / 24, 20f / 24)
                    NavigationDestination.Sensors -> FigmaGroupIcon(
                        R.drawable.figma_sensor_muted, label, Modifier.size(28.dp), tint,
                        24.2667f / 28, 16.9016f / 28, (-0.95).dp)
                    NavigationDestination.Profile -> Unit
                }
            }
        }
    }
}

// Exported vector groups occupy only part of their Figma icon slots.
@Composable
internal fun FigmaGroupIcon(
    @DrawableRes resource: Int,
    description: String?,
    modifier: Modifier,
    tint: Color? = null,
    widthFraction: Float,
    heightFraction: Float,
    verticalOffset: Dp = 0.dp
) {
    BoxWithConstraints(modifier, contentAlignment = Alignment.Center) {
        Image(painterResource(resource), description,
            Modifier.size(maxWidth * widthFraction, maxHeight * heightFraction).offset(y = verticalOffset),
            colorFilter = tint?.let { ColorFilter.tint(it) })
    }
}
