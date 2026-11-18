package app.urbanshade.ui.theme

import app.urbanshade.ui.components.map.EnvironmentalLayer
import app.urbanshade.ui.components.map.HeatmapPalette

// Five intensity bands per environmental layer, from lowest to highest value.
// Opacity (~80%) is applied by the generator / ClimateMap.
internal val TemperaturePalette = HeatmapPalette(
    listOf(
        0xFF2166AC.toInt(), // Azul
        0xFF67A9CF.toInt(), // Azul claro
        0xFFFEE08B.toInt(), // Amarelo
        0xFFF46D43.toInt(), // Laranja
        0xFFD73027.toInt()  // Vermelho
    )
)

internal val TreeCoverPalette = HeatmapPalette(
    listOf(
        0xFFEDE0C8.toInt(), // Bege
        0xFFC5D89D.toInt(), // Bege-esverdeado
        0xFF7EBA68.toInt(), // Verde médio
        0xFF388E3C.toInt(), // Verde forte
        0xFF1B5E20.toInt()  // Verde escuro
    )
)

internal val HumidityPalette = HeatmapPalette(
    listOf(
        0xFFE1F5FE.toInt(), // Azul claro
        0xFF81D4FA.toInt(), // Azul celeste
        0xFF29B6F6.toInt(), // Azul médio
        0xFF1E88E5.toInt(), // Azul royal
        0xFF0D47A1.toInt()  // Azul escuro
    )
)

internal val UrbanHeatmapPalette = TemperaturePalette

internal fun paletteFor(layer: EnvironmentalLayer): HeatmapPalette = when (layer) {
    EnvironmentalLayer.Temperature -> TemperaturePalette
    EnvironmentalLayer.TreeCover -> TreeCoverPalette
    EnvironmentalLayer.Humidity -> HumidityPalette
}
