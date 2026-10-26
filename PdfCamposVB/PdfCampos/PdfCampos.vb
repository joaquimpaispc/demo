' =============================================================================
'  PdfCampos - leitura e substituição dos campos "Lista", "Obra" e código de
'  barras na legenda de desenhos PDF exportados do SOLIDWORKS.
'
'  Abordagem:
'   1. Extrai todos os caracteres da página com a sua posição (iText 9).
'   2. Procura os campos por expressões regulares sobre o texto (não usa
'      coordenadas fixas), por isso funciona com outros desenhos do mesmo modelo.
'   3. Remove de facto o texto antigo com o pdfSweep (PdfCleanUpTool) - não é
'      apenas uma caixa branca por cima.
'   4. Escreve o novo texto na mesma posição / tamanho com uma fonte completa
'      (Century Gothic Bold, se existir) e redesenha o código de barras Code 39
'      como gráfico vetorial, no mesmo retângulo do original.
' =============================================================================
Option Strict On
Imports System.IO
Imports IOPath = System.IO.Path
Imports System.Text
Imports System.Text.RegularExpressions
Imports iText.Barcodes
Imports iText.IO.Font
Imports iText.IO.Font.Constants
Imports iText.Kernel.Colors
Imports iText.Kernel.Font
Imports iText.Kernel.Geom
Imports iText.Kernel.Pdf
Imports iText.Kernel.Pdf.Canvas
Imports iText.Kernel.Pdf.Canvas.Parser
Imports iText.Kernel.Pdf.Canvas.Parser.Data
Imports iText.Kernel.Pdf.Canvas.Parser.Listener
Imports iText.PdfCleanup

''' <summary>Valores atuais lidos da legenda do desenho.</summary>
Public Class CamposPdf
    Public Property Lista As String
    Public Property ObraPoco As String
    Public Property ObraPeriplast As String
    ''' <summary>Conteúdo do código de barras, sem os asteriscos (ex.: 3066.05-5432-0).</summary>
    Public Property CodigoBarras As String
    ''' <summary>Marcação extraída do código de barras (ex.: 5432).</summary>
    Public Property Marcacao As String
    ''' <summary>Revisão extraída do código de barras (ex.: 0).</summary>
    Public Property Revisao As String
    ''' <summary>Avisos não fatais produzidos durante a substituição.</summary>
    Public Property Avisos As New List(Of String)

    Public Overrides Function ToString() As String
        Return $"Lista={Lista}; Obra Poço={ObraPoco}; Obra Periplast={ObraPeriplast}; Código barras=*{CodigoBarras}* (Marcação={Marcacao}, Revisão={Revisao})"
    End Function
End Class

''' <summary>Erro específico quando um campo não é encontrado ou é inválido.</summary>
Public Class PdfCamposException
    Inherits Exception
    Public Sub New(mensagem As String)
        MyBase.New(mensagem)
    End Sub
End Class

Public NotInheritable Class PdfCampos

    ' Expressões usadas para localizar os campos (grupo 1 = valor).
    Private Shared ReadOnly RxLista As New Regex("\bLista\s*(\d+)", RegexOptions.IgnoreCase)
    Private Shared ReadOnly RxObraPoco As New Regex("Obra\s*Po.o\s*:\s*([0-9][0-9.,/\-]*)", RegexOptions.IgnoreCase)
    Private Shared ReadOnly RxObraPeriplast As New Regex("Obra\s*Periplast\s*:\s*([0-9][0-9.,/\-]*)", RegexOptions.IgnoreCase)
    Private Shared ReadOnly RxBarras As New Regex("\*([0-9A-Z.\-$/+%]+)\*")

    ''' <summary>Fontes a experimentar (pela ordem) para o novo texto.</summary>
    Public Shared ReadOnly Property FontesCandidatas As New List(Of String) From {
        "C:\Windows\Fonts\GOTHICB.TTF",                              ' Century Gothic Bold
        "C:\Windows\Fonts\arialbd.ttf",                              ' Arial Bold
        "/usr/share/fonts/opentype/urw-base35/URWGothic-Demi.otf",   ' clone livre de Century Gothic (Linux)
        "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"
    }

    Private Sub New()
    End Sub

    ' -------------------------------------------------------------------------
    '  API pública
    ' -------------------------------------------------------------------------

    ''' <summary>Lê os valores atuais (da primeira página onde existam).</summary>
    ''' <param name="mapaIA">Opcional: campos identificados pela IA (têm prioridade; ver AssistenteIA).</param>
    Public Shared Function Ler(caminho As String, Optional mapaIA As IList(Of CampoIdentificado) = Nothing) As CamposPdf
        Using pdf As New PdfDocument(New PdfReader(caminho))
            For p = 1 To pdf.GetNumberOfPages()
                Dim c = LocalizarCampos(pdf.GetPage(p), If(p = 1, mapaIA, Nothing))
                If c.Lista IsNot Nothing OrElse c.ObraPoco IsNot Nothing OrElse c.Barras IsNot Nothing Then
                    Return c.ParaCampos()
                End If
            Next
        End Using
        Throw New PdfCamposException("Não foi encontrado nenhum dos campos (Lista, Obra, código de barras) no PDF.")
    End Function

    ''' <summary>
    ''' Devolve as linhas de texto da 1.ª página com posição (para enviar à IA).
    ''' Coordenadas PDF em pontos, origem no canto inferior esquerdo.
    ''' </summary>
    Public Shared Function ExtrairTextos(caminho As String) As List(Of TextoPosicionado)
        Using pdf As New PdfDocument(New PdfReader(caminho))
            Return LocalizarCampos(pdf.GetPage(1), Nothing).Linhas.Select(Function(l) l.Info).ToList()
        End Using
    End Function

    ''' <summary>
    ''' Substitui os campos e grava o resultado num novo ficheiro.
    ''' Parâmetros a Nothing (ou vazios) ficam inalterados.
    ''' Se novoCodigoBarras não for indicado, é reconstruído como
    ''' &lt;ObraPoço&gt;-&lt;Marcação&gt;-&lt;Revisão&gt; mantendo a marcação e a revisão atuais.
    ''' </summary>
    ''' <param name="caminhoFonte">Opcional: ficheiro .ttf/.otf a usar no novo texto.</param>
    Public Shared Function Substituir(entrada As String, saida As String,
                                      novaLista As String, novaObraPoco As String,
                                      Optional novaObraPeriplast As String = Nothing,
                                      Optional novoCodigoBarras As String = Nothing,
                                      Optional caminhoFonte As String = Nothing) As CamposPdf
        Return Substituir(entrada, saida, New AlteracoesCampos With {
            .Lista = novaLista, .ObraPoco = novaObraPoco,
            .ObraPeriplast = novaObraPeriplast, .CodigoBarras = novoCodigoBarras}, Nothing, caminhoFonte)
    End Function

    ''' <summary>Substitui os campos indicados em <paramref name="alteracoes"/> (versão completa).</summary>
    ''' <param name="mapaIA">Opcional: campos identificados pela IA (prioridade sobre a pesquisa por texto).</param>
    Public Shared Function Substituir(entrada As String, saida As String, alteracoes As AlteracoesCampos,
                                      Optional mapaIA As IList(Of CampoIdentificado) = Nothing,
                                      Optional caminhoFonte As String = Nothing) As CamposPdf
        If alteracoes Is Nothing Then Throw New ArgumentNullException(NameOf(alteracoes))
        If alteracoes.Descricao1 IsNot Nothing OrElse alteracoes.Descricao2 IsNot Nothing OrElse alteracoes.DescricoesPecas.Count > 0 Then
            Throw New PdfCamposException("A alteração de descrições só está disponível para ficheiros DWG.")
        End If
        Dim novaLista = alteracoes.Lista, novaObraPoco = alteracoes.ObraPoco
        Dim novaObraPeriplast = alteracoes.ObraPeriplast, novoCodigoBarras = alteracoes.CodigoBarras
        If String.IsNullOrWhiteSpace(entrada) OrElse Not File.Exists(entrada) Then
            Throw New FileNotFoundException("Ficheiro de entrada não encontrado.", entrada)
        End If
        If String.Equals(IOPath.GetFullPath(entrada), IOPath.GetFullPath(saida), StringComparison.OrdinalIgnoreCase) Then
            Throw New PdfCamposException("O ficheiro de saída tem de ser diferente do ficheiro de entrada.")
        End If
        novaLista = Limpar(novaLista)
        novaObraPoco = Limpar(novaObraPoco)
        novaObraPeriplast = Limpar(novaObraPeriplast)
        novoCodigoBarras = Limpar(novoCodigoBarras)
        If novoCodigoBarras IsNot Nothing Then novoCodigoBarras = novoCodigoBarras.Trim("*"c).ToUpperInvariant()
        If novoCodigoBarras IsNot Nothing AndAlso Not Regex.IsMatch(novoCodigoBarras, "^[0-9A-Z.\-$/+% ]+$") Then
            Throw New PdfCamposException("O código de barras só pode ter caracteres Code 39 (0-9, A-Z, espaço e - . $ / + %).")
        End If

        Dim encontrouAlgum = False
        Dim resultado As CamposPdf = Nothing
        Dim pastaSaida = IOPath.GetDirectoryName(IOPath.GetFullPath(saida))
        If Not String.IsNullOrEmpty(pastaSaida) Then Directory.CreateDirectory(pastaSaida)

        Using pdf As New PdfDocument(New PdfReader(entrada), New PdfWriter(saida))
            Dim fonte = CriarFonte(caminhoFonte)
            For p = 1 To pdf.GetNumberOfPages()
                Dim pagina = pdf.GetPage(p)
                Dim c = LocalizarCampos(pagina, If(p = 1, mapaIA, Nothing))
                If c.Lista Is Nothing AndAlso c.ObraPoco Is Nothing AndAlso c.Barras Is Nothing Then Continue For
                encontrouAlgum = True

                If novaLista IsNot Nothing AndAlso c.Lista Is Nothing Then Throw New PdfCamposException($"Campo 'Lista' não encontrado na página {p}.")
                If novaObraPoco IsNot Nothing AndAlso c.ObraPoco Is Nothing Then Throw New PdfCamposException($"Campo 'Obra Poço' não encontrado na página {p}.")
                If novaObraPeriplast IsNot Nothing AndAlso c.ObraPeriplast Is Nothing Then Throw New PdfCamposException($"Campo 'Obra Periplast' não encontrado na página {p}.")

                ' Determinar o novo conteúdo do código de barras
                Dim codigo As String = novoCodigoBarras
                If codigo Is Nothing AndAlso novaObraPoco IsNot Nothing AndAlso c.Barras IsNot Nothing Then
                    Dim atual = c.ParaCampos()
                    If atual.Marcacao IsNot Nothing Then
                        codigo = $"{novaObraPoco}-{atual.Marcacao}-{atual.Revisao}"
                    End If
                End If
                If codigo IsNot Nothing AndAlso c.Barras Is Nothing Then Throw New PdfCamposException($"Código de barras não encontrado na página {p}.")

                ' Lista de alterações a fazer nesta página
                Dim trocas As New List(Of (Campo As CampoEncontrado, Novo As String, EBarras As Boolean))
                If novaLista IsNot Nothing Then trocas.Add((c.Lista, novaLista, False))
                If novaObraPoco IsNot Nothing Then trocas.Add((c.ObraPoco, novaObraPoco, False))
                If novaObraPeriplast IsNot Nothing Then trocas.Add((c.ObraPeriplast, novaObraPeriplast, False))
                If codigo IsNot Nothing Then trocas.Add((c.Barras, codigo, True))
                If trocas.Count = 0 Then Continue For

                ' 1) Remover o conteúdo antigo (remoção real com pdfSweep, sem preenchimento)
                Dim zonas As New List(Of PdfCleanUpLocation)
                For Each a In trocas
                    zonas.Add(New PdfCleanUpLocation(p, a.Campo.ZonaLimpeza(), Nothing))
                Next
                PdfCleaner.CleanUp(pdf, zonas)

                ' 2) Desenhar o novo conteúdo
                Dim canvas As New PdfCanvas(pagina.NewContentStreamAfter(), pagina.GetResources(), pdf)
                For Each a In trocas
                    If a.EBarras Then
                        DesenharCodigoBarras(pdf, canvas, a.Campo, a.Novo)
                    Else
                        DesenharTexto(canvas, a.Campo, a.Novo, fonte)
                    End If
                Next
                canvas.Release()

                If resultado Is Nothing Then
                    resultado = c.ParaCampos()
                    If novaLista IsNot Nothing Then resultado.Lista = novaLista
                    If novaObraPoco IsNot Nothing Then resultado.ObraPoco = novaObraPoco
                    If novaObraPeriplast IsNot Nothing Then resultado.ObraPeriplast = novaObraPeriplast
                    If codigo IsNot Nothing Then
                        resultado.CodigoBarras = codigo
                        DividirCodigo(codigo, resultado)
                    End If
                End If
            Next
        End Using

        If Not encontrouAlgum Then
            Try : File.Delete(saida) : Catch : End Try
            Throw New PdfCamposException("Não foi encontrado nenhum dos campos (Lista, Obra, código de barras) no PDF.")
        End If
        Return resultado
    End Function

    ' -------------------------------------------------------------------------
    '  Localização dos campos
    ' -------------------------------------------------------------------------

    Private Shared Function LocalizarCampos(pagina As PdfPage, mapaIA As IList(Of CampoIdentificado)) As CamposPagina
        Dim recolha As New RecolhaCaracteres()
        Dim proc As New PdfCanvasProcessor(recolha)
        proc.ProcessPageContent(pagina)

        ' Construir texto contínuo + mapa índice -> caractere
        Dim sb As New StringBuilder()
        Dim mapa As New List(Of CaracterPdf)
        Dim anterior As CaracterPdf = Nothing
        Dim linhas As New List(Of LinhaTexto)
        Dim inicioLinha = 0
        ' Ordenar geometricamente (linhas de cima para baixo, depois esquerda->direita),
        ' para que texto acrescentado no fim do conteúdo também seja encontrado.
        Dim ordenados = OrdenarPorLinhas(recolha.Caracteres)
        For Each ch In ordenados
            If anterior IsNot Nothing Then
                If Math.Abs(ch.YBase - anterior.YBase) > Math.Max(1.0F, anterior.Tamanho * 0.3F) OrElse ch.X0 < anterior.X0 - 1 Then
                    linhas.Add(NovaLinha(linhas.Count, sb, mapa, inicioLinha))
                    sb.Append(ControlChars.Lf) : mapa.Add(Nothing)
                    inicioLinha = sb.Length
                ElseIf ch.X0 - anterior.X1 > anterior.Tamanho * 0.15F Then
                    sb.Append(" "c) : mapa.Add(Nothing)
                End If
            End If
            For Each letra In ch.Texto
                sb.Append(letra) : mapa.Add(ch)
            Next
            anterior = ch
        Next
        If sb.Length > inicioLinha Then linhas.Add(NovaLinha(linhas.Count, sb, mapa, inicioLinha))
        Dim texto = sb.ToString()

        Dim r As New CamposPagina()
        r.Lista = Primeiro(RxLista, texto, mapa, Nothing)
        r.ObraPoco = Primeiro(RxObraPoco, texto, mapa, Nothing)
        r.ObraPeriplast = Primeiro(RxObraPeriplast, texto, mapa, Nothing)
        ' Código de barras: preferir texto escrito numa fonte de código de barras (nome com "39")
        r.Barras = Primeiro(RxBarras, texto, mapa, Function(cp) cp.NomeFonte.Contains("39"))
        If r.Barras Is Nothing Then r.Barras = Primeiro(RxBarras, texto, mapa, Nothing)
        r.Linhas = linhas

        ' Campos indicados pela IA têm prioridade
        If mapaIA IsNot Nothing Then
            For Each ci In mapaIA
                Dim campo = DoMapaIA(ci, linhas, mapa)
                If campo Is Nothing Then Continue For
                Select Case ci.Campo
                    Case CamposIA.Lista : r.Lista = campo
                    Case CamposIA.ObraPoco : r.ObraPoco = campo
                    Case CamposIA.ObraPeriplast : r.ObraPeriplast = campo
                    Case CamposIA.CodigoBarras : r.Barras = campo
                End Select
            Next
        End If
        Return r
    End Function

    Private Shared Function NovaLinha(id As Integer, sb As StringBuilder, mapa As List(Of CaracterPdf), inicio As Integer) As LinhaTexto
        Dim t = sb.ToString(inicio, sb.Length - inicio)
        Dim primeiro = mapa.Skip(inicio).FirstOrDefault(Function(c) c IsNot Nothing)
        Return New LinhaTexto With {.Inicio = inicio, .Info = New TextoPosicionado With {
            .Id = id, .Texto = t,
            .X = If(primeiro Is Nothing, 0, Math.Round(primeiro.X0, 1)),
            .Y = If(primeiro Is Nothing, 0, Math.Round(primeiro.YBase, 1)),
            .Altura = If(primeiro Is Nothing, 0, Math.Round(primeiro.Tamanho, 1)), .Bloco = "página 1"}}
    End Function

    ''' <summary>Converte uma indicação da IA (linha + valor) nos caracteres correspondentes.</summary>
    Private Shared Function DoMapaIA(ci As CampoIdentificado, linhas As List(Of LinhaTexto), mapa As List(Of CaracterPdf)) As CampoEncontrado
        If ci Is Nothing OrElse String.IsNullOrEmpty(ci.Valor) OrElse ci.TextoId < 0 OrElse ci.TextoId >= linhas.Count Then Return Nothing
        Dim l = linhas(ci.TextoId)
        Dim valor = ci.Valor
        If ci.Campo = CamposIA.CodigoBarras Then valor = valor.Trim("*"c)
        Dim idx = l.Info.Texto.IndexOf(valor, StringComparison.Ordinal)
        If idx < 0 Then Return Nothing
        Dim ini = l.Inicio + idx, fim = ini + valor.Length
        If ci.Campo = CamposIA.CodigoBarras Then
            ' incluir os asteriscos de início/fim, se existirem
            If idx > 0 AndAlso l.Info.Texto(idx - 1) = "*"c Then ini -= 1
            If idx + valor.Length < l.Info.Texto.Length AndAlso l.Info.Texto(idx + valor.Length) = "*"c Then fim += 1
        End If
        Dim chars As New List(Of CaracterPdf)
        For i = ini To fim - 1
            If mapa(i) IsNot Nothing AndAlso Not chars.Contains(mapa(i)) Then chars.Add(mapa(i))
        Next
        If chars.Count = 0 Then Return Nothing
        Return New CampoEncontrado With {.Valor = valor, .Caracteres = chars}
    End Function

    Private Class LinhaTexto
        Public Inicio As Integer
        Public Info As TextoPosicionado
    End Class

    Private Shared Function OrdenarPorLinhas(chars As List(Of CaracterPdf)) As List(Of CaracterPdf)
        Dim porY = chars.OrderByDescending(Function(c) c.YBase).ToList()
        Dim linhas As New List(Of List(Of CaracterPdf))
        For Each c In porY
            Dim ultima = If(linhas.Count > 0, linhas(linhas.Count - 1), Nothing)
            If ultima IsNot Nothing AndAlso Math.Abs(ultima(0).YBase - c.YBase) <= Math.Max(1.0F, Math.Min(ultima(0).Tamanho, c.Tamanho) * 0.3F) Then
                ultima.Add(c)
            Else
                linhas.Add(New List(Of CaracterPdf) From {c})
            End If
        Next
        Dim r As New List(Of CaracterPdf)
        For Each l In linhas
            r.AddRange(l.OrderBy(Function(c) c.X0))
        Next
        Return r
    End Function

    Private Shared Function Primeiro(rx As Regex, texto As String, mapa As List(Of CaracterPdf),
                                     filtro As Func(Of CaracterPdf, Boolean)) As CampoEncontrado
        For Each m As Match In rx.Matches(texto)
            Dim g = m.Groups(1)
            Dim eBarras = rx Is RxBarras
            ' No código de barras a zona inclui os asteriscos de início/fim
            Dim ini = If(eBarras, m.Index, g.Index)
            Dim fim = If(eBarras, m.Index + m.Length, g.Index + g.Length)
            Dim chars As New List(Of CaracterPdf)
            For i = ini To fim - 1
                Dim cp = mapa(i)
                If cp IsNot Nothing AndAlso Not chars.Contains(cp) Then chars.Add(cp)
            Next
            If chars.Count = 0 Then Continue For
            If filtro IsNot Nothing AndAlso Not filtro(chars(0)) Then Continue For
            Return New CampoEncontrado With {.Valor = g.Value.Trim(), .Caracteres = chars}
        Next
        Return Nothing
    End Function

    Private Shared Sub DividirCodigo(codigo As String, c As CamposPdf)
        ' Formato esperado: <obra>-<marcação>-<revisão>
        Dim partes = codigo.Split("-"c)
        If partes.Length >= 3 Then
            c.Revisao = partes(partes.Length - 1)
            c.Marcacao = partes(partes.Length - 2)
        End If
    End Sub

    ' -------------------------------------------------------------------------
    '  Desenho do novo conteúdo
    ' -------------------------------------------------------------------------

    Private Shared Function CriarFonte(caminhoFonte As String) As PdfFont
        Dim lista As New List(Of String)
        If Not String.IsNullOrWhiteSpace(caminhoFonte) Then lista.Add(caminhoFonte)
        lista.AddRange(FontesCandidatas)
        For Each f In lista
            If File.Exists(f) Then
                Try
                    Return PdfFontFactory.CreateFont(f, PdfEncodings.IDENTITY_H, PdfFontFactory.EmbeddingStrategy.PREFER_EMBEDDED)
                Catch
                    ' tenta a seguinte
                End Try
            End If
        Next
        Return PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD)
    End Function

    Private Shared Sub DesenharTexto(canvas As PdfCanvas, campo As CampoEncontrado, novo As String, fonte As PdfFont)
        Dim c0 = campo.Caracteres(0)
        canvas.SaveState()
        canvas.BeginText()
        canvas.SetFillColor(ColorConstants.BLACK)
        canvas.SetFontAndSize(fonte, c0.Tamanho)
        canvas.SetTextMatrix(c0.X0, c0.YBase)    ' mesmo ponto de início e linha de base
        canvas.ShowText(novo)
        canvas.EndText()
        canvas.RestoreState()
    End Sub

    Private Shared Sub DesenharCodigoBarras(pdf As PdfDocument, canvas As PdfCanvas, campo As CampoEncontrado, codigo As String)
        Dim alvo = campo.Retangulo()
        Const N As Single = 2.5F                       ' razão barra larga / estreita
        Const Margem As Single = 1.2F                  ' margem interior (as barras ficam dentro da zona de limpeza)
        Dim textoLegivel = "*" & codigo & "*"
        ' Cada carácter Code 39 = 6 elementos estreitos + 3 largos, mais 1 espaço estreito entre caracteres
        Dim modulos = textoLegivel.Length * (6 + 3 * N) + (textoLegivel.Length - 1)
        Dim larguraUtil = alvo.GetWidth() - 2 * Margem
        Dim x = larguraUtil / modulos                  ' largura do módulo para ocupar a largura original
        Dim tamTexto = alvo.GetHeight() * 0.28F
        Dim zonaTexto = tamTexto * 1.15F
        Dim x0 = alvo.GetX() + Margem

        ' 1) Barras (desenhadas diretamente na página, como retângulos vetoriais)
        Dim bc As New Barcode39(pdf)
        bc.SetCode(codigo)
        bc.SetStartStopText(True)
        bc.SetExtended(False)
        bc.SetN(N)
        bc.SetX(x)
        bc.SetFont(Nothing)                            ' sem texto: é desenhado abaixo, carácter a carácter
        bc.SetBarHeight(alvo.GetHeight() - zonaTexto - Margem)
        canvas.SaveState()
        canvas.ConcatMatrix(1, 0, 0, 1, x0, alvo.GetY() + zonaTexto)
        bc.PlaceBarcode(canvas, ColorConstants.BLACK, ColorConstants.BLACK)
        canvas.RestoreState()

        ' 2) Linha legível: cada carácter centrado por baixo do seu símbolo (como a fonte original)
        Dim f = PdfFontFactory.CreateFont(StandardFonts.HELVETICA)
        Dim largSimbolo = (6 + 3 * N) * x
        canvas.SaveState()
        canvas.SetFillColor(ColorConstants.BLACK)
        For i = 0 To textoLegivel.Length - 1
            Dim s = textoLegivel(i).ToString()
            Dim cx = x0 + i * (largSimbolo + x) + largSimbolo / 2
            Dim w = f.GetWidth(s, tamTexto)
            canvas.BeginText()
            canvas.SetFontAndSize(f, tamTexto)
            canvas.SetTextMatrix(cx - w / 2, alvo.GetY() + Margem)
            canvas.ShowText(s)
            canvas.EndText()
        Next

        ' 3) Texto invisível "*código*" a ocupar todo o retângulo: permite pesquisar/copiar
        '    o código e voltar a processar o PDF gerado (Ler/Substituir) mais tarde.
        Dim asc = f.GetFontProgram().GetFontMetrics().GetTypoAscender() / 1000.0F
        Dim desc = f.GetFontProgram().GetFontMetrics().GetTypoDescender() / 1000.0F
        Dim tamInv = alvo.GetHeight() / (asc - desc)
        Dim largInv = f.GetWidth(textoLegivel, tamInv)
        canvas.BeginText()
        canvas.SetTextRenderingMode(PdfCanvasConstants.TextRenderingMode.INVISIBLE)
        canvas.SetFontAndSize(f, tamInv)
        canvas.SetHorizontalScaling(100.0F * alvo.GetWidth() / largInv)
        canvas.SetTextMatrix(alvo.GetX(), alvo.GetY() - desc * tamInv)
        canvas.ShowText(textoLegivel)
        canvas.EndText()
        canvas.RestoreState()
    End Sub

    Private Shared Function Limpar(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return Nothing
        Return s.Trim()
    End Function

    ' -------------------------------------------------------------------------
    '  Tipos internos
    ' -------------------------------------------------------------------------

    Private Class CamposPagina
        Public Lista, ObraPoco, ObraPeriplast, Barras As CampoEncontrado
        Public Linhas As New List(Of LinhaTexto)
        Public Function ParaCampos() As CamposPdf
            Dim c As New CamposPdf With {
                .Lista = Lista?.Valor, .ObraPoco = ObraPoco?.Valor,
                .ObraPeriplast = ObraPeriplast?.Valor, .CodigoBarras = Barras?.Valor}
            If c.CodigoBarras IsNot Nothing Then DividirCodigo(c.CodigoBarras, c)
            Return c
        End Function
    End Class
End Class

''' <summary>Um campo encontrado: valor e caracteres que o compõem.</summary>
Friend Class CampoEncontrado
    Public Valor As String
    Public Caracteres As List(Of CaracterPdf)

    ''' <summary>Retângulo que envolve os caracteres (descendente até ascendente).</summary>
    Public Function Retangulo() As Rectangle
        Dim x0 = Caracteres.Min(Function(c) c.X0)
        Dim x1 = Caracteres.Max(Function(c) c.X1)
        Dim y0 = Caracteres.Min(Function(c) c.YDesc)
        Dim y1 = Caracteres.Max(Function(c) c.YAsc)
        Return New Rectangle(x0, y0, x1 - x0, y1 - y0)
    End Function

    ''' <summary>Zona a limpar: ligeiramente para dentro, para não apanhar texto/linhas vizinhos.</summary>
    Public Function ZonaLimpeza() As Rectangle
        Dim r = Retangulo()
        Dim mx = 0.4F, my = Math.Min(1.0F, r.GetHeight() * 0.1F)
        Return New Rectangle(r.GetX() + mx, r.GetY() + my, r.GetWidth() - 2 * mx, r.GetHeight() - 2 * my)
    End Function
End Class

''' <summary>Um caractere (ou glifo) com a sua posição na página.</summary>
Friend Class CaracterPdf
    Public Texto As String
    Public X0, X1, YBase, YAsc, YDesc, Tamanho As Single
    Public NomeFonte As String
End Class

''' <summary>Ouvinte que recolhe todos os caracteres da página com posição.</summary>
Friend Class RecolhaCaracteres
    Implements IEventListener

    Public ReadOnly Caracteres As New List(Of CaracterPdf)

    Public Sub EventOccurred(data As IEventData, type As EventType) Implements IEventListener.EventOccurred
        If type <> EventType.RENDER_TEXT Then Return
        Dim tri = CType(data, TextRenderInfo)
        Dim nomeFonte = ""
        Try
            nomeFonte = tri.GetFont().GetFontProgram().GetFontNames().GetFontName()
        Catch
        End Try
        ' Tamanho efetivo = tamanho da fonte x escala vertical (matriz de texto x CTM)
        Dim m = tri.GetTextMatrix().Multiply(tri.GetGraphicsState().GetCtm())
        Dim escala = CSng(Math.Sqrt(m.Get(Matrix.I21) ^ 2 + m.Get(Matrix.I22) ^ 2))
        Dim tamanho = tri.GetFontSize() * escala
        For Each c In tri.GetCharacterRenderInfos()
            Dim b = c.GetBaseline(), a = c.GetAscentLine(), d = c.GetDescentLine()
            Caracteres.Add(New CaracterPdf With {
                .Texto = If(c.GetText(), ""),
                .X0 = b.GetStartPoint().Get(Vector.I1),
                .X1 = b.GetEndPoint().Get(Vector.I1),
                .YBase = b.GetStartPoint().Get(Vector.I2),
                .YAsc = a.GetStartPoint().Get(Vector.I2),
                .YDesc = d.GetStartPoint().Get(Vector.I2),
                .Tamanho = tamanho,
                .NomeFonte = nomeFonte})
        Next
    End Sub

    Public Function GetSupportedEvents() As ICollection(Of EventType) Implements IEventListener.GetSupportedEvents
        Return New HashSet(Of EventType) From {EventType.RENDER_TEXT}
    End Function
End Class
