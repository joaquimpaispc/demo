# Sugestão de Fornecedores

Aplicação web autónoma, num único ficheiro HTML, para uma empresa portuguesa de aço e metalomecânica. A interface está em português (pt-PT).

A partir das linhas de uma requisição e do histórico de compras, sugere um fornecedor por linha, com pontuação transparente, e permite exportar a análise ou um pedido de cotação.

Esta pasta é independente da aplicação Cotai (Next.js) na raiz do repositório, incluindo a página da requisição 56749.

## O que faz

1. Lê um Excel de requisição. O cabeçalho tem de incluir `Designacao` e `Quantidade` ou `Quantidade_Requisitada`. No ficheiro de exemplo, as linhas da requisição estão nas linhas 4–17.
2. Lê o histórico de compras: blocos com os cabeçalhos `Fornecedor` e `DataEncomenda`. No ficheiro de exemplo, esses blocos começam na linha 54 da folha `Queries Histórico Compras`.
3. Liga cada linha da requisição ao histórico por três vias:
   - `Auto_ID` da linha da requisição, quando o histórico traz essa ligação;
   - `AUTO_IDARTIGO`, quando o artigo é o mesmo;
   - designação normalizada (perfil e medida: HEA, HEB, IPE, tubos SHS/RHS/CHS, chapa, cantoneira, varão roscado, entre outros).
4. Agrega as compras por fornecedor, ordena pela pontuação e estima o valor da linha.
5. Exporta Excel ou CSV e gera o texto do pedido de cotação para os fornecedores melhor classificados.

Se não houver histórico no ficheiro carregado, a página usa o histórico embutido (dados de exemplo de outubro de 2026, em `data/samples/aoa.json`).

## Pontuação

A pontuação (0–100) é a média ponderada de cinco critérios, cada um entre 0 e 1, multiplicada pelo grau de correspondência do artigo. Os pesos por omissão podem ser ajustados na página.

| Critério | Peso |
|----------|------|
| Preço | 35% |
| Frequência | 20% |
| Recência | 20% |
| Prazo de entrega | 15% |
| Qualidade / material | 10% |

- **Preço.** Preço médio mais baixo entre os fornecedores da linha, a dividir pelo preço médio do fornecedor. A média é ponderada pela quantidade. A unidade de comparação é €/kg em perfis e chapa, €/ML em tubos e €/un em varão. Sem preço válido, o critério vale 0,4.
- **Frequência.** Número de encomendas do fornecedor a dividir pelo máximo observado na linha.
- **Recência.** `0,5` elevado a (dias desde a última encomenda ÷ 180).
- **Prazo.** (prazo mais curto + 1) ÷ (prazo médio do fornecedor + 1). O prazo é a data de receção menos a data de encomenda. Sem dados, o critério vale 0,5.
- **Qualidade.** 1 se a classe de aço coincide (por exemplo S275); 0,5 se a classe é compatível mas diferente; 0,75 se é desconhecida. Inox face a aço carbono fica de fora.

Grau de correspondência da designação:

- artigo igual × 1
- medida próxima (por exemplo, a mesma família com espessura diferente) × 0,85
- semelhante × 0,7

Ficam fora do cálculo as linhas anuladas, as encomendas da própria requisição e o material incompatível. Ficam fora da média de preço os preços pendentes e os preços atípicos (abaixo de 60% ou acima de 180% da mediana da linha).

## Estrutura

```
sugestao-fornecedores/
  src/core.js          lógica de leitura, correspondência e pontuação
  src/template.html    modelo da interface
  src/build.py         gera o HTML autónomo
  data/samples/        Excel de exemplo e histórico convertido (aoa.json)
  tests/               testes de lógica, interface e exportação
  dist/                HTML gerado
```

## Como construir

Na pasta `sugestao-fornecedores`:

```bash
npm install
npm run build
```

`npm run build` executa `src/build.py`. O script incorpora `src/core.js`, a biblioteca SheetJS (`node_modules/xlsx`) e o histórico em `data/samples/aoa.json` no modelo, e escreve `dist/sugestao_fornecedores.html`.

## Como usar

Abra `dist/sugestao_fornecedores.html` no browser. Não é preciso servidor.

1. Em **Requisição (ou ficheiro completo)**, carregue um Excel. Pode ser só a requisição ou o ficheiro que também traz os blocos de histórico.
2. Em **Histórico de compras (opcional)**, carregue um segundo ficheiro se o histórico vier à parte. Se ficar vazio, a página usa o histórico do ficheiro da requisição ou, na falta deste, o histórico embutido.
3. Ajuste a data de referência e, se quiser, os pesos em **Critérios de pontuação**.
4. Consulte os separadores **Por linha**, **Por fornecedor (pedidos de cotação)** e **Histórico carregado**. Clique numa linha para ver o detalhe e as alternativas.
5. Use **Exportar Excel** ou **Exportar CSV**. No separador de fornecedores pode copiar o pedido de cotação.

Ficheiros de exemplo:

- `data/samples/historico.xlsx` — requisição (linhas 4–17) e histórico (a partir da linha 54), folha `Queries Histórico Compras`
- `data/samples/fornecedores.xlsx` — requisição simples, sem histórico

## Testes

```bash
npm test            # lógica de correspondência e pontuação (Node)
npm run test:ui     # interface em Chrome headless
npm run test:export # exportação Excel, CSV e pedido de cotação
```

Os testes de interface e de exportação usam Google Chrome em `/usr/bin/google-chrome` (`puppeteer-core` não descarrega um browser).
