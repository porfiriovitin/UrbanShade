package app.urbanshade.ui.map

import app.urbanshade.ui.components.map.ClimatePoint
import app.urbanshade.ui.components.map.MapBounds

// Bounds supplied for the bundled 2048 x 2048 image; kept outside the reusable map.
internal val SjcMapBounds = MapBounds(
    latitudeMin = -23.241161,
    latitudeMax = -23.211161,
    longitudeMin = -45.901925,
    longitudeMax = -45.871925,
    aspectRatio = 1f
)

// Illustrative readings for all three environmental layers (temperature, tree cover, humidity).
internal val DemoClimatePoints = listOf(
    ClimatePoint("shopping_vale_sul", "Vale Sul / Centro Comercial", -23.2185, -45.8935, 34.8f, treeCoverPercent = 12f, relativeHumidityPercent = 38f),
    ClimatePoint("jd_satelite", "Jardim Satélite", -23.2290, -45.8855, 33.2f, treeCoverPercent = 24f, relativeHumidityPercent = 44f),
    ClimatePoint("dutra_eixo", "Rodovia Presidente Dutra", -23.2140, -45.8890, 35.5f, treeCoverPercent = 8f, relativeHumidityPercent = 32f),
    ClimatePoint("parque_ribeirao", "Ribeirão Vidoca", -23.2255, -45.8995, 22.4f, treeCoverPercent = 68f, relativeHumidityPercent = 76f),
    ClimatePoint("reserva_sudeste", "Mata Nativa Sudeste", -23.2385, -45.8725, 18.5f, treeCoverPercent = 92f, relativeHumidityPercent = 88f),
    ClimatePoint("bosque_sul", "Bosque dos Eucaliptos", -23.2360, -45.8920, 24.0f, treeCoverPercent = 55f, relativeHumidityPercent = 65f)
)
