package app.urbanshade.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color

private val DarkColorScheme = darkColorScheme(
    primary = UrbanLime,
    onPrimary = AuthBackground,
    secondary = UrbanMist,
    onSecondary = UrbanInk,
    background = AuthBackground,
    onBackground = UrbanMist,
    surface = UrbanSurface,
    onSurface = UrbanMist,
    surfaceVariant = UrbanSurface,
    onSurfaceVariant = UrbanMuted,
    outline = UrbanOutline,
    error = UrbanHeat
)

private val LightColorScheme = lightColorScheme(
    primary = UrbanInk,
    onPrimary = UrbanMist,
    secondary = UrbanLime,
    onSecondary = UrbanInk,
    background = UrbanMist,
    onBackground = UrbanInk,
    surface = Color.White,
    onSurface = UrbanInk,
    surfaceVariant = Color(0xFFF4F9F8),
    onSurfaceVariant = UrbanOutline,
    outline = UrbanMuted,
    error = Color(0xFFB3261E)
)

@Composable
fun UrbanShadeTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit
) {
    MaterialTheme(
        colorScheme = if (darkTheme) DarkColorScheme else LightColorScheme,
        typography = UrbanShadeTypography,
        content = content
    )
}
