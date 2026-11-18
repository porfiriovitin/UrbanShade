package app.urbanshade.ui.components.map

import androidx.annotation.DrawableRes
import androidx.compose.foundation.Canvas
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectTransformGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.requiredSize
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.produceState
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ColorFilter
import androidx.compose.ui.graphics.ColorMatrix
import androidx.compose.ui.graphics.FilterQuality
import androidx.compose.ui.graphics.ImageBitmap
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.Shadow
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.graphics.drawscope.translate
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.drawText
import androidx.compose.ui.text.rememberTextMeasurer
import androidx.compose.ui.unit.Density
import androidx.compose.ui.unit.IntSize
import androidx.compose.ui.unit.dp
import app.urbanshade.R
import app.urbanshade.ui.theme.UrbanHeatmapPalette
import app.urbanshade.ui.theme.UrbanMapPinRed
import app.urbanshade.ui.theme.paletteFor
import kotlin.math.roundToInt
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

/**
 * Offline map for any north-up EPSG:4326 image and its matching [bounds].
 * Supports Temperature, Tree Cover and Humidity layers with 5 intensity bands.
 * Satellite backdrop saturation and contrast are softened for contrast against the layer.
 * Zoom and pan transform is preserved across layer selections.
 */
@Composable
internal fun ClimateMap(
    @DrawableRes imageResId: Int,
    bounds: MapBounds,
    points: List<ClimatePoint>,
    modifier: Modifier = Modifier,
    layer: EnvironmentalLayer = EnvironmentalLayer.Temperature,
    palette: HeatmapPalette = paletteFor(layer),
    valueMin: Float = layer.valueMin,
    valueMax: Float = layer.valueMax,
    gridWidth: Int = HeatmapGenerator.DefaultGridWidth,
    radiusDegrees: Double? = null,
    heatmapAlpha: Int = HeatmapGenerator.DefaultAlpha,
    showMarkers: Boolean = true,
    showLabels: Boolean = true,
    showLegend: Boolean = false,
    showHeatmap: Boolean = true,
    temperatureMin: Float = valueMin,
    temperatureMax: Float = valueMax,
    routes: List<MapRoute> = emptyList(),
    selectedRouteId: String? = null,
    markers: List<MapMarker> = emptyList(),
    fitRequest: MapFitRequest? = null,
    gesturesEnabled: Boolean = true,
    terrainTreatment: TerrainTreatment? = null
) {
    val effectiveMin = if (temperatureMin != valueMin) temperatureMin else valueMin
    val effectiveMax = if (temperatureMax != valueMax) temperatureMax else valueMax
    val resources = LocalContext.current.resources
    val useTerrain = terrainTreatment != null && imageResId == R.drawable.map_satellite_sjc

    val bitmap by produceState<ImageBitmap?>(
        null, points, bounds, palette, gridWidth, effectiveMin, effectiveMax, radiusDegrees, heatmapAlpha, showHeatmap, layer,
        imageResId, terrainTreatment
    ) {
        value = null
        if (showHeatmap && points.isNotEmpty()) {
            value = withContext(Dispatchers.Default) {
                val rendered = if (useTerrain && layer != EnvironmentalLayer.Humidity) {
                    SjcTerrainOverlay.generate(
                        resources, bounds, points, layer, effectiveMin, effectiveMax, requireNotNull(terrainTreatment)
                    )
                } else HeatmapGenerator.generateBitmap(
                    points = points,
                    bounds = bounds,
                    palette = palette,
                    gridWidth = gridWidth,
                    valueMin = effectiveMin,
                    valueMax = effectiveMax,
                    radiusDegrees = radiusDegrees,
                    alpha = if (useTerrain) minOf(heatmapAlpha, 89) else heatmapAlpha,
                    valueSelector = { it.valueFor(layer) }
                )
                rendered.asImageBitmap()
            }
        }
    }

    var transform by rememberSaveable(imageResId, bounds, stateSaver = MapTransform.Saver) {
        mutableStateOf(MapTransform())
    }
    val textMeasurer = rememberTextMeasurer()
    val labelStyle = MaterialTheme.typography.labelSmall.copy(
        color = Color.White,
        shadow = Shadow(Color.Black, blurRadius = 5f)
    )

    val labels = points.map { point ->
        val reading = point.valueFor(layer)
        if (layer == EnvironmentalLayer.Temperature) {
            stringResource(R.string.climate_map_temperature, reading)
        } else {
            stringResource(R.string.climate_map_percent, reading)
        }
    }
    val layerName = stringResource(layer.labelResId)
    val readings = points.mapIndexed { index, point -> "${point.name}: ${labels[index]}" }.joinToString("; ")
    val description = stringResource(R.string.climate_map_layer_description, layerName, readings)
    val satelliteFilter = remember { discreteSatelliteColorFilter() }
    val colors = MaterialTheme.colorScheme
    val pinPainter = painterResource(R.drawable.map_pin_filled)

    BoxWithConstraints(
        modifier = modifier.fillMaxWidth().clipToBounds()
            .semantics { contentDescription = description },
        contentAlignment = Alignment.Center
    ) {
        // Cover the viewport with the same aspect ratio for all three map layers.
        val viewportHeight = if (constraints.hasBoundedHeight) maxHeight else maxWidth / bounds.aspectRatio
        val imageWidth = maxOf(maxWidth, viewportHeight * bounds.aspectRatio)
        val imageHeight = imageWidth / bounds.aspectRatio
        val density = LocalDensity.current
        val viewportSize = with(density) { Size(maxWidth.toPx(), viewportHeight.toPx()) }
        val imageSize = with(density) { Size(imageWidth.toPx(), imageHeight.toPx()) }
        val currentTransform = transform.constrained(viewportSize, imageSize)
        LaunchedEffect(viewportSize, imageSize) {
            transform = transform.constrained(viewportSize, imageSize)
        }

        var lastFramedRequestId by rememberSaveable(imageResId, bounds) {
            mutableStateOf<String?>(null)
        }
        LaunchedEffect(fitRequest?.id, viewportSize, imageSize) {
            val req = fitRequest
            if (req != null && req.coordinates.isNotEmpty() && req.id != lastFramedRequestId &&
                viewportSize.width > 0f && viewportSize.height > 0f && imageSize.width > 0f && imageSize.height > 0f
            ) {
                transform = calculateFitTransform(
                    coordinates = req.coordinates,
                    bounds = bounds,
                    viewportSize = viewportSize,
                    imageSize = imageSize,
                    padding = req.padding,
                    density = density
                )
                lastFramedRequestId = req.id
            } else if (req == null && lastFramedRequestId != null) {
                lastFramedRequestId = null
            }
        }

        Box(
            modifier = Modifier.fillMaxWidth().height(viewportHeight).then(
                if (gesturesEnabled) Modifier.pointerInput(viewportSize, imageSize) {
                    detectTransformGestures { centroid, pan, zoom, _ ->
                        transform = transform.transformed(viewportSize, imageSize, centroid, pan, zoom)
                    }
                } else Modifier
            ),
            contentAlignment = Alignment.Center
        ) {
            Box(
                Modifier.requiredSize(imageWidth, imageHeight).graphicsLayer {
                    scaleX = currentTransform.scale
                    scaleY = currentTransform.scale
                    translationX = currentTransform.offset.x
                    translationY = currentTransform.offset.y
                }
            ) {
                Image(
                    painter = painterResource(imageResId),
                    contentDescription = null,
                    modifier = Modifier.fillMaxSize(),
                    contentScale = ContentScale.FillBounds,
                    colorFilter = satelliteFilter
                )
                Canvas(Modifier.fillMaxSize()) {
                    bitmap?.let {
                        drawImage(
                            image = it,
                            dstSize = IntSize(size.width.roundToInt(), size.height.roundToInt()),
                            filterQuality = FilterQuality.High
                        )
                    }
                    if (showMarkers) {
                        points.forEachIndexed { index, point ->
                            val normalized = bounds.normalized(point.latitude, point.longitude)
                            if (normalized.x !in 0f..1f || normalized.y !in 0f..1f) return@forEachIndexed
                            val position = Offset(normalized.x * size.width, normalized.y * size.height)
                            val pointValue = point.valueFor(layer)
                            drawCircle(Color.Black, 7.dp.toPx(), position)
                            drawCircle(
                                Color(
                                    HeatmapGenerator.bandColor(
                                        pointValue, palette, effectiveMin, effectiveMax, 255
                                    )
                                ),
                                5.dp.toPx(),
                                position
                            )
                            drawCircle(Color.White, 5.dp.toPx(), position, style = Stroke(2.dp.toPx()))
                            if (showLabels) {
                                val text = textMeasurer.measure(labels[index], labelStyle)
                                val textX = (position.x - text.size.width / 2f)
                                    .coerceIn(0f, (size.width - text.size.width).coerceAtLeast(0f))
                                drawText(
                                    text,
                                    topLeft = Offset(
                                        textX,
                                        (position.y - 10.dp.toPx() - text.size.height).coerceAtLeast(0f)
                                    )
                                )
                            }
                        }
                    }
                    if (routes.isNotEmpty()) {
                        val sortedRoutes = routes.sortedBy { it.id == selectedRouteId }
                        sortedRoutes.forEach { route ->
                            val isSelected = route.id == selectedRouteId
                            val path = Path()
                            var isFirst = true
                            route.points.forEach { pt ->
                                val normalized = bounds.normalized(pt.latitude, pt.longitude)
                                val x = normalized.x * size.width
                                val y = normalized.y * size.height
                                if (isFirst) {
                                    path.moveTo(x, y)
                                    isFirst = false
                                } else {
                                    path.lineTo(x, y)
                                }
                            }
                            val baseColor = when (route.id) {
                                "fresca" -> colors.primary
                                "rapida" -> colors.secondary
                                else -> colors.primary
                            }
                            val strokeWidth = if (isSelected) 6.dp.toPx() else 4.dp.toPx()
                            val outlineWidth = strokeWidth + 2.dp.toPx()
                            val innerAlpha = if (isSelected) 1.0f else 0.55f
                            val innerColor = baseColor.copy(alpha = innerAlpha)

                            // Outer thin outline in background color (#07171C, 1 dp each side = +2 dp total)
                            drawPath(
                                path = path,
                                color = colors.background,
                                style = Stroke(
                                    width = outlineWidth,
                                    cap = StrokeCap.Round,
                                    join = StrokeJoin.Round
                                )
                            )
                            // Inner colored stroke (primary / secondary with alpha)
                            drawPath(
                                path = path,
                                color = innerColor,
                                style = Stroke(
                                    width = strokeWidth,
                                    cap = StrokeCap.Round,
                                    join = StrokeJoin.Round
                                )
                            )
                        }
                    }
                    if (markers.isNotEmpty()) {
                        markers.forEach { marker ->
                            val normalized = bounds.normalized(marker.coordinate.latitude, marker.coordinate.longitude)
                            if (normalized.x in -0.2f..1.2f && normalized.y in -0.2f..1.2f) {
                                val pos = Offset(normalized.x * size.width, normalized.y * size.height)
                                if (marker.isOrigin) {
                                    // Simulated user GPS origin: radar glow, white halo ring, vibrant blue core
                                    drawCircle(Color(0x332196F3), 16.dp.toPx(), pos)
                                    drawCircle(Color.White, 9.dp.toPx(), pos)
                                    drawCircle(Color(0xFF2196F3), 6.dp.toPx(), pos)
                                } else {
                                    // Destination marker: map_pin_filled vector of 28 dp, anchored at the tip (x=12/24, y=22/24)
                                    val pinSizePx = 28.dp.toPx()
                                    val pinLeft = pos.x - pinSizePx / 2f
                                    val pinTop = pos.y - pinSizePx * (22f / 24f)
                                    translate(left = pinLeft, top = pinTop) {
                                        with(pinPainter) {
                                            draw(
                                                size = Size(pinSizePx, pinSizePx),
                                                colorFilter = ColorFilter.tint(UrbanMapPinRed)
                                            )
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        if (showLegend) {
            EnvironmentalLegend(
                valueMin = effectiveMin,
                valueMax = effectiveMax,
                palette = palette,
                isTemperature = layer == EnvironmentalLayer.Temperature,
                modifier = Modifier.align(Alignment.BottomCenter).padding(12.dp)
            )
        }
    }
}

/** Reduces saturation and softens contrast so the satellite layer stays unobtrusive. */
internal fun discreteSatelliteColorFilter(): ColorFilter {
    val sat = 0.35f
    val contrast = 0.8f
    val invSat = 1f - sat
    val r = 0.213f * invSat
    val g = 0.715f * invSat
    val b = 0.072f * invSat
    val offset = (1f - contrast) * 64f
    return ColorFilter.colorMatrix(
        ColorMatrix(
            floatArrayOf(
                (r + sat) * contrast, g * contrast, b * contrast, 0f, offset,
                r * contrast, (g + sat) * contrast, b * contrast, 0f, offset,
                r * contrast, g * contrast, (b + sat) * contrast, 0f, offset,
                0f, 0f, 0f, 1f, 0f
            )
        )
    )
}

@Composable
private fun EnvironmentalLegend(
    valueMin: Float,
    valueMax: Float,
    palette: HeatmapPalette,
    isTemperature: Boolean,
    modifier: Modifier = Modifier
) {
    val colors = remember(palette) {
        palette.bands.map { Color(it) }
    }
    Column(
        modifier.fillMaxWidth()
            .background(MaterialTheme.colorScheme.surface.copy(alpha = 0.9f), RoundedCornerShape(12.dp))
            .padding(horizontal = 12.dp, vertical = 8.dp),
        verticalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        Box(
            Modifier.fillMaxWidth().height(10.dp).clip(RoundedCornerShape(5.dp))
                .background(Brush.horizontalGradient(colors))
        )
        Box(Modifier.fillMaxWidth()) {
            val middle = valueMin + (valueMax - valueMin) / 2f
            listOf(
                valueMin to Alignment.CenterStart,
                middle to Alignment.Center,
                valueMax to Alignment.CenterEnd
            ).forEach { (value, alignment) ->
                val text = if (isTemperature) {
                    stringResource(R.string.climate_map_temperature, value)
                } else {
                    stringResource(R.string.climate_map_percent, value)
                }
                Text(
                    text,
                    modifier = Modifier.align(alignment),
                    color = MaterialTheme.colorScheme.onSurface,
                    style = MaterialTheme.typography.labelSmall
                )
            }
        }
    }
}

/**
 * Calculates a [MapTransform] that fits the given [coordinates] inside the useful viewport area
 * determined by [padding], respecting the constraints of the map image projection.
 */
internal fun calculateFitTransform(
    coordinates: List<MapCoordinate>,
    bounds: MapBounds,
    viewportSize: Size,
    imageSize: Size,
    padding: MapViewportPadding,
    density: Density
): MapTransform {
    if (coordinates.isEmpty()) return MapTransform().constrained(viewportSize, imageSize)

    val normCoords = coordinates.map { bounds.normalized(it.latitude, it.longitude) }
    val minNormX = normCoords.minOf { it.x }
    val maxNormX = normCoords.maxOf { it.x }
    val minNormY = normCoords.minOf { it.y }
    val maxNormY = normCoords.maxOf { it.y }

    val minImgX = (minNormX - 0.5f) * imageSize.width
    val maxImgX = (maxNormX - 0.5f) * imageSize.width
    val minImgY = (minNormY - 0.5f) * imageSize.height
    val maxImgY = (maxNormY - 0.5f) * imageSize.height

    val boxImgWidth = (maxImgX - minImgX).coerceAtLeast(1f)
    val boxImgHeight = (maxImgY - minImgY).coerceAtLeast(1f)
    val boxCenterImgX = (minImgX + maxImgX) / 2f
    val boxCenterImgY = (minImgY + maxImgY) / 2f

    val paddingStartPx = with(density) { padding.start.toPx() }
    val paddingEndPx = with(density) { padding.end.toPx() }
    val paddingTopPx = with(density) { padding.top.toPx() }
    val paddingBottomPx = with(density) { padding.bottom.toPx() }

    val usefulWidth = (viewportSize.width - paddingStartPx - paddingEndPx).coerceAtLeast(10f)
    val usefulHeight = (viewportSize.height - paddingTopPx - paddingBottomPx).coerceAtLeast(10f)

    val scaleToFit = minOf(usefulWidth / boxImgWidth, usefulHeight / boxImgHeight).coerceIn(1f, 6f)

    val targetCenterX = paddingStartPx + usefulWidth / 2f
    val targetCenterY = paddingTopPx + usefulHeight / 2f

    val viewportCenterX = viewportSize.width / 2f
    val viewportCenterY = viewportSize.height / 2f

    val targetOffsetX = targetCenterX - viewportCenterX - boxCenterImgX * scaleToFit
    val targetOffsetY = targetCenterY - viewportCenterY - boxCenterImgY * scaleToFit

    return MapTransform(scaleToFit, Offset(targetOffsetX, targetOffsetY)).constrained(viewportSize, imageSize)
}

