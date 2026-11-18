package app.urbanshade.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import app.urbanshade.R
import app.urbanshade.ui.theme.UrbanSpacing

@Composable
internal fun PageHeader(
    title: String,
    description: String? = null,
    onBack: () -> Unit,
    modifier: Modifier = Modifier
) {
    val colors = MaterialTheme.colorScheme
    Column(
        modifier = modifier
            .fillMaxWidth()
            .background(colors.primary)
            .padding(vertical = if (!description.isNullOrBlank()) 24.dp else 16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        Row(Modifier.fillMaxWidth().padding(horizontal = 12.dp), verticalAlignment = Alignment.CenterVertically) {
            IconButton(onClick = onBack, modifier = Modifier.size(44.dp)) {
                Icon(painterResource(R.drawable.ic_navigate_before), stringResource(R.string.navigate_back),
                    Modifier.size(24.dp), tint = colors.onPrimary)
            }
            Text(title, Modifier.weight(1f).semantics { heading() }, color = colors.onPrimary,
                style = MaterialTheme.typography.headlineLarge, textAlign = TextAlign.Center)
            Spacer(Modifier.width(44.dp))
        }
        if (!description.isNullOrBlank()) {
            Text(description, Modifier.fillMaxWidth().padding(horizontal = UrbanSpacing.Page),
                color = colors.onPrimary, style = MaterialTheme.typography.bodyLarge, textAlign = TextAlign.Center)
        }
    }
}
