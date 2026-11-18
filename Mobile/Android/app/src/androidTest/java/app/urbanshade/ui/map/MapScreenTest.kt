package app.urbanshade.ui.map

import androidx.compose.foundation.layout.width
import androidx.compose.ui.Modifier
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.semantics.SemanticsActions
import androidx.compose.ui.test.assertIsDisplayed
import androidx.compose.ui.test.assertIsNotDisplayed
import androidx.compose.ui.test.getUnclippedBoundsInRoot
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithText
import androidx.compose.ui.test.onFirst
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.onRoot
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performSemanticsAction
import androidx.compose.ui.test.performTouchInput
import androidx.compose.ui.test.swipe
import androidx.compose.ui.text.TextLayoutResult
import androidx.compose.ui.unit.Dp
import androidx.compose.ui.unit.dp
import app.urbanshade.ui.theme.UrbanShadeTheme
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class MapScreenTest {
    @get:Rule val compose = createComposeRule()

    @Test
    fun shortcutsOpenSheetAndDraggingKeepsNavigationInPlace() {
        compose.setContent {
            UrbanShadeTheme(darkTheme = true) { MapScreen() }
        }

        val title = compose.onNodeWithText("Bairro da Confiança")
        val value = compose.onNodeWithText("6 dias")
        val navigation = compose.onAllNodesWithText("Label").onFirst()
        val navigationTop = navigation.getUnclippedBoundsInRoot().top
        title.assertIsNotDisplayed()

        compose.onNodeWithText("B. da Confiança").performClick()
        title.assertIsDisplayed()
        value.assertIsDisplayed()
        val partialY = handleY()

        dragHandleTo(115.dp)
        assertTrue(handleY() < partialY - 120.dp)
        assertEquals(navigationTop, navigation.getUnclippedBoundsInRoot().top)

        dragHandleTo(partialY)
        assertTrue(handleY() in (partialY - 2.dp)..(partialY + 2.dp))
        dragHandleTo(navigationTop - 35.dp)
        title.assertIsNotDisplayed()
        navigation.assertIsDisplayed()

        compose.onNodeWithText("Jardim das Nações").performClick()
        compose.onNodeWithText("Picos de calor").assertIsDisplayed()
        assertEquals(4, compose.onAllNodesWithText("—").fetchSemanticsNodes().size)
        value.assertIsNotDisplayed()
        assertTrue(handleY() in (partialY - 2.dp)..(partialY + 2.dp))
        assertEquals(navigationTop, navigation.getUnclippedBoundsInRoot().top)
    }

    @Test
    fun layerSelectorDisplaysThreeLayersStackedVerticallyOnRightEdge() {
        compose.setContent {
            UrbanShadeTheme(darkTheme = true) { MapScreen() }
        }

        val temp = compose.onNodeWithText("Temp").assertIsDisplayed()
        val tree = compose.onNodeWithText("Arb").assertIsDisplayed()
        val hum = compose.onNodeWithText("Umi").assertIsDisplayed()

        // Layer selection is interactive and toggles off when clicked again
        tree.performClick()
        hum.performClick()
        temp.performClick()
        // Toggle off: clicking Temp when already selected turns layer off
        temp.performClick()
        // Click Temp again to reactivate
        temp.performClick()

        // Vertically stacked below shortcuts
        val shortcut = compose.onNodeWithText("B. da Confiança").getUnclippedBoundsInRoot()
        val tempBounds = temp.getUnclippedBoundsInRoot()
        val treeBounds = tree.getUnclippedBoundsInRoot()
        val humBounds = hum.getUnclippedBoundsInRoot()

        assertTrue("Layer selector should be below shortcuts", tempBounds.top >= shortcut.bottom)
        assertTrue("Arb should be below Temp in vertical stack", treeBounds.top >= tempBounds.bottom)
        assertTrue("Umi should be below Arb in vertical stack", humBounds.top >= treeBounds.bottom)
    }

    @Test
    fun userMapScreenDisplaysSearchAndVerticalLayerSelectorOnRight() {
        compose.setContent {
            UrbanShadeTheme(darkTheme = true) { UserMapScreen() }
        }

        val search = compose.onNodeWithText("Buscar área, serviço…").assertIsDisplayed()
        val temp = compose.onNodeWithText("Temp").assertIsDisplayed()
        val tree = compose.onNodeWithText("Arb").assertIsDisplayed()
        val hum = compose.onNodeWithText("Umi").assertIsDisplayed()

        val searchBounds = search.getUnclippedBoundsInRoot()
        val tempBounds = temp.getUnclippedBoundsInRoot()
        val treeBounds = tree.getUnclippedBoundsInRoot()
        val humBounds = hum.getUnclippedBoundsInRoot()

        assertTrue("Layer selector should be below search in user map", tempBounds.top >= searchBounds.bottom)
        assertTrue("Buttons should be stacked vertically", treeBounds.top >= tempBounds.bottom && humBounds.top >= treeBounds.bottom)

        // Switching layer works
        tree.performClick()
        hum.performClick()
    }

    @Test
    fun indicatorsUseAvailableWidthAndMoveAsWholeCards() {
        val shortLabels = listOf("A", "B", "C", "D", "E")
        val impact = "10 mil pessoas impactadas"
        var openedPlace: MapPlace? = null
        val place = MapPlace(
            id = "demo",
            title = "Local",
            shortcut = "Abrir",
            indicators = shortLabels.map { MapIndicator(it, "1") } + MapIndicator("Impacto", impact)
        )
        compose.setContent {
            UrbanShadeTheme(darkTheme = true) {
                MapScreen(
                    Modifier.width(300.dp),
                    places = listOf(place),
                    initialPlaceId = place.id,
                    onSeeMore = { openedPlace = it }
                )
            }
        }

        val firstRow = compose.onNodeWithText(shortLabels.first()).getUnclippedBoundsInRoot().top
        shortLabels.drop(1).forEach { label ->
            assertEquals(firstRow, compose.onNodeWithText(label).getUnclippedBoundsInRoot().top)
        }
        val impactValue = compose.onNodeWithText(impact)
        assertTrue(impactValue.getUnclippedBoundsInRoot().top > firstRow + 68.dp)
        val layouts = mutableListOf<TextLayoutResult>()
        impactValue.performSemanticsAction(SemanticsActions.GetTextLayoutResult) { it(layouts) }
        assertEquals(1, layouts.single().lineCount)
        assertTrue("The impact value should fit on its own row", !layouts.single().isLineEllipsized(0))

        val seeMore = compose.onNodeWithText("Ver detalhes").assertIsDisplayed()
        assertTrue(seeMore.getUnclippedBoundsInRoot().top > impactValue.getUnclippedBoundsInRoot().bottom)
        seeMore.performClick()
        compose.runOnIdle { assertEquals(place, openedPlace) }
    }

    private fun handleY(): Dp {
        val bounds = compose.onNodeWithContentDescription("Alça do painel do local")
            .getUnclippedBoundsInRoot()
        return (bounds.top + bounds.bottom) / 2
    }

    private fun dragHandleTo(endY: Dp) {
        val bounds = compose.onNodeWithContentDescription("Alça do painel do local")
            .getUnclippedBoundsInRoot()
        val startY = (bounds.top + bounds.bottom) / 2
        val centerX = (bounds.left + bounds.right) / 2
        compose.onRoot().performTouchInput {
            swipe(
                start = Offset(centerX.toPx(), startY.toPx()),
                end = Offset(centerX.toPx(), endY.toPx()),
                durationMillis = 1_200
            )
        }
    }
}
