package app.urbanshade.ui.map

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.ElevatedButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import app.urbanshade.ui.components.map.EnvironmentalLayer
import app.urbanshade.ui.theme.UrbanSpacing
import app.urbanshade.ui.theme.UrbanSurface
import app.urbanshade.ui.theme.UrbanMotion

/**
 * Vertical layer selector stacked on the right edge of the screen: [Temp] [Arb] [Umi].
 * Supports toggle behavior: clicking the active button deselects it, returning null (pure satellite).
 */
@Composable
internal fun EnvironmentalLayerSelector(
    selectedLayer: EnvironmentalLayer?,
    onLayerSelected: (EnvironmentalLayer?) -> Unit,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier,
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Small)
    ) {
        EnvironmentalLayer.entries.forEach { layer ->
            val isSelected = layer == selectedLayer
            val colors = MaterialTheme.colorScheme
            val container by animateColorAsState(if (isSelected) colors.primary else UrbanSurface,
                tween(UrbanMotion.Selection), label = "Layer background")
            val content by animateColorAsState(if (isSelected) colors.onPrimary else colors.onSurface,
                tween(UrbanMotion.Selection), label = "Layer content")
            val description = stringResource(layer.contentDescriptionResId)
            ElevatedButton(
                onClick = { onLayerSelected(if (isSelected) null else layer) },
                modifier = Modifier
                    .width(52.dp)
                    .height(36.dp)
                    .semantics { contentDescription = description },
                colors = ButtonDefaults.elevatedButtonColors(
                    containerColor = container,
                    contentColor = content
                ),
                contentPadding = PaddingValues(0.dp),
                shape = RoundedCornerShape(8.dp),
                elevation = ButtonDefaults.elevatedButtonElevation(defaultElevation = 2.dp, pressedElevation = 4.dp)
            ) {
                Text(
                    text = stringResource(layer.shortLabelResId),
                    style = MaterialTheme.typography.labelSmall.copy(fontWeight = FontWeight.Bold),
                    textAlign = TextAlign.Center,
                    maxLines = 1
                )
            }
        }
    }
}
