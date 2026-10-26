' Aplicação de consola (PDF e DWG).
' Uso:
'   PdfCampos.Consola <entrada.pdf|dwg>                       -> mostra os valores atuais
'   PdfCampos.Consola <entrada> <saida> <lista> <obraPoço> [obraPeriplast] [códigoBarras]
'        (forma antiga; "-" deixa o campo inalterado)
'   PdfCampos.Consola <entrada> <saida> [opções]
'        --lista N  --obra X  --periplast X  --barras X
'        --desc1 "texto"  --desc2 "texto"            (só DWG)
'        --peca 5432-1="nova descrição"             (só DWG; Marcação ou ITEM; repetível)
'        --pedido "muda a obra para 4077.10"         (IA xAI; requer XAI_API_KEY)
'        --ia                                        (identificar os campos com IA)
Imports System.IO
Imports PdfCampos

Module Program
    Private Const Uso As String =
        "Uso:" & vbLf &
        "  PdfCampos.Consola <entrada.pdf|.dwg>" & vbLf &
        "  PdfCampos.Consola <entrada> <saida> <lista> <obraPoço> [obraPeriplast] [códigoBarras]   (""-"" = inalterado)" & vbLf &
        "  PdfCampos.Consola <entrada> <saida> [--lista N] [--obra X] [--periplast X] [--barras X]" & vbLf &
        "                    [--desc1 T] [--desc2 T] [--peca MARCAÇÃO=T ...] [--pedido ""texto""] [--ia]"

    Function Main(args As String()) As Integer
        Console.OutputEncoding = System.Text.Encoding.UTF8
        Try
            If args.Length = 1 Then
                If args(0) = "-h" OrElse args(0) = "--help" Then Console.WriteLine(Uso) : Return 0
                Console.WriteLine(Desenhos.Ler(args(0)).ToString())
                Return 0
            End If
            If args.Length < 3 Then
                Console.WriteLine(Uso)
                Return 1
            End If
            Dim entrada = args(0), saida = args(1)
            Dim alt As New AlteracoesCampos()
            Dim pedido As String = Nothing, usarIA = False

            If Not args(2).StartsWith("--") Then
                ' forma antiga posicional
                Dim v = Function(i As Integer) If(args.Length > i AndAlso args(i) <> "-", args(i), Nothing)
                alt.Lista = v(2) : alt.ObraPoco = v(3) : alt.ObraPeriplast = v(4) : alt.CodigoBarras = v(5)
            Else
                Dim i = 2
                While i < args.Length
                    Dim op = args(i).ToLowerInvariant()
                    Dim valor = Function() As String
                                    If i + 1 >= args.Length Then Throw New ArgumentException($"Falta o valor de {op}.")
                                    i += 1
                                    Return args(i)
                                End Function
                    Select Case op
                        Case "--lista" : alt.Lista = valor()
                        Case "--obra", "--obrapoco" : alt.ObraPoco = valor()
                        Case "--periplast", "--obraperiplast" : alt.ObraPeriplast = valor()
                        Case "--barras" : alt.CodigoBarras = valor()
                        Case "--desc1" : alt.Descricao1 = valor()
                        Case "--desc2" : alt.Descricao2 = valor()
                        Case "--peca"
                            Dim pv = valor()
                            Dim k = pv.IndexOf("="c)
                            If k <= 0 Then Throw New ArgumentException("--peca deve ter a forma MARCAÇÃO=descrição.")
                            alt.DescricoesPecas(pv.Substring(0, k).Trim()) = pv.Substring(k + 1)
                        Case "--pedido" : pedido = valor()
                        Case "--ia" : usarIA = True
                        Case Else : Throw New ArgumentException($"Opção desconhecida: {args(i)}" & vbLf & Uso)
                    End Select
                    i += 1
                End While
            End If

            Dim mapa As List(Of CampoIdentificado) = Nothing
            Dim antes As CamposPdf
            If usarIA OrElse pedido IsNot Nothing Then
                Using ia As New AssistenteIA()
                    Dim r = Desenhos.LerComIAAsync(entrada, ia, usarIA).GetAwaiter().GetResult()
                    antes = r.Campos : mapa = r.Mapa
                    If mapa IsNot Nothing Then Console.WriteLine("Mapa IA: " & String.Join(", ", mapa))
                    If pedido IsNot Nothing Then
                        Dim altIA = ia.InterpretarPedidoAsync(pedido, antes).GetAwaiter().GetResult()
                        Console.WriteLine("Pedido interpretado: " & altIA.ToString())
                        Juntar(alt, altIA)
                    End If
                End Using
            Else
                antes = Desenhos.Ler(entrada)
            End If
            Console.WriteLine("Antes : " & antes.ToString())
            If alt.Vazia Then Throw New ArgumentException("Nenhuma alteração indicada.")
            Dim res = Desenhos.Substituir(entrada, saida, alt, mapa)
            For Each a In res.Avisos
                Console.WriteLine("AVISO: " & a)
            Next
            Console.WriteLine("Depois: " & Desenhos.Ler(saida).ToString())
            Console.WriteLine("Gravado em: " & Path.GetFullPath(saida))
            Return 0
        Catch ex As Exception
            Console.Error.WriteLine("ERRO: " & ex.Message)
            Return 2
        End Try
    End Function

    ''' <summary>Junta as alterações da IA às indicadas explicitamente (as explícitas prevalecem).</summary>
    Private Sub Juntar(alvo As AlteracoesCampos, ia As AlteracoesCampos)
        If alvo.Lista Is Nothing Then alvo.Lista = ia.Lista
        If alvo.ObraPoco Is Nothing Then alvo.ObraPoco = ia.ObraPoco
        If alvo.ObraPeriplast Is Nothing Then alvo.ObraPeriplast = ia.ObraPeriplast
        If alvo.CodigoBarras Is Nothing Then alvo.CodigoBarras = ia.CodigoBarras
        If alvo.Descricao1 Is Nothing Then alvo.Descricao1 = ia.Descricao1
        If alvo.Descricao2 Is Nothing Then alvo.Descricao2 = ia.Descricao2
        For Each kv In ia.DescricoesPecas
            If Not alvo.DescricoesPecas.ContainsKey(kv.Key) Then alvo.DescricoesPecas(kv.Key) = kv.Value
        Next
    End Sub
End Module
