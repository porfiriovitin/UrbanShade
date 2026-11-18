package app.urbanshade.ui.components.map

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.size
import androidx.compose.runtime.mutableStateOf
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.toPixelMap
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.captureToImage
import androidx.compose.ui.test.getUnclippedBoundsInRoot
import androidx.compose.ui.test.junit4.StateRestorationTester
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.pinch
import androidx.compose.ui.test.swipe
import androidx.compose.ui.unit.dp
import androidx.test.platform.app.InstrumentationRegistry
import app.urbanshade.R
import app.urbanshade.ui.theme.HumidityPalette
import app.urbanshade.ui.theme.TreeCoverPalette
import app.urbanshade.ui.theme.UrbanHeatmapPalette
import app.urbanshade.ui.theme.UrbanShadeTheme
import kotlin.math.abs
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class ClimateMapTest {
    @get:Rule val compose = createComposeRule()
    private val bounds = MapBounds(0.0, 1.0, 0.0, 1.0)

    @Test
    fun changedReadingsAndPaletteUpdateTheRenderedLayer() {
        val points = mutableStateOf(emptyList<ClimatePoint>())
        val palette = mutableStateOf(UrbanHeatmapPalette)
        compose.setContent {
            UrbanShadeTheme {
                ClimateMap(
                    R.drawable.map_satellite_sjc, bounds, points.value,
                    Modifier.size(240.dp, 180.dp).testTag("map"),
                    palette = palette.value,
                    heatmapAlpha = 255, showMarkers = false, showLegend = false
                )
            }
        }
        val baseColor = centerColor("map")
        compose.runOnIdle { points.value = listOf(point(10f)) }
        awaitCenterColor("map", Color(UrbanHeatmapPalette.cold))
        compose.runOnIdle { points.value = listOf(point(40f)) }
        awaitCenterColor("map", Color(UrbanHeatmapPalette.hot))
        compose.runOnIdle { palette.value = palette.value.copy(hot = 0xFF0000FF.toInt()) }
        awaitCenterColor("map", Color.Blue)
        compose.runOnIdle { points.value = emptyList() }
        awaitCenterColor("map", baseColor)
    }

    @Test
    fun switchingEnvironmentalLayersUpdatesRenderedColors() {
        val layer = mutableStateOf(EnvironmentalLayer.Temperature)
        val testPoints = listOf(
            ClimatePoint("sensor", "Sensor", 0.5, 0.5, temperatureCelsius = 40f, treeCoverPercent = 95f, relativeHumidityPercent = 95f)
        )
        compose.setContent {
            UrbanShadeTheme {
                ClimateMap(
                    R.drawable.map_satellite_sjc, bounds, testPoints,
                    Modifier.size(240.dp, 180.dp).testTag("map"),
                    layer = layer.value,
                    heatmapAlpha = 255, showMarkers = false, showLegend = false
                )
            }
        }
        // Temperature layer hot is red
        awaitCenterColor("map", Color(UrbanHeatmapPalette.hot))

        // Switch to TreeCover layer (95% is high tree cover: dark green)
        compose.runOnIdle { layer.value = EnvironmentalLayer.TreeCover }
        awaitCenterColor("map", Color(TreeCoverPalette.bands.last()))

        // Switch to Humidity layer (95% is high humidity: deep dark blue)
        compose.runOnIdle { layer.value = EnvironmentalLayer.Humidity }
        awaitCenterColor("map", Color(HumidityPalette.bands.last()))
    }

    @Test
    fun twoInstancesUseIndependentBoundsAndTemperatureScales() {
        compose.setContent {
            UrbanShadeTheme {
                Column {
                    ClimateMap(
                        R.drawable.map_satellite_sjc, bounds, listOf(point(10f)),
                        Modifier.size(200.dp, 120.dp).testTag("first"),
                        temperatureMin = 0f, temperatureMax = 20f,
                        heatmapAlpha = 255, showMarkers = false, showLegend = false
                    )
                    ClimateMap(
                        R.drawable.map_satellite_sjc, MapBounds(-1.0, 0.0, -1.0, 0.0),
                        listOf(ClimatePoint("other", "Other sensor", -0.5, -0.5, 10f)),
                        Modifier.size(120.dp, 200.dp).testTag("second"),
                        heatmapAlpha = 255, showMarkers = false, showLegend = false
                    )
                }
            }
        }
        awaitCenterColor("first", Color(UrbanHeatmapPalette.middle))
        awaitCenterColor("second", Color(UrbanHeatmapPalette.cold))
    }

    @Test
    fun portraitViewportHasNoEmptyBands() {
        compose.setContent {
            UrbanShadeTheme {
                Box(Modifier.size(160.dp, 300.dp).testTag("viewport")) {
                    ClimateMap(
                        R.drawable.map_satellite_sjc, bounds, listOf(point(40f)),
                        Modifier.fillMaxSize(),
                        heatmapAlpha = 255, showMarkers = false, showLegend = false
                    )
                }
            }
        }
        awaitCenterColor("viewport", Color(UrbanHeatmapPalette.hot))
        val pixels = compose.onNodeWithTag("viewport").captureToImage().toPixelMap()
        listOf(0, pixels.height / 4, pixels.height * 3 / 4, pixels.height - 1).forEach { y ->
            val color = pixels[pixels.width / 2, y]
            assertTrue("The image and thermal layer must cover the entire viewport", color.matches(Color(UrbanHeatmapPalette.hot)))
        }
    }

    @Test
    fun gesturesMoveMarkersAndSurviveStateRestoration() {
        val restoration = StateRestorationTester(compose)
        restoration.setContent {
            UrbanShadeTheme {
                ClimateMap(
                    R.drawable.map_satellite_sjc, bounds, listOf(point(36f)),
                    Modifier.size(200.dp).testTag("map"),
                    palette = HeatmapPalette(0xFF000000.toInt(), 0xFF888888.toInt(), 0xFFFF0000.toInt()),
                    heatmapAlpha = 0, showLabels = false, showLegend = false
                )
            }
        }
        val before = redMarker()
        val map = compose.onNodeWithTag("map")
        map.performTouchInput {
            pinch(
                start0 = Offset(width * 0.4f, centerY), end0 = Offset(width * 0.2f, centerY),
                start1 = Offset(width * 0.6f, centerY), end1 = Offset(width * 0.8f, centerY)
            )
        }
        val zoomed = redMarker()
        assertTrue(zoomed.first > before.first * 2)
        assertEquals(before.second, zoomed.second, 3f)
        map.performTouchInput { swipe(center, Offset(width * 0.7f, centerY), durationMillis = 600) }
        val dragged = redMarker()
        assertTrue(dragged.second > zoomed.second + 20)
        restoration.emulateSavedInstanceStateRestore()
        val restored = redMarker()
        assertEquals(dragged.first, restored.first)
        assertEquals(dragged.second, restored.second, 1f)
    }

    @Test
    fun legendPlacesMiddleTemperatureAtTheCenter() {
        compose.setContent {
            UrbanShadeTheme {
                ClimateMap(
                    R.drawable.map_satellite_sjc,
                    bounds,
                    emptyList(),
                    Modifier.size(300.dp).testTag("map"),
                    showLegend = true
                )
            }
        }
        val context = InstrumentationRegistry.getInstrumentation().targetContext
        fun labelBounds(value: Float) = compose.onNodeWithText(context.getString(R.string.climate_map_temperature, value))
            .getUnclippedBoundsInRoot()
        val cold = labelBounds(10f)
        val middle = labelBounds(25f)
        val hot = labelBounds(40f)
        val map = compose.onNodeWithTag("map").getUnclippedBoundsInRoot()
        assertTrue(cold.right < middle.left && middle.right < hot.left)
        val difference = (map.left + map.right) / 2 - (middle.left + middle.right) / 2
        assertTrue(difference in (-1).dp..1.dp)
    }

    private fun awaitCenterColor(tag: String, expected: Color) {
        compose.waitUntil(5_000) {
            centerColor(tag).matches(expected)
        }
    }

    private fun Color.matches(expected: Color) =
        abs(red - expected.red) < 0.02f && abs(green - expected.green) < 0.02f && abs(blue - expected.blue) < 0.02f

    private fun centerColor(tag: String): Color {
        val pixels = compose.onNodeWithTag(tag).captureToImage().toPixelMap()
        return pixels[pixels.width / 2, pixels.height / 2]
    }

    private fun redMarker(): Pair<Int, Float> {
        val pixels = compose.onNodeWithTag("map").captureToImage().toPixelMap()
        var count = 0
        var totalX = 0L
        for (y in 0 until pixels.height) {
            for (x in 0 until pixels.width) {
                val pixel = pixels[x, y]
                if (pixel.red > 0.95f && pixel.green < 0.05f && pixel.blue < 0.05f) {
                    count++
                    totalX += x
                }
            }
        }
        assertTrue("The sensor marker should remain visible", count > 0)
        return count to totalX.toFloat() / count
    }

    private fun point(temperature: Float) = ClimatePoint("sensor", "Sensor", 0.5, 0.5, temperature)
}
