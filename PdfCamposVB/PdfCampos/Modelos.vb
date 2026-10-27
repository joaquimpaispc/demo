' Tipos partilhados entre PDF, DWG e o assistente de IA.
Option Strict On

''' <summary>Nomes (chaves) dos campos usados na comunicação com a IA.</summary>
Public NotInheritable Class CamposIA
    Public Const Lista As String = "lista"
    Public Const ObraPoco As String = "obra_poco"
    Public Const ObraPeriplast As String = "obra_periplast"
    Public Const CodigoBarras As String = "codigo_barras"
    Public Const Descricao1 As String = "descricao_1"
    Public Const Descricao2 As String = "descricao_2"
    Public Const DescricaoPeca As String = "descricao_peca"

    ''' <summary>Campos que a IA pode identificar num desenho.</summary>
    Public Shared ReadOnly Identificaveis As String() = {Lista, ObraPoco, ObraPeriplast, CodigoBarras, Descricao1, Descricao2}
    ''' <summary>Campos que um pedido em linguagem natural pode alterar.</summary>
    Public Shared ReadOnly Alteraveis As String() = {Lista, ObraPoco, ObraPeriplast, CodigoBarras, Descricao1, Descricao2, DescricaoPeca}

    Private Sub New()
    End Sub
End Class

''' <summary>Um texto do desenho com a sua posição (enviado à IA).</summary>
Public Class TextoPosicionado
    Public Property Id As Integer
    Public Property Texto As String
    Public Property X As Double
    Public Property Y As Double
    Public Property Altura As Double
    ''' <summary>Bloco/espaço onde o texto está (DWG) ou página (PDF).</summary>
    Public Property Bloco As String
End Class

''' <summary>Um campo identificado (pela IA): em que texto está e qual o valor atual.</summary>
Public Class CampoIdentificado
    Public Property Campo As String
    Public Property TextoId As Integer
    Public Property Valor As String
    Public Overrides Function ToString() As String
        Return $"{Campo} = '{Valor}' (texto #{TextoId})"
    End Function
End Class

''' <summary>Alterações a aplicar. Valores Nothing/vazios = não alterar.</summary>
Public Class AlteracoesCampos
    Public Property Lista As String
    Public Property ObraPoco As String
    Public Property ObraPeriplast As String
    ''' <summary>Conteúdo do código de barras (sem asteriscos). Se vazio e a Obra Poço mudar, é reconstruído.</summary>
    Public Property CodigoBarras As String
    ''' <summary>1.ª linha da descrição na legenda (ex.: "Pá Superior c/ Carboneto de Crómio"). Só DWG.</summary>
    Public Property Descricao1 As String
    ''' <summary>2.ª linha da descrição na legenda (ex.: "Pá Superior"). Só DWG.</summary>
    Public Property Descricao2 As String
    ''' <summary>Descrições da lista de peças: chave = Marcação (ex.: "5432-1") ou ITEM (ex.: "1"). Só DWG.</summary>
    Public Property DescricoesPecas As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Public ReadOnly Property Vazia As Boolean
        Get
            Return String.IsNullOrWhiteSpace(Lista) AndAlso String.IsNullOrWhiteSpace(ObraPoco) AndAlso
                   String.IsNullOrWhiteSpace(ObraPeriplast) AndAlso String.IsNullOrWhiteSpace(CodigoBarras) AndAlso
                   String.IsNullOrWhiteSpace(Descricao1) AndAlso String.IsNullOrWhiteSpace(Descricao2) AndAlso
                   DescricoesPecas.Count = 0
        End Get
    End Property

    Public Overrides Function ToString() As String
        Dim p As New List(Of String)
        If Not String.IsNullOrWhiteSpace(Lista) Then p.Add("Lista=" & Lista)
        If Not String.IsNullOrWhiteSpace(ObraPoco) Then p.Add("Obra Poço=" & ObraPoco)
        If Not String.IsNullOrWhiteSpace(ObraPeriplast) Then p.Add("Obra Periplast=" & ObraPeriplast)
        If Not String.IsNullOrWhiteSpace(CodigoBarras) Then p.Add("Código barras=" & CodigoBarras)
        If Not String.IsNullOrWhiteSpace(Descricao1) Then p.Add("Descrição 1=" & Descricao1)
        If Not String.IsNullOrWhiteSpace(Descricao2) Then p.Add("Descrição 2=" & Descricao2)
        For Each kv In DescricoesPecas
            p.Add($"Peça {kv.Key}={kv.Value}")
        Next
        Return If(p.Count = 0, "(sem alterações)", String.Join("; ", p))
    End Function
End Class

''' <summary>Uma linha da lista de peças (DWG).</summary>
Public Class LinhaPeca
    Public Property Item As String
    Public Property Quantidade As String
    Public Property Marcacao As String
    Public Property Descricao As String
    Public Overrides Function ToString() As String
        Return $"{Item} | {Quantidade} | {Marcacao} | {Descricao}"
    End Function
End Class

''' <summary>Valores lidos de um DWG (inclui descrições e lista de peças).</summary>
Public Class CamposDwg
    Inherits CamposPdf
    ''' <summary>Linhas de descrição da legenda, de cima para baixo.</summary>
    Public Property Descricoes As New List(Of String)
    Public Property Pecas As New List(Of LinhaPeca)

    Public Overrides Function ToString() As String
        Dim sb As New System.Text.StringBuilder(MyBase.ToString())
        For i = 0 To Descricoes.Count - 1
            sb.Append($"; Descrição {i + 1}={Descricoes(i)}")
        Next
        For Each p In Pecas
            sb.AppendLine().Append("   Peça: " & p.ToString())
        Next
        Return sb.ToString()
    End Function
End Class

''' <summary>Ponto de entrada comum: escolhe PDF ou DWG pela extensão.</summary>
Public NotInheritable Class Desenhos
    Private Sub New()
    End Sub

    Public Shared Function EDwg(caminho As String) As Boolean
        Return String.Equals(System.IO.Path.GetExtension(caminho), ".dwg", StringComparison.OrdinalIgnoreCase)
    End Function

    Public Shared Function Ler(caminho As String, Optional mapaIA As IList(Of CampoIdentificado) = Nothing) As CamposPdf
        Return If(EDwg(caminho), DwgCampos.Ler(caminho, mapaIA), PdfCampos.Ler(caminho, mapaIA))
    End Function

    Public Shared Function ExtrairTextos(caminho As String) As List(Of TextoPosicionado)
        Return If(EDwg(caminho), DwgCampos.ExtrairTextos(caminho), PdfCampos.ExtrairTextos(caminho))
    End Function

    Public Shared Function Substituir(entrada As String, saida As String, alteracoes As AlteracoesCampos,
                                      Optional mapaIA As IList(Of CampoIdentificado) = Nothing) As CamposPdf
        If EDwg(entrada) <> EDwg(saida) Then
            Throw New PdfCamposException("O ficheiro de saída tem de ter a mesma extensão do ficheiro de entrada.")
        End If
        Return If(EDwg(entrada), DwgCampos.Substituir(entrada, saida, alteracoes, mapaIA), PdfCampos.Substituir(entrada, saida, alteracoes, mapaIA))
    End Function

    ''' <summary>Indica se faltam campos principais (Lista, Obra Poço ou código de barras).</summary>
    Public Shared Function FaltamCampos(c As CamposPdf) As Boolean
        Return c Is Nothing OrElse c.Lista Is Nothing OrElse c.ObraPoco Is Nothing OrElse c.CodigoBarras Is Nothing
    End Function

    ''' <summary>
    ''' Lê os campos; se usarIA for True, ou se faltarem campos e a IA estiver configurada,
    ''' pede à IA a identificação dos campos e volta a ler com esse mapa.
    ''' Devolve os campos e o mapa da IA (Nothing se não foi usada) para passar a Substituir.
    ''' </summary>
    Public Shared Async Function LerComIAAsync(caminho As String, assistente As AssistenteIA, usarIA As Boolean,
                                               Optional ct As Threading.CancellationToken = Nothing) As Task(Of (Campos As CamposPdf, Mapa As List(Of CampoIdentificado)))
        Dim c = Ler(caminho)
        If assistente Is Nothing Then Return (c, Nothing)
        Dim precisa = usarIA OrElse (FaltamCampos(c) AndAlso assistente.Configurada)
        If Not precisa Then Return (c, Nothing)
        Dim textos = ExtrairTextos(caminho)
        Dim mapa = Await assistente.IdentificarCamposAsync(textos, ct).ConfigureAwait(False)
        Return (Ler(caminho, mapa), mapa)
    End Function
End Class
