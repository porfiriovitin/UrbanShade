package app.urbanshade.ui.alerts

import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.heading
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import app.urbanshade.R
import app.urbanshade.data.ClimateAlerts
import app.urbanshade.ui.components.AlertCard
import app.urbanshade.ui.components.MonitoringPage
import app.urbanshade.ui.components.NavigationDestination
import app.urbanshade.ui.theme.UrbanShadeTheme

@Composable
internal fun AlertsScreen(
    onBack: () -> Unit = {},
    onNavigate: (NavigationDestination) -> Unit = {}
) {
    val colors = MaterialTheme.colorScheme
    val alerts = remember { ClimateAlerts.getAllAlerts() }
    val groupedAlerts = remember(alerts) { alerts.groupBy { it.dateIso } }

    MonitoringPage(
        title = stringResource(R.string.navigation_alerts),
        description = stringResource(R.string.alerts_subtitle_all),
        addDescription = stringResource(R.string.alerts_add),
        selected = NavigationDestination.Alerts,
        onBack = onBack,
        onNavigate = onNavigate
    ) {
        groupedAlerts.forEach { (dateIso, dateAlerts) ->
            item(key = "header_$dateIso") {
                val isFirstGroup = dateIso == groupedAlerts.keys.first()
                Text(
                    text = ClimateAlerts.formatDateHeader(dateIso),
                    style = MaterialTheme.typography.titleMedium,
                    color = colors.onSurface,
                    modifier = Modifier
                        .semantics { heading() }
                        .then(if (!isFirstGroup) Modifier.padding(top = 8.dp) else Modifier)
                )
            }
            items(dateAlerts, key = { it.id }) { alert ->
                AlertCard(alert.message)
            }
        }
    }
}

@Preview(widthDp = 412, heightDp = 917)
@Composable
private fun AlertsPreview() {
    UrbanShadeTheme(true) { AlertsScreen() }
}
