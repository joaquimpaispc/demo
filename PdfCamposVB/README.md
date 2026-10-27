# PdfCamposVB

Altera os campos da legenda de desenhos exportados do SOLIDWORKS e grava o resultado com outro nome:

| Campo | PDF | DWG |
|---|:-:|:-:|
| **Lista** | ✔ | ✔ |
| **Obra Poço** / **Obra Periplast** | ✔ | ✔ |
| **Código de barras** `*obra-marcação-revisão*` (reconstruído automaticamente) | ✔ (Code 39 vetorial) | ✔ (texto em fonte de barras) |
| **Descrição 1 / Descrição 2** da legenda | – | ✔ |
| **Descrição** de cada linha da lista de peças | – | ✔ |

Opcionalmente, usa **IA (xAI Grok)** para identificar os campos quando o modelo de legenda é
diferente, ou para interpretar um pedido em linguagem natural
(ex.: *"muda a obra para 4077.10 e a descrição da peça 5432-1 para Chapa reforçada"*).

## Projetos
| Projeto | Descrição |
|---|---|
| `PdfCampos` | Biblioteca (net8.0): `Desenhos` (PDF ou DWG pela extensão), `PdfCampos`, `DwgCampos`, `AssistenteIA` |
| `PdfCampos.Consola` | Aplicação de consola (PDF e DWG) |
| `PdfCampos.WinForms` | Formulário Windows (net8.0-windows) |
| `PdfCampos.Testes` | Testes (consola): IA com `HttpMessageHandler` simulado, validação do JSON, ficheiros de exemplo |

Requisitos: .NET 8 SDK (ou Visual Studio 2022). Os pacotes NuGet são descarregados no primeiro build.

## Demonstração

[demo/PdfCamposVB-demo.mp4](demo/PdfCamposVB-demo.mp4) (~53 s) mostra a aplicação de consola a editar Lista, Obra, código de barras e descrição num PDF e num DWG. A consola usa a mesma biblioteca que a janela Windows Forms.

Os desenhos de exemplo não estão no repositório. `demo/mk.py` espera `in.pdf` e `in.dwg` localmente (`demo/rd.py` é o auxiliar que renderiza o DWG).

## Formulário (WinForms)
1. **Procurar...** – escolha um `.pdf` ou `.dwg`. São mostrados os valores atuais.
   Nos DWG aparecem também as descrições e a grelha da lista de peças.
2. Escreva os novos valores (deixe em branco o que não quer alterar). Nas peças, escreva na
   coluna **Nova descrição**.
3. (Opcional) **Pedido em linguagem natural** + **Aplicar pedido**: a IA preenche os campos;
   reveja-os antes de gravar.
   **Usar IA** obriga a identificar os campos com a IA (útil noutros modelos de legenda); se
   faltarem campos e houver chave configurada, o programa pergunta se quer usar a IA.
4. **Gravar como...** – grava com outro nome, na mesma extensão (PDF→PDF, DWG→DWG).

## Consola
```
PdfCampos.Consola entrada.dwg                                     (mostra os valores)
PdfCampos.Consola entrada.pdf saida.pdf 1234 4077.10 [obraPeriplast] [códigoBarras]   ("-" = inalterado)
PdfCampos.Consola entrada.dwg saida.dwg --lista 1234 --obra 4077.10 --periplast 2600.01
        --desc1 "Pá Inferior c/ Carboneto de Tungsténio" --desc2 "Pá Inferior"
        --peca 5432-1="Chapa reforçada" --peca 2="Reforço"
PdfCampos.Consola entrada.dwg saida.dwg --pedido "obra 4077.10 e lista 1234"   (IA)
PdfCampos.Consola entrada.pdf saida.pdf --ia --lista 1234                      (identificação por IA)
```
`--peca` aceita a **Marcação** (5432-1) ou o **ITEM** (1). `--barras` só é preciso para forçar outro código.

## Utilização no código
```vb
Imports PdfCampos

Dim atuais = Desenhos.Ler("C:\Desenhos\5432.dwg")          ' CamposDwg (ou CamposPdf para .pdf)
Dim alt As New AlteracoesCampos With {.Lista = "1234", .ObraPoco = "4077.10", .Descricao2 = "Pá Inferior"}
alt.DescricoesPecas("5432-1") = "Chapa reforçada"
Dim r = Desenhos.Substituir("C:\Desenhos\5432.dwg", "C:\Desenhos\5432_L1234.dwg", alt)
For Each aviso In r.Avisos : Console.WriteLine(aviso) : Next

' Com IA
Using ia As New AssistenteIA()                               ' lê XAI_API_KEY / assistenteia.json
    Dim lido = Await Desenhos.LerComIAAsync(caminho, ia, usarIA:=True)
    Dim pedido = Await ia.InterpretarPedidoAsync("muda a obra para 4077.10", lido.Campos)
    Desenhos.Substituir(caminho, saida, pedido, lido.Mapa)
End Using
```
* Campos `Nothing` ficam inalterados. O ficheiro de saída tem de ser diferente do de entrada.
* Se o código de barras não for indicado e a Obra Poço mudar, é reconstruído como
  `*<ObraPoço>-<Marcação>-<Revisão>*` (marcação e revisão atuais).
* A API antiga `PdfCampos.PdfCampos.Substituir(entrada, saida, lista, obra, ...)` continua a funcionar.

## Configurar a IA (xAI / Grok)
A chave **nunca** está no código. Por ordem de prioridade:
1. Variáveis de ambiente:
   ```bat
   setx XAI_API_KEY "xai-...."
   setx XAI_MODEL "grok-4.3"      (opcional; por omissão grok-4.3)
   ```
   (feche e volte a abrir a aplicação depois do `setx`).
2. Ficheiro `assistenteia.json` junto ao executável ou em `%APPDATA%\PdfCampos\assistenteia.json`:
   ```json
   { "ApiKey": "xai-....", "Modelo": "grok-4.3", "TimeoutSegundos": 60 }
   ```
   Proteja este ficheiro (não o partilhe nem o coloque em controlo de versões).

A chave obtém-se em https://console.x.ai. Cada pedido tem um pequeno custo (pago por tokens).
Para mais precisão pode usar um modelo maior (ex.: `grok-4.7`), mais caro.
Endpoint usado: `https://api.x.ai/v1/chat/completions`, com respostas em JSON estruturado
(`response_format` json_schema; se o modelo não o suportar, `json_object`). As respostas
são validadas: campos desconhecidos, ids inexistentes, valores que não existem no texto ou
peças inexistentes são rejeitados. **O que é enviado à xAI**: os textos do desenho
(com posições) ou os valores atuais + o pedido, não o ficheiro.

## Como funciona
**PDF** (iText): extrai os caracteres com posição e procura os campos por texto; remove
realmente o texto antigo com o pdfSweep; escreve o novo na mesma linha de base e tamanho
(Century Gothic Bold → Arial Bold → Helvetica-Bold); redesenha o Code 39 vetorial com a linha
legível e um texto invisível `*código*` para o PDF poder ser processado de novo.

**DWG** (ACadSharp): lê o desenho, percorre model/paper space e blocos (com transformação dos
INSERT), e encontra por texto, não por handles: `Lista N`, rótulos `Obra Poço:`/`Obra Periplast:`
e o valor à direita, o texto `*...*` mais alto (código de barras), as duas linhas de descrição
entre a linha das obras e o código de barras, e a tabela `SW_TABLEANNOTATION_*` (colunas pelas
linhas verticais e pelo cabeçalho *Descrição*). Só o valor é substituído no MTEXT (mantém
prefixo "Lista ", estilo e formatação). Nas células da lista de peças, se o texto estava centrado
passa a ser ancorado ao centro da célula (fica centrado com qualquer comprimento); se o novo texto
parecer maior que a célula, é dado um aviso. Grava na **mesma versão** (ex.: AC1015/AutoCAD 2000),
com caracteres fora do cp1252 codificados como `\U+XXXX`.

## Licenças
* **iText Core / pdfSweep**: **AGPL v3** ou licença **comercial**. Uso interno sem distribuir costuma
  ser compatível com AGPL; se distribuir a aplicação (ou a disponibilizar como serviço) sem publicar
  o código-fonte, é necessária licença comercial da Apryse/iText.
* **ACadSharp**: **MIT** (livre, também para uso comercial; manter o aviso de licença).
* xAI: serviço pago, sujeito aos termos da xAI.

## Limitações
* PDF: as descrições não são alteradas (só Lista, Obras e código de barras). O mapa da IA aplica-se à página 1.
* DWG: a largura do texto é **estimada** (não há motor de fontes), por isso o aviso de "texto mais
  largo que a célula" é aproximado. Não altera atributos de bloco, campos (FIELD) nem propriedades DWGPROPS
  (o desenho de exemplo não os tem).
* **Abra uma vez os DWG gerados no AutoCAD ou no DWG TrueView** (gratuito) para confirmar o resultado
  antes de os usar em produção; os testes foram feitos com ACadSharp e LibreDWG, não com o AutoCAD.
* O código de barras só aceita caracteres Code 39 sem espaços (0-9, A-Z, - . $ / + %).
* Outras ocorrências do mesmo número fora destes campos (notas, nome do ficheiro) não são alteradas.
* A IA pode enganar-se: os valores propostos aparecem no formulário para revisão antes de gravar.
* O pdfSweep só tem binários net461 no NuGet; funcionam em .NET 8 (aviso NU1701 suprimido).

## Testes
```
dotnet run --project PdfCampos.Testes -- C:\pasta\com\exemplos   (usa in.pdf / in.dwg se existirem)
```
