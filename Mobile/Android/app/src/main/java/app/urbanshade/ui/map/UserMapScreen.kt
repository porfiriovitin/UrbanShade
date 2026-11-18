package app.urbanshade.ui.map

import androidx.activity.compose.BackHandler
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.expandVertically
import androidx.compose.animation.shrinkVertically
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.animation.core.tween
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.selection.selectable
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.layout.onSizeChanged
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import app.urbanshade.R
import app.urbanshade.data.UserNavigationFixtures
import app.urbanshade.ui.components.FigmaGroupIcon
import app.urbanshade.ui.components.NavigationDestination
import app.urbanshade.ui.components.LocalNavigationActive
import app.urbanshade.ui.components.UrbanShadeNavigationBar
import app.urbanshade.ui.components.map.EnvironmentalLayer
import app.urbanshade.ui.components.map.MapFitRequest
import app.urbanshade.ui.components.map.MapRoute
import app.urbanshade.ui.components.map.MapViewportPadding
import app.urbanshade.ui.theme.UrbanOutline
import app.urbanshade.ui.theme.UrbanShadeTheme
import app.urbanshade.ui.theme.UrbanSpacing
import app.urbanshade.ui.theme.UrbanSurface
import app.urbanshade.ui.theme.UrbanMotion

@Composable
internal fun UserMapScreen(
    onNavigate: (NavigationDestination) -> Unit = {},
    initialLayer: EnvironmentalLayer? = null,
    initialDestination: String? = null,
    initialSelectedRouteId: String = "fresca"
) {
    var query by rememberSaveable { mutableStateOf(initialDestination ?: "") }
    var selectedDestination by rememberSaveable { mutableStateOf<String?>(initialDestination) }
    var selectedRouteId by rememberSaveable { mutableStateOf(initialSelectedRouteId) }
    var selectedLayer by rememberSaveable { mutableStateOf<EnvironmentalLayer?>(initialLayer) }
    var isSuggestionsDismissed by rememberSaveable { mutableStateOf(false) }

    var isSearchFocused by remember { mutableStateOf(false) }
    val focusManager = LocalFocusManager.current
    val keyboardController = LocalSoftwareKeyboardController.current

    val density = LocalDensity.current
    var topAreaHeightPx by remember { mutableIntStateOf(0) }
    var panelHeightPx by remember { mutableIntStateOf(0) }

    val isDestinationSelected = selectedDestination != null
    val showSuggestions = !isDestinationSelected &&
        !isSuggestionsDismissed &&
        query.isNotBlank() &&
        UserNavigationFixtures.matchesDestination(query)

    // BackHandler: dismiss keyboard or open suggestions first; outside those, preserve app navigation.
    val canDismissSearchUi = showSuggestions || isSearchFocused
    BackHandler(enabled = canDismissSearchUi && LocalNavigationActive.current) {
        if (showSuggestions) {
            isSuggestionsDismissed = true
        }
        if (isSearchFocused) {
            focusManager.clearFocus()
            keyboardController?.hide()
        }
    }

    // Active markers: origin is always visible (fixed simulated GPS location); destination added when selected.
    val activeMarkers = remember(isDestinationSelected) {
        if (isDestinationSelected) {
            listOf(UserNavigationFixtures.OriginMarker, UserNavigationFixtures.DestinationMarker)
        } else {
            listOf(UserNavigationFixtures.OriginMarker)
        }
    }

    // Active routes and fit request
    val activeRoutes = remember(isDestinationSelected) {
        if (isDestinationSelected) UserNavigationFixtures.AllRoutes else emptyList()
    }

    val allRouteCoordinates = remember {
        (UserNavigationFixtures.RouteFresca.points + UserNavigationFixtures.RouteRapida.points).distinct()
    }

    val fitPadding = remember(topAreaHeightPx, panelHeightPx) {
        val topDp = with(density) { topAreaHeightPx.toDp() } + 24.dp
        val bottomDp = with(density) { panelHeightPx.toDp() } + 24.dp
        MapViewportPadding(top = topDp, bottom = bottomDp, start = 24.dp, end = 24.dp)
    }

    val fitRequest = if (isDestinationSelected && panelHeightPx > 0) {
        MapFitRequest(
            id = "fit_destination_${selectedDestination}",
            coordinates = allRouteCoordinates,
            padding = fitPadding
        )
    } else null

    Column(Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background)) {
        Box(Modifier.weight(1f).fillMaxWidth()) {
            MapBackdrop(
                modifier = Modifier.fillMaxSize(),
                layer = selectedLayer,
                showHeatmap = (selectedLayer != null),
                routes = activeRoutes,
                selectedRouteId = selectedRouteId,
                markers = activeMarkers,
                fitRequest = fitRequest
            )

            // Top Area: Search field, suggestions dropdown and layer selector
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(UrbanSpacing.Page)
                    .onSizeChanged { topAreaHeightPx = it.height },
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                MapSearchField(
                    query = query,
                    onQueryChanged = { newText ->
                        query = newText
                        isSuggestionsDismissed = false
                        // Editing or clearing removes routes and panel, preserving the origin marker.
                        if (selectedDestination != null && newText != selectedDestination) {
                            selectedDestination = null
                            selectedRouteId = "fresca"
                        }
                    },
                    onClearQuery = {
                        query = ""
                        selectedDestination = null
                        selectedRouteId = "fresca"
                        isSuggestionsDismissed = false
                        focusManager.clearFocus()
                        keyboardController?.hide()
                    },
                    onSearch = {
                        if (UserNavigationFixtures.matchesDestination(query)) {
                            query = UserNavigationFixtures.DestinationAddress
                            selectedDestination = UserNavigationFixtures.DestinationAddress
                            selectedRouteId = "fresca"
                            isSuggestionsDismissed = true
                            focusManager.clearFocus()
                            keyboardController?.hide()
                        }
                    },
                    onFocusChanged = { isSearchFocused = it },
                    modifier = Modifier.fillMaxWidth()
                )

                AnimatedVisibility(visible = showSuggestions,
                    enter = fadeIn(tween(UrbanMotion.Transition)) + expandVertically(tween(UrbanMotion.Transition)),
                    exit = fadeOut(tween(UrbanMotion.Transition)) + shrinkVertically(tween(UrbanMotion.Transition))) {
                    SuggestionDropdown(
                        address = UserNavigationFixtures.DestinationAddress,
                        subtitle = UserNavigationFixtures.DestinationSubtitle,
                        onSelect = {
                            query = UserNavigationFixtures.DestinationAddress
                            selectedDestination = UserNavigationFixtures.DestinationAddress
                            selectedRouteId = "fresca"
                            isSuggestionsDismissed = true
                            focusManager.clearFocus()
                            keyboardController?.hide()
                        },
                        modifier = Modifier.fillMaxWidth()
                    )
                }

                Box(modifier = Modifier.fillMaxWidth(), contentAlignment = Alignment.TopEnd) {
                    EnvironmentalLayerSelector(
                        selectedLayer = selectedLayer,
                        onLayerSelected = { selectedLayer = it }
                    )
                }
            }

            // Bottom Area: Route comparison panel (positioned above the bottom navigation bar)
            androidx.compose.animation.AnimatedVisibility(visible = isDestinationSelected, modifier = Modifier.align(Alignment.BottomCenter),
                enter = fadeIn(tween(UrbanMotion.Detail)) + expandVertically(tween(UrbanMotion.Detail), expandFrom = Alignment.Bottom),
                exit = fadeOut(tween(UrbanMotion.Transition)) + shrinkVertically(tween(UrbanMotion.Transition), shrinkTowards = Alignment.Bottom)) {
                RouteComparisonPanel(
                    destination = selectedDestination ?: UserNavigationFixtures.DestinationAddress,
                    routes = activeRoutes,
                    selectedRouteId = selectedRouteId,
                    onRouteSelected = { route -> selectedRouteId = route.id },
                    modifier = Modifier
                        .onSizeChanged { panelHeightPx = it.height }
                )
            }
        }

        UrbanShadeNavigationBar(selected = NavigationDestination.Map, onNavigate = onNavigate)
    }
}

@Composable
internal fun MapSearchField(
    query: String,
    onQueryChanged: (String) -> Unit,
    onClearQuery: () -> Unit,
    onSearch: () -> Unit = {},
    onFocusChanged: (Boolean) -> Unit = {},
    modifier: Modifier = Modifier
) {
    val colors = MaterialTheme.colorScheme
    val hint = stringResource(R.string.user_map_search_hint)
    BasicTextField(
        value = query,
        onValueChange = onQueryChanged,
        modifier = modifier
            .height(48.dp)
            .shadow(2.dp, RoundedCornerShape(12.dp))
            .background(colors.background, RoundedCornerShape(12.dp))
            .border(1.dp, colors.outline, RoundedCornerShape(12.dp))
            .onFocusChanged { onFocusChanged(it.isFocused) }
            .semantics { contentDescription = hint },
        textStyle = MaterialTheme.typography.bodyMedium.copy(color = colors.onSurface),
        singleLine = true,
        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
        keyboardActions = KeyboardActions(onSearch = { onSearch() }),
        cursorBrush = SolidColor(colors.primary),
        decorationBox = { innerTextField ->
            Row(
                Modifier.fillMaxSize().padding(horizontal = 16.dp),
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)
            ) {
                FigmaGroupIcon(
                    R.drawable.figma_search, null, Modifier.size(20.dp), colors.onSurfaceVariant,
                    widthFraction = 15f / 20, heightFraction = 15f / 20
                )
                Box(Modifier.weight(1f)) {
                    if (query.isEmpty()) {
                        Text(hint, style = MaterialTheme.typography.bodyMedium, color = colors.onSurfaceVariant)
                    }
                    innerTextField()
                }
                if (query.isNotEmpty()) {
                    IconButton(
                        onClick = onClearQuery,
                        modifier = Modifier.size(28.dp).semantics {
                            contentDescription = "Limpar busca"
                        }
                    ) {
                        Icon(
                            painter = painterResource(R.drawable.ic_clear),
                            contentDescription = stringResource(R.string.user_map_search_clear),
                            tint = colors.onSurfaceVariant,
                            modifier = Modifier.size(16.dp)
                        )
                    }
                }
            }
        }
    )
}

@Composable
private fun SuggestionDropdown(
    address: String,
    subtitle: String,
    onSelect: () -> Unit,
    modifier: Modifier = Modifier
) {
    val colors = MaterialTheme.colorScheme
    Box(
        modifier = modifier
            .shadow(4.dp, RoundedCornerShape(12.dp))
            .background(UrbanSurface, RoundedCornerShape(12.dp))
            .border(1.dp, UrbanOutline, RoundedCornerShape(12.dp))
            .clickable(onClick = onSelect)
            .padding(horizontal = 16.dp, vertical = 12.dp)
            .semantics { contentDescription = "Sugestão: $address, $subtitle" }
    ) {
        Column {
            Text(
                text = address,
                style = MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
                color = colors.onSurface
            )
            Text(
                text = subtitle,
                style = MaterialTheme.typography.bodySmall,
                color = colors.onSurfaceVariant
            )
        }
    }
}

@Composable
private fun RouteComparisonPanel(
    destination: String,
    routes: List<MapRoute>,
    selectedRouteId: String,
    onRouteSelected: (MapRoute) -> Unit,
    modifier: Modifier = Modifier
) {
    val colors = MaterialTheme.colorScheme
    Box(
        modifier = modifier
            .fillMaxWidth()
            .padding(horizontal = UrbanSpacing.Page, vertical = UrbanSpacing.Small)
            .shadow(6.dp, RoundedCornerShape(16.dp))
            .background(colors.background, RoundedCornerShape(16.dp))
            .padding(16.dp)
            .semantics { contentDescription = "Painel de comparação de rotas para $destination" }
    ) {
        Column(verticalArrangement = Arrangement.spacedBy(10.dp)) {
            Column {
                Text(
                    text = destination,
                    style = MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold),
                    color = colors.onSurface
                )
                Text(
                    text = stringResource(R.string.user_map_comparison_panel_title),
                    style = MaterialTheme.typography.labelSmall,
                    color = colors.onSurfaceVariant
                )
            }

            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                routes.forEach { route ->
                    RouteComparisonCard(
                        route = route,
                        isSelected = route.id == selectedRouteId,
                        onSelect = { onRouteSelected(route) }
                    )
                }
            }
        }
    }
}

@Composable
private fun RouteComparisonCard(
    route: MapRoute,
    isSelected: Boolean,
    onSelect: () -> Unit,
    modifier: Modifier = Modifier
) {
    val colors = MaterialTheme.colorScheme
    val routeBaseColor = when (route.id) {
        "fresca" -> colors.primary
        "rapida" -> colors.secondary
        else -> colors.primary
    }
    val backgroundColor by animateColorAsState(if (isSelected) routeBaseColor else colors.surface,
        tween(UrbanMotion.Selection), label = "Route background")
    val titleColor by animateColorAsState(if (isSelected) colors.onPrimary else colors.onSurface,
        tween(UrbanMotion.Selection), label = "Route title")
    val metricColor by animateColorAsState(if (isSelected) colors.onPrimary else colors.onSurfaceVariant,
        tween(UrbanMotion.Selection), label = "Route metrics")
    val descriptionColor by animateColorAsState(if (isSelected) colors.onPrimary.copy(alpha = 0.85f) else colors.onSurfaceVariant,
        tween(UrbanMotion.Selection), label = "Route description")

    val accessibilityDesc = "${route.name}, ${route.durationMinutes} minutos, ${route.distanceMeters} metros, ${route.description}, ${if (isSelected) "selecionada" else "não selecionada"}"

    Row(
        modifier = modifier
            .fillMaxWidth()
            .clip(RoundedCornerShape(12.dp))
            .background(backgroundColor, RoundedCornerShape(12.dp))
            .selectable(
                selected = isSelected,
                onClick = onSelect,
                role = Role.RadioButton
            )
            .semantics { contentDescription = accessibilityDesc }
            .padding(horizontal = 14.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        RouteLineBadge(route = route, isSelected = isSelected)

        Column(modifier = Modifier.weight(1f), verticalArrangement = Arrangement.spacedBy(2.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = route.name,
                    style = MaterialTheme.typography.titleSmall.copy(fontWeight = FontWeight.Bold),
                    color = titleColor
                )
                Text(
                    text = route.durationAndDistanceText,
                    style = MaterialTheme.typography.labelMedium.copy(fontWeight = FontWeight.Bold),
                    color = metricColor
                )
            }
            Text(
                text = route.description,
                style = MaterialTheme.typography.bodySmall,
                color = descriptionColor
            )
        }
    }
}

@Composable
private fun RouteLineBadge(route: MapRoute, isSelected: Boolean) {
    val colors = MaterialTheme.colorScheme
    val baseColor = when (route.id) {
        "fresca" -> colors.primary
        "rapida" -> colors.secondary
        else -> colors.primary
    }
    val lineColor = if (isSelected) colors.onPrimary else baseColor.copy(alpha = 0.55f)

    Canvas(modifier = Modifier.size(width = 24.dp, height = 24.dp)) {
        val y = size.height / 2f
        val innerStrokeWidth = 4.dp.toPx()
        if (!isSelected) {
            // Outer contour in background color (#07171C) when on surface
            drawLine(
                color = colors.background,
                start = Offset(2.dp.toPx(), y),
                end = Offset(size.width - 2.dp.toPx(), y),
                strokeWidth = innerStrokeWidth + 2.dp.toPx(),
                cap = StrokeCap.Round
            )
        }
        // Inner line stroke (onPrimary when selected, baseColor at 55% when unselected)
        drawLine(
            color = lineColor,
            start = Offset(2.dp.toPx(), y),
            end = Offset(size.width - 2.dp.toPx(), y),
            strokeWidth = innerStrokeWidth,
            cap = StrokeCap.Round
        )
    }
}

@Preview(widthDp = 412, heightDp = 917)
@Composable
private fun UserMapInitialPreview() {
    UrbanShadeTheme(darkTheme = true) {
        UserMapScreen(initialLayer = null)
    }
}

@Preview(widthDp = 412, heightDp = 917)
@Composable
private fun UserMapWithRoutesPreview() {
    UrbanShadeTheme(darkTheme = true) {
        UserMapScreen(
            initialLayer = null,
            initialDestination = UserNavigationFixtures.DestinationAddress
        )
    }
}
