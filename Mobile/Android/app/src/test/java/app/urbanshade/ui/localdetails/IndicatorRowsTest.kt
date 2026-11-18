package app.urbanshade.ui.localdetails

import org.junit.Assert.assertEquals
import org.junit.Test

class IndicatorRowsTest {
    @Test
    fun reordersCardsWhenInputOrderWouldUseAnExtraRow() {
        val rows = packIndicatorRows(widths = listOf(6, 6, 4, 4), rowWidth = 12, gap = 2)

        assertEquals(listOf(listOf(0, 2), listOf(1, 3)), rows)
    }

    @Test
    fun keepsInputOrderWhenItIsAlreadyOptimal() {
        val rows = packIndicatorRows(widths = listOf(4, 6, 6, 4), rowWidth = 12, gap = 2)

        assertEquals(listOf(listOf(0, 1), listOf(2, 3)), rows)
    }
}
