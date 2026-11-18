package app.urbanshade.ui.home

import androidx.annotation.DrawableRes
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalViewConfiguration
import androidx.compose.ui.platform.ViewConfiguration
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.DpSize
import app.urbanshade.ui.theme.UrbanSpacing
import app.urbanshade.R
import app.urbanshade.ui.components.*
import app.urbanshade.ui.theme.UrbanShadeTheme

@Composable
internal fun HomeScreen(onNavigate: (NavigationDestination) -> Unit = {}) {
    val colors = MaterialTheme.colorScheme
    Column(Modifier.fillMaxSize().background(colors.background)) {
        Column(Modifier.weight(1f).fillMaxWidth().verticalScroll(rememberScrollState()).padding(bottom = 24.dp),
            verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Section)) {
            Column {
                HomeBrandHeader(onNavigate)
                Column(Modifier.fillMaxWidth().padding(horizontal = UrbanSpacing.Page), verticalArrangement = Arrangement.spacedBy(4.dp)) {
                    Text(stringResource(R.string.home_greeting), style = MaterialTheme.typography.displayLarge, color = colors.secondary)
                    Text(stringResource(R.string.home_question), style = MaterialTheme.typography.titleLarge, color = colors.onSurfaceVariant)
                }
            }
            Column(Modifier.padding(horizontal = UrbanSpacing.Page), verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Medium)) {
                repeat(2) { row ->
                    Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Medium)) {
                        repeat(2) { column ->
                            val active = row == 0 && column == 0
                            val (label, icon) = when (row * 2 + column) {
                                0 -> R.string.home_green_areas to R.drawable.figma_tree_active
                                1 -> R.string.home_urban_comfort to R.drawable.ic_bench
                                2 -> R.string.home_mobility to R.drawable.ic_bicycle
                                else -> R.string.home_occurrences to R.drawable.ic_warning_outline
                            }
                            VerticalShortcut(stringResource(label), icon,
                                active, { onNavigate(NavigationDestination.Map) }, Modifier.weight(1f))
                        }
                    }
                }
            }
            Column(Modifier.padding(horizontal = UrbanSpacing.Page), verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Medium)) {
                Text(stringResource(R.string.home_highlights), style = MaterialTheme.typography.headlineMedium, color = colors.secondary)
                CityHighlightCard()
            }
        }
        UrbanShadeNavigationBar(selected = NavigationDestination.Home, onNavigate = onNavigate)
    }
}

@Composable
internal fun VerticalShortcut(
    label: String, @DrawableRes icon: Int, active: Boolean, onClick: () -> Unit, modifier: Modifier = Modifier
) {
    val colors = MaterialTheme.colorScheme
    Surface(onClick = onClick, modifier = modifier.heightIn(min = 110.dp), shape = RoundedCornerShape(18.dp),
        color = if (active) colors.primary else colors.surface,
        contentColor = if (active) colors.onPrimary else colors.onSurface) {
        Column(Modifier.padding(UrbanSpacing.Medium), verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
            if (active) {
                FigmaGroupIcon(icon, null, Modifier.size(32.dp), widthFraction = 25.7625f / 32, heightFraction = 28.95f / 32,
                    verticalOffset = (-0.375).dp)
            } else {
                Icon(painterResource(icon), null, Modifier.size(32.dp))
            }
            Text(label, style = MaterialTheme.typography.titleMedium)
        }
    }
}

@Composable
private fun CityHighlightCard(
    modifier: Modifier = Modifier,
    @DrawableRes imageRes: Int = R.drawable.home_highlight_city
) {
    val colors = MaterialTheme.colorScheme
    Surface(
        modifier = modifier.fillMaxWidth(),
        shape = RoundedCornerShape(24.dp),
        color = colors.surface
    ) {
        Column(Modifier.fillMaxWidth()) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(120.dp)
            ) {
                Image(
                    painter = painterResource(imageRes),
                    contentDescription = null,
                    modifier = Modifier.fillMaxSize(),
                    contentScale = ContentScale.Crop
                )
                Surface(
                    modifier = Modifier
                        .align(Alignment.TopEnd)
                        .padding(12.dp)
                        .size(70.dp, 30.dp),
                    shape = RoundedCornerShape(100.dp),
                    color = colors.primary,
                    contentColor = colors.onPrimary
                ) {
                    Box(contentAlignment = Alignment.Center) {
                        Text(stringResource(R.string.home_new), style = MaterialTheme.typography.labelLarge)
                    }
                }
            }
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(UrbanSpacing.Medium),
                verticalArrangement = Arrangement.Center
            ) {
                Text(stringResource(R.string.home_highlight_title), style = MaterialTheme.typography.titleMedium, color = colors.secondary)
                Spacer(Modifier.height(6.dp))
                Text(stringResource(R.string.home_highlight_description), style = MaterialTheme.typography.bodyLarge, color = colors.secondary)
            }
        }
    }
}

@Preview(widthDp = 412, heightDp = 917)
@Composable
private fun HomePreview() { UrbanShadeTheme(true) { HomeScreen() } }

@Composable
internal fun HomeBrandHeader(onNavigate: (NavigationDestination) -> Unit) {
    val viewConfiguration = LocalViewConfiguration.current
    val profileTouchConfiguration = remember(viewConfiguration) {
        object : ViewConfiguration by viewConfiguration {
            override val minimumTouchTargetSize = DpSize(32.dp, 32.dp)
        }
    }
    BoxWithConstraints(Modifier.fillMaxWidth().height(84.dp)) {
        Image(painterResource(R.drawable.figma_home_wave), null,
            Modifier.offset(x = (-1).dp, y = 11.65.dp).requiredSize(maxWidth + 2.dp, 70.35.dp),
            contentScale = ContentScale.FillBounds)
        CompositionLocalProvider(LocalViewConfiguration provides profileTouchConfiguration,
            LocalMinimumInteractiveComponentSize provides 32.dp) {
            IconButton(onClick = { onNavigate(NavigationDestination.Profile) },
                modifier = Modifier.align(Alignment.TopEnd).padding(top = 12.dp, end = 24.dp).size(32.dp)) {
                Image(painterResource(R.drawable.figma_home_profile), stringResource(R.string.navigation_profile),
                    Modifier.size(32.dp))
            }
        }
    }
}
