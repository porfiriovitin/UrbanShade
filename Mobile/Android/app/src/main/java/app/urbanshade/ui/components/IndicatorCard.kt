package app.urbanshade.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Shape
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.Dp
import app.urbanshade.R

@Composable
internal fun IndicatorCard(
    label: String,
    value: String?,
    modifier: Modifier = Modifier,
    contentPadding: Dp = 12.dp,
    shape: Shape = RoundedCornerShape(12.dp),
    singleLine: Boolean = true
) {
    val missingValue = stringResource(R.string.indicator_no_data)
    Column(
        modifier.heightIn(min = 70.dp)
            .background(MaterialTheme.colorScheme.surface, shape)
            .padding(contentPadding),
        verticalArrangement = Arrangement.spacedBy(4.dp, Alignment.CenterVertically)
    ) {
        Text(
            label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            softWrap = !singleLine,
            maxLines = if (singleLine) 1 else Int.MAX_VALUE,
            overflow = if (singleLine) TextOverflow.Ellipsis else TextOverflow.Clip
        )
        Text(
            value ?: stringResource(R.string.indicator_no_data_symbol),
            modifier = if (value == null) {
                Modifier.semantics { contentDescription = missingValue }
            } else Modifier,
            style = MaterialTheme.typography.titleMedium,
            color = MaterialTheme.colorScheme.onSurface,
            softWrap = !singleLine,
            maxLines = if (singleLine) 1 else Int.MAX_VALUE,
            overflow = if (singleLine) TextOverflow.Ellipsis else TextOverflow.Clip
        )
    }
}
