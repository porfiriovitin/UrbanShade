package app.urbanshade.data

import app.urbanshade.ui.components.map.MapCoordinate
import app.urbanshade.ui.components.map.MapMarker
import app.urbanshade.ui.components.map.MapRoute
import java.text.Normalizer

internal object UserNavigationFixtures {
    const val DestinationAddress = "Rua das Acácias, 120"
    const val DestinationSubtitle = "Jardim Satélite, São José dos Campos"

    // Fixed simulated walking origin and destination within SjcMapBounds
    val OriginCoordinate = MapCoordinate(latitude = -23.2248, longitude = -45.8892)
    val DestinationCoordinate = MapCoordinate(latitude = -23.2286, longitude = -45.8852)

    val OriginMarker = MapMarker(
        id = "mock_origin",
        coordinate = OriginCoordinate,
        title = "Sua localização",
        isOrigin = true
    )

    val DestinationMarker = MapMarker(
        id = "mock_destination",
        coordinate = DestinationCoordinate,
        title = DestinationAddress,
        isOrigin = false
    )

    // Mais fresca: 12 min · 850 m, com maior cobertura arbórea ilustrativa (primary no tema)
    val RouteFresca = MapRoute(
        id = "fresca",
        name = "Mais fresca",
        durationMinutes = 12,
        distanceMeters = 850,
        description = "Maior cobertura arbórea ilustrativa.",
        points = listOf(
            OriginCoordinate,
            MapCoordinate(-23.2244, -45.8884),
            MapCoordinate(-23.2242, -45.8872),
            MapCoordinate(-23.2246, -45.8860),
            MapCoordinate(-23.2256, -45.8850),
            MapCoordinate(-23.2268, -45.8844),
            MapCoordinate(-23.2278, -45.8846),
            DestinationCoordinate
        )
    )

    // Mais rápida: 9 min · 650 m, com maior exposição ilustrativa ao calor (secondary no tema)
    val RouteRapida = MapRoute(
        id = "rapida",
        name = "Mais rápida",
        durationMinutes = 9,
        distanceMeters = 650,
        description = "Maior exposição ilustrativa ao calor.",
        points = listOf(
            OriginCoordinate,
            MapCoordinate(-23.2255, -45.8885),
            MapCoordinate(-23.2265, -45.8875),
            MapCoordinate(-23.2274, -45.8865),
            MapCoordinate(-23.2282, -45.8857),
            DestinationCoordinate
        )
    )

    val AllRoutes = listOf(RouteFresca, RouteRapida)

    /**
     * Normalizes text by removing diacritical marks/accents and converting to lowercase.
     * Used for accent- and case-insensitive search queries.
     */
    fun normalizeForSearch(text: String): String =
        Normalizer.normalize(text, Normalizer.Form.NFD)
            .replace("\\p{Mn}+".toRegex(), "")
            .lowercase()
            .trim()

    /**
     * Matches the registered destination when [query] contains any matching fragment of the
     * address name or number, ignoring case and accents. Empty or non-matching query returns false.
     */
    fun matchesDestination(query: String): Boolean {
        val trimmed = query.trim()
        if (trimmed.isEmpty()) return false
        val normalizedQuery = normalizeForSearch(trimmed)
        val normalizedTarget = normalizeForSearch(DestinationAddress)
        return normalizedTarget.contains(normalizedQuery)
    }
}
