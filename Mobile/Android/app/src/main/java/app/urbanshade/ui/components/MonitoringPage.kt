package app.urbanshade.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyListScope
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import app.urbanshade.ui.theme.UrbanSpacing
import app.urbanshade.R

@Composable
internal fun MonitoringPage(
    title: String,
    description: String,
    addDescription: String,
    selected: NavigationDestination,
    onBack: () -> Unit,
    onNavigate: (NavigationDestination) -> Unit,
    onAdd: () -> Unit = {},
    content: LazyListScope.() -> Unit
) {
    val colors = MaterialTheme.colorScheme
    Column(Modifier.fillMaxSize().background(colors.primary)) {
        PageHeader(title, description, onBack, Modifier.height(128.dp))
        Box(Modifier.weight(1f).fillMaxWidth()
            .clip(RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp))
            .background(colors.background, RoundedCornerShape(topStart = 24.dp, topEnd = 24.dp))) {
            LazyColumn(Modifier.fillMaxSize().padding(horizontal = UrbanSpacing.Page),
                contentPadding = PaddingValues(top = 24.dp, bottom = 104.dp),
                verticalArrangement = Arrangement.spacedBy(UrbanSpacing.Small), content = content)
            FloatingActionButton(onClick = onAdd,
                modifier = Modifier.align(Alignment.BottomCenter).padding(bottom = 24.dp).size(56.dp)
                    .semantics { contentDescription = addDescription },
                shape = RoundedCornerShape(16.dp), containerColor = colors.primary, contentColor = colors.onPrimary) {
                Icon(painterResource(R.drawable.ic_add), null, Modifier.size(24.dp))
            }
        }
        UrbanShadeNavigationBar(selected, onNavigate)
    }
}
