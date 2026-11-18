package app.urbanshade

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.SystemBarStyle
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.animation.*
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.saveable.rememberSaveableStateHolder
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clipToBounds
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.unit.dp
import androidx.core.view.WindowCompat
import app.urbanshade.ui.alerts.AlertsScreen
import app.urbanshade.ui.components.NavigationDestination
import app.urbanshade.ui.home.HomeScreen
import app.urbanshade.ui.home.AdminHomeScreen
import app.urbanshade.ui.components.LocalAdminNavigation
import app.urbanshade.ui.components.LocalNavigationActive
import app.urbanshade.ui.localdetails.LocalDetailsScreen
import app.urbanshade.ui.login.LoginScreen
import app.urbanshade.ui.map.*
import app.urbanshade.ui.sensors.SensorsScreen
import app.urbanshade.ui.theme.UrbanShadeTheme
import app.urbanshade.ui.theme.UrbanMotion
import app.urbanshade.ui.welcome.WelcomeScreen

private enum class Screen { Welcome, Login, Map, LocalDetails, Alerts, Sensors, Home, UserMap }
private enum class PreviewProfile { Admin, User }
private data class NavigationScene(val screen: Screen, val profile: PreviewProfile, val placeId: String?)

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge(
            statusBarStyle = SystemBarStyle.dark(android.graphics.Color.TRANSPARENT),
            navigationBarStyle = SystemBarStyle.dark(android.graphics.Color.TRANSPARENT)
        )
        setContent {
            var screen by rememberSaveable { mutableStateOf(Screen.Welcome) }
            var profile by rememberSaveable { mutableStateOf(PreviewProfile.Admin) }
            var detailsPlaceId by rememberSaveable { mutableStateOf<String?>(null) }
            val screenState = rememberSaveableStateHolder()
            val home = Screen.Home
            val goBack: () -> Unit = {
                screen = when (screen) {
                    Screen.LocalDetails -> Screen.Map
                    Screen.Map, Screen.Alerts, Screen.Sensors, Screen.UserMap -> home
                    Screen.Home -> Screen.Login
                    else -> Screen.Welcome
                }
            }
            val navigate: (NavigationDestination) -> Unit = { destination ->
                screen = when (destination) {
                    NavigationDestination.Home -> home
                    NavigationDestination.Map -> if (profile == PreviewProfile.Admin) Screen.Map else Screen.UserMap
                    NavigationDestination.Alerts -> Screen.Alerts
                    NavigationDestination.Sensors -> if (profile == PreviewProfile.Admin) Screen.Sensors else screen
                    NavigationDestination.Profile -> screen
                }
            }
            val hasLightHeader = screen in listOf(Screen.LocalDetails, Screen.Alerts, Screen.Sensors)
            SideEffect {
                WindowCompat.getInsetsController(window, window.decorView).isAppearanceLightStatusBars = hasLightHeader
            }
            BackHandler(enabled = screen != Screen.Welcome, onBack = goBack)
            UrbanShadeTheme(darkTheme = true) {
                val places = demoMapPlaces()
                val scene = NavigationScene(screen, profile, detailsPlaceId.takeIf { screen == Screen.LocalDetails })
                val slideDistance = with(LocalDensity.current) { 24.dp.roundToPx() }
                AnimatedContent(
                    targetState = scene,
                    modifier = Modifier.fillMaxSize().background(MaterialTheme.colorScheme.background).clipToBounds(),
                    contentKey = { it.profile to it.screen },
                    transitionSpec = {
                        val openingDetails = initialState.screen == Screen.Map && targetState.screen == Screen.LocalDetails
                        val closingDetails = initialState.screen == Screen.LocalDetails && targetState.screen == Screen.Map
                        val duration = if (openingDetails || closingDetails) UrbanMotion.Detail else UrbanMotion.Transition
                        val enter = fadeIn(tween(duration)) + when {
                            openingDetails -> slideInHorizontally(tween(duration)) { slideDistance }
                            closingDetails -> slideInHorizontally(tween(duration)) { -slideDistance }
                            else -> EnterTransition.None
                        }
                        val exit = fadeOut(tween(duration)) + when {
                            openingDetails -> slideOutHorizontally(tween(duration)) { -slideDistance }
                            closingDetails -> slideOutHorizontally(tween(duration)) { slideDistance }
                            else -> ExitTransition.None
                        }
                        (enter togetherWith exit).using(null)
                    },
                    label = "Screen transition"
                ) { displayedScene ->
                    CompositionLocalProvider(LocalNavigationActive provides (displayedScene == scene)) {
                        when (displayedScene.screen) {
                            Screen.Welcome -> WelcomeScreen(onLogin = { screen = Screen.Login })
                            Screen.Login -> LoginScreen(onBack = goBack, onContinue = { email ->
                                val selected = when (email.trim().lowercase()) {
                                    "adm@us.com" -> PreviewProfile.Admin
                                    "user@us.com" -> PreviewProfile.User
                                    else -> null
                                }
                                if (selected != null) {
                                    profile = selected
                                    screen = Screen.Home
                                }
                            })
                            else -> {
                                val colors = MaterialTheme.colorScheme
                                val sceneHasLightHeader = displayedScene.screen in listOf(Screen.LocalDetails, Screen.Alerts, Screen.Sensors)
                                Column(Modifier.fillMaxSize().background(colors.background)
                                    .windowInsetsPadding(WindowInsets.safeDrawing.only(WindowInsetsSides.Bottom + WindowInsetsSides.Horizontal))) {
                                    Spacer(Modifier.fillMaxWidth().windowInsetsTopHeight(WindowInsets.safeDrawing)
                                        .background(if (sceneHasLightHeader) colors.primary else colors.background))
                                    Box(Modifier.weight(1f).fillMaxWidth()) {
                                        CompositionLocalProvider(LocalAdminNavigation provides (displayedScene.profile == PreviewProfile.Admin)) {
                                            screenState.SaveableStateProvider("${displayedScene.profile.name}/${displayedScene.screen.name}") {
                                                when (displayedScene.screen) {
                                                    Screen.Map -> MapScreen(places = places, onSeeMore = { place ->
                                                        detailsPlaceId = place.id
                                                        screen = Screen.LocalDetails
                                                    }, onNavigate = navigate)
                                                    Screen.LocalDetails -> LocalDetailsScreen(
                                                        places.firstOrNull { it.id == displayedScene.placeId } ?: places.first(),
                                                        onBack = goBack, onNavigate = navigate)
                                                    Screen.Alerts -> AlertsScreen(goBack, navigate)
                                                    Screen.Sensors -> SensorsScreen(goBack, navigate)
                                                    Screen.Home -> if (displayedScene.profile == PreviewProfile.Admin) AdminHomeScreen(navigate) else HomeScreen(navigate)
                                                    Screen.UserMap -> UserMapScreen(navigate)
                                                    else -> Unit
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
