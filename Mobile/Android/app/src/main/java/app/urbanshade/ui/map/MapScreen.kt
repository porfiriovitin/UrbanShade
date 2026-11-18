package app.urbanshade.ui.map

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.consumeWindowInsets
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.BottomSheetScaffold
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.ElevatedButton
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.LocalRippleConfiguration
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SheetValue
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.Typography
import androidx.compose.material3.rememberBottomSheetScaffoldState
import androidx.compose.material3.rememberStandardBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import app.urbanshade.ui.theme.UrbanSpacing
import app.urbanshade.R
import app.urbanshade.data.ClimateFixtures
import app.urbanshade.ui.components.IndicatorCard
import app.urbanshade.ui.components.UrbanShadeNavigationBar
import app.urbanshade.ui.components.NavigationDestination
import app.urbanshade.ui.components.LocalNavigationActive
import app.urbanshade.ui.components.map.EnvironmentalLayer
import app.urbanshade.ui.theme.AuthBackground
import app.urbanshade.ui.theme.UrbanShadeTheme
import kotlinx.coroutines.launch

internal data class MapPlace(
    val id: String,
    val title: String,
    val shortcut: String,
    val indicators: List<MapIndicator>
)

internal data class MapIndicator(val label: String, val value: String?)

internal const val ConfiancaId = "confianca"
internal const val JardimId = "jardim"
private val HandleHeight = 32.dp
private val ShortcutTop = 24.dp
private val MaterialShortcutStyle = Typography().labelLarge

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun MapScreen(
    modifier: Modifier = Modifier,
    places: List<MapPlace> = demoMapPlaces(),
    initialPlaceId: String? = null,
    onSeeMore: (MapPlace) -> Unit = {},
    onNavigate: (NavigationDestination) -> Unit = {},
    initialLayer: EnvironmentalLayer? = EnvironmentalLayer.Temperature,
    showHeatmap: Boolean = true
) {
    var selectedId by rememberSaveable { mutableStateOf(initialPlaceId) }
    var selectedLayer by rememberSaveable { mutableStateOf<EnvironmentalLayer?>(initialLayer) }
    val place = places.firstOrNull { it.id == selectedId }
    val sheetState = rememberStandardBottomSheetState(
        initialValue = if (place == null) SheetValue.Hidden else SheetValue.PartiallyExpanded,
        skipHiddenState = false
    )
    val scope = rememberCoroutineScope()
    BackHandler(enabled = sheetState.isVisible && LocalNavigationActive.current) {
        scope.launch { sheetState.hide() }
    }
    val selectPlace: (MapPlace) -> Unit = { selected ->
        selectedId = selected.id
        if (sheetState.targetValue == SheetValue.Hidden) scope.launch { sheetState.show() }
    }

    Column(modifier.fillMaxSize().background(AuthBackground)) {
        BoxWithConstraints(Modifier.weight(1f).fillMaxWidth().clipToBounds()) {
            val expandedHeight = (maxHeight - 104.dp).coerceAtLeast(HandleHeight)
            val peekHeight = minOf(302.dp, expandedHeight * 0.8f)
            BottomSheetScaffold(
                scaffoldState = rememberBottomSheetScaffoldState(sheetState),
                sheetPeekHeight = peekHeight,
                sheetMaxWidth = maxWidth,
                sheetShape = RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp),
                sheetContainerColor = AuthBackground,
                sheetContentColor = MaterialTheme.colorScheme.onSurface,
                sheetShadowElevation = 0.dp,
                sheetDragHandle = { MapSheetHandle() },
                sheetContent = {
                    Box(Modifier.height(expandedHeight - HandleHeight).fillMaxWidth()) {
                        if (place != null) PlaceIndicators(place, onSeeMore = { onSeeMore(place) })
                    }
                }
            ) {
                MapBackdrop(Modifier.fillMaxSize(), layer = selectedLayer, showHeatmap = showHeatmap)
            }

            val top = PaddingValues(top = ShortcutTop)
            Column(
                modifier = Modifier.fillMaxWidth().padding(top).consumeWindowInsets(top)
                    .padding(horizontal = UrbanSpacing.Page),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth().height(40.dp),
                    horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)
                ) {
                    places.forEach { shortcut ->
                        Box(Modifier.weight(1f).fillMaxHeight(), contentAlignment = Alignment.Center) {
                            ElevatedButton(
                                onClick = { selectPlace(shortcut) },
                                modifier = Modifier.fillMaxWidth().height(32.dp),
                                colors = ButtonDefaults.elevatedButtonColors(
                                    containerColor = AuthBackground,
                                    contentColor = MaterialTheme.colorScheme.onSurface
                                ),
                                contentPadding = PaddingValues(horizontal = 12.dp)
                            ) {
                                Icon(painterResource(R.drawable.map_pin_outline), null, Modifier.size(18.dp))
                                Spacer(Modifier.width(4.dp))
                                Text(shortcut.shortcut, style = MaterialShortcutStyle, maxLines = 1)
                            }
                        }
                    }
                }
                Box(modifier = Modifier.fillMaxWidth(), contentAlignment = Alignment.TopEnd) {
                    EnvironmentalLayerSelector(
                        selectedLayer = selectedLayer,
                        onLayerSelected = { selectedLayer = it }
                    )
                }
            }
        }

        UrbanShadeNavigationBar(selected = NavigationDestination.Map, onNavigate = onNavigate)
    }
}

@Composable
private fun MapSheetHandle() {
    Box(Modifier.fillMaxWidth().height(HandleHeight).padding(top = 12.dp), Alignment.TopCenter) {
        Image(
            painter = painterResource(R.drawable.figma_sheet_handle),
            contentDescription = stringResource(R.string.map_sheet_handle),
            modifier = Modifier.size(54.dp, 4.dp)
        )
    }
}

@Composable
private fun PlaceIndicators(place: MapPlace, onSeeMore: () -> Unit) {
    Column(
        modifier = Modifier.fillMaxSize().verticalScroll(rememberScrollState())
            .padding(start = 24.dp, end = 24.dp, bottom = 12.dp)
    ) {
        Text(place.title, style = MaterialTheme.typography.titleLarge)
        Spacer(Modifier.height(16.dp))
        Column(verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
            place.indicators.chunked(2).forEach { row ->
                Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)) {
                    row.forEach { indicator ->
                        IndicatorCard(indicator.label, indicator.value, Modifier.weight(1f))
                    }
                }
            }
        }
        Spacer(Modifier.height(16.dp))
        TextButton(
            onClick = onSeeMore,
            modifier = Modifier.fillMaxWidth().height(44.dp),
            colors = ButtonDefaults.textButtonColors(contentColor = MaterialTheme.colorScheme.onSurfaceVariant),
            contentPadding = PaddingValues(horizontal = 12.dp)
        ) {
            Text(stringResource(R.string.map_see_more), style = MaterialTheme.typography.labelLarge)
        }
    }
}

@Composable
internal fun demoMapPlaces(): List<MapPlace> {
    val daysAboveTargetLabel = stringResource(R.string.map_days_above_target)
    val readingsAverageLabel = stringResource(R.string.map_readings_average)
    val imperviousSurfaceLabel = stringResource(R.string.map_impervious_surface)
    val populationImpactedLabel = stringResource(R.string.map_population_impacted)

    return listOf(
        ClimateFixtures.ConfiancaLocation,
        ClimateFixtures.JardimLocation
    ).map { loc ->
        val latestRecord = loc.dailyRecords.first()
        val indicators = listOf(
            MapIndicator(daysAboveTargetLabel, "${loc.daysAboveTargetCount} de ${loc.totalDaysCount}"),
            MapIndicator(readingsAverageLabel, ClimateFixtures.formatTemperature(latestRecord.averageTemperature)),
            MapIndicator(imperviousSurfaceLabel, "${loc.imperviousSoilPercent}%"),
            MapIndicator(populationImpactedLabel, ClimateFixtures.formatPopulationShort(loc.impactedPopulation))
        )
        MapPlace(
            id = loc.id,
            title = loc.name,
            shortcut = if (loc.id == "jardim") stringResource(R.string.map_jardim_short) else loc.shortName,
            indicators = indicators
        )
    }
}

@Preview(widthDp = 412, heightDp = 917)
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun MapScreenPreview() {
    UrbanShadeTheme(darkTheme = true) {
        CompositionLocalProvider(LocalRippleConfiguration provides null) {
            MapScreen(initialPlaceId = ConfiancaId, initialLayer = EnvironmentalLayer.Temperature)
        }
    }
}

@Preview(widthDp = 412, heightDp = 917)
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun MapScreenTreeCoverPreview() {
    UrbanShadeTheme(darkTheme = true) {
        CompositionLocalProvider(LocalRippleConfiguration provides null) {
            MapScreen(initialPlaceId = ConfiancaId, initialLayer = EnvironmentalLayer.TreeCover)
        }
    }
}

@Preview(widthDp = 412, heightDp = 917)
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun MapScreenHumidityPreview() {
    UrbanShadeTheme(darkTheme = true) {
        CompositionLocalProvider(LocalRippleConfiguration provides null) {
            MapScreen(initialPlaceId = ConfiancaId, initialLayer = EnvironmentalLayer.Humidity)
        }
    }
}

@Preview(widthDp = 412, heightDp = 917)
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun MapScreenSatelliteOnlyPreview() {
    UrbanShadeTheme(darkTheme = true) {
        CompositionLocalProvider(LocalRippleConfiguration provides null) {
            MapScreen(initialPlaceId = ConfiancaId, initialLayer = null)
        }
    }
}
