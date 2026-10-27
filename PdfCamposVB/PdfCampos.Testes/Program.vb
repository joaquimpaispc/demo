' Testes simples (sem framework): IA com HttpMessageHandler simulado, validação de
' JSON e, se existirem, ficheiros de exemplo PDF/DWG.
' Uso: PdfCampos.Testes [pasta com in.pdf / in.dwg]
Option Strict On
Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Text.Json.Nodes
Imports System.Threading
Imports PdfCampos

Module Program
    Private _ok As Integer, _falhas As Integer
    Private _pasta As String

    Function Main(args As String()) As Integer
        Console.OutputEncoding = System.Text.Encoding.UTF8
        _pasta = If(args.Length > 0, args(0), Directory.GetCurrentDirectory())
        Teste("Sem chave -> erro em português", AddressOf SemChave)
        Teste("Identificação: pedido HTTP e mapa válido", AddressOf IdentificacaoValida)
        Teste("Identificação: ids/valores inválidos são descartados", AddressOf IdentificacaoFiltra)
        Teste("Identificação: JSON dentro de ```json", AddressOf BlocoCodigo)
        Teste("Pedido: conversão para AlteracoesCampos", AddressOf PedidoValido)
        Teste("Pedido: campo desconhecido -> erro", AddressOf PedidoCampoInvalido)
        Teste("Pedido: peça inexistente -> erro", AddressOf PedidoPecaInexistente)
        Teste("Pedido: sem alterações -> erro com observações", AddressOf PedidoVazio)
        Teste("HTTP 401 -> mensagem de chave recusada", AddressOf Erro401)
        Teste("HTTP 429 -> mensagem de limite", AddressOf Erro429)
        Teste("HTTP 400 response_format -> repete com json_object", AddressOf RecorreJsonObject)
        Teste("Falha de rede -> mensagem", AddressOf FalhaRede)
        Teste("Tempo esgotado -> mensagem", AddressOf TempoEsgotado)
        Teste("Configuração: XAI_API_KEY / XAI_MODEL", AddressOf ConfigAmbiente)
        Dim dwg = Path.Combine(_pasta, "in.dwg"), pdf = Path.Combine(_pasta, "in.pdf")
        If File.Exists(dwg) Then
            Teste("DWG: mapa da IA tem prioridade", Sub() MapaIADwg(dwg))
            Teste("DWG: pedido (IA simulada) aplicado e gravado", Sub() PedidoDwgFimAFim(dwg))
        Else
            Console.WriteLine($"(ignorado: {dwg} não existe)")
        End If
        If File.Exists(pdf) Then
            Teste("PDF: mapa da IA tem prioridade", Sub() MapaIAPdf(pdf))
        Else
            Console.WriteLine($"(ignorado: {pdf} não existe)")
        End If
        Console.WriteLine($"{_ok} OK, {_falhas} falhas")
        Return If(_falhas = 0, 0, 1)
    End Function

    ' ------------------------------------------------------------------ util

    Private Sub Teste(nome As String, corpo As Action)
        Try
            corpo()
            _ok += 1 : Console.WriteLine("OK     " & nome)
        Catch ex As Exception
            _falhas += 1 : Console.WriteLine("FALHA  " & nome & ": " & ex.Message)
        End Try
    End Sub

    Private Sub Afirmar(cond As Boolean, msg As String)
        If Not cond Then Throw New Exception(msg)
    End Sub

    Private Function Erro(Of T)(f As Func(Of T)) As String
        Try
            f()
        Catch ex As AggregateException
            Return ex.InnerException.Message
        Catch ex As Exception
            Return ex.Message
        End Try
        Throw New Exception("era esperada uma exceção")
    End Function

    Private Class HandlerSimulado
        Inherits HttpMessageHandler
        Public ReadOnly Pedidos As New List(Of (Req As HttpRequestMessage, Corpo As String))
        Private ReadOnly _resposta As Func(Of Integer, HttpResponseMessage)
        Public Sub New(resposta As Func(Of Integer, HttpResponseMessage))
            _resposta = resposta
        End Sub
        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, ct As CancellationToken) As Task(Of HttpResponseMessage)
            Dim corpo = If(request.Content Is Nothing, "", Await request.Content.ReadAsStringAsync(ct))
            Pedidos.Add((request, corpo))
            Return _resposta(Pedidos.Count)
        End Function
    End Class

    Private Function RespostaChat(conteudo As String, Optional estado As HttpStatusCode = HttpStatusCode.OK) As HttpResponseMessage
        Dim o As New JsonObject From {
            {"id", "x"}, {"object", "chat.completion"},
            {"choices", New JsonArray(New JsonObject From {{"index", 0}, {"message", New JsonObject From {{"role", "assistant"}, {"content", conteudo}}}, {"finish_reason", "stop"}})}}
        Return New HttpResponseMessage(estado) With {.Content = New StringContent(o.ToJsonString(), System.Text.Encoding.UTF8, "application/json")}
    End Function

    Private Function Cfg() As ConfiguracaoIA
        Return New ConfiguracaoIA With {.ApiKey = "chave-de-teste", .Modelo = "modelo-teste"}
    End Function

    Private Function TextosExemplo() As List(Of TextoPosicionado)
        Return New List(Of TextoPosicionado) From {
            New TextoPosicionado With {.Id = 0, .Texto = "Lista 7874", .X = 10, .Y = 10, .Altura = 2.6},
            New TextoPosicionado With {.Id = 1, .Texto = "Obra Poço:", .X = 50, .Y = 40, .Altura = 2.6},
            New TextoPosicionado With {.Id = 2, .Texto = "3066.05", .X = 65, .Y = 40, .Altura = 2.6},
            New TextoPosicionado With {.Id = 3, .Texto = "*3066.05-5432-0*", .X = 40, .Y = 20, .Altura = 5.8}}
    End Function

    Private Function DwgExemplo() As CamposDwg
        Dim c As New CamposDwg With {.Lista = "7874", .ObraPoco = "3066.05", .ObraPeriplast = "2514.02", .CodigoBarras = "3066.05-5432-0"}
        c.Descricoes.AddRange({"Pá Superior c/ Carboneto de Crómio", "Pá Superior"})
        c.Pecas.Add(New LinhaPeca With {.Item = "1", .Quantidade = "1", .Marcacao = "5432-1", .Descricao = "Peça da Pá Superior"})
        c.Pecas.Add(New LinhaPeca With {.Item = "2", .Quantidade = "1", .Marcacao = "5432-2", .Descricao = "Peça da Pá Superior"})
        Return c
    End Function

    ' ------------------------------------------------------------------ testes

    Private Sub SemChave()
        Dim antes = Environment.GetEnvironmentVariable("XAI_API_KEY")
        Using ia As New AssistenteIA(New ConfiguracaoIA(), New HandlerSimulado(Function(n) RespostaChat("{}")))
            Afirmar(Not ia.Configurada, "não devia estar configurada")
            Dim m = Erro(Function() ia.IdentificarCamposAsync(TextosExemplo()).Result)
            Afirmar(m.Contains("XAI_API_KEY"), m)
        End Using
    End Sub

    Private Sub IdentificacaoValida()
        Dim h As New HandlerSimulado(Function(n) RespostaChat(
            "{""campos"":[{""campo"":""lista"",""texto_id"":0,""valor"":""7874""},{""campo"":""obra_poco"",""texto_id"":2,""valor"":""3066.05""},{""campo"":""codigo_barras"",""texto_id"":3,""valor"":""*3066.05-5432-0*""}]}"))
        Using ia As New AssistenteIA(Cfg(), h)
            Dim r = ia.IdentificarCamposAsync(TextosExemplo()).Result
            Afirmar(r.Count = 3, "esperados 3 campos, obtidos " & r.Count)
            Afirmar(r.Any(Function(c) c.Campo = "codigo_barras" AndAlso c.Valor = "3066.05-5432-0"), "código sem asteriscos")
        End Using
        Afirmar(h.Pedidos.Count = 1, "1 pedido HTTP")
        Dim req = h.Pedidos(0)
        Afirmar(req.Req.RequestUri.ToString() = ConfiguracaoIA.EndpointPorOmissao, "endpoint")
        Afirmar(req.Req.Headers.Authorization.Scheme = "Bearer" AndAlso req.Req.Headers.Authorization.Parameter = "chave-de-teste", "autorização")
        Dim j = JsonNode.Parse(req.Corpo)
        Afirmar(j("model").GetValue(Of String)() = "modelo-teste", "modelo")
        Afirmar(j("response_format")("type").GetValue(Of String)() = "json_schema", "response_format")
        Afirmar(j("response_format")("json_schema")("strict").GetValue(Of Boolean)(), "strict")
        Afirmar(j("messages").AsArray().Count = 2, "mensagens")
        Afirmar(j("messages")(1)("content").GetValue(Of String)().Contains("3|40|20|5.8||*3066.05-5432-0*"), "textos enviados")
    End Sub

    Private Sub IdentificacaoFiltra()
        Dim r = AssistenteIA.ProcessarRespostaIdentificacao(
            "{""campos"":[{""campo"":""lista"",""texto_id"":99,""valor"":""7874""}," &
            "{""campo"":""obra_poco"",""texto_id"":0,""valor"":""3066.05""}," &
            "{""campo"":""inventado"",""texto_id"":2,""valor"":""3066.05""}," &
            "{""campo"":""obra_poco"",""texto_id"":2,""valor"":""3066.05""}," &
            "{""campo"":""obra_poco"",""texto_id"":2,""valor"":""3066.05""}]}", TextosExemplo())
        Afirmar(r.Count = 1 AndAlso r(0).Campo = "obra_poco" AndAlso r(0).TextoId = 2, "só deve ficar obra_poco #2; obtido " & String.Join(",", r))
        Dim m = Erro(Function() AssistenteIA.ProcessarRespostaIdentificacao("não é json", TextosExemplo()))
        Afirmar(m.Contains("JSON"), m)
        m = Erro(Function() AssistenteIA.ProcessarRespostaIdentificacao("{""x"":1}", TextosExemplo()))
        Afirmar(m.Contains("campos"), m)
    End Sub

    Private Sub BlocoCodigo()
        Dim r = AssistenteIA.ProcessarRespostaIdentificacao("```json" & vbLf & "{""campos"":[{""campo"":""lista"",""texto_id"":0,""valor"":""7874""}]}" & vbLf & "```", TextosExemplo())
        Afirmar(r.Count = 1, "bloco de código")
    End Sub

    Private Sub PedidoValido()
        Dim h As New HandlerSimulado(Function(n) RespostaChat(
            "{""alteracoes"":[{""campo"":""lista"",""peca"":null,""valor"":""Lista 1234""},{""campo"":""obra_poco"",""peca"":null,""valor"":""4077.10""}," &
            "{""campo"":""descricao_peca"",""peca"":""5432-1"",""valor"":""Peça nova""},{""campo"":""descricao_2"",""peca"":null,""valor"":""Pá Inferior""}],""observacoes"":""""}"))
        Using ia As New AssistenteIA(Cfg(), h)
            Dim a = ia.InterpretarPedidoAsync("lista 1234, obra 4077.10, peça 5432-1 'Peça nova', descrição Pá Inferior", DwgExemplo()).Result
            Afirmar(a.Lista = "1234", "lista=" & a.Lista)
            Afirmar(a.ObraPoco = "4077.10", "obra")
            Afirmar(a.Descricao2 = "Pá Inferior" AndAlso a.Descricao1 Is Nothing, "descrições")
            Afirmar(a.DescricoesPecas("5432-1") = "Peça nova", "peça")
            Afirmar(a.CodigoBarras Is Nothing, "o código é reconstruído pelo programa, não pela IA")
        End Using
        Dim j = JsonNode.Parse(h.Pedidos(0).Corpo)
        Dim u = j("messages")(1)("content").GetValue(Of String)()
        Afirmar(u.Contains("5432-1") AndAlso u.Contains("3066.05"), "valores atuais enviados")
    End Sub

    Private Sub PedidoCampoInvalido()
        Dim m = Erro(Function() AssistenteIA.ProcessarRespostaPedido("{""alteracoes"":[{""campo"":""cliente"",""peca"":null,""valor"":""x""}],""observacoes"":""""}", DwgExemplo()))
        Afirmar(m.Contains("campo desconhecido"), m)
    End Sub

    Private Sub PedidoPecaInexistente()
        Dim m = Erro(Function() AssistenteIA.ProcessarRespostaPedido("{""alteracoes"":[{""campo"":""descricao_peca"",""peca"":""9999-9"",""valor"":""x""}],""observacoes"":""""}", DwgExemplo()))
        Afirmar(m.Contains("inexistente"), m)
    End Sub

    Private Sub PedidoVazio()
        Dim m = Erro(Function() AssistenteIA.ProcessarRespostaPedido("{""alteracoes"":[],""observacoes"":""Não percebi qual obra.""}", DwgExemplo()))
        Afirmar(m.Contains("nenhuma alteração") AndAlso m.Contains("Não percebi"), m)
    End Sub

    Private Sub Erro401()
        Using ia As New AssistenteIA(Cfg(), New HandlerSimulado(Function(n) RespostaChat("x", HttpStatusCode.Unauthorized)))
            Dim m = Erro(Function() ia.IdentificarCamposAsync(TextosExemplo()).Result)
            Afirmar(m.Contains("recusou a chave"), m)
        End Using
    End Sub

    Private Sub Erro429()
        Using ia As New AssistenteIA(Cfg(), New HandlerSimulado(Function(n) RespostaChat("x", CType(429, HttpStatusCode))))
            Dim m = Erro(Function() ia.InterpretarPedidoAsync("obra 1", DwgExemplo()).Result)
            Afirmar(m.Contains("429"), m)
        End Using
    End Sub

    Private Sub RecorreJsonObject()
        Dim h As New HandlerSimulado(Function(n)
                                         If n = 1 Then
                                             Return New HttpResponseMessage(HttpStatusCode.BadRequest) With {.Content = New StringContent("{""error"":""response_format json_schema not supported""}")}
                                         End If
                                         Return RespostaChat("{""campos"":[{""campo"":""lista"",""texto_id"":0,""valor"":""7874""}]}")
                                     End Function)
        Using ia As New AssistenteIA(Cfg(), h)
            Dim r = ia.IdentificarCamposAsync(TextosExemplo()).Result
            Afirmar(r.Count = 1, "resultado")
        End Using
        Afirmar(h.Pedidos.Count = 2, "2 pedidos")
        Afirmar(JsonNode.Parse(h.Pedidos(1).Corpo)("response_format")("type").GetValue(Of String)() = "json_object", "json_object")
    End Sub

    Private Sub FalhaRede()
        Using ia As New AssistenteIA(Cfg(), New HandlerSimulado(Function(n) As HttpResponseMessage
                                                                    Throw New HttpRequestException("Nome não resolvido")
                                                                End Function))
            Dim m = Erro(Function() ia.IdentificarCamposAsync(TextosExemplo()).Result)
            Afirmar(m.StartsWith("Falha de rede"), m)
        End Using
    End Sub

    Private Sub TempoEsgotado()
        Using ia As New AssistenteIA(Cfg(), New HandlerSimulado(Function(n) As HttpResponseMessage
                                                                    Throw New TaskCanceledException("timeout")
                                                                End Function))
            Dim m = Erro(Function() ia.IdentificarCamposAsync(TextosExemplo()).Result)
            Afirmar(m.StartsWith("Tempo esgotado"), m)
        End Using
    End Sub

    Private Sub ConfigAmbiente()
        Dim k0 = Environment.GetEnvironmentVariable("XAI_API_KEY"), m0 = Environment.GetEnvironmentVariable("XAI_MODEL")
        Try
            Environment.SetEnvironmentVariable("XAI_API_KEY", "abc")
            Environment.SetEnvironmentVariable("XAI_MODEL", "grok-teste")
            Dim c = ConfiguracaoIA.Carregar()
            Afirmar(c.ApiKey = "abc" AndAlso c.Modelo = "grok-teste" AndAlso c.Endpoint = ConfiguracaoIA.EndpointPorOmissao, "config")
            Environment.SetEnvironmentVariable("XAI_MODEL", Nothing)
            Afirmar(ConfiguracaoIA.Carregar().Modelo = ConfiguracaoIA.ModeloPorOmissao, "modelo por omissão")
        Finally
            Environment.SetEnvironmentVariable("XAI_API_KEY", k0)
            Environment.SetEnvironmentVariable("XAI_MODEL", m0)
        End Try
    End Sub

    Private Function IdDe(textos As List(Of TextoPosicionado), texto As String) As Integer
        Dim t = textos.FirstOrDefault(Function(x) x.Texto.Trim() = texto)
        Afirmar(t IsNot Nothing, "texto não encontrado: " & texto)
        Return t.Id
    End Function

    Private Sub MapaIADwg(dwg As String)
        Dim textos = DwgCampos.ExtrairTextos(dwg)
        ' a "IA" diz que a lista é o texto da Marcação (5432): o mapa tem prioridade sobre a regex
        Dim mapa As New List(Of CampoIdentificado) From {
            New CampoIdentificado With {.Campo = "lista", .TextoId = IdDe(textos, "Lista 7874"), .Valor = "7874"},
            New CampoIdentificado With {.Campo = "obra_poco", .TextoId = IdDe(textos, "2514.02"), .Valor = "2514.02"}}
        Dim c = DwgCampos.Ler(dwg, mapa)
        Afirmar(c.Lista = "7874", "lista")
        Afirmar(c.ObraPoco = "2514.02", "obra pelo mapa: " & c.ObraPoco)
    End Sub

    Private Sub PedidoDwgFimAFim(dwg As String)
        Dim atuais = DwgCampos.Ler(dwg)
        Dim h As New HandlerSimulado(Function(n) RespostaChat(
            "{""alteracoes"":[{""campo"":""obra_poco"",""peca"":null,""valor"":""5000.01""},{""campo"":""descricao_peca"",""peca"":""2"",""valor"":""Peça da Pá Inferior""}],""observacoes"":""""}"))
        Dim saida = Path.Combine(Path.GetTempPath(), "teste-ia.dwg")
        Using ia As New AssistenteIA(Cfg(), h)
            Dim a = ia.InterpretarPedidoAsync("obra 5000.01 e item 2 passa a Peça da Pá Inferior", atuais).Result
            DwgCampos.Substituir(dwg, saida, a)
        End Using
        Dim d = DwgCampos.Ler(saida)
        Afirmar(d.ObraPoco = "5000.01", "obra")
        Afirmar(d.CodigoBarras = "5000.01-5432-0", "código de barras: " & d.CodigoBarras)
        Afirmar(d.Pecas.First(Function(p) p.Item = "2").Descricao = "Peça da Pá Inferior", "peça 2")
        Afirmar(d.Pecas.First(Function(p) p.Item = "1").Descricao = "Peça da Pá Superior", "peça 1 inalterada")
        Afirmar(d.Lista = atuais.Lista, "lista inalterada")
    End Sub

    Private Sub MapaIAPdf(pdf As String)
        Dim textos = Global.PdfCampos.PdfCampos.ExtrairTextos(pdf)
        Afirmar(textos.Count > 10, "textos do PDF")
        Dim t = textos.First(Function(x) x.Texto.Contains("7874"))
        Dim mapa As New List(Of CampoIdentificado) From {New CampoIdentificado With {.Campo = "lista", .TextoId = t.Id, .Valor = "7874"}}
        Dim c = Global.PdfCampos.PdfCampos.Ler(pdf, mapa)
        Afirmar(c.Lista = "7874" AndAlso c.ObraPoco = "3066.05", c.ToString())
    End Sub
End Module
