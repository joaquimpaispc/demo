' =============================================================================
'  DwgCampos - leitura e substituição dos campos da legenda em desenhos DWG
'  (exportados do SOLIDWORKS) com a biblioteca ACadSharp (MIT).
'
'  Campos: Lista, Obra Poço, Obra Periplast, texto do código de barras,
'  linhas de descrição da legenda e coluna "Descrição" da lista de peças.
'  Tudo é localizado por padrões de texto e posição relativa (não por handles).
'  O ficheiro é gravado na mesma versão (ex.: AC1015 = AutoCAD 2000).
' =============================================================================
Option Strict On
Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports ACadSharp
Imports ACadSharp.Entities
Imports ACadSharp.IO
Imports ACadSharp.Tables
Imports IOPath = System.IO.Path

Public NotInheritable Class DwgCampos

    Private Shared ReadOnly RxLista As New Regex("^\s*Lista\s*[:.]?\s*(\d+)\s*$", RegexOptions.IgnoreCase)
    Private Shared ReadOnly RxListaRotulo As New Regex("^\s*Lista\s*:?\s*$", RegexOptions.IgnoreCase)
    Private Shared ReadOnly RxObraPoco As New Regex("Obra\s*Po.o\s*:\s*([0-9][0-9.,/\-]*)", RegexOptions.IgnoreCase)
    Private Shared ReadOnly RxObraPocoRotulo As New Regex("^\s*Obra\s*Po.o\s*:?\s*$", RegexOptions.IgnoreCase)
    Private Shared ReadOnly RxObraPeriplast As New Regex("Obra\s*Periplast\s*:\s*([0-9][0-9.,/\-]*)", RegexOptions.IgnoreCase)
    Private Shared ReadOnly RxObraPeriplastRotulo As New Regex("^\s*Obra\s*Periplast\s*:?\s*$", RegexOptions.IgnoreCase)
    Private Shared ReadOnly RxValorObra As New Regex("^\s*[0-9][0-9.,/\-]*\s*$")
    Private Shared ReadOnly RxNumero As New Regex("^\s*\d+\s*$")
    Private Shared ReadOnly RxBarras As New Regex("^\s*\*([0-9A-Z.\-$/+%]+)\*\s*$")
    Private Shared ReadOnly RxCabecalhoDescricao As New Regex("^\s*Descri[çc][ãa]o\s*$", RegexOptions.IgnoreCase)

    ''' <summary>Fator médio de largura de carácter (x altura x fator de largura do estilo), calibrado no TXT.shx.</summary>
    Public Shared Property FatorLarguraCaracter As Double = 0.93

    Private Sub New()
    End Sub

    ' -------------------------------------------------------------------------
    '  API pública
    ' -------------------------------------------------------------------------

    ''' <summary>Lê os valores atuais do DWG.</summary>
    Public Shared Function Ler(caminho As String, Optional mapaIA As IList(Of CampoIdentificado) = Nothing) As CamposDwg
        Dim doc = Abrir(caminho)
        Dim c = Localizar(doc, mapaIA)
        If c.Lista Is Nothing AndAlso c.ObraPoco Is Nothing AndAlso c.Barras Is Nothing AndAlso c.Descricoes.Count = 0 AndAlso c.Pecas.Count = 0 Then
            Throw New PdfCamposException("Não foi encontrado nenhum dos campos (Lista, Obra, código de barras, descrições) no DWG.")
        End If
        Return c.ParaCampos()
    End Function

    ''' <summary>Todos os textos do desenho com posição (coordenadas do desenho) para enviar à IA.</summary>
    Public Shared Function ExtrairTextos(caminho As String) As List(Of TextoPosicionado)
        Return RecolherTextos(Abrir(caminho)).Select(Function(t) t.Info()).ToList()
    End Function

    ''' <summary>Versão simples (mesmos parâmetros do PDF).</summary>
    Public Shared Function Substituir(entrada As String, saida As String,
                                      novaLista As String, novaObraPoco As String,
                                      Optional novaObraPeriplast As String = Nothing,
                                      Optional novoCodigoBarras As String = Nothing) As CamposDwg
        Return Substituir(entrada, saida, New AlteracoesCampos With {
            .Lista = novaLista, .ObraPoco = novaObraPoco,
            .ObraPeriplast = novaObraPeriplast, .CodigoBarras = novoCodigoBarras})
    End Function

    ''' <summary>
    ''' Aplica as alterações e grava num novo DWG (mesma versão do original).
    ''' Valores Nothing/vazios ficam inalterados. O código de barras é reconstruído
    ''' como *ObraPoço-Marcação-Revisão* se a Obra Poço mudar e não for indicado.
    ''' </summary>
    Public Shared Function Substituir(entrada As String, saida As String, alteracoes As AlteracoesCampos,
                                      Optional mapaIA As IList(Of CampoIdentificado) = Nothing) As CamposDwg
        If alteracoes Is Nothing Then Throw New ArgumentNullException(NameOf(alteracoes))
        If String.IsNullOrWhiteSpace(entrada) OrElse Not File.Exists(entrada) Then
            Throw New FileNotFoundException("Ficheiro de entrada não encontrado.", entrada)
        End If
        If String.Equals(IOPath.GetFullPath(entrada), IOPath.GetFullPath(saida), StringComparison.OrdinalIgnoreCase) Then
            Throw New PdfCamposException("O ficheiro de saída tem de ser diferente do ficheiro de entrada.")
        End If

        Dim doc = Abrir(entrada)
        Dim c = Localizar(doc, mapaIA)
        Dim avisos As New List(Of String)

        Dim lista = Limpar(alteracoes.Lista)
        Dim obraPoco = Limpar(alteracoes.ObraPoco)
        Dim obraPeriplast = Limpar(alteracoes.ObraPeriplast)
        Dim codigo = Limpar(alteracoes.CodigoBarras)
        If codigo IsNot Nothing Then codigo = codigo.Trim("*"c).ToUpperInvariant()
        If codigo IsNot Nothing AndAlso Not Regex.IsMatch(codigo, "^[0-9A-Z.\-$/+%]+$") Then
            Throw New PdfCamposException("O código de barras só pode ter caracteres Code 39 (0-9, A-Z e - . $ / + %), sem espaços.")
        End If

        If lista IsNot Nothing Then
            Exigir(c.Lista, "Lista")
            If Not RxNumero.IsMatch(lista) Then avisos.Add("A nova Lista não é um número: '" & lista & "'.")
            c.Lista.Substituir(lista)
        End If
        If obraPoco IsNot Nothing Then Exigir(c.ObraPoco, "Obra Poço") : c.ObraPoco.Substituir(obraPoco)
        If obraPeriplast IsNot Nothing Then Exigir(c.ObraPeriplast, "Obra Periplast") : c.ObraPeriplast.Substituir(obraPeriplast)

        If codigo Is Nothing AndAlso obraPoco IsNot Nothing AndAlso c.Barras IsNot Nothing Then
            Dim atual = c.ParaCampos()
            If atual.Marcacao IsNot Nothing Then codigo = $"{obraPoco}-{atual.Marcacao}-{atual.Revisao}".ToUpperInvariant()
        End If
        If codigo IsNot Nothing Then Exigir(c.Barras, "código de barras") : c.Barras.Substituir(codigo)

        Dim d1 = Limpar(alteracoes.Descricao1), d2 = Limpar(alteracoes.Descricao2)
        If d1 IsNot Nothing Then
            If c.DescricoesLegenda.Count < 1 Then Throw New PdfCamposException("Descrição (linha 1) da legenda não encontrada no DWG.")
            c.DescricoesLegenda(0).Substituir(d1)
        End If
        If d2 IsNot Nothing Then
            If c.DescricoesLegenda.Count < 2 Then Throw New PdfCamposException("Descrição (linha 2) da legenda não encontrada no DWG.")
            c.DescricoesLegenda(1).Substituir(d2)
        End If

        For Each kv In alteracoes.DescricoesPecas
            Dim novo = Limpar(kv.Value)
            If novo Is Nothing Then Continue For
            Dim linha = c.LinhasPecas.FirstOrDefault(Function(l) String.Equals(l.Marcacao, kv.Key.Trim(), StringComparison.OrdinalIgnoreCase))
            If linha Is Nothing Then linha = c.LinhasPecas.FirstOrDefault(Function(l) String.Equals(l.Item, kv.Key.Trim(), StringComparison.OrdinalIgnoreCase))
            If linha Is Nothing OrElse linha.CampoDescricao Is Nothing Then
                Throw New PdfCamposException($"Peça '{kv.Key}' não encontrada na lista de peças (indique a Marcação ou o ITEM).")
            End If
            Dim centrado = linha.CampoDescricao.EstaCentradoNaCelula(linha.CelulaEsquerda, linha.CelulaDireita)
            linha.CampoDescricao.Substituir(novo)
            Dim aviso = linha.CampoDescricao.RealinharNaCelula(linha.CelulaEsquerda, linha.CelulaDireita, centrado)
            If aviso IsNot Nothing Then avisos.Add($"Peça {kv.Key}: {aviso}")
        Next

        Dim pasta = IOPath.GetDirectoryName(IOPath.GetFullPath(saida))
        If Not String.IsNullOrEmpty(pasta) Then Directory.CreateDirectory(pasta)
        Using w As New DwgWriter(saida, doc)   ' mantém doc.Header.Version (ex.: AC1015)
            w.Write()
        End Using

        Dim r = c.ParaCampos()
        r.Avisos.AddRange(avisos)
        Return r
    End Function

    ' -------------------------------------------------------------------------
    '  Leitura e recolha de textos
    ' -------------------------------------------------------------------------

    Private Shared Function Abrir(caminho As String) As CadDocument
        If String.IsNullOrWhiteSpace(caminho) OrElse Not File.Exists(caminho) Then
            Throw New FileNotFoundException("Ficheiro DWG não encontrado.", caminho)
        End If
        Try
            Return DwgReader.Read(caminho)
        Catch ex As Exception
            Throw New PdfCamposException("Não foi possível ler o DWG: " & ex.Message)
        End Try
    End Function

    ''' <summary>Percorre o espaço modelo e os layouts (com blocos inseridos) e devolve os textos em coordenadas do desenho.</summary>
    Private Shared Function RecolherTextos(doc As CadDocument) As List(Of TextoDwg)
        Dim r As New List(Of TextoDwg)
        Dim espacos = doc.BlockRecords.Where(Function(b) b.Name.Equals("*Model_Space", StringComparison.OrdinalIgnoreCase) OrElse
                                                         b.Name.StartsWith("*Paper_Space", StringComparison.OrdinalIgnoreCase)).
                                       OrderBy(Function(b) If(b.Name.StartsWith("*Model", StringComparison.OrdinalIgnoreCase), 0, 1)).ToList()
        For Each esp In espacos
            Percorrer(esp, Transformacao.Identidade, esp.Name, esp.Name, r, 0)
        Next
        For i = 0 To r.Count - 1
            r(i).Id = i
        Next
        Return r
    End Function

    Private Shared Sub Percorrer(bloco As BlockRecord, t As Transformacao, espaco As String, nomeBloco As String, r As List(Of TextoDwg), nivel As Integer)
        If nivel > 10 Then Return
        For Each e In bloco.Entities
            If TypeOf e Is MText OrElse (TypeOf e Is TextEntity AndAlso Not TypeOf e Is AttributeDefinition) Then
                Dim td = TextoDwg.Criar(e, t, espaco, nomeBloco)
                If td IsNot Nothing Then r.Add(td)
                ' atributos (ATTRIB) também são TextEntity e ficam incluídos via Insert abaixo
            End If
            Dim ins = TryCast(e, Insert)
            If ins IsNot Nothing AndAlso ins.Block IsNot Nothing Then
                For Each a In ins.Attributes
                    Dim ta = TextoDwg.Criar(a, Transformacao.Identidade.ComposicaoCom(t), espaco, nomeBloco & "/" & ins.Block.Name & "@" & a.Tag)
                    If ta IsNot Nothing Then r.Add(ta)
                Next
                Percorrer(ins.Block, t.Compor(ins), espaco, ins.Block.Name, r, nivel + 1)
            End If
        Next
    End Sub

    ' -------------------------------------------------------------------------
    '  Localização dos campos
    ' -------------------------------------------------------------------------

    Private Shared Function Localizar(doc As CadDocument, mapaIA As IList(Of CampoIdentificado)) As Resultado
        Dim textos = RecolherTextos(doc)
        Dim r As New Resultado()

        ' --- Lista
        For Each t In textos
            Dim m = RxLista.Match(t.Visivel)
            If m.Success Then r.Lista = New CampoDwg(t, m.Groups(1).Value) : Exit For
        Next
        If r.Lista Is Nothing Then r.Lista = ValorAoLado(textos, RxListaRotulo, RxNumero)

        ' --- Obras (no mesmo texto "Obra Poço: 123" ou rótulo + valor à direita)
        r.ObraPoco = ObraCombinada(textos, RxObraPoco)
        If r.ObraPoco Is Nothing Then r.ObraPoco = ValorAoLado(textos, RxObraPocoRotulo, RxValorObra)
        r.ObraPeriplast = ObraCombinada(textos, RxObraPeriplast)
        If r.ObraPeriplast Is Nothing Then r.ObraPeriplast = ValorAoLado(textos, RxObraPeriplastRotulo, RxValorObra)

        ' --- Código de barras: texto *...*, o de maior altura
        Dim bc = textos.Where(Function(t) RxBarras.IsMatch(t.Visivel)).OrderByDescending(Function(t) t.Altura).FirstOrDefault()
        If bc IsNot Nothing Then r.Barras = New CampoDwg(bc, RxBarras.Match(bc.Visivel).Groups(1).Value)

        ' --- Descrições da legenda: por baixo da linha das Obras, na mesma faixa horizontal
        LocalizarDescricoes(textos, r)

        ' --- Lista de peças
        LocalizarListaPecas(doc, textos, r)

        ' --- Indicações da IA (prioridade)
        If mapaIA IsNot Nothing Then
            For Each ci In mapaIA
                If ci Is Nothing OrElse ci.TextoId < 0 OrElse ci.TextoId >= textos.Count OrElse String.IsNullOrEmpty(ci.Valor) Then Continue For
                Dim t = textos(ci.TextoId)
                Dim valor = ci.Valor
                If ci.Campo = CamposIA.CodigoBarras Then valor = valor.Trim("*"c)
                If t.Visivel.IndexOf(valor, StringComparison.Ordinal) < 0 Then Continue For
                Dim campo As New CampoDwg(t, valor)
                Select Case ci.Campo
                    Case CamposIA.Lista : r.Lista = campo
                    Case CamposIA.ObraPoco : r.ObraPoco = campo
                    Case CamposIA.ObraPeriplast : r.ObraPeriplast = campo
                    Case CamposIA.CodigoBarras : r.Barras = campo
                    Case CamposIA.Descricao1, CamposIA.Descricao2
                        Dim idx = If(ci.Campo = CamposIA.Descricao1, 0, 1)
                        Dim inteiro As New CampoDwg(t, t.Visivel)
                        While r.DescricoesLegenda.Count <= idx
                            r.DescricoesLegenda.Add(Nothing)
                        End While
                        r.DescricoesLegenda(idx) = inteiro
                End Select
            Next
            r.DescricoesLegenda.RemoveAll(Function(x) x Is Nothing)
        End If
        Return r
    End Function

    Private Shared Function ObraCombinada(textos As List(Of TextoDwg), rx As Regex) As CampoDwg
        For Each t In textos
            Dim m = rx.Match(t.Visivel)
            If m.Success Then Return New CampoDwg(t, m.Groups(1).Value)
        Next
        Return Nothing
    End Function

    ''' <summary>Rótulo isolado (ex.: "Obra Poço:") + valor no texto mais próximo à direita, na mesma linha.</summary>
    Private Shared Function ValorAoLado(textos As List(Of TextoDwg), rotulo As Regex, valor As Regex) As CampoDwg
        For Each lb In textos.Where(Function(t) rotulo.IsMatch(t.Visivel))
            Dim tol = Math.Max(lb.Altura, 0.1) * 0.6
            Dim cand = textos.Where(Function(t) t IsNot lb AndAlso t.Espaco = lb.Espaco AndAlso
                                                valor.IsMatch(t.Visivel) AndAlso
                                                Math.Abs(t.WY - lb.WY) <= tol AndAlso t.WX > lb.WX).
                              OrderBy(Function(t) t.WX - lb.WX).FirstOrDefault()
            If cand IsNot Nothing Then Return New CampoDwg(cand, cand.Visivel.Trim())
        Next
        Return Nothing
    End Function

    Private Shared Sub LocalizarDescricoes(textos As List(Of TextoDwg), r As Resultado)
        Dim rotulos = textos.Where(Function(t) RxObraPocoRotulo.IsMatch(t.Visivel) OrElse RxObraPeriplastRotulo.IsMatch(t.Visivel) OrElse
                                               RxObraPoco.IsMatch(t.Visivel) OrElse RxObraPeriplast.IsMatch(t.Visivel)).ToList()
        If rotulos.Count = 0 Then Return
        Dim refT = rotulos(0)
        Dim yRef = rotulos.Min(Function(t) t.WY)
        Dim h = rotulos.Max(Function(t) t.Altura)
        Dim valores = New CampoDwg() {r.ObraPoco, r.ObraPeriplast}.Where(Function(v) v IsNot Nothing).Select(Function(v) v.Texto)
        Dim xs = rotulos.Concat(valores).Select(Function(t) t.WX).ToList()
        Dim xMin = xs.Min() - 10 * h, xMax = xs.Max() + 10 * h
        Dim yMin = If(r.Barras IsNot Nothing, r.Barras.Texto.WY + 0.5 * h, yRef - 8 * h)
        Dim usados As New HashSet(Of TextoDwg)(New CampoDwg() {r.Lista, r.ObraPoco, r.ObraPeriplast, r.Barras}.Where(Function(c) c IsNot Nothing).Select(Function(c) c.Texto))
        Dim desc = textos.Where(Function(t) t.Espaco = refT.Espaco AndAlso Not usados.Contains(t) AndAlso Not rotulos.Contains(t) AndAlso
                                            t.WY < yRef - 0.5 * h AndAlso t.WY > yMin AndAlso
                                            t.WX >= xMin AndAlso t.WX <= xMax AndAlso
                                            t.Altura >= 0.8 * h AndAlso t.Visivel.Trim().Length > 0 AndAlso
                                            Not RxValorObra.IsMatch(t.Visivel)).
                          OrderByDescending(Function(t) t.WY).ToList()
        For Each t In desc
            r.DescricoesLegenda.Add(New CampoDwg(t, t.Visivel))
        Next
    End Sub

    Private Shared Sub LocalizarListaPecas(doc As CadDocument, textos As List(Of TextoDwg), r As Resultado)
        For Each blk In doc.BlockRecords
            Dim mts = blk.Entities.OfType(Of MText)().ToList()
            Dim cab = mts.FirstOrDefault(Function(m) RxCabecalhoDescricao.IsMatch(TextoDwg.TextoVisivel(m.Value)))
            If cab Is Nothing Then Continue For
            If Not blk.Name.StartsWith("SW_TABLEANNOTATION", StringComparison.OrdinalIgnoreCase) AndAlso
               Not mts.Any(Function(m) Regex.IsMatch(TextoDwg.TextoVisivel(m.Value), "^\s*(ITEM|Marca[çc][ãa]o)\s*$", RegexOptions.IgnoreCase)) Then Continue For

            Dim tol = cab.Height * 0.5
            Dim cabecalhos = mts.Where(Function(m) Math.Abs(m.InsertPoint.Y - cab.InsertPoint.Y) <= tol).ToList()
            ' Linhas verticais da tabela -> limites das colunas
            Dim xsLinhas = blk.Entities.OfType(Of Line)().
                Where(Function(l) Math.Abs(l.StartPoint.X - l.EndPoint.X) < 0.001 AndAlso Math.Abs(l.StartPoint.Y - l.EndPoint.Y) > 0.001).
                Select(Function(l) Math.Round(l.StartPoint.X, 3)).Distinct().OrderBy(Function(x) x).ToList()
            Dim colunaDe = Function(m As MText) As (Esq As Double, Dir As Double)
                               Dim c = TextoDwg.CentroEstimado(m)
                               If xsLinhas.Count >= 2 Then
                                   Dim esq = xsLinhas.Where(Function(x) x <= c).DefaultIfEmpty(Double.NaN).Max()
                                   Dim dir = xsLinhas.Where(Function(x) x > c).DefaultIfEmpty(Double.NaN).Min()
                                   If Not Double.IsNaN(esq) AndAlso Not Double.IsNaN(dir) Then Return (esq, dir)
                               End If
                               Return (Double.NaN, Double.NaN)
                           End Function
            ' Mapa nome do cabeçalho -> coluna
            Dim colunas As New Dictionary(Of String, (Esq As Double, Dir As Double, Centro As Double))
            For Each hm In cabecalhos
                Dim nome = Normalizar(TextoDwg.TextoVisivel(hm.Value))
                Dim col = colunaDe(hm)
                colunas(nome) = (col.Esq, col.Dir, TextoDwg.CentroEstimado(hm))
            Next
            Dim chaveCol = Function(m As MText) As String
                               Dim c = TextoDwg.CentroEstimado(m)
                               If xsLinhas.Count >= 2 Then
                                   For Each kv In colunas
                                       If Not Double.IsNaN(kv.Value.Esq) AndAlso c >= kv.Value.Esq AndAlso c < kv.Value.Dir Then Return kv.Key
                                   Next
                               End If
                               ' sem linhas: cabeçalho com centro mais próximo
                               Return colunas.OrderBy(Function(kv) Math.Abs(kv.Value.Centro - c)).First().Key
                           End Function

            ' Agrupar o resto em linhas (por Y)
            Dim restantes = mts.Except(cabecalhos).OrderByDescending(Function(m) m.InsertPoint.Y).ToList()
            Dim grupos As New List(Of List(Of MText))
            For Each m In restantes
                If grupos.Count > 0 AndAlso Math.Abs(grupos.Last()(0).InsertPoint.Y - m.InsertPoint.Y) <= tol Then
                    grupos.Last().Add(m)
                Else
                    grupos.Add(New List(Of MText) From {m})
                End If
            Next
            For Each g In grupos
                Dim lp As New LinhaPecaInterna()
                For Each m In g
                    Dim k = chaveCol(m)
                    Dim v = TextoDwg.TextoVisivel(m.Value).Trim()
                    Select Case k
                        Case "item" : lp.Item = v
                        Case "qtd", "qtd.", "quant", "quant.", "quantidade" : lp.Quantidade = v
                        Case "marcacao" : lp.Marcacao = v
                        Case "descricao"
                            Dim td = textos.FirstOrDefault(Function(t) t.Entidade Is m)
                            If td Is Nothing Then td = TextoDwg.Criar(m, Transformacao.Identidade, blk.Name, blk.Name)
                            lp.CampoDescricao = New CampoDwg(td, td.Visivel)
                            Dim col = colunas(k)
                            lp.CelulaEsquerda = col.Esq : lp.CelulaDireita = col.Dir
                    End Select
                Next
                If lp.CampoDescricao IsNot Nothing OrElse lp.Marcacao IsNot Nothing Then r.LinhasPecas.Add(lp)
            Next
        Next
        ' Ordenar por ITEM (numérico quando possível)
        r.LinhasPecas.Sort(Function(a, b)
                               Dim ia, ib As Integer
                               If Integer.TryParse(a.Item, ia) AndAlso Integer.TryParse(b.Item, ib) Then Return ia.CompareTo(ib)
                               Return String.Compare(a.Item, b.Item, StringComparison.Ordinal)
                           End Function)
    End Sub

    Private Shared Function Normalizar(s As String) As String
        Dim n = s.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD)
        Dim sb As New StringBuilder()
        For Each ch In n
            If Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) <> Globalization.UnicodeCategory.NonSpacingMark Then sb.Append(ch)
        Next
        Return sb.ToString()
    End Function

    Private Shared Sub Exigir(c As CampoDwg, nome As String)
        If c Is Nothing Then Throw New PdfCamposException($"Campo '{nome}' não encontrado no DWG.")
    End Sub

    Private Shared Function Limpar(s As String) As String
        If String.IsNullOrWhiteSpace(s) Then Return Nothing
        Return s.Trim()
    End Function

    ' -------------------------------------------------------------------------
    '  Tipos internos
    ' -------------------------------------------------------------------------

    Private Class LinhaPecaInterna
        Public Item, Quantidade, Marcacao As String
        Public CampoDescricao As CampoDwg
        Public CelulaEsquerda As Double = Double.NaN, CelulaDireita As Double = Double.NaN
    End Class

    Private Class Resultado
        Public Lista, ObraPoco, ObraPeriplast, Barras As CampoDwg
        Public DescricoesLegenda As New List(Of CampoDwg)
        Public LinhasPecas As New List(Of LinhaPecaInterna)

        Public ReadOnly Property Descricoes As List(Of String)
            Get
                Return DescricoesLegenda.Select(Function(d) d.ValorAtual).ToList()
            End Get
        End Property
        Public ReadOnly Property Pecas As List(Of LinhaPeca)
            Get
                Return LinhasPecas.Select(Function(l) New LinhaPeca With {.Item = l.Item, .Quantidade = l.Quantidade, .Marcacao = l.Marcacao, .Descricao = l.CampoDescricao?.ValorAtual}).ToList()
            End Get
        End Property

        Public Function ParaCampos() As CamposDwg
            Dim c As New CamposDwg With {
                .Lista = Lista?.ValorAtual, .ObraPoco = ObraPoco?.ValorAtual, .ObraPeriplast = ObraPeriplast?.ValorAtual,
                .CodigoBarras = Barras?.ValorAtual}
            c.Descricoes.AddRange(Descricoes)
            c.Pecas.AddRange(Pecas)
            If c.CodigoBarras IsNot Nothing Then
                Dim partes = c.CodigoBarras.Split("-"c)
                If partes.Length >= 3 Then c.Revisao = partes(partes.Length - 1) : c.Marcacao = partes(partes.Length - 2)
            End If
            Return c
        End Function
    End Class
End Class

''' <summary>Transformação 2D simples (escala + rotação + translação) de inserções de blocos.</summary>
Friend Structure Transformacao
    Public A, B, C, D, Tx, Ty As Double   ' x' = A*x + C*y + Tx ; y' = B*x + D*y + Ty
    Public Shared ReadOnly Property Identidade As Transformacao
        Get
            Return New Transformacao With {.A = 1, .D = 1}
        End Get
    End Property
    Public Function Aplicar(x As Double, y As Double) As (X As Double, Y As Double)
        Return (A * x + C * y + Tx, B * x + D * y + Ty)
    End Function
    Public ReadOnly Property Escala As Double
        Get
            Return Math.Sqrt(Math.Abs(A * D - B * C))
        End Get
    End Property
    ''' <summary>Compõe esta transformação com a de uma inserção (a inserção é aplicada primeiro).</summary>
    Public Function Compor(ins As Insert) As Transformacao
        Dim cs = Math.Cos(ins.Rotation), sn = Math.Sin(ins.Rotation)
        Dim bp = If(ins.Block?.BlockEntity IsNot Nothing, ins.Block.BlockEntity.BasePoint, New CSMath.XYZ(0, 0, 0))
        ' local: p -> R*S*(p - base) + insert
        Dim la = cs * ins.XScale, lb = sn * ins.XScale, lc = -sn * ins.YScale, ld = cs * ins.YScale
        Dim ltx = ins.InsertPoint.X - (la * bp.X + lc * bp.Y)
        Dim lty = ins.InsertPoint.Y - (lb * bp.X + ld * bp.Y)
        Return New Transformacao With {
            .A = A * la + C * lb, .B = B * la + D * lb,
            .C = A * lc + C * ld, .D = B * lc + D * ld,
            .Tx = A * ltx + C * lty + Tx, .Ty = B * ltx + D * lty + Ty}
    End Function
    ''' <summary>Atributos já estão em coordenadas do espaço que contém a inserção.</summary>
    Public Function ComposicaoCom(t As Transformacao) As Transformacao
        Return t
    End Function
End Structure

''' <summary>Um texto (MTEXT, TEXT ou ATTRIB) com posição em coordenadas do desenho.</summary>
Friend Class TextoDwg
    Public Id As Integer
    Public Entidade As Entity
    Public Espaco As String
    Public Bloco As String
    Public WX, WY, Altura As Double

    Public Shared Function Criar(e As Entity, t As Transformacao, espaco As String, bloco As String) As TextoDwg
        Dim p As CSMath.XYZ, h As Double
        Dim mt = TryCast(e, MText)
        If mt IsNot Nothing Then
            p = mt.InsertPoint : h = mt.Height
        Else
            Dim te = TryCast(e, TextEntity)
            If te Is Nothing Then Return Nothing
            p = te.InsertPoint : h = te.Height
        End If
        Dim w = t.Aplicar(p.X, p.Y)
        Return New TextoDwg With {.Entidade = e, .Espaco = espaco, .Bloco = bloco, .WX = w.X, .WY = w.Y, .Altura = h * t.Escala}
    End Function

    Public Property Bruto As String
        Get
            Dim mt = TryCast(Entidade, MText)
            If mt IsNot Nothing Then Return If(mt.Value, "")
            Return If(DirectCast(Entidade, TextEntity).Value, "")
        End Get
        Set(v As String)
            Dim mt = TryCast(Entidade, MText)
            If mt IsNot Nothing Then mt.Value = v Else DirectCast(Entidade, TextEntity).Value = v
        End Set
    End Property

    Public ReadOnly Property EMText As Boolean
        Get
            Return TypeOf Entidade Is MText
        End Get
    End Property

    Public ReadOnly Property Visivel As String
        Get
            Return If(EMText, TextoVisivel(Bruto), DecodificarUnicode(Bruto))
        End Get
    End Property

    Public Function Info() As TextoPosicionado
        Return New TextoPosicionado With {.Id = Id, .Texto = Visivel, .X = Math.Round(WX, 2), .Y = Math.Round(WY, 2), .Altura = Math.Round(Altura, 2), .Bloco = Bloco}
    End Function

    ' ---- utilitários de texto MTEXT ----

    ''' <summary>Remove os códigos de formatação MTEXT (\f..; \H..; {}, \P, ...) e descodifica \U+XXXX.</summary>
    Public Shared Function TextoVisivel(bruto As String) As String
        If String.IsNullOrEmpty(bruto) Then Return ""
        Dim s = Regex.Replace(bruto, "\\[fFcCHhTtQqWwAa][^;]*;", "")
        s = Regex.Replace(s, "\\S([^;]*);", Function(m) m.Groups(1).Value.Replace("^", "/").Replace("#", "/"))
        s = Regex.Replace(s, "\\[Pp]", " ")
        s = Regex.Replace(s, "\\[LlOoKk]", "")
        s = s.Replace("\{", ChrW(1)).Replace("\}", ChrW(2)).Replace("\\", ChrW(3))
        s = s.Replace("{", "").Replace("}", "")
        s = s.Replace(ChrW(1), "{").Replace(ChrW(2), "}").Replace(ChrW(3), "\")
        Return DecodificarUnicode(s)
    End Function

    Public Shared Function DecodificarUnicode(s As String) As String
        Return Regex.Replace(s, "\\U\+([0-9A-Fa-f]{4})", Function(m) ChrW(Convert.ToInt32(m.Groups(1).Value, 16)).ToString())
    End Function

    Private Shared ReadOnly Cp1252 As Encoding = ObterCp1252()
    Private Shared Function ObterCp1252() As Encoding
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
        Return Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
    End Function

    ''' <summary>Prepara texto novo: carateres fora do cp1252 passam a \U+XXXX; em MTEXT escapa \ { }.</summary>
    Public Shared Function Codificar(texto As String, mtext As Boolean) As String
        Dim sb As New StringBuilder()
        For Each ch In texto
            If mtext AndAlso (ch = "\"c OrElse ch = "{"c OrElse ch = "}"c) Then
                sb.Append("\"c).Append(ch)
                Continue For
            End If
            Dim ok As Boolean
            Try
                Cp1252.GetBytes(ch.ToString()) : ok = True
            Catch
                ok = False
            End Try
            If ok Then sb.Append(ch) Else sb.Append("\U+").Append(AscW(ch).ToString("X4"))
        Next
        Return sb.ToString()
    End Function

    ''' <summary>Largura estimada (unidades do desenho, local) de um texto num MTEXT.</summary>
    Public Shared Function LarguraEstimada(texto As String, m As MText) As Double
        Dim fator = If(m.Style IsNot Nothing AndAlso m.Style.Width > 0, m.Style.Width, 1.0)
        Return texto.Length * m.Height * fator * DwgCampos.FatorLarguraCaracter
    End Function

    ''' <summary>Centro horizontal estimado (coordenadas locais) de um MTEXT, conforme o ponto de inserção.</summary>
    Public Shared Function CentroEstimado(m As MText) As Double
        Dim w = LarguraEstimada(TextoVisivel(m.Value), m)
        Select Case m.AttachmentPoint
            Case AttachmentPointType.TopCenter, AttachmentPointType.MiddleCenter, AttachmentPointType.BottomCenter
                Return m.InsertPoint.X
            Case AttachmentPointType.TopRight, AttachmentPointType.MiddleRight, AttachmentPointType.BottomRight
                Return m.InsertPoint.X - w / 2
            Case Else
                Return m.InsertPoint.X + w / 2
        End Select
    End Function
End Class

''' <summary>Um campo encontrado: o texto que o contém e o valor (substring visível).</summary>
Friend Class CampoDwg
    Public Texto As TextoDwg
    Public ValorAtual As String

    Public Sub New(t As TextoDwg, valor As String)
        Texto = t : ValorAtual = valor
    End Sub

    ''' <summary>Troca o valor mantendo o resto do texto (ex.: "Lista ") e a formatação.</summary>
    Public Sub Substituir(novo As String)
        Dim bruto = Texto.Bruto
        Dim cod = TextoDwg.Codificar(novo, Texto.EMText)
        Dim idx = bruto.IndexOf(ValorAtual, StringComparison.Ordinal)
        If idx >= 0 Then
            Texto.Bruto = bruto.Substring(0, idx) & cod & bruto.Substring(idx + ValorAtual.Length)
        Else
            ' valor partido por códigos de formatação: substitui no texto visível (perde a formatação interna)
            Dim vis = Texto.Visivel
            Dim j = vis.IndexOf(ValorAtual, StringComparison.Ordinal)
            Texto.Bruto = If(j >= 0, TextoDwg.Codificar(vis.Substring(0, j), Texto.EMText) & cod & TextoDwg.Codificar(vis.Substring(j + ValorAtual.Length), Texto.EMText), cod)
        End If
        ValorAtual = novo
    End Sub

    ''' <summary>Indica se o texto (atual) parece centrado na célula [esq, dir].</summary>
    Public Function EstaCentradoNaCelula(esq As Double, dir As Double) As Boolean
        Dim m = TryCast(Texto.Entidade, MText)
        If m Is Nothing OrElse Double.IsNaN(esq) OrElse Double.IsNaN(dir) Then Return False
        Return Math.Abs(TextoDwg.CentroEstimado(m) - (esq + dir) / 2) < 0.15 * (dir - esq)
    End Function

    ''' <summary>
    ''' Numa célula de tabela: se o texto original estava centrado, passa o ponto de
    ''' inserção para "centro" no meio da célula (o AutoCAD recentra sozinho qualquer
    ''' comprimento). Se estava alinhado à esquerda, mantém. Devolve um aviso se o
    ''' novo texto parecer mais largo do que a célula.
    ''' </summary>
    Public Function RealinharNaCelula(esq As Double, dir As Double, estavaCentrado As Boolean) As String
        Dim m = TryCast(Texto.Entidade, MText)
        If m Is Nothing OrElse Double.IsNaN(esq) OrElse Double.IsNaN(dir) Then Return Nothing
        Dim largCel = dir - esq, centroCel = (esq + dir) / 2
        If estavaCentrado Then
            Select Case m.AttachmentPoint
                Case AttachmentPointType.TopLeft, AttachmentPointType.TopRight : m.AttachmentPoint = AttachmentPointType.TopCenter
                Case AttachmentPointType.MiddleLeft, AttachmentPointType.MiddleRight : m.AttachmentPoint = AttachmentPointType.MiddleCenter
                Case AttachmentPointType.BottomLeft, AttachmentPointType.BottomRight : m.AttachmentPoint = AttachmentPointType.BottomCenter
            End Select
            m.InsertPoint = New CSMath.XYZ(centroCel, m.InsertPoint.Y, m.InsertPoint.Z)
        End If
        If TextoDwg.LarguraEstimada(ValorAtual, m) > largCel * 0.95 Then Return "o novo texto pode ser mais largo do que a célula."
        Return Nothing
    End Function
End Class
