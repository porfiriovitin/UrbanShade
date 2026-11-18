package app.urbanshade.ui.components

import androidx.compose.foundation.Canvas
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.Path
import androidx.compose.ui.graphics.PathEffect
import androidx.compose.ui.graphics.StrokeCap
import androidx.compose.ui.graphics.StrokeJoin
import androidx.compose.ui.graphics.drawscope.Stroke
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import app.urbanshade.R
import app.urbanshade.ui.theme.UrbanHeat
import app.urbanshade.ui.theme.UrbanLime
import app.urbanshade.ui.theme.UrbanSpacing
import app.urbanshade.ui.theme.UrbanWarning

/**
 * High-fidelity, accessible hourly series chart matching Figma node 129:589.
 * Features a smooth cubic Bézier curve, soft vertical gradient area fill,
 * crisp reference threshold line, and optional dual-series comparison with legend.
 */
@Composable
internal fun HourlySeriesChart(
    title: String,
    hours: List<String>,
    primarySeries: List<Double>,
    primaryColor: Color,
    modifier: Modifier = Modifier,
    primaryName: String? = null,
    secondarySeries: List<Double>? = null,
    secondaryColor: Color? = null,
    secondaryName: String? = null,
    yMin: Double = 0.0,
    yMax: Double = 100.0,
    referenceValue: Double? = null,
    referenceLabel: String? = null,
    unit: String = "",
    accessibleDescription: String = ""
) {
    val colors = MaterialTheme.colorScheme

    Column(
        modifier = modifier
            .fillMaxWidth()
            .background(colors.surface, RoundedCornerShape(16.dp))
            .padding(UrbanSpacing.Medium),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Text(
            text = title,
            style = MaterialTheme.typography.bodyLarge,
            color = colors.onSurface
        )

        // Comparison legend (when secondary series is present)
        if (secondarySeries != null && primaryName != null && secondaryName != null) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = 2.dp, bottom = 4.dp),
                horizontalArrangement = Arrangement.spacedBy(14.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Primary legend (Atual)
                Row(
                    horizontalArrangement = Arrangement.spacedBy(5.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Canvas(Modifier.size(16.dp, 8.dp)) {
                        drawLine(
                            color = primaryColor,
                            start = Offset(0f, size.height / 2),
                            end = Offset(size.width, size.height / 2),
                            strokeWidth = 2.5.dp.toPx(),
                            cap = StrokeCap.Round
                        )
                    }
                    Text(
                        text = primaryName,
                        style = MaterialTheme.typography.labelSmall,
                        color = colors.onSurfaceVariant
                    )
                }

                // Secondary legend (Com arborização)
                Row(
                    horizontalArrangement = Arrangement.spacedBy(5.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    val secColor = secondaryColor ?: UrbanLime
                    Canvas(Modifier.size(16.dp, 8.dp)) {
                        drawLine(
                            color = secColor,
                            start = Offset(0f, size.height / 2),
                            end = Offset(size.width, size.height / 2),
                            strokeWidth = 2.5.dp.toPx(),
                            pathEffect = PathEffect.dashPathEffect(floatArrayOf(6f, 4f), 0f),
                            cap = StrokeCap.Round
                        )
                    }
                    Text(
                        text = secondaryName,
                        style = MaterialTheme.typography.labelSmall,
                        color = colors.onSurfaceVariant
                    )
                }

                // Reference legend (Meta 36 °C)
                if (referenceValue != null) {
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(5.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Canvas(Modifier.size(16.dp, 8.dp)) {
                            drawLine(
                                color = UrbanWarning.copy(alpha = 0.85f),
                                start = Offset(0f, size.height / 2),
                                end = Offset(size.width, size.height / 2),
                                strokeWidth = 1.2.dp.toPx()
                            )
                        }
                        Text(
                            text = stringResource(R.string.chart_legend_target),
                            style = MaterialTheme.typography.labelSmall,
                            color = UrbanWarning
                        )
                    }
                }
            }
        }

        BoxWithConstraints(
            modifier = Modifier
                .fillMaxWidth()
                .height(132.dp)
                .semantics { contentDescription = accessibleDescription }
        ) {
            val plotLeftDp = 40.dp
            val plotRightDp = maxWidth - 10.dp
            val plotWidthDp = (plotRightDp - plotLeftDp).coerceAtLeast(0.dp)
            val plotTopDp = 6.dp
            val plotBottomDp = 104.dp
            val plotHeightDp = plotBottomDp - plotTopDp

            Canvas(Modifier.fillMaxSize()) {
                val plotLeft = plotLeftDp.toPx()
                val plotRight = plotRightDp.toPx()
                val plotTop = plotTopDp.toPx()
                val plotBottom = plotBottomDp.toPx()
                val plotWidth = plotRight - plotLeft
                val plotHeight = plotBottom - plotTop

                fun getX(index: Int): Float {
                    val step = if (hours.size > 1) plotWidth / (hours.size - 1) else 0f
                    return plotLeft + index * step
                }

                fun getY(value: Double): Float {
                    val norm = if (yMax > yMin) ((value - yMin) / (yMax - yMin)).coerceIn(0.0, 1.0) else 0.0
                    return (plotBottom - norm * plotHeight).toFloat()
                }

                // Reference threshold line (e.g. 36 °C thermal target)
                if (referenceValue != null) {
                    val refY = getY(referenceValue)
                    drawLine(
                        color = UrbanWarning.copy(alpha = 0.85f),
                        start = Offset(plotLeft, refY),
                        end = Offset(plotRight, refY),
                        strokeWidth = 1.2.dp.toPx()
                    )
                }

                // Secondary series (e.g. Com arborização in Smart Analysis)
                if (secondarySeries != null && secondarySeries.isNotEmpty()) {
                    val secColor = secondaryColor ?: UrbanLime
                    val secPoints = secondarySeries.mapIndexed { i, v -> Offset(getX(i), getY(v)) }
                    val secCurvePath = buildSmoothPath(secPoints)
                    val secAreaPath = buildAreaPath(secPoints, secCurvePath, plotBottom)

                    drawPath(
                        path = secAreaPath,
                        brush = Brush.verticalGradient(
                            colors = listOf(
                                secColor.copy(alpha = 0.12f),
                                secColor.copy(alpha = 0.01f)
                            ),
                            startY = plotTop,
                            endY = plotBottom
                        )
                    )

                    drawPath(
                        path = secCurvePath,
                        color = secColor,
                        style = Stroke(
                            width = 2.5.dp.toPx(),
                            cap = StrokeCap.Round,
                            join = StrokeJoin.Round,
                            pathEffect = PathEffect.dashPathEffect(floatArrayOf(10f, 6f), 0f)
                        )
                    )
                }

                // Primary series (smooth curve + gradient area fill matching Figma)
                if (primarySeries.isNotEmpty()) {
                    val points = primarySeries.mapIndexed { i, v -> Offset(getX(i), getY(v)) }
                    val curvePath = buildSmoothPath(points)
                    val areaPath = buildAreaPath(points, curvePath, plotBottom)

                    drawPath(
                        path = areaPath,
                        brush = Brush.verticalGradient(
                            colors = listOf(
                                primaryColor.copy(alpha = 0.20f),
                                primaryColor.copy(alpha = 0.02f)
                            ),
                            startY = plotTop,
                            endY = plotBottom
                        )
                    )

                    drawPath(
                        path = curvePath,
                        color = primaryColor,
                        style = Stroke(
                            width = 2.5.dp.toPx(),
                            cap = StrokeCap.Round,
                            join = StrokeJoin.Round
                        )
                    )
                }
            }

            // Reference label on Y-axis
            if (referenceValue != null) {
                val refFraction = ((referenceValue - yMin) / (yMax - yMin)).coerceIn(0.0, 1.0)
                val refYDp = plotTopDp + plotHeightDp * (1.0 - refFraction).toFloat()
                Text(
                    text = referenceLabel ?: "${referenceValue.toInt()} $unit",
                    modifier = Modifier
                        .offset(x = 0.dp, y = refYDp - 8.dp)
                        .width(36.dp),
                    style = MaterialTheme.typography.labelSmall,
                    color = UrbanWarning,
                    textAlign = TextAlign.Right
                )
            } else {
                // Min / Max labels when no reference line (e.g. humidity 0..100%)
                Text(
                    text = "${yMax.toInt()}$unit",
                    modifier = Modifier
                        .offset(x = 0.dp, y = plotTopDp - 6.dp)
                        .width(36.dp),
                    style = MaterialTheme.typography.labelSmall,
                    color = colors.onSurfaceVariant,
                    textAlign = TextAlign.Right
                )
                Text(
                    text = "${yMin.toInt()}$unit",
                    modifier = Modifier
                        .offset(x = 0.dp, y = plotBottomDp - 8.dp)
                        .width(36.dp),
                    style = MaterialTheme.typography.labelSmall,
                    color = colors.onSurfaceVariant,
                    textAlign = TextAlign.Right
                )
            }

            // X-axis hour labels matching Figma geometry
            val stepDp = if (hours.size > 1) plotWidthDp / (hours.size - 1) else 0.dp
            hours.forEachIndexed { index, hour ->
                val xCenter = plotLeftDp + stepDp * index
                val labelWidth = 34.dp
                Text(
                    text = hour,
                    modifier = Modifier
                        .offset(x = xCenter - labelWidth / 2, y = 112.dp)
                        .width(labelWidth),
                    style = MaterialTheme.typography.labelSmall,
                    color = colors.onSurfaceVariant,
                    textAlign = TextAlign.Center
                )
            }
        }
    }
}

/** Builds a smooth cubic Bézier curve connecting points with Catmull-Rom tangents. */
private fun buildSmoothPath(points: List<Offset>): Path {
    val path = Path()
    if (points.isEmpty()) return path
    path.moveTo(points[0].x, points[0].y)
    if (points.size == 1) return path

    for (i in 0 until points.size - 1) {
        val p0 = if (i > 0) points[i - 1] else points[i]
        val p1 = points[i]
        val p2 = points[i + 1]
        val p3 = if (i + 2 < points.size) points[i + 2] else p2

        val tension = 0.35f
        val cp1X = p1.x + (p2.x - p0.x) * tension
        val cp1Y = p1.y + (p2.y - p0.y) * tension
        val cp2X = p2.x - (p3.x - p1.x) * tension
        val cp2Y = p2.y - (p3.y - p1.y) * tension

        path.cubicTo(cp1X, cp1Y, cp2X, cp2Y, p2.x, p2.y)
    }
    return path
}

/** Closes the curve path into an area down to the plot bottom for gradient shading. */
private fun buildAreaPath(points: List<Offset>, curvePath: Path, bottomY: Float): Path {
    val areaPath = Path()
    if (points.isEmpty()) return areaPath
    areaPath.addPath(curvePath)
    areaPath.lineTo(points.last().x, bottomY)
    areaPath.lineTo(points.first().x, bottomY)
    areaPath.close()
    return areaPath
}

/** Legacy / default wrapper for TemperatureChart */
@Composable
internal fun TemperatureChart(modifier: Modifier = Modifier) {
    val defaultHours = listOf("06h", "09h", "12h", "15h", "18h")
    val defaultTemps = listOf(27.0, 32.4, 37.4, 39.2, 34.0)
    HourlySeriesChart(
        title = stringResource(R.string.chart_temperature_title),
        hours = defaultHours,
        primarySeries = defaultTemps,
        primaryColor = MaterialTheme.colorScheme.primary,
        yMin = 18.0,
        yMax = 42.0,
        referenceValue = 36.0,
        referenceLabel = stringResource(R.string.chart_critical_value),
        unit = "°C",
        accessibleDescription = stringResource(R.string.chart_description),
        modifier = modifier
    )
}
