package app.urbanshade.ui.components.map

import androidx.compose.runtime.Immutable
import androidx.compose.runtime.saveable.listSaver
import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size

@Immutable
internal data class MapTransform(val scale: Float = 1f, val offset: Offset = Offset.Zero) {
    fun constrained(viewport: Size, image: Size): MapTransform {
        val boundedScale = scale.coerceIn(1f, 6f)
        val maxX = ((image.width * boundedScale - viewport.width) / 2).coerceAtLeast(0f)
        val maxY = ((image.height * boundedScale - viewport.height) / 2).coerceAtLeast(0f)
        return MapTransform(boundedScale, Offset(offset.x.coerceIn(-maxX, maxX), offset.y.coerceIn(-maxY, maxY)))
    }

    fun transformed(viewport: Size, image: Size, centroid: Offset, pan: Offset, zoom: Float): MapTransform {
        val current = constrained(viewport, image)
        val nextScale = (current.scale * zoom).coerceIn(1f, 6f)
        val center = Offset(viewport.width / 2, viewport.height / 2)
        val focusAdjustment = (centroid - center - current.offset) * (1 - nextScale / current.scale)
        return MapTransform(nextScale, current.offset + pan + focusAdjustment).constrained(viewport, image)
    }

    companion object {
        val Saver = listSaver<MapTransform, Float>(
            save = { listOf(it.scale, it.offset.x, it.offset.y) },
            restore = { MapTransform(it[0], Offset(it[1], it[2])) }
        )
    }
}
