package app.urbanshade.ui.components.map

import androidx.compose.ui.geometry.Offset
import androidx.compose.ui.geometry.Size
import org.junit.Assert.assertEquals
import org.junit.Test

class MapTransformTest {
    private val viewport = Size(400f, 400f)

    @Test
    fun pinchKeepsTheCoordinateUnderTheFingers() {
        val centroid = Offset(250f, 150f)
        val before = MapTransform(2f, Offset(20f, -10f))
        val after = before.transformed(viewport, viewport, centroid, Offset.Zero, 1.5f)
        val center = Offset(200f, 200f)
        val originalPoint = (centroid - center - before.offset) / before.scale
        val transformedPoint = center + after.offset + originalPoint * after.scale
        assertEquals(centroid.x, transformedPoint.x, 0.001f)
        assertEquals(centroid.y, transformedPoint.y, 0.001f)
    }

    @Test
    fun panAndZoomStopAtMapEdges() {
        val after = MapTransform().transformed(viewport, viewport, Offset(200f, 200f), Offset(9000f, -9000f), 20f)
        assertEquals(6f, after.scale, 0f)
        assertEquals(Offset(1000f, -1000f), after.offset)
        val reset = after.transformed(viewport, viewport, Offset.Zero, Offset.Zero, 0.001f)
        assertEquals(1f, reset.scale, 0f)
        assertEquals(0f, reset.offset.x, 0f)
        assertEquals(0f, reset.offset.y, 0f)
    }

    @Test
    fun croppedImageCanPanAtBaseZoomWithoutExposingBackground() {
        val image = Size(800f, 400f)
        val after = MapTransform().transformed(viewport, image, Offset.Zero, Offset(900f, 900f), 1f)
        assertEquals(Offset(200f, 0f), after.offset)
    }

    @Test
    fun resizingClampsOldOffsetsToNewEdges() {
        val after = MapTransform(3f, Offset(350f, -350f)).constrained(Size(200f, 200f), Size(200f, 200f))
        assertEquals(Offset(200f, -200f), after.offset)
    }
}
