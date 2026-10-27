Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports Spire.Pdf
Imports Spire.Pdf.Graphics
Imports Spire.Pdf.Utilities

''' <summary>
''' Stamps one Poço carimbo (logo, lote, data, utilizador) onto a blank
''' region of a PDF. The blank region is found by rendering each page and
''' searching for a near-white rectangle; coordinates are not hard-coded.
'''
''' Usage:
'''   dotnet run --project carimbo -- input.pdf stamp.jpg lote data utilizador output.pdf
'''   dotnet run --project carimbo -- --input a.pdf --stamp logo.jpg --lote 123 --data 26/10/2026 --utilizador "Joaquim Pais" --output out.pdf
'''
''' The free Spire.PDF edition may draw an evaluation watermark. This tool does not remove it.
''' </summary>
Module Program

    Private Const FontSize As Single = 8.5F
    Private Const LogoWidthPt As Single = 126.0F
    Private Const PadPt As Single = 6.0F
    Private Const GapPt As Single = 4.0F
    Private Const MaxTextWidthPt As Single = 188.0F
    Private Const PageMarginPt As Single = 28.0F
    Private Const PreferredClearancePt As Single = 8.0F
    Private Const RenderDpi As Integer = 96

    Function Main(args As String()) As Integer
        Dim options As StampOptions = Nothing
        Dim errorMessage As String = Nothing
        If Not TryParseArgs(args, options, errorMessage) Then
            Console.Error.WriteLine(errorMessage)
            Console.Error.WriteLine()
            Console.Error.WriteLine(Usage())
            Return 2
        End If

        Try
            Stamp(options)
            Return 0
        Catch ex As Exception
            Console.Error.WriteLine("carimbo: " & ex.Message)
            Return 1
        End Try
    End Function

    Private Sub Stamp(options As StampOptions)
        Dim fontPath = FindFontFile()
        Dim font = PdfTrueTypeFont.FromFontFile(fontPath, FontSize, PdfFontStyle.Regular)
        Dim lines = BuildLines(options, font)
        Dim layout = MeasureLayout(lines, font, options.StampPath)

        Using doc As New PdfDocument()
            doc.LoadFromFile(options.InputPath)
            If doc.Pages.Count = 0 Then
                Throw New InvalidOperationException("The PDF has no pages.")
            End If

            Dim placement = FindPlacement(doc, layout)
            If placement Is Nothing Then
                Throw New InvalidOperationException(
                    "No near-white rectangle large enough for the carimbo was found on any page.")
            End If

            Dim page = doc.Pages(placement.PageIndex)
            DrawStamp(page, layout, font, options.StampPath, placement.X, placement.Y)

            Dim outDir = Path.GetDirectoryName(options.OutputPath)
            If Not String.IsNullOrEmpty(outDir) Then
                Directory.CreateDirectory(outDir)
            End If
            doc.SaveToFile(options.OutputPath)

            Console.WriteLine("lote=" & options.Lote)
            Console.WriteLine("page=" & (placement.PageIndex + 1).ToString(CultureInfo.InvariantCulture))
            Console.WriteLine("xPt=" & placement.X.ToString("0.0", CultureInfo.InvariantCulture))
            Console.WriteLine("yPt=" & placement.Y.ToString("0.0", CultureInfo.InvariantCulture))
            Console.WriteLine("widthPt=" & layout.BlockWidth.ToString("0.0", CultureInfo.InvariantCulture))
            Console.WriteLine("heightPt=" & layout.BlockHeight.ToString("0.0", CultureInfo.InvariantCulture))
            Console.WriteLine("clearancePt=" & placement.ClearancePt.ToString("0.0", CultureInfo.InvariantCulture))
            Console.WriteLine("whiteRatio=" & placement.WhiteRatio.ToString("0.000", CultureInfo.InvariantCulture))
            Console.WriteLine("output=" & options.OutputPath)
        End Using
    End Sub

    Private Function BuildLines(options As StampOptions, font As PdfTrueTypeFont) As List(Of String)
        Dim raw = New List(Of String) From {
            "Número do lote: " & options.Lote.Trim(),
            "Data: " & options.Data.Trim(),
            "Utilizador: " & options.Utilizador.Trim()
        }
        Dim wrapped As New List(Of String)
        For Each line In raw
            wrapped.AddRange(WrapLine(line, font, MaxTextWidthPt))
        Next
        Return wrapped
    End Function

    Private Function WrapLine(text As String, font As PdfTrueTypeFont, maxWidth As Single) As List(Of String)
        Dim result As New List(Of String)
        If font.MeasureString(text).Width <= maxWidth Then
            result.Add(text)
            Return result
        End If

        Dim current = ""
        For Each word In text.Split(" "c)
            Dim trial = If(current = "", word, current & " " & word)
            If font.MeasureString(trial).Width <= maxWidth Then
                current = trial
            Else
                If current <> "" Then result.Add(current)
                current = word
            End If
        Next
        If current <> "" Then result.Add(current)
        If result.Count = 0 Then result.Add(text)
        Return result
    End Function

    Private Function MeasureLayout(lines As List(Of String), font As PdfTrueTypeFont, stampPath As String) As StampLayout
        Using probe As Image = LoadBitmap(stampPath)
            Dim aspect = CSng(probe.Height) / CSng(probe.Width)
            Dim logoH = LogoWidthPt * aspect
            Dim textWidth As Single = 0
            Dim lineHeight = font.MeasureString("Número Ag").Height + 1.6F
            For Each line In lines
                textWidth = Math.Max(textWidth, font.MeasureString(line).Width)
            Next
            Dim contentWidth = Math.Max(LogoWidthPt, textWidth)
            Dim layout As New StampLayout With {
                .Lines = lines,
                .LogoWidth = LogoWidthPt,
                .LogoHeight = logoH,
                .Pad = PadPt,
                .LineHeight = lineHeight,
                .BlockWidth = contentWidth + PadPt * 2.0F,
                .BlockHeight = PadPt + logoH + GapPt + lineHeight * lines.Count + PadPt,
                .TextTop = PadPt + logoH + GapPt
            }
            Return layout
        End Using
    End Function

    Private Function FindPlacement(doc As PdfDocument, layout As StampLayout) As Placement
        Dim best As Placement = Nothing
        Dim first As Placement = Nothing

        For i = 0 To doc.Pages.Count - 1
            Dim page = doc.Pages(i)
            Dim found = FindOnPage(doc, i, page.Size.Width, page.Size.Height, layout.BlockWidth, layout.BlockHeight)
            If found Is Nothing Then Continue For
            If i = 0 Then first = found
            If best Is Nothing OrElse found.ClearancePt > best.ClearancePt + 0.5F Then
                best = found
            End If
        Next

        ' Prefer the first page when it has a real blank region.
        If first IsNot Nothing AndAlso first.ClearancePt >= PreferredClearancePt Then
            Return first
        End If
        Return best
    End Function

    Private Sub BlockImageBounds(blocked As Boolean(,), page As PdfPageBase, scaleX As Single, scaleY As Single)
        Dim infos = New PdfImageHelper().GetImagesInfo(page)
        If infos Is Nothing Then Return
        Dim h = blocked.GetLength(0)
        Dim w = blocked.GetLength(1)
        For Each info In infos
            Dim bounds = info.Bounds
            If bounds.Width <= 0 OrElse bounds.Height <= 0 Then Continue For
            Dim x0 = Math.Max(0, CInt(Math.Floor(bounds.X / scaleX)))
            Dim y0 = Math.Max(0, CInt(Math.Floor(bounds.Y / scaleY)))
            Dim x1 = Math.Min(w, CInt(Math.Ceiling((bounds.X + bounds.Width) / scaleX)))
            Dim y1 = Math.Min(h, CInt(Math.Ceiling((bounds.Y + bounds.Height) / scaleY)))
            For yy = y0 To y1 - 1
                For xx = x0 To x1 - 1
                    blocked(yy, xx) = True
                Next
            Next
        Next
    End Sub

    Private Function FindOnPage(doc As PdfDocument,
                                pageIndex As Integer,
                                pageWidthPt As Single,
                                pageHeightPt As Single,
                                blockWidthPt As Single,
                                blockHeightPt As Single) As Placement
        Using rendered As Image = doc.SaveAsImage(pageIndex, PdfImageType.Bitmap, RenderDpi, RenderDpi)
            Using bmp As Bitmap = ToArgb(rendered)
                Dim scaleX = pageWidthPt / bmp.Width
                Dim scaleY = pageHeightPt / bmp.Height
                Dim blockW = CInt(Math.Ceiling(blockWidthPt / scaleX))
                Dim blockH = CInt(Math.Ceiling(blockHeightPt / scaleY))
                Dim marginX = CInt(Math.Ceiling(PageMarginPt / scaleX))
                Dim marginY = CInt(Math.Ceiling(PageMarginPt / scaleY))
                If blockW + marginX * 2 >= bmp.Width OrElse blockH + marginY * 2 >= bmp.Height Then
                    Return Nothing
                End If

                Dim blocked = BuildBlocked(bmp, marginX, marginY)
                ' Spire's bitmap renderer can miss soft-mask images (the signature on this
                ' file). Mark every embedded image rectangle so the search stays off them.
                BlockImageBounds(blocked, doc.Pages(pageIndex), scaleX, scaleY)
                Dim maxRadius = Math.Min(90, Math.Min(bmp.Width, bmp.Height) \ 6)
                Dim bestRadius = -1
                Dim lo = 0
                Dim hi = maxRadius
                While lo <= hi
                    Dim mid = (lo + hi) \ 2
                    If WindowExists(blocked, mid, blockW, blockH, 4) Then
                        bestRadius = mid
                        lo = mid + 1
                    Else
                        hi = mid - 1
                    End If
                End While
                If bestRadius < 0 Then Return Nothing

                Dim spot = ChooseSpot(blocked, bestRadius, blockW, blockH)
                Dim white = WhiteRatio(blocked, spot.X, spot.Y, blockW, blockH)
                If white < 0.995 Then Return Nothing

                Return New Placement With {
                    .PageIndex = pageIndex,
                    .X = spot.X * scaleX,
                    .Y = spot.Y * scaleY,
                    .ClearancePt = bestRadius * ((scaleX + scaleY) / 2.0F),
                    .WhiteRatio = white
                }
            End Using
        End Using
    End Function

    Private Sub DrawStamp(page As PdfPageBase, layout As StampLayout, font As PdfTrueTypeFont, stampPath As String, x As Single, y As Single)
        Dim fill As PdfBrush = New PdfSolidBrush(Color.White)
        Dim pen As New PdfPen(Color.FromArgb(70, 70, 70), 0.7F)
        page.Canvas.DrawRectangle(pen, fill, x, y, layout.BlockWidth, layout.BlockHeight)

        Dim stamp = LoadPdfImage(stampPath)
        page.Canvas.DrawImage(stamp, x + layout.Pad, y + layout.Pad, layout.LogoWidth, layout.LogoHeight)

        Dim brush As PdfBrush = New PdfSolidBrush(Color.Black)
        Dim textY = y + layout.TextTop
        For Each line In layout.Lines
            page.Canvas.DrawString(line, font, brush, x + layout.Pad, textY)
            textY += layout.LineHeight
        Next
    End Sub

    Private Function LoadPdfImage(path As String) As PdfImage
        Try
            Return PdfImage.FromFile(path)
        Catch
            Using bmp = LoadBitmap(path)
                Return PdfImage.FromImage(bmp)
            End Using
        End Try
    End Function

    Private Function LoadBitmap(path As String) As Bitmap
        Dim bytes = File.ReadAllBytes(path)
        Using ms As New MemoryStream(bytes)
            Using img = Image.FromStream(ms, False, True)
                Return New Bitmap(img)
            End Using
        End Using
    End Function

    Private Function ToArgb(source As Image) As Bitmap
        Dim bmp As New Bitmap(source.Width, source.Height, Imaging.PixelFormat.Format32bppArgb)
        Using g = Graphics.FromImage(bmp)
            g.DrawImage(source, 0, 0, source.Width, source.Height)
        End Using
        Return bmp
    End Function

    Private Function FindFontFile() As String
        Dim candidates = {
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/TTF/DejaVuSans.ttf",
            "C:\Windows\Fonts\arial.ttf",
            "C:\Windows\Fonts\segoeui.ttf"
        }
        For Each path In candidates
            If File.Exists(path) Then Return path
        Next
        Throw New FileNotFoundException("No Unicode sans font found (Liberation Sans, DejaVu Sans, or Arial).")
    End Function

    Private Function Usage() As String
        Return "Usage:" & Environment.NewLine &
            "  carimbo <input.pdf> <stamp-image> <lote> <data> <utilizador> <output.pdf>" & Environment.NewLine &
            "  carimbo --input <pdf> --stamp <image> --lote <text> --data <dd/MM/yyyy> --utilizador <name> --output <pdf>" & Environment.NewLine &
            Environment.NewLine &
            "The stamp image defaults to assets/poco-carimbo.jpg when --stamp is omitted." & Environment.NewLine &
            "Placement scans each rendered page for a large near-white rectangle."
    End Function

    Private Function TryParseArgs(args As String(), ByRef options As StampOptions, ByRef errorMessage As String) As Boolean
        options = New StampOptions()
        If args Is Nothing OrElse args.Length = 0 OrElse args(0) = "--help" OrElse args(0) = "-h" Then
            errorMessage = "Arguments are required."
            Return False
        End If

        If args(0).StartsWith("--", StringComparison.Ordinal) Then
            Dim i = 0
            While i < args.Length
                Dim key = args(i)
                Dim value As String = Nothing
                Dim eq = key.IndexOf("="c)
                If eq >= 0 Then
                    value = key.Substring(eq + 1)
                    key = key.Substring(0, eq)
                Else
                    If i + 1 >= args.Length Then
                        errorMessage = "Missing value for " & key
                        Return False
                    End If
                    i += 1
                    value = args(i)
                End If
                Select Case key
                    Case "--input" : options.InputPath = value
                    Case "--stamp" : options.StampPath = value
                    Case "--lote" : options.Lote = value
                    Case "--data" : options.Data = value
                    Case "--utilizador" : options.Utilizador = value
                    Case "--output" : options.OutputPath = value
                    Case Else
                        errorMessage = "Unknown option " & key
                        Return False
                End Select
                i += 1
            End While
        Else
            If args.Length <> 6 Then
                errorMessage = "Expected 6 arguments, got " & args.Length.ToString(CultureInfo.InvariantCulture) & "."
                Return False
            End If
            options.InputPath = args(0)
            options.StampPath = args(1)
            options.Lote = args(2)
            options.Data = args(3)
            options.Utilizador = args(4)
            options.OutputPath = args(5)
        End If

        If String.IsNullOrWhiteSpace(options.StampPath) Then
            options.StampPath = Path.Combine(AppContext.BaseDirectory, "assets", "poco-carimbo.jpg")
        End If
        If String.IsNullOrWhiteSpace(options.InputPath) OrElse
           String.IsNullOrWhiteSpace(options.Lote) OrElse
           String.IsNullOrWhiteSpace(options.Data) OrElse
           String.IsNullOrWhiteSpace(options.Utilizador) OrElse
           String.IsNullOrWhiteSpace(options.OutputPath) Then
            errorMessage = "input, lote, data, utilizador, and output are required."
            Return False
        End If
        If Not File.Exists(options.InputPath) Then
            errorMessage = "Input PDF not found: " & options.InputPath
            Return False
        End If
        If Not File.Exists(options.StampPath) Then
            errorMessage = "Stamp image not found: " & options.StampPath
            Return False
        End If
        Return True
    End Function

End Module
