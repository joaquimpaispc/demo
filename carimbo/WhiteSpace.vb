Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Linq

''' <summary>A near-white rectangle found by scanning a rendered page.</summary>
Friend Class Placement
    Public Property PageIndex As Integer
    Public Property X As Single
    Public Property Y As Single
    Public Property ClearancePt As Single
    Public Property WhiteRatio As Double
End Class

Friend Class StampLayout
    Public Property Lines As List(Of String)
    Public Property BlockWidth As Single
    Public Property BlockHeight As Single
    Public Property LogoWidth As Single
    Public Property LogoHeight As Single
    Public Property Pad As Single
    Public Property LineHeight As Single
    Public Property TextTop As Single
End Class

Friend Class StampOptions
    Public Property InputPath As String
    Public Property StampPath As String
    Public Property Lote As String
    Public Property Data As String
    Public Property Utilizador As String
    Public Property OutputPath As String
End Class

''' <summary>
''' Finds the largest clearance at which a stamp-sized rectangle is entirely near-white.
''' Page edges inside the margin are treated as occupied so the stamp stays inset.
''' </summary>
Friend Module WhiteSpace

    Private Const WhiteMin As Integer = 246

    Friend Function BuildBlocked(bmp As Bitmap, marginX As Integer, marginY As Integer) As Boolean(,)
        Dim w = bmp.Width
        Dim h = bmp.Height
        Dim blocked(h - 1, w - 1) As Boolean
        Dim rect As New Rectangle(0, 0, w, h)
        Dim data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb)
        Try
            Dim stride = data.Stride
            Dim bytes(Math.Abs(stride) * h - 1) As Byte
            System.Runtime.InteropServices.Marshal.Copy(data.Scan0, bytes, 0, bytes.Length)
            Dim origin = If(stride < 0, (h - 1) * Math.Abs(stride), 0)
            Dim stepY = If(stride < 0, -Math.Abs(stride), stride)
            For y = 0 To h - 1
                Dim row = origin + y * stepY
                Dim edge = y < marginY OrElse y >= h - marginY
                For x = 0 To w - 1
                    If edge OrElse x < marginX OrElse x >= w - marginX Then
                        blocked(y, x) = True
                        Continue For
                    End If
                    Dim i = row + x * 4
                    Dim b = CInt(bytes(i))
                    Dim g = CInt(bytes(i + 1))
                    Dim r = CInt(bytes(i + 2))
                    blocked(y, x) = r < WhiteMin OrElse g < WhiteMin OrElse b < WhiteMin
                Next
            Next
        Finally
            bmp.UnlockBits(data)
        End Try
        Return blocked
    End Function

    Friend Function WindowExists(blocked As Boolean(,), radius As Integer, blockW As Integer, blockH As Integer, stride As Integer) As Boolean
        Dim mask = Dilate(blocked, radius)
        Dim ii = Integral(mask)
        Dim h = blocked.GetLength(0)
        Dim w = blocked.GetLength(1)
        For y = 0 To h - blockH Step Math.Max(1, stride)
            For x = 0 To w - blockW Step Math.Max(1, stride)
                If RectSum(ii, x, y, x + blockW, y + blockH) = 0 Then Return True
            Next
        Next
        Return False
    End Function

    Friend Function ChooseSpot(blocked As Boolean(,), radius As Integer, blockW As Integer, blockH As Integer) As Point
        Dim mask = Dilate(blocked, radius)
        Dim ii = Integral(mask)
        Dim h = blocked.GetLength(0)
        Dim w = blocked.GetLength(1)
        Dim spots As New List(Of Point)
        For y = 0 To h - blockH Step 2
            For x = 0 To w - blockW Step 2
                If RectSum(ii, x, y, x + blockW, y + blockH) = 0 Then
                    spots.Add(New Point(x, y))
                End If
            Next
        Next
        If spots.Count = 0 Then
            Throw New InvalidOperationException("White rectangle disappeared while choosing a spot.")
        End If
        Dim orderedX = spots.Select(Function(p) p.X).OrderBy(Function(v) v).ToList()
        Dim orderedY = spots.Select(Function(p) p.Y).OrderBy(Function(v) v).ToList()
        Dim targetX = orderedX(orderedX.Count \ 2)
        Dim targetY = orderedY(orderedY.Count \ 2)
        Dim best = spots(0)
        Dim bestD = Long.MaxValue
        For Each spot In spots
            Dim dx = CLng(spot.X - targetX)
            Dim dy = CLng(spot.Y - targetY)
            Dim d = dx * dx + dy * dy
            If d < bestD Then
                bestD = d
                best = spot
            End If
        Next
        Return best
    End Function

    Friend Function WhiteRatio(blocked As Boolean(,), x As Integer, y As Integer, blockW As Integer, blockH As Integer) As Double
        Dim ink = 0
        For yy = y To y + blockH - 1
            For xx = x To x + blockW - 1
                If blocked(yy, xx) Then ink += 1
            Next
        Next
        Dim area = CDbl(blockW) * blockH
        Return (area - ink) / area
    End Function

    Private Function Dilate(blocked As Boolean(,), radius As Integer) As Boolean(,)
        Dim h = blocked.GetLength(0)
        Dim w = blocked.GetLength(1)
        If radius <= 0 Then
            Dim copy(h - 1, w - 1) As Boolean
            For y = 0 To h - 1
                For x = 0 To w - 1
                    copy(y, x) = blocked(y, x)
                Next
            Next
            Return copy
        End If

        Dim horizontal(h - 1, w - 1) As Boolean
        For y = 0 To h - 1
            Dim prefix(w) As Integer
            For x = 0 To w - 1
                prefix(x + 1) = prefix(x) + If(blocked(y, x), 1, 0)
            Next
            For x = 0 To w - 1
                Dim left = Math.Max(0, x - radius)
                Dim right = Math.Min(w, x + radius + 1)
                horizontal(y, x) = prefix(right) - prefix(left) > 0
            Next
        Next

        Dim dilated(h - 1, w - 1) As Boolean
        For x = 0 To w - 1
            Dim prefix(h) As Integer
            For y = 0 To h - 1
                prefix(y + 1) = prefix(y) + If(horizontal(y, x), 1, 0)
            Next
            For y = 0 To h - 1
                Dim top = Math.Max(0, y - radius)
                Dim bottom = Math.Min(h, y + radius + 1)
                dilated(y, x) = prefix(bottom) - prefix(top) > 0
            Next
        Next
        Return dilated
    End Function

    Private Function Integral(mask As Boolean(,)) As Integer(,)
        Dim h = mask.GetLength(0)
        Dim w = mask.GetLength(1)
        Dim ii(h, w) As Integer
        For y = 0 To h - 1
            Dim row = 0
            For x = 0 To w - 1
                row += If(mask(y, x), 1, 0)
                ii(y + 1, x + 1) = ii(y, x + 1) + row
            Next
        Next
        Return ii
    End Function

    Private Function RectSum(ii As Integer(,), x0 As Integer, y0 As Integer, x1 As Integer, y1 As Integer) As Integer
        Return ii(y1, x1) - ii(y0, x1) - ii(y1, x0) + ii(y0, x0)
    End Function

End Module
