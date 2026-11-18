package app.urbanshade.ui.localdetails

/** Packs whole cards into the fewest rows, keeping the input order when it already fits. */
internal fun packIndicatorRows(widths: List<Int>, rowWidth: Int, gap: Int): List<List<Int>> {
    if (widths.isEmpty()) return emptyList()

    // Charging a trailing gap to each card lets a single capacity account for row spacing.
    val capacity = rowWidth + gap
    val sizes = widths.map { it + gap }
    val orderedRows = mutableListOf(mutableListOf<Int>())
    var usedWidth = 0
    for (index in widths.indices) {
        if (usedWidth + sizes[index] > capacity) {
            orderedRows.add(mutableListOf())
            usedWidth = 0
        }
        orderedRows.last().add(index)
        usedWidth += sizes[index]
    }

    var best: List<List<Int>> = orderedRows
    if (capacity == 0) return best
    val minimumRows = (sizes.sum() + capacity - 1) / capacity
    if (best.size == minimumRows) return best

    val largestFirst = widths.indices.sortedByDescending { sizes[it] }
    val rows = mutableListOf<MutableList<Int>>()
    val occupied = mutableListOf<Int>()

    fun search(next: Int) {
        if (rows.size >= best.size || best.size == minimumRows) return
        if (next == largestFirst.size) {
            best = rows.map { it.sorted() }.sortedBy { it.first() }
            return
        }

        val index = largestFirst[next]
        val size = sizes[index]
        val triedWidths = mutableSetOf<Int>()
        for (row in rows.indices) {
            val width = occupied[row]
            if (width + size > capacity || !triedWidths.add(width)) continue
            rows[row].add(index)
            occupied[row] += size
            search(next + 1)
            occupied[row] -= size
            rows[row].removeAt(rows[row].lastIndex)
        }
        if (rows.size + 1 < best.size) {
            rows.add(mutableListOf(index))
            occupied.add(size)
            search(next + 1)
            occupied.removeAt(occupied.lastIndex)
            rows.removeAt(rows.lastIndex)
        }
    }

    search(0)
    return best
}
