' Formulário: escolher um desenho PDF ou DWG, ver os valores atuais, indicar os
' novos valores (à mão ou com um pedido em linguagem natural interpretado pela IA)
' e gravar com outro nome.
Option Strict On
Imports System.IO
Imports System.Windows.Forms
Imports PdfCampos

Public Class Form1
    Inherits Form

    Private ReadOnly txtFicheiro As New TextBox With {.ReadOnly = True, .Width = 470}
    Private ReadOnly btnProcurar As New Button With {.Text = "Procurar...", .Width = 100}

    Private ReadOnly lblListaAtual As New Label With {.AutoSize = True}
    Private ReadOnly lblObraPocoAtual As New Label With {.AutoSize = True}
    Private ReadOnly lblObraPeriplastAtual As New Label With {.AutoSize = True}
    Private ReadOnly lblBarrasAtual As New Label With {.AutoSize = True}
    Private ReadOnly lblDesc1Atual As New Label With {.AutoSize = True, .MaximumSize = New Drawing.Size(300, 0)}
    Private ReadOnly lblDesc2Atual As New Label With {.AutoSize = True, .MaximumSize = New Drawing.Size(300, 0)}

    Private ReadOnly txtLista As New TextBox With {.Width = 260}
    Private ReadOnly txtObraPoco As New TextBox With {.Width = 260}
    Private ReadOnly txtObraPeriplast As New TextBox With {.Width = 260}
    Private ReadOnly txtBarras As New TextBox With {.Width = 260, .Enabled = False}
    Private ReadOnly chkBarrasAuto As New CheckBox With {.Text = "Automático (Obra Poço-Marcação-Revisão)", .Checked = True, .AutoSize = True}
    Private ReadOnly txtDesc1 As New TextBox With {.Width = 260}
    Private ReadOnly txtDesc2 As New TextBox With {.Width = 260}

    Private ReadOnly grelha As New DataGridView With {
        .Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False,
        .RowHeadersVisible = False, .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        .SelectionMode = DataGridViewSelectionMode.CellSelect}

    Private ReadOnly chkUsarIA As New CheckBox With {.Text = "Usar IA (xAI Grok) para identificar os campos", .AutoSize = True}
    Private ReadOnly txtPedido As New TextBox With {.Multiline = True, .Height = 48, .Dock = DockStyle.Fill, .ScrollBars = ScrollBars.Vertical}
    Private ReadOnly btnAplicarPedido As New Button With {.Text = "Aplicar pedido", .Width = 120, .Height = 30, .Enabled = False}
    Private ReadOnly lblEstado As New Label With {.AutoSize = True, .ForeColor = Drawing.Color.DimGray}

    Private ReadOnly btnGravar As New Button With {.Text = "Gravar como...", .Width = 140, .Height = 32, .Enabled = False}

    Private atuais As CamposPdf
    Private mapaIA As List(Of CampoIdentificado)

    Public Sub New()
        Text = "Alterar campos de desenhos (PDF / DWG)"
        StartPosition = FormStartPosition.CenterScreen
        Size = New Drawing.Size(900, 780)
        MinimumSize = New Drawing.Size(760, 600)
        Padding = New Padding(10)

        ' --- ficheiro e campos
        Dim t As New TableLayoutPanel With {.ColumnCount = 3, .AutoSize = True, .Dock = DockStyle.Top}
        t.Controls.Add(New Label With {.Text = "Ficheiro (PDF/DWG):", .AutoSize = True, .Anchor = AnchorStyles.Left}, 0, 0)
        t.Controls.Add(txtFicheiro, 1, 0)
        t.Controls.Add(btnProcurar, 2, 0)
        Dim negrito = New Drawing.Font(Font, Drawing.FontStyle.Bold)
        t.Controls.Add(New Label With {.Text = "Campo", .AutoSize = True, .Font = negrito}, 0, 1)
        t.Controls.Add(New Label With {.Text = "Valor atual", .AutoSize = True, .Font = negrito}, 1, 1)
        t.Controls.Add(New Label With {.Text = "Novo valor", .AutoSize = True, .Font = negrito}, 2, 1)
        AdicionarLinha(t, 2, "Lista:", lblListaAtual, txtLista)
        AdicionarLinha(t, 3, "Obra Poço:", lblObraPocoAtual, txtObraPoco)
        AdicionarLinha(t, 4, "Obra Periplast:", lblObraPeriplastAtual, txtObraPeriplast)
        AdicionarLinha(t, 5, "Código de barras:", lblBarrasAtual, txtBarras)
        t.Controls.Add(chkBarrasAuto, 2, 6)
        AdicionarLinha(t, 7, "Descrição 1 (DWG):", lblDesc1Atual, txtDesc1)
        AdicionarLinha(t, 8, "Descrição 2 (DWG):", lblDesc2Atual, txtDesc2)
        t.Controls.Add(New Label With {.Text = "Deixe em branco os campos que não quer alterar.", .AutoSize = True, .ForeColor = Drawing.Color.DimGray}, 1, 9)

        ' --- lista de peças (DWG)
        grelha.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Item", .HeaderText = "ITEM", .ReadOnly = True, .FillWeight = 10})
        grelha.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Qtd", .HeaderText = "Qtd.", .ReadOnly = True, .FillWeight = 10})
        grelha.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Marcacao", .HeaderText = "Marcação", .ReadOnly = True, .FillWeight = 18})
        grelha.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Descricao", .HeaderText = "Descrição atual", .ReadOnly = True, .FillWeight = 31})
        grelha.Columns.Add(New DataGridViewTextBoxColumn With {.Name = "Nova", .HeaderText = "Nova descrição", .FillWeight = 31})
        Dim grpPecas As New GroupBox With {.Text = "Lista de peças (só DWG) - escreva a nova descrição na última coluna", .Dock = DockStyle.Fill, .Padding = New Padding(6)}
        grpPecas.Controls.Add(grelha)

        ' --- IA
        Dim grpIA As New GroupBox With {.Text = "Inteligência artificial (opcional; requer XAI_API_KEY)", .Dock = DockStyle.Bottom, .Height = 150, .Padding = New Padding(6)}
        Dim tIA As New TableLayoutPanel With {.ColumnCount = 2, .RowCount = 4, .Dock = DockStyle.Fill}
        tIA.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100))
        tIA.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        tIA.Controls.Add(chkUsarIA, 0, 0)
        tIA.Controls.Add(New Label With {.Text = "Pedido em linguagem natural (ex.: ""muda a obra para 4077.10 e a lista para 1234""):", .AutoSize = True}, 0, 1)
        tIA.Controls.Add(txtPedido, 0, 2)
        tIA.Controls.Add(btnAplicarPedido, 1, 2)
        tIA.Controls.Add(lblEstado, 0, 3)
        grpIA.Controls.Add(tIA)

        ' --- fundo
        Dim fundo As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .FlowDirection = FlowDirection.RightToLeft, .Height = 44}
        fundo.Controls.Add(btnGravar)

        Controls.Add(grpPecas)
        Controls.Add(t)
        Controls.Add(grpIA)
        Controls.Add(fundo)

        DefinirDwg(False)
        AddHandler btnProcurar.Click, AddressOf Procurar
        AddHandler btnGravar.Click, AddressOf Gravar
        AddHandler btnAplicarPedido.Click, AddressOf AplicarPedido
        AddHandler chkBarrasAuto.CheckedChanged, Sub() txtBarras.Enabled = Not chkBarrasAuto.Checked
        AddHandler chkUsarIA.CheckedChanged, AddressOf UsarIAAlterado
    End Sub

    Private Shared Sub AdicionarLinha(t As TableLayoutPanel, linha As Integer, nome As String, atual As Label, novo As TextBox)
        t.Controls.Add(New Label With {.Text = nome, .AutoSize = True, .Anchor = AnchorStyles.Left}, 0, linha)
        atual.Anchor = AnchorStyles.Left
        t.Controls.Add(atual, 1, linha)
        t.Controls.Add(novo, 2, linha)
    End Sub

    Private ReadOnly Property EDwg As Boolean
        Get
            Return Desenhos.EDwg(txtFicheiro.Text)
        End Get
    End Property

    Private Sub DefinirDwg(dwg As Boolean)
        txtDesc1.Enabled = dwg : txtDesc2.Enabled = dwg : grelha.Enabled = dwg
    End Sub

    ' ------------------------------------------------------------------ abrir

    Private Async Sub Procurar(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog With {
            .Filter = "Desenhos (*.pdf;*.dwg)|*.pdf;*.dwg|Ficheiros PDF (*.pdf)|*.pdf|Ficheiros DWG (*.dwg)|*.dwg",
            .Title = "Escolher desenho"}
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            txtFicheiro.Text = dlg.FileName
        End Using
        Await Carregar(chkUsarIA.Checked)
    End Sub

    Private Async Sub UsarIAAlterado(sender As Object, e As EventArgs)
        If chkUsarIA.Checked AndAlso Not String.IsNullOrEmpty(txtFicheiro.Text) Then Await Carregar(True)
    End Sub

    Private Async Function Carregar(usarIA As Boolean) As Task
        mapaIA = Nothing
        Try
            Ocupado(True, "A ler o desenho...")
            Dim f = txtFicheiro.Text
            atuais = Await Task.Run(Function() Desenhos.Ler(f))
            If Not usarIA AndAlso Desenhos.FaltamCampos(atuais) Then
                Using ia As New AssistenteIA()
                    If ia.Configurada AndAlso MessageBox.Show(Me, "Alguns campos não foram encontrados automaticamente." & vbCrLf &
                                                             "Quer pedir à IA (xAI) que os identifique?", "Campos em falta",
                                                             MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then usarIA = True
                End Using
            End If
            If usarIA Then
                Ocupado(True, "A pedir à IA a identificação dos campos...")
                Using ia As New AssistenteIA()
                    Dim r = Await Desenhos.LerComIAAsync(f, ia, True)
                    atuais = r.Campos : mapaIA = r.Mapa
                End Using
            End If
            MostrarAtuais()
            btnGravar.Enabled = True : btnAplicarPedido.Enabled = True
            Ocupado(False, If(mapaIA Is Nothing, "Campos lidos.", $"Campos identificados pela IA: {mapaIA.Count}."))
        Catch ex As Exception
            Ocupado(False, "")
            btnGravar.Enabled = atuais IsNot Nothing : btnAplicarPedido.Enabled = atuais IsNot Nothing
            If atuais IsNot Nothing Then MostrarAtuais()
            MessageBox.Show(Me, "Não foi possível ler o desenho:" & vbCrLf & ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Function

    Private Sub MostrarAtuais()
        Const NaoEncontrado = "(não encontrado)"
        lblListaAtual.Text = If(atuais.Lista, NaoEncontrado)
        lblObraPocoAtual.Text = If(atuais.ObraPoco, NaoEncontrado)
        lblObraPeriplastAtual.Text = If(atuais.ObraPeriplast, NaoEncontrado)
        lblBarrasAtual.Text = If(atuais.CodigoBarras Is Nothing, NaoEncontrado, "*" & atuais.CodigoBarras & "*")
        grelha.Rows.Clear()
        Dim dwg = TryCast(atuais, CamposDwg)
        DefinirDwg(dwg IsNot Nothing)
        If dwg IsNot Nothing Then
            lblDesc1Atual.Text = If(dwg.Descricoes.Count > 0, dwg.Descricoes(0), NaoEncontrado)
            lblDesc2Atual.Text = If(dwg.Descricoes.Count > 1, dwg.Descricoes(1), NaoEncontrado)
            For Each p In dwg.Pecas
                grelha.Rows.Add(p.Item, p.Quantidade, p.Marcacao, p.Descricao, "")
            Next
        Else
            lblDesc1Atual.Text = "(só DWG)" : lblDesc2Atual.Text = "(só DWG)"
        End If
    End Sub

    Private Sub Ocupado(sim As Boolean, estado As String)
        Cursor = If(sim, Cursors.WaitCursor, Cursors.Default)
        btnProcurar.Enabled = Not sim
        If sim Then btnGravar.Enabled = False : btnAplicarPedido.Enabled = False
        lblEstado.Text = estado
    End Sub

    ' ------------------------------------------------------------------ IA: pedido

    Private Async Sub AplicarPedido(sender As Object, e As EventArgs)
        If atuais Is Nothing Then Return
        Try
            Ocupado(True, "A interpretar o pedido com a IA...")
            Dim a As AlteracoesCampos
            Using ia As New AssistenteIA()
                a = Await ia.InterpretarPedidoAsync(txtPedido.Text, atuais)
            End Using
            If a.Lista IsNot Nothing Then txtLista.Text = a.Lista
            If a.ObraPoco IsNot Nothing Then txtObraPoco.Text = a.ObraPoco
            If a.ObraPeriplast IsNot Nothing Then txtObraPeriplast.Text = a.ObraPeriplast
            If a.CodigoBarras IsNot Nothing Then chkBarrasAuto.Checked = False : txtBarras.Text = a.CodigoBarras
            If a.Descricao1 IsNot Nothing Then txtDesc1.Text = a.Descricao1
            If a.Descricao2 IsNot Nothing Then txtDesc2.Text = a.Descricao2
            For Each kv In a.DescricoesPecas
                For Each r As DataGridViewRow In grelha.Rows
                    If String.Equals(CStr(r.Cells("Marcacao").Value), kv.Key, StringComparison.OrdinalIgnoreCase) OrElse
                       String.Equals(CStr(r.Cells("Item").Value), kv.Key, StringComparison.OrdinalIgnoreCase) Then r.Cells("Nova").Value = kv.Value
                Next
            Next
            Ocupado(False, "Pedido aplicado aos campos: " & a.ToString() & ". Reveja e clique em 'Gravar como...'.")
        Catch ex As Exception
            Ocupado(False, "")
            MessageBox.Show(Me, ex.Message, "IA", MessageBoxButtons.OK, MessageBoxIcon.Warning)
        Finally
            btnGravar.Enabled = atuais IsNot Nothing : btnAplicarPedido.Enabled = atuais IsNot Nothing
        End Try
    End Sub

    ' ------------------------------------------------------------------ gravar

    Private Function RecolherAlteracoes() As AlteracoesCampos
        Dim v = Function(tb As TextBox) If(String.IsNullOrWhiteSpace(tb.Text) OrElse Not tb.Enabled, Nothing, tb.Text.Trim())
        Dim a As New AlteracoesCampos With {
            .Lista = v(txtLista), .ObraPoco = v(txtObraPoco), .ObraPeriplast = v(txtObraPeriplast),
            .CodigoBarras = If(chkBarrasAuto.Checked, Nothing, v(txtBarras))}
        If EDwg Then
            a.Descricao1 = v(txtDesc1) : a.Descricao2 = v(txtDesc2)
            For Each r As DataGridViewRow In grelha.Rows
                Dim nova = CStr(r.Cells("Nova").Value)
                If String.IsNullOrWhiteSpace(nova) Then Continue For
                Dim chave = CStr(r.Cells("Marcacao").Value)
                If String.IsNullOrWhiteSpace(chave) Then chave = CStr(r.Cells("Item").Value)
                a.DescricoesPecas(chave) = nova.Trim()
            Next
        End If
        Return a
    End Function

    Private Sub Gravar(sender As Object, e As EventArgs)
        If String.IsNullOrEmpty(txtFicheiro.Text) Then Return
        grelha.EndEdit()
        Dim alt = RecolherAlteracoes()
        If alt.Vazia Then
            MessageBox.Show(Me, "Não indicou nenhuma alteração.", "Gravar", MessageBoxButtons.OK, MessageBoxIcon.Information)
            Return
        End If
        Dim ext = Path.GetExtension(txtFicheiro.Text).ToLowerInvariant()
        Dim baseNome = Path.GetFileNameWithoutExtension(txtFicheiro.Text)
        Dim nomeSugerido = If(alt.Lista Is Nothing, baseNome & "_alterado" & ext, baseNome & "_L" & alt.Lista & ext)
        Dim filtro = If(ext = ".dwg", "Ficheiros DWG (*.dwg)|*.dwg", "Ficheiros PDF (*.pdf)|*.pdf")
        Using dlg As New SaveFileDialog With {
            .Filter = filtro, .DefaultExt = ext.TrimStart("."c), .AddExtension = True,
            .Title = "Gravar desenho como", .InitialDirectory = Path.GetDirectoryName(txtFicheiro.Text), .FileName = nomeSugerido}
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            If String.Equals(Path.GetFullPath(dlg.FileName), Path.GetFullPath(txtFicheiro.Text), StringComparison.OrdinalIgnoreCase) Then
                MessageBox.Show(Me, "Escolha um nome diferente do ficheiro original.", "Gravar", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If
            Try
                Cursor = Cursors.WaitCursor
                Dim r = Desenhos.Substituir(txtFicheiro.Text, dlg.FileName, alt, mapaIA)
                Cursor = Cursors.Default
                Dim avisos = If(r.Avisos.Count = 0, "", vbCrLf & vbCrLf & "Avisos:" & vbCrLf & String.Join(vbCrLf, r.Avisos))
                MessageBox.Show(Me, "Gravado em:" & vbCrLf & dlg.FileName & vbCrLf & vbCrLf & r.ToString() & avisos, "Concluído",
                                MessageBoxButtons.OK, If(r.Avisos.Count = 0, MessageBoxIcon.Information, MessageBoxIcon.Warning))
            Catch ex As Exception
                Cursor = Cursors.Default
                MessageBox.Show(Me, "Erro ao gravar:" & vbCrLf & ex.Message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub
End Class
