package app.urbanshade.ui.welcome

import androidx.annotation.DrawableRes
import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.runtime.CompositionLocalProvider
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.layout.Layout
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.semantics.role
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.Constraints
import androidx.compose.ui.unit.dp
import app.urbanshade.R
import app.urbanshade.ui.components.UrbanActionButton
import app.urbanshade.ui.theme.UrbanShadeTheme
import app.urbanshade.ui.theme.UrbanSpacing
import app.urbanshade.ui.theme.WelcomeSocialOutline

private val PanelCorner = 24.dp

@Composable
fun WelcomeScreen(modifier: Modifier = Modifier, onLogin: () -> Unit = {}) {
    BoxWithConstraints(modifier.fillMaxSize().background(MaterialTheme.colorScheme.background).safeDrawingPadding()) {
        val viewportHeight = maxHeight
        Layout(content = {
            Image(painterResource(R.drawable.welcome_hero), null, contentScale = ContentScale.Crop)
            AuthenticationPanel(onLogin)
        }, modifier = Modifier.fillMaxWidth().verticalScroll(rememberScrollState())) { measurables, constraints ->
            val width = constraints.maxWidth
            val panel = measurables[1].measure(Constraints.fixedWidth(width))
            val height = maxOf(viewportHeight.roundToPx(), panel.height + 180.dp.roundToPx())
            val panelTop = height - panel.height
            // Extend the photograph behind the rounded top corners of the panel.
            val photo = measurables[0].measure(Constraints.fixed(width, panelTop + PanelCorner.roundToPx()))
            layout(width, height) {
                photo.placeRelative(0, 0)
                panel.placeRelative(0, panelTop)
            }
        }
    }
}

@Composable
private fun AuthenticationPanel(onLogin: () -> Unit) {
    Column(Modifier.fillMaxWidth()
        .background(MaterialTheme.colorScheme.background, RoundedCornerShape(topStart = PanelCorner, topEnd = PanelCorner))
        .padding(start = UrbanSpacing.Page, end = UrbanSpacing.Page, top = 24.dp, bottom = 32.dp),
        horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(24.dp)) {
        Column(verticalArrangement = Arrangement.spacedBy(16.dp)) {
            UrbanActionButton(stringResource(R.string.welcome_login), onLogin)
            UrbanActionButton(stringResource(R.string.welcome_signup), onClick = {}, secondary = true)
        }
        Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text(stringResource(R.string.welcome_sign_in_with), style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                SocialButton(R.drawable.welcome_google, stringResource(R.string.welcome_google))
                SocialButton(R.drawable.welcome_facebook, stringResource(R.string.welcome_facebook))
            }
        }
    }
}

@Composable
private fun SocialButton(@DrawableRes logo: Int, label: String) {
    Surface(onClick = {}, modifier = Modifier.size(48.dp).semantics { role = Role.Button },
        shape = CircleShape, color = Color.Transparent) {
        Box(contentAlignment = Alignment.Center) {
            Box(Modifier.size(32.dp).border(1.dp, WelcomeSocialOutline, CircleShape), Alignment.Center) {
                Image(painterResource(logo), label, Modifier.size(20.dp))
            }
        }
    }
}

@Preview(widthDp = 412, heightDp = 917)
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun WelcomeScreenPreview() {
    UrbanShadeTheme(true) {
        CompositionLocalProvider(LocalRippleConfiguration provides null) { WelcomeScreen() }
    }
}
