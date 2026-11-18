package app.urbanshade.ui.login

import androidx.compose.foundation.Image
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.relocation.BringIntoViewRequester
import androidx.compose.foundation.relocation.bringIntoViewRequester
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.input.TextFieldState
import androidx.compose.foundation.text.input.rememberTextFieldState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.platform.LocalFocusManager
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.semantics.Role
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import app.urbanshade.R
import app.urbanshade.ui.components.UrbanActionButton
import app.urbanshade.ui.components.UrbanFormField
import app.urbanshade.ui.theme.UrbanShadeTheme
import app.urbanshade.ui.theme.UrbanSpacing
import kotlinx.coroutines.flow.collectLatest

@Composable
fun LoginScreen(onBack: () -> Unit, onContinue: (String) -> Unit = {}) {
    val email = rememberTextFieldState()
    val keyboard = LocalSoftwareKeyboardController.current
    val focus = LocalFocusManager.current
    val dismissKeyboard = { focus.clearFocus(); keyboard?.hide(); Unit }
    Column(Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background).safeDrawingPadding().imePadding()) {
        LoginHeader { dismissKeyboard(); onBack() }
        Column(Modifier.weight(1f).fillMaxWidth().verticalScroll(rememberScrollState())
            .padding(horizontal = UrbanSpacing.Page, vertical = 32.dp),
            verticalArrangement = Arrangement.spacedBy(32.dp), horizontalAlignment = Alignment.CenterHorizontally) {
            Column(horizontalAlignment = Alignment.CenterHorizontally, verticalArrangement = Arrangement.spacedBy(16.dp)) {
                Image(painterResource(R.drawable.login_brand_mark), null, Modifier.size(128.dp, 160.dp))
                Text(stringResource(R.string.login_greeting), Modifier.fillMaxWidth(),
                    style = MaterialTheme.typography.headlineMedium, color = MaterialTheme.colorScheme.secondary,
                    textAlign = TextAlign.Center)
            }
            Column(verticalArrangement = Arrangement.spacedBy(24.dp)) {
                LoginFields(email)
                UrbanActionButton(stringResource(R.string.login_continue), onClick = {
                    dismissKeyboard()
                    onContinue(email.text.toString())
                })
            }
        }
    }
}

@Composable
private fun LoginHeader(onBack: () -> Unit) {
    Row(Modifier.fillMaxWidth().height(84.dp).padding(start = 12.dp, end = 56.dp, top = 24.dp, bottom = 16.dp),
        verticalAlignment = Alignment.CenterVertically) {
        IconButton(onClick = onBack, modifier = Modifier.size(44.dp)) {
            Icon(painterResource(R.drawable.ic_navigate_before), stringResource(R.string.navigate_back),
                Modifier.size(24.dp), tint = MaterialTheme.colorScheme.primary)
        }
        Text(stringResource(R.string.login_title), Modifier.weight(1f),
            style = MaterialTheme.typography.headlineLarge, color = MaterialTheme.colorScheme.secondary,
            textAlign = TextAlign.Center)
    }
}

@Composable
private fun LoginFields(email: TextFieldState) {
    val password = remember { TextFieldState() }
    val emailBounds = remember { BringIntoViewRequester() }
    val passwordBounds = remember { BringIntoViewRequester() }
    var focusedField by remember { mutableStateOf<BringIntoViewRequester?>(null) }
    val ime = WindowInsets.ime
    val density = LocalDensity.current
    LaunchedEffect(focusedField, ime, density) {
        val field = focusedField ?: return@LaunchedEffect
        snapshotFlow { ime.getBottom(density) }.collectLatest { bottom ->
            if (bottom > 0) {
                // Wait for the resized viewport before revealing the complete labeled field.
                withFrameNanos { }
                field.bringIntoView()
            }
        }
    }
    Column(verticalArrangement = Arrangement.spacedBy(20.dp)) {
        UrbanFormField(stringResource(R.string.login_email), stringResource(R.string.login_email_hint), email,
            Modifier.bringIntoViewRequester(emailBounds).onFocusChanged {
                if (it.hasFocus) focusedField = emailBounds
                else if (focusedField == emailBounds) focusedField = null
            }, keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email, imeAction = ImeAction.Next))
        Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
            UrbanFormField(stringResource(R.string.login_password), stringResource(R.string.login_password_hint), password,
                Modifier.bringIntoViewRequester(passwordBounds).onFocusChanged {
                    if (it.hasFocus) focusedField = passwordBounds
                    else if (focusedField == passwordBounds) focusedField = null
                }, secure = true, keyboardOptions = KeyboardOptions(
                    autoCorrectEnabled = false, keyboardType = KeyboardType.Password, imeAction = ImeAction.Done))
            Box(Modifier.fillMaxWidth().height(44.dp).clickable(role = Role.Button, onClick = {}),
                contentAlignment = Alignment.TopEnd) {
                Text(stringResource(R.string.login_forgot_password), style = MaterialTheme.typography.labelLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
        }
    }
}

@Preview(widthDp = 412, heightDp = 917)
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun LoginScreenPreview() {
    UrbanShadeTheme(true) {
        CompositionLocalProvider(LocalRippleConfiguration provides null) { LoginScreen(onBack = {}) }
    }
}
