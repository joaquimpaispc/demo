' =============================================================================
'  AssistenteIA - opção de IA com a xAI (Grok), API compatível com OpenAI:
'     POST https://api.x.ai/v1/chat/completions
'  Funções:
'   (a) IdentificarCamposAsync: recebe os textos do desenho (com posição) e
'       devolve onde estão Lista, Obra, código de barras, descrições.
'   (b) InterpretarPedidoAsync: pedido em linguagem natural + valores atuais
'       -> lista de alterações (AlteracoesCampos), que o código depois aplica.
'  Usa "structured outputs" (response_format json_schema) e valida a resposta.
'  A chave NUNCA está no código: variável de ambiente XAI_API_KEY ou ficheiro
'  assistenteia.json (ver README).
' =============================================================================
Option Strict On
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Nodes
Imports System.Text.RegularExpressions
Imports System.Threading

''' <summary>Configuração da ligação à xAI.</summary>
Public Class ConfiguracaoIA
    Public Const ModeloPorOmissao As String = "grok-4.3"
    Public Const EndpointPorOmissao As String = "https://api.x.ai/v1/chat/completions"
    Public Const NomeFicheiro As String = "assistenteia.json"

    Public Property ApiKey As String
    Public Property Modelo As String = ModeloPorOmissao
    Public Property Endpoint As String = EndpointPorOmissao
    Public Property TimeoutSegundos As Integer = 60

    ''' <summary>Locais onde é procurado o ficheiro de configuração (pela ordem).</summary>
    Public Shared Function LocaisFicheiro() As String()
        Return {
            Path.Combine(AppContext.BaseDirectory, NomeFicheiro),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PdfCampos", NomeFicheiro)}
    End Function

    ''' <summary>
    ''' Carrega a configuração: ficheiro assistenteia.json (se existir) e depois as
    ''' variáveis de ambiente XAI_API_KEY / XAI_MODEL / XAI_ENDPOINT, que têm prioridade.
    ''' </summary>
    Public Shared Function Carregar() As ConfiguracaoIA
        Dim c As New ConfiguracaoIA()
        For Each f In LocaisFicheiro()
            If Not File.Exists(f) Then Continue For
            Try
                Dim n = JsonNode.Parse(File.ReadAllText(f))
                If n?("ApiKey") IsNot Nothing Then c.ApiKey = n("ApiKey").GetValue(Of String)()
                If n?("Modelo") IsNot Nothing Then c.Modelo = n("Modelo").GetValue(Of String)()
                If n?("Endpoint") IsNot Nothing Then c.Endpoint = n("Endpoint").GetValue(Of String)()
                If n?("TimeoutSegundos") IsNot Nothing Then c.TimeoutSegundos = n("TimeoutSegundos").GetValue(Of Integer)()
            Catch ex As Exception
                Throw New PdfCamposException($"O ficheiro de configuração da IA '{f}' é inválido: {ex.Message}")
            End Try
            Exit For
        Next
        Dim ev = Environment.GetEnvironmentVariable("XAI_API_KEY")
        If Not String.IsNullOrWhiteSpace(ev) Then c.ApiKey = ev.Trim()
        ev = Environment.GetEnvironmentVariable("XAI_MODEL")
        If Not String.IsNullOrWhiteSpace(ev) Then c.Modelo = ev.Trim()
        ev = Environment.GetEnvironmentVariable("XAI_ENDPOINT")
        If Not String.IsNullOrWhiteSpace(ev) Then c.Endpoint = ev.Trim()
        Return c
    End Function

    Public ReadOnly Property TemChave As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(ApiKey)
        End Get
    End Property
End Class

Public Class AssistenteIA
    Implements IDisposable

    Private ReadOnly _http As HttpClient
    Public ReadOnly Property Configuracao As ConfiguracaoIA

    ''' <param name="configuracao">Nothing = ConfiguracaoIA.Carregar().</param>
    ''' <param name="handler">Opcional (testes): HttpMessageHandler simulado.</param>
    Public Sub New(Optional configuracao As ConfiguracaoIA = Nothing, Optional handler As HttpMessageHandler = Nothing)
        Me.Configuracao = If(configuracao, ConfiguracaoIA.Carregar())
        _http = If(handler Is Nothing, New HttpClient(), New HttpClient(handler))
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, Me.Configuracao.TimeoutSegundos))
    End Sub

    Public ReadOnly Property Configurada As Boolean
        Get
            Return Configuracao.TemChave
        End Get
    End Property

    ' -------------------------------------------------------------------------
    '  (a) Identificar campos
    ' -------------------------------------------------------------------------

    Public Async Function IdentificarCamposAsync(textos As IList(Of TextoPosicionado), Optional ct As CancellationToken = Nothing) As Task(Of List(Of CampoIdentificado))
        If textos Is Nothing OrElse textos.Count = 0 Then Throw New PdfCamposException("Não há textos para enviar à IA.")
        Dim sb As New StringBuilder()
        sb.AppendLine("id|x|y|altura|bloco|texto")
        For Each t In textos.Take(1500)
            sb.AppendLine($"{t.Id}|{t.X.ToString(Globalization.CultureInfo.InvariantCulture)}|{t.Y.ToString(Globalization.CultureInfo.InvariantCulture)}|{t.Altura.ToString(Globalization.CultureInfo.InvariantCulture)}|{t.Bloco}|{t.Texto.Replace(vbCr, " ").Replace(vbLf, " ")}")
        Next
        Dim sistema =
            "És um assistente que analisa a legenda (title block) de desenhos técnicos exportados do SOLIDWORKS (PDF ou DWG), em português. " &
            "Recebes uma lista de textos com id, posição (x, y; y cresce para cima), altura e bloco. Identifica os campos: " &
            "lista (número da lista, normalmente junto a 'Lista'), obra_poco (valor a seguir a 'Obra Poço:'), obra_periplast (valor a seguir a 'Obra Periplast:'), " &
            "codigo_barras (texto entre asteriscos, ex. *3066.05-5432-0*), descricao_1 e descricao_2 (as duas linhas de descrição da peça na legenda, por baixo da linha das obras). " &
            "Para cada campo encontrado devolve texto_id (o id do texto que contém o valor) e valor (APENAS o valor, exatamente como aparece no texto, sem o rótulo). " &
            "Omite os campos que não encontrares. Não inventes valores."
        Dim schema = New JsonObject From {
            {"type", "object"},
            {"properties", New JsonObject From {
                {"campos", New JsonObject From {
                    {"type", "array"},
                    {"items", New JsonObject From {
                        {"type", "object"},
                        {"properties", New JsonObject From {
                            {"campo", New JsonObject From {{"type", "string"}, {"enum", ArrayJson(CamposIA.Identificaveis)}}},
                            {"texto_id", New JsonObject From {{"type", "integer"}}},
                            {"valor", New JsonObject From {{"type", "string"}}}}},
                        {"required", ArrayJson({"campo", "texto_id", "valor"})},
                        {"additionalProperties", False}}}}}}},
            {"required", ArrayJson({"campos"})},
            {"additionalProperties", False}}
        Dim conteudo = Await PedirJsonAsync(sistema, sb.ToString(), "campos_desenho", schema, ct).ConfigureAwait(False)
        Return ProcessarRespostaIdentificacao(conteudo, textos)
    End Function

    ''' <summary>Valida a resposta JSON da identificação (público para testes).</summary>
    Public Shared Function ProcessarRespostaIdentificacao(json As String, textos As IList(Of TextoPosicionado)) As List(Of CampoIdentificado)
        Dim raiz = LerJson(json)
        Dim arr = TryCast(raiz("campos"), JsonArray)
        If arr Is Nothing Then Throw New PdfCamposException("Resposta da IA inválida: falta a lista 'campos'.")
        Dim r As New List(Of CampoIdentificado)
        For Each n In arr
            Dim o = TryCast(n, JsonObject)
            If o Is Nothing Then Continue For
            Dim campo = Texto(o, "campo"), valor = Texto(o, "valor")
            Dim id As Integer
            If Not Integer.TryParse(o("texto_id")?.ToString(), id) Then Continue For
            If campo Is Nothing OrElse Not CamposIA.Identificaveis.Contains(campo) Then Continue For
            If String.IsNullOrWhiteSpace(valor) Then Continue For
            Dim t = textos.FirstOrDefault(Function(x) x.Id = id)
            If t Is Nothing Then Continue For
            Dim v = If(campo = CamposIA.CodigoBarras, valor.Trim().Trim("*"c), valor.Trim())
            If t.Texto.IndexOf(v, StringComparison.Ordinal) < 0 Then Continue For   ' valor tem de existir no texto indicado
            If r.Any(Function(x) x.Campo = campo) Then Continue For
            r.Add(New CampoIdentificado With {.Campo = campo, .TextoId = id, .Valor = v})
        Next
        Return r
    End Function

    ' -------------------------------------------------------------------------
    '  (b) Interpretar pedido em linguagem natural
    ' -------------------------------------------------------------------------

    Public Async Function InterpretarPedidoAsync(pedido As String, atuais As CamposPdf, Optional ct As CancellationToken = Nothing) As Task(Of AlteracoesCampos)
        If String.IsNullOrWhiteSpace(pedido) Then Throw New PdfCamposException("Escreva o pedido (ex.: 'muda a obra para 4077.10').")
        Dim sistema =
            "Converte pedidos em português sobre a legenda de um desenho técnico numa lista de alterações. " &
            "Campos possíveis: lista (só o número), obra_poco, obra_periplast, codigo_barras (só se o utilizador o pedir explicitamente; sem asteriscos), " &
            "descricao_1 (1.ª linha da descrição da legenda), descricao_2 (2.ª linha), descricao_peca (descrição de uma linha da lista de peças; indica em 'peca' a Marcação, ex. 5432-1, ou o ITEM). " &
            "'Obra' sem mais indicação significa obra_poco. 'Descrição' sem mais indicação significa descricao_2 se existir só um nome curto, ou descricao_1 se o texto incluir pormenores; " &
            "na dúvida usa descricao_2. Usa peca = null exceto em descricao_peca. Não alteres campos que não foram pedidos. Em 'observacoes' explica brevemente ambiguidades (ou deixa vazio)."
        Dim atuaisJson As New JsonObject From {
            {"lista", atuais?.Lista}, {"obra_poco", atuais?.ObraPoco}, {"obra_periplast", atuais?.ObraPeriplast},
            {"codigo_barras", atuais?.CodigoBarras}}
        Dim dwg = TryCast(atuais, CamposDwg)
        If dwg IsNot Nothing Then
            atuaisJson("descricao_1") = If(dwg.Descricoes.Count > 0, dwg.Descricoes(0), Nothing)
            atuaisJson("descricao_2") = If(dwg.Descricoes.Count > 1, dwg.Descricoes(1), Nothing)
            Dim pecas As New JsonArray()
            For Each p In dwg.Pecas
                pecas.Add(New JsonObject From {{"item", p.Item}, {"marcacao", p.Marcacao}, {"descricao", p.Descricao}})
            Next
            atuaisJson("pecas") = pecas
        End If
        Dim utilizador = "Valores atuais: " & atuaisJson.ToJsonString() & vbLf & "Pedido: " & pedido
        Dim schema = New JsonObject From {
            {"type", "object"},
            {"properties", New JsonObject From {
                {"alteracoes", New JsonObject From {
                    {"type", "array"},
                    {"items", New JsonObject From {
                        {"type", "object"},
                        {"properties", New JsonObject From {
                            {"campo", New JsonObject From {{"type", "string"}, {"enum", ArrayJson(CamposIA.Alteraveis)}}},
                            {"peca", New JsonObject From {{"type", ArrayJson({"string", "null"})}}},
                            {"valor", New JsonObject From {{"type", "string"}}}}},
                        {"required", ArrayJson({"campo", "peca", "valor"})},
                        {"additionalProperties", False}}}}},
                {"observacoes", New JsonObject From {{"type", "string"}}}}},
            {"required", ArrayJson({"alteracoes", "observacoes"})},
            {"additionalProperties", False}}
        Dim conteudo = Await PedirJsonAsync(sistema, utilizador, "alteracoes_desenho", schema, ct).ConfigureAwait(False)
        Return ProcessarRespostaPedido(conteudo, atuais)
    End Function

    ''' <summary>Valida a resposta JSON do pedido e converte em AlteracoesCampos (público para testes).</summary>
    Public Shared Function ProcessarRespostaPedido(json As String, atuais As CamposPdf) As AlteracoesCampos
        Dim raiz = LerJson(json)
        Dim arr = TryCast(raiz("alteracoes"), JsonArray)
        If arr Is Nothing Then Throw New PdfCamposException("Resposta da IA inválida: falta a lista 'alteracoes'.")
        Dim obs = Texto(raiz, "observacoes")
        Dim a As New AlteracoesCampos()
        Dim dwg = TryCast(atuais, CamposDwg)
        For Each n In arr
            Dim o = TryCast(n, JsonObject)
            If o Is Nothing Then Continue For
            Dim campo = Texto(o, "campo"), valor = Texto(o, "valor")?.Trim(), peca = Texto(o, "peca")?.Trim()
            If campo Is Nothing OrElse Not CamposIA.Alteraveis.Contains(campo) Then
                Throw New PdfCamposException($"Resposta da IA inválida: campo desconhecido '{campo}'.")
            End If
            If String.IsNullOrEmpty(valor) Then Continue For
            Select Case campo
                Case CamposIA.Lista
                    Dim m = Regex.Match(valor, "\d+")
                    If Not m.Success Then Throw New PdfCamposException($"A IA propôs uma Lista inválida: '{valor}'.")
                    a.Lista = m.Value
                Case CamposIA.ObraPoco : a.ObraPoco = valor
                Case CamposIA.ObraPeriplast : a.ObraPeriplast = valor
                Case CamposIA.CodigoBarras
                    Dim v = valor.Trim("*"c).ToUpperInvariant()
                    If Not Regex.IsMatch(v, "^[0-9A-Z.\-$/+%]+$") Then Throw New PdfCamposException($"A IA propôs um código de barras inválido: '{valor}'.")
                    a.CodigoBarras = v
                Case CamposIA.Descricao1 : a.Descricao1 = valor
                Case CamposIA.Descricao2 : a.Descricao2 = valor
                Case CamposIA.DescricaoPeca
                    If String.IsNullOrEmpty(peca) Then Throw New PdfCamposException("A IA não indicou a que peça se refere a descrição (Marcação ou ITEM).")
                    If dwg IsNot Nothing AndAlso dwg.Pecas.Count > 0 AndAlso
                       Not dwg.Pecas.Any(Function(p) String.Equals(p.Marcacao, peca, StringComparison.OrdinalIgnoreCase) OrElse String.Equals(p.Item, peca, StringComparison.OrdinalIgnoreCase)) Then
                        Throw New PdfCamposException($"A IA referiu uma peça inexistente: '{peca}'.")
                    End If
                    a.DescricoesPecas(peca) = valor
            End Select
        Next
        If a.Vazia Then
            Throw New PdfCamposException("A IA não identificou nenhuma alteração no pedido." & If(String.IsNullOrWhiteSpace(obs), "", " " & obs))
        End If
        Return a
    End Function

    ' -------------------------------------------------------------------------
    '  HTTP
    ' -------------------------------------------------------------------------

    Private Async Function PedirJsonAsync(sistema As String, utilizador As String, nomeSchema As String, schema As JsonObject, ct As CancellationToken) As Task(Of String)
        If Not Configurada Then
            Throw New PdfCamposException("A chave da API xAI não está configurada. Defina a variável de ambiente XAI_API_KEY " &
                                         "ou crie o ficheiro " & ConfiguracaoIA.NomeFicheiro & " (ver README).")
        End If
        Dim formato As JsonObject = New JsonObject From {
            {"type", "json_schema"},
            {"json_schema", New JsonObject From {{"name", nomeSchema}, {"schema", schema}, {"strict", True}}}}
        Dim recorrer As Boolean = False
        Try
            Return Await EnviarAsync(sistema, utilizador, formato, ct).ConfigureAwait(False)
        Catch ex As RespostaHttpException When ex.Estado = HttpStatusCode.BadRequest AndAlso ex.Corpo.IndexOf("response_format", StringComparison.OrdinalIgnoreCase) >= 0
            recorrer = True   ' modelo sem suporte para json_schema
        Catch ex As RespostaHttpException
            Throw New PdfCamposException(MensagemErroHttp(ex.Estado, ex.Corpo))
        End Try
        ' alternativa: json_object com o esquema descrito no texto
        Dim formato2 As New JsonObject From {{"type", "json_object"}}
        Try
            Return Await EnviarAsync(sistema & " Responde apenas com JSON válido conforme este esquema: " & schema.ToJsonString(), utilizador, formato2, ct).ConfigureAwait(False)
        Catch ex As RespostaHttpException
            Throw New PdfCamposException(MensagemErroHttp(ex.Estado, ex.Corpo))
        End Try
    End Function

    Private Async Function EnviarAsync(sistema As String, utilizador As String, formato As JsonObject, ct As CancellationToken) As Task(Of String)
        Dim corpo As New JsonObject From {
            {"model", Configuracao.Modelo},
            {"temperature", 0},
            {"messages", New JsonArray(
                New JsonObject From {{"role", "system"}, {"content", sistema}},
                New JsonObject From {{"role", "user"}, {"content", utilizador}})},
            {"response_format", formato}}
        Using req As New HttpRequestMessage(HttpMethod.Post, Configuracao.Endpoint)
            req.Headers.Authorization = New AuthenticationHeaderValue("Bearer", Configuracao.ApiKey)
            req.Content = New StringContent(corpo.ToJsonString(), Encoding.UTF8, "application/json")
            Dim resp As HttpResponseMessage
            Try
                resp = Await _http.SendAsync(req, ct).ConfigureAwait(False)
            Catch ex As TaskCanceledException When Not ct.IsCancellationRequested
                Throw New PdfCamposException("Tempo esgotado ao contactar a xAI. Verifique a ligação à Internet e tente novamente.")
            Catch ex As HttpRequestException
                Throw New PdfCamposException("Falha de rede ao contactar a xAI: " & ex.Message)
            End Try
            Using resp
                Dim txt = Await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(False)
                If Not resp.IsSuccessStatusCode Then Throw New RespostaHttpException(resp.StatusCode, txt)
                Return ExtrairConteudo(txt)
            End Using
        End Using
    End Function

    ''' <summary>Obtém choices[0].message.content da resposta (público para testes).</summary>
    Public Shared Function ExtrairConteudo(respostaApi As String) As String
        Try
            Dim n = JsonNode.Parse(respostaApi)
            Dim c = n?("choices")?(0)?("message")?("content")
            If c Is Nothing Then Throw New PdfCamposException("Resposta da xAI sem conteúdo.")
            Return c.GetValue(Of String)()
        Catch ex As JsonException
            Throw New PdfCamposException("Resposta da xAI inválida (não é JSON): " & ex.Message)
        End Try
    End Function

    Public Shared Function MensagemErroHttp(estado As HttpStatusCode, corpo As String) As String
        Dim det = If(corpo, "")
        If det.Length > 300 Then det = det.Substring(0, 300) & "..."
        Select Case CInt(estado)
            Case 401, 403 : Return $"A xAI recusou a chave da API ({CInt(estado)}). Verifique XAI_API_KEY."
            Case 404 : Return $"Modelo ou endereço não encontrado (404). Verifique o modelo configurado. {det}"
            Case 429 : Return "Limite de pedidos ou de crédito da xAI atingido (429). Aguarde ou verifique a conta."
            Case >= 500 : Return $"O serviço da xAI devolveu um erro ({CInt(estado)}). Tente mais tarde."
            Case Else : Return $"Erro da xAI ({CInt(estado)}): {det}"
        End Select
    End Function

    ' -------------------------------------------------------------------------
    '  Utilitários JSON
    ' -------------------------------------------------------------------------

    Private Shared Function LerJson(json As String) As JsonObject
        If String.IsNullOrWhiteSpace(json) Then Throw New PdfCamposException("Resposta da IA vazia.")
        Dim s = json.Trim()
        ' remover eventuais blocos ```json ... ```
        Dim m = Regex.Match(s, "^```(?:json)?\s*(.*?)\s*```$", RegexOptions.Singleline)
        If m.Success Then s = m.Groups(1).Value
        Try
            Dim o = TryCast(JsonNode.Parse(s), JsonObject)
            If o Is Nothing Then Throw New PdfCamposException("Resposta da IA inválida: não é um objeto JSON.")
            Return o
        Catch ex As JsonException
            Throw New PdfCamposException("Resposta da IA inválida (JSON mal formado): " & ex.Message)
        End Try
    End Function

    Private Shared Function Texto(o As JsonObject, chave As String) As String
        Dim n = o(chave)
        If n Is Nothing Then Return Nothing
        Dim v = TryCast(n, JsonValue)
        If v Is Nothing Then Return n.ToJsonString()
        Dim s As String = Nothing
        If v.TryGetValue(Of String)(s) Then Return s
        Return v.ToString()
    End Function

    Private Shared Function ArrayJson(itens As IEnumerable(Of String)) As JsonArray
        Dim a As New JsonArray()
        For Each i In itens
            a.Add(i)
        Next
        Return a
    End Function

    Private Class RespostaHttpException
        Inherits Exception
        Public ReadOnly Estado As HttpStatusCode
        Public ReadOnly Corpo As String
        Public Sub New(estado As HttpStatusCode, corpo As String)
            MyBase.New($"HTTP {CInt(estado)}")
            Me.Estado = estado : Me.Corpo = If(corpo, "")
        End Sub
    End Class

    Public Sub Dispose() Implements IDisposable.Dispose
        _http.Dispose()
    End Sub
End Class
