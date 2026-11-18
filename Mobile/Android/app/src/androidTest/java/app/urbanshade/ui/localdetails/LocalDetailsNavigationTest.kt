package app.urbanshade.ui.localdetails

import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotSelected
import androidx.compose.ui.test.assertIsSelected
import androidx.compose.ui.test.getUnclippedBoundsInRoot
import androidx.compose.ui.test.junit4.createAndroidComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onFirst
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.test.espresso.Espresso.pressBack
import app.urbanshade.MainActivity
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

class LocalDetailsNavigationTest {
    @get:Rule val compose = createAndroidComposeRule<MainActivity>()

    @Test
    fun seeMoreShowsHistoryForTheSelectedPlaceAndBackRestoresTheMapSheet() {
        compose.onNodeWithText("I already have an account").performClick()
        compose.onNodeWithText("Continue").performClick()
        compose.onNodeWithText("Jardim das Nações").performClick()

        val handle = compose.onNodeWithContentDescription("Alça do painel do local")
        val navigation = compose.onAllNodesWithText("Label").onFirst()
        val initialSheetHeight = navigation.getUnclippedBoundsInRoot().top - handle.getUnclippedBoundsInRoot().top
        compose.onNodeWithText("See more").performClick()
        compose.onNodeWithText("Histórico").assertIsDisplayed()
        compose.onNodeWithText("Jardim das Nações").assertIsDisplayed()
        val firstDate = compose.onNodeWithText("26/12").assertIsSelected()
        val secondDate = compose.onNodeWithText("25/12").assertIsNotSelected()
        compose.onNodeWithText("39,2 °C").assertIsDisplayed()
        compose.onNodeWithText("+4,1 °C").assertIsDisplayed()
        compose.onNodeWithText("Análise Inteligente").assertDoesNotExist()

        secondDate.performClick().assertIsSelected()
        firstDate.assertIsNotSelected()
        compose.onNodeWithText("37,8 °C").assertIsDisplayed()
        compose.onNodeWithText("+2,7 °C").assertIsDisplayed()
        compose.onNodeWithText("39,2 °C").assertDoesNotExist()

        pressBack()
        compose.onNodeWithText("See more").assertIsDisplayed()
        val restoredSheetHeight = navigation.getUnclippedBoundsInRoot().top - handle.getUnclippedBoundsInRoot().top
        assertEquals(initialSheetHeight, restoredSheetHeight)

        compose.onNodeWithText("B. da Confiança").performClick()
        compose.onNodeWithText("See more").performClick()
        compose.onNodeWithText("Bairro da Confiança").assertIsDisplayed()
        compose.onNodeWithText("34 °C / 30 °C").assertIsDisplayed()
        compose.onNodeWithText("26/12").assertDoesNotExist()
        compose.onNodeWithText("39,2 °C").assertDoesNotExist()
    }
}
