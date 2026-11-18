package app.urbanshade.ui.components

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.text.input.TextFieldLineLimits
import androidx.compose.foundation.text.input.TextFieldState
import androidx.compose.foundation.text.input.TextObfuscationMode
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp

@OptIn(ExperimentalMaterial3Api::class)
@Composable
internal fun UrbanFormField(
    label: String,
    placeholder: String,
    state: TextFieldState,
    modifier: Modifier = Modifier,
    secure: Boolean = false,
    keyboardOptions: KeyboardOptions = KeyboardOptions.Default,
    isError: Boolean = false
) {
    val scheme = MaterialTheme.colorScheme
    val colors = OutlinedTextFieldDefaults.colors(
        focusedTextColor = scheme.onSurface, unfocusedTextColor = scheme.onSurface,
        focusedBorderColor = scheme.primary, unfocusedBorderColor = scheme.outline,
        focusedContainerColor = scheme.background, unfocusedContainerColor = scheme.background,
        focusedPlaceholderColor = scheme.onSurfaceVariant, unfocusedPlaceholderColor = scheme.onSurfaceVariant
    )
    Column(modifier.fillMaxWidth(), verticalArrangement = Arrangement.spacedBy(8.dp)) {
        Text(label, style = MaterialTheme.typography.bodyMedium,
            color = if (isError) scheme.error else scheme.onSurface)
        val inputModifier = Modifier.fillMaxWidth().height(48.dp).semantics { contentDescription = label }
        val hint: @Composable () -> Unit = { Text(placeholder, style = MaterialTheme.typography.bodyLarge) }
        if (secure) {
            OutlinedSecureTextField(state, modifier = inputModifier,
                placeholder = hint, textStyle = MaterialTheme.typography.bodyLarge,
                textObfuscationMode = TextObfuscationMode.Hidden,
                keyboardOptions = keyboardOptions, colors = colors, isError = isError,
                shape = RoundedCornerShape(12.dp), contentPadding = PaddingValues(horizontal = 16.dp, vertical = 12.dp))
        } else {
            OutlinedTextField(state, modifier = inputModifier,
                placeholder = hint, textStyle = MaterialTheme.typography.bodyLarge,
                lineLimits = TextFieldLineLimits.SingleLine,
                keyboardOptions = keyboardOptions, colors = colors, isError = isError,
                shape = RoundedCornerShape(12.dp), contentPadding = PaddingValues(horizontal = 16.dp, vertical = 12.dp))
        }
    }
}
