package app.urbanshade.ui.components.map

import androidx.compose.ui.geometry.Offset
import app.urbanshade.ui.theme.HumidityPalette
import app.urbanshade.ui.theme.TemperaturePalette
import app.urbanshade.ui.theme.TreeCoverPalette
import app.urbanshade.ui.theme.UrbanHeatmapPalette
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertThrows
import org.junit.Assert.assertTrue
import org.junit.Test

class HeatmapGeneratorTest {
    private val bounds = MapBounds(0.0, 1.0, 0.0, 1.0)
    private val fiveBandPalette = HeatmapPalette(
        listOf(
            0xFF102D35.toInt(),
            0xFF3A5A60.toInt(),
            0xFFA8C2C6.toInt(),
            0xFFE5A8A8.toInt(),
            0xFFF27676.toInt()
        )
    )

    @Test
    fun coordinatesMapCornersAndRoundTrip() {
        assertEquals(Offset.Zero, bounds.normalized(1.0, 0.0))
        assertEquals(Offset(1f, 1f), bounds.normalized(0.0, 1.0))
        val (latitude, longitude) = bounds.coordinates(0.25f, 0.75f)
        assertEquals(Offset(0.25f, 0.75f), bounds.normalized(latitude, longitude))
        assertTrue(bounds.normalized(0.5, 2.0).x > 1f)
    }

    @Test
    fun classifyBandSplitsScaleIntoFiveEqualIntervalsAndClampsExtremes() {
        // Temperature scale: 10 to 40 °C (intervals of 6 °C)
        assertEquals(0, HeatmapGenerator.classifyBand(10f, 10f, 40f))
        assertEquals(0, HeatmapGenerator.classifyBand(15.9f, 10f, 40f))
        assertEquals(1, HeatmapGenerator.classifyBand(16f, 10f, 40f))
        assertEquals(1, HeatmapGenerator.classifyBand(21.9f, 10f, 40f))
        assertEquals(2, HeatmapGenerator.classifyBand(22f, 10f, 40f))
        assertEquals(2, HeatmapGenerator.classifyBand(27.9f, 10f, 40f))
        assertEquals(3, HeatmapGenerator.classifyBand(28f, 10f, 40f))
        assertEquals(3, HeatmapGenerator.classifyBand(33.9f, 10f, 40f))
        assertEquals(4, HeatmapGenerator.classifyBand(34f, 10f, 40f))
        assertEquals(4, HeatmapGenerator.classifyBand(40f, 10f, 40f))

        // Clamps extremes below min and above max
        assertEquals(0, HeatmapGenerator.classifyBand(-5f, 10f, 40f))
        assertEquals(4, HeatmapGenerator.classifyBand(55f, 10f, 40f))

        // Percentage scale: 0 to 100% (intervals of 20%)
        assertEquals(0, HeatmapGenerator.classifyBand(0f, 0f, 100f))
        assertEquals(0, HeatmapGenerator.classifyBand(19f, 0f, 100f))
        assertEquals(1, HeatmapGenerator.classifyBand(20f, 0f, 100f))
        assertEquals(2, HeatmapGenerator.classifyBand(40f, 0f, 100f))
        assertEquals(3, HeatmapGenerator.classifyBand(60f, 0f, 100f))
        assertEquals(4, HeatmapGenerator.classifyBand(80f, 0f, 100f))
        assertEquals(4, HeatmapGenerator.classifyBand(100f, 0f, 100f))
    }

    @Test
    fun bandColorAppliesEightyPercentOpacityAndSmoothsTransitions() {
        // Core of band 0 is solid band 0 color with ~80% alpha (204 = 0xCC)
        val band0 = HeatmapGenerator.bandColor(12f, fiveBandPalette, 10f, 40f)
        assertEquals(0xCC, band0 ushr 24)
        assertEquals(fiveBandPalette.bands[0] and 0x00FFFFFF, band0 and 0x00FFFFFF)

        // Core of band 4 is solid band 4 color
        val band4 = HeatmapGenerator.bandColor(38f, fiveBandPalette, 10f, 40f)
        assertEquals(0xCC, band4 ushr 24)
        assertEquals(fiveBandPalette.bands[4] and 0x00FFFFFF, band4 and 0x00FFFFFF)

        // Boundary between band 0 and 1 (16 °C): lightly smoothed, not equal to pure band 0 or 1
        val boundary = HeatmapGenerator.bandColor(16f, fiveBandPalette, 10f, 40f)
        assertNotEquals(band0 and 0x00FFFFFF, boundary and 0x00FFFFFF)
        assertEquals(0xCC, boundary ushr 24)
    }

    @Test
    fun palettesProvideFiveDistinctBandsForEveryEnvironmentalLayer() {
        assertEquals(5, TemperaturePalette.bands.size)
        assertEquals(5, TreeCoverPalette.bands.size)
        assertEquals(5, HumidityPalette.bands.size)

        // Temperature: blue (cold) to red (hot)
        val tempCold = TemperaturePalette.bands.first()
        val tempHot = TemperaturePalette.bands.last()
        assertTrue("Temperature lowest band should be blue-dominant", (tempCold and 0xFF) > ((tempCold ushr 16) and 0xFF))
        assertTrue("Temperature highest band should be red-dominant", ((tempHot ushr 16) and 0xFF) > (tempHot and 0xFF))

        // Tree Cover: beige to dark green
        val treeLow = TreeCoverPalette.bands.first()
        val treeHigh = TreeCoverPalette.bands.last()
        assertTrue("Tree cover highest band should be green-dominant", ((treeHigh ushr 8) and 0xFF) > (treeHigh and 0xFF))

        // Humidity: light blue to dark blue
        val humLow = HumidityPalette.bands.first()
        val humHigh = HumidityPalette.bands.last()
        assertTrue("Humidity lowest band should be very light", ((humLow ushr 16) and 0xFF) > 200)
        assertTrue("Humidity highest band should be deep dark blue", ((humHigh ushr 16) and 0xFF) < 30)
    }

    @Test
    fun customPaletteIsAppliedToGeneratedPixels() {
        val custom = HeatmapPalette(listOf(0xFF000000.toInt(), 0xFF444444.toInt(), 0xFF888888.toInt(), 0xFFCCCCCC.toInt(), 0xFFFFFFFF.toInt()))
        val pixels = HeatmapGenerator.generatePixels(listOf(point(0.5, 40f)), bounds, custom, 1, 1, alpha = 80)
        assertEquals(0x50FFFFFF, pixels.single())
    }

    @Test
    fun pixelGenerationInterpolatesMultipleSensorsAndMatchesExactReadings() {
        val points = listOf(point(0.25, 12f), point(0.75, 36f))
        val pixels = HeatmapGenerator.generatePixels(points, bounds, fiveBandPalette, gridWidth = 3, gridHeight = 1)
        assertEquals(HeatmapGenerator.bandColor(24f, fiveBandPalette, 10f, 40f), pixels[1])
        val exactPixels = HeatmapGenerator.generatePixels(points, bounds, fiveBandPalette, gridWidth = 2, gridHeight = 1)
        assertEquals(HeatmapGenerator.bandColor(12f, fiveBandPalette, 10f, 40f), exactPixels[0])
        assertEquals(HeatmapGenerator.bandColor(36f, fiveBandPalette, 10f, 40f), exactPixels[1])
    }

    @Test
    fun radiusFadesNearSensorAndIsTransparentOutside() {
        val pixels = HeatmapGenerator.generatePixels(listOf(point(0.5, 36f)), bounds, fiveBandPalette, 5, 1, radiusDegrees = 0.3, alpha = 200)
        assertEquals(0, pixels.first())
        assertEquals(0, pixels.last())
        assertEquals(200, pixels[2] ushr 24)
        assertTrue((pixels[1] ushr 24) in 1..199)
        assertTrue((pixels[3] ushr 24) in 1..199)
    }

    @Test
    fun emptyReadingsAndZeroAlphaProduceTransparentPixels() {
        assertTrue(HeatmapGenerator.generatePixels(emptyList(), bounds, fiveBandPalette, 3, 2).all { it == 0 })
        assertTrue(HeatmapGenerator.generatePixels(listOf(point(0.5, 36f)), bounds, fiveBandPalette, 3, 2, alpha = 0).all { it == 0 })
    }

    @Test
    fun gridRespectsImageAspectRatio() {
        val wideBounds = bounds.copy(aspectRatio = 2f)
        assertEquals(8, HeatmapGenerator.generatePixels(emptyList(), wideBounds, fiveBandPalette, gridWidth = 4).size)
    }

    @Test
    fun invalidBoundsAndReadingsFailAtTheBoundary() {
        assertThrows(IllegalArgumentException::class.java) { bounds.copy(latitudeMax = 0.0) }
        assertThrows(IllegalArgumentException::class.java) { bounds.copy(longitudeMin = Double.NaN) }
        assertThrows(IllegalArgumentException::class.java) { bounds.copy(aspectRatio = 0f) }
        assertThrows(IllegalArgumentException::class.java) { point(0.5, Float.NaN) }
        // Invalid percentages fail at boundary
        assertThrows(IllegalArgumentException::class.java) {
            ClimatePoint("invalid_tree", "Sensor", 0.5, 0.5, 25f, treeCoverPercent = -1f)
        }
        assertThrows(IllegalArgumentException::class.java) {
            ClimatePoint("invalid_tree", "Sensor", 0.5, 0.5, 25f, treeCoverPercent = 101f)
        }
        assertThrows(IllegalArgumentException::class.java) {
            ClimatePoint("invalid_hum", "Sensor", 0.5, 0.5, 25f, relativeHumidityPercent = -0.5f)
        }
        assertThrows(IllegalArgumentException::class.java) {
            ClimatePoint("invalid_hum", "Sensor", 0.5, 0.5, 25f, relativeHumidityPercent = 100.1f)
        }
    }

    @Test
    fun invalidRenderOptionsFailEvenWithoutReadings() {
        assertThrows(IllegalArgumentException::class.java) {
            HeatmapGenerator.generatePixels(emptyList(), bounds, fiveBandPalette, gridWidth = 0)
        }
        assertThrows(IllegalArgumentException::class.java) {
            HeatmapGenerator.generatePixels(emptyList(), bounds, fiveBandPalette, temperatureMax = 10f, temperatureMin = 10f)
        }
        assertThrows(IllegalArgumentException::class.java) {
            HeatmapGenerator.generatePixels(emptyList(), bounds, fiveBandPalette, radiusDegrees = -1.0)
        }
        assertThrows(IllegalArgumentException::class.java) {
            HeatmapGenerator.generatePixels(emptyList(), bounds, fiveBandPalette, alpha = 256)
        }
        assertThrows(IllegalArgumentException::class.java) {
            HeatmapGenerator.classifyBand(Float.NaN, 0f, 100f)
        }
    }

    private fun point(longitude: Double, temperature: Float) =
        ClimatePoint(longitude.toString(), "Sensor", 0.5, longitude, temperature)
}
