package app.urbanshade.ui.components

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import app.urbanshade.ui.theme.UrbanSpacing

@Composable
internal fun UrbanActionButton(
    label: String,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    secondary: Boolean = false
) {
    val colors = MaterialTheme.colorScheme
    Surface(onClick = onClick,
        modifier = modifier.fillMaxWidth().height(48.dp).semantics { role = Role.Button },
        shape = RoundedCornerShape(12.dp),
        color = if (secondary) colors.background else colors.primary,
        contentColor = if (secondary) colors.primary else colors.onPrimary,
        border = if (secondary) BorderStroke(1.dp, colors.primary) else null) {
        Box(Modifier.padding(horizontal = UrbanSpacing.Medium), contentAlignment = Alignment.Center) {
            Text(label, style = MaterialTheme.typography.labelLarge, textAlign = TextAlign.Center)
        }
    }
}
