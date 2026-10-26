# Cotai — Comparador de propostas (xAICMP)

Aplicação web em português para comparar uma **requisição Excel** com **propostas PDF** de fornecedores e recomendar a melhor opção com pontuação transparente.

Marca do produto: **Cotai** · Projecto **xAICMP** · Joaquim Pais.

## Como correr

```bash
npm install
npm run dev
```

Abra [http://localhost:3000](http://localhost:3000).

Produção:

```bash
npm run build
npm start
```

### Variáveis de ambiente

Nenhuma chave é obrigatória na v1. A extracção e o matching são **heurísticos e offline**.

Se no futuro quiser extracção com LLM, pode adicionar (não usados ainda):

| Variável | Uso previsto |
|----------|----------------|
| `OPENAI_API_KEY` | Extracção estruturada de PDF / matching semântico |
| `ANTHROPIC_API_KEY` | Alternativa ao OpenAI |

Sem estas variáveis, a app funciona na íntegra com regras locais.

## Fluxo

1. Carregar o Excel da requisição (`.xlsx`).
2. Carregar um ou mais PDFs de proposta.
3. Rever mapeamento de colunas e linhas extraídas.
4. Comparar → ver tabela e a **melhor opção** com racional em português.

Os uploads ficam em memória na sessão do servidor (sem autenticação na v1). Reiniciar o servidor limpa as sessões.

## Colunas Excel esperadas

A app detecta cabeçalhos comuns (PT/EN). Pode corrigir o mapeamento na UI.

| Papel | Exemplos de cabeçalho |
|-------|------------------------|
| Descrição / item | `Descrição`, `Artigo`, `Produto`, `Item`, `Material` |
| Quantidade | `Quantidade`, `Qtd`, `Qty` |
| Unidade | `Unidade`, `Un`, `UM` |
| Especificações | `Especificações`, `Specs`, `Obs`, `Detalhe` |
| Código | `Código`, `Ref`, `SKU` |

Ficheiro de exemplo: [`fixtures/requisicao-exemplo.xlsx`](fixtures/requisicao-exemplo.xlsx).

## PDFs de proposta

Idealmente PDF com **texto seleccionável** (não só imagem digitalizada). A extracção procura:

- linhas com preços (€ / EUR / decimais);
- quantidades e unidades;
- prazo (`Prazo … X dias/semanas`);
- condições de pagamento / entrega quando presentes no texto;
- tabelas quando o motor as consegue ler.

Exemplos em [`fixtures/`](fixtures/).

## Como funciona a comparação

Para cada fornecedor:

1. **Matching** das linhas da proposta às da requisição (semelhança de tokens, limiares parcial/completo).
2. Detecção de **lacunas** (item em falta) e **quantidades diferentes**.
3. **Pontuação relativa** entre as propostas carregadas:

| Critério | Peso |
|----------|------|
| Preço (cesto comparável; menor é melhor) | 45% |
| Completude (cobertura do pedido) | 30% |
| Prazo de entrega (quando extraível) | 15% |
| Acerto de quantidades | 10% |

No critério de preço, linhas em falta são imputadas com o maior preço unitário dos outros fornecedores × quantidade × 1,15, para não premiar propostas incompletas.

A **melhor opção global** é a proposta com maior pontuação. Por linha, recomenda-se o fornecedor com menor preço unitário entre os que cobrem o item.

## Stack

- Next.js (App Router) + TypeScript + Tailwind CSS
- Excel: SheetJS (`xlsx`)
- PDF: `pdf-parse` (texto + tabelas quando possível)
- Sessão: mapa em memória no processo Node

## Scripts úteis

```bash
npm run lint
npm run build
```

Para regenerar fixtures de demonstração (opcional), use o gerador em `scripts/generate-fixtures.mjs` se existir, ou os ficheiros já presentes em `fixtures/`.

## Sugestão de Fornecedores

Ferramenta autónoma (um HTML) que sugere fornecedores por linha de requisição a partir do histórico de compras. É independente desta aplicação Next.js. Instruções em [`sugestao-fornecedores/README.md`](sugestao-fornecedores/README.md).
