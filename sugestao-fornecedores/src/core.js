/* Núcleo de cálculo: leitura, normalização, correspondência e pontuação de fornecedores */
var SFCore = (function () {
  'use strict';
  var DAY = 86400000;

  function clean(v) {
    if (v === undefined || v === null) return null;
    if (typeof v === 'string') {
      var s = v.trim();
      if (s === '' || s.toUpperCase() === 'NULL') return null;
      return s;
    }
    return v;
  }
  function num(v) {
    v = clean(v);
    if (v === null) return null;
    if (typeof v === 'number') return isFinite(v) ? v : null;
    var s = String(v).replace(/\s/g, '');
    if (/^-?\d{1,3}(\.\d{3})+,\d+$/.test(s)) s = s.replace(/\./g, '').replace(',', '.');
    else s = s.replace(',', '.');
    var n = parseFloat(s);
    return isNaN(n) ? null : n;
  }
  /* datas como "dia epoch" (inteiro, UTC) */
  function toDay(v) {
    v = clean(v);
    if (v === null) return null;
    if (v instanceof Date) return Math.floor(Date.UTC(v.getFullYear(), v.getMonth(), v.getDate()) / DAY);
    if (typeof v === 'number') return Math.floor(v) - 25569; // série Excel
    var s = String(v), m = s.match(/^(\d{4})-(\d{1,2})-(\d{1,2})/);
    if (m) return Math.floor(Date.UTC(+m[1], +m[2] - 1, +m[3]) / DAY);
    m = s.match(/^(\d{1,2})[\/.-](\d{1,2})[\/.-](\d{4})/);
    if (m) return Math.floor(Date.UTC(+m[3], +m[2] - 1, +m[1]) / DAY);
    return null;
  }
  function dayToISO(d) { return d === null || d === undefined ? '' : new Date(d * DAY).toISOString().slice(0, 10); }
  function dayToPT(d) { if (d === null || d === undefined) return ''; var s = dayToISO(d); return s.slice(8, 10) + '/' + s.slice(5, 7) + '/' + s.slice(0, 4); }

  function normText(s) {
    return (s === null || s === undefined ? '' : String(s)).toUpperCase()
      .replace(/Ø/g, ' ').normalize('NFD').replace(/[\u0300-\u036f]/g, '')
      .replace(/(\d),(\d)/g, '$1.$2').replace(/\s+/g, ' ').trim();
  }
  function fmtN(x) { return String(parseFloat(x)); }
  var STOP = { TUBO: 1, VIGA: 1, PERFIL: 1, DE: 1, EM: 1, COM: 1, MM: 1, M: 1, X: 1, ML: 1, UNI: 1, UN: 1 };

  /* Chave normalizada do artigo: tipo de perfil + medida */
  function itemKey(desig) {
    var t = normText(desig), m;
    if ((m = t.match(/\b(HEA|HEB|HEM|IPE|IPN|UPN|UPE|UAP)\s*-?\s*(\d{2,4})\b/)))
      return { fam: m[1], key: m[1] + m[2], base: m[1] + m[2] };
    var t2 = t.replace(/\bTUBO\s+QUAD(?:R(?:ADO)?)?\.?(?=\s|\d)/, 'SHS ').replace(/\bTUBO\s+RECT?(?:ANGULAR)?\.?(?=\s|\d)/, 'RHS ')
      .replace(/\bTUBO\s+RED(?:ONDO)?\.?(?=\s|\d)/, 'CHS ');
    if ((m = t2.match(/\b(SHS|RHS)\s*(\d+(?:\.\d+)?)\s*X\s*(\d+(?:\.\d+)?)(?:\s*X\s*(\d+(?:\.\d+)?))?/))) {
      var dims = [m[2], m[3], m[4]].filter(Boolean).map(fmtN);
      var fam = m[1];
      if (fam === 'SHS' && dims.length === 3 && dims[0] !== dims[1]) fam = 'RHS';
      if (fam === 'SHS' && dims.length === 3) dims = [dims[0], dims[2]];
      var sec = fam === 'RHS' && dims.length === 3 ? dims[0] + 'X' + dims[1] : dims[0];
      return { fam: fam, key: fam + ' ' + dims.join('X'), base: fam + ' ' + sec };
    }
    if ((m = t2.match(/\bCHS\s*(\d+(?:\.\d+)?)\s*X\s*(\d+(?:\.\d+)?)/)))
      return { fam: 'CHS', key: 'CHS ' + fmtN(m[1]) + 'X' + fmtN(m[2]), base: 'CHS ' + fmtN(m[1]) };
    if (/\b(VARAO|BARRA|VARETA)\s+ROSC/.test(t) && (m = t.match(/\bM\s?(\d{1,2})\b/)))
      return { fam: 'VARAO', key: 'VARAO ROSCADO M' + m[1], base: 'VARAO ROSCADO M' + m[1] };
    if (/\bCHAPA\b/.test(t)) {
      m = t.match(/\bCHAPA\b\D*?(\d+(?:\.\d+)?)\s*MM\b/) || t.match(/\bCHAPA\s+(?:[A-Z]+\s+)?(\d+(?:\.\d+)?)\b/);
      if (m) return { fam: 'CHAPA', key: 'CHAPA ' + fmtN(m[1]) + 'MM', base: 'CHAPA ' + fmtN(m[1]) + 'MM' };
    }
    if ((m = t.match(/\b(?:CANTONEIRA|L)\s*(\d+)\s*X\s*(\d+)(?:\s*X\s*(\d+(?:\.\d+)?))?/))) {
      var d = [m[1], m[2], m[3]].filter(Boolean).map(fmtN);
      return { fam: 'L', key: 'L ' + d.join('X'), base: 'L ' + d.slice(0, 2).join('X') };
    }
    if ((m = t.match(/\b(?:BARRA\s+CHATA|BCH|FERRO\s+CHATO)\s*(\d+)\s*X\s*(\d+)/)))
      return { fam: 'BCH', key: 'BCH ' + m[1] + 'X' + m[2], base: 'BCH ' + m[1] + 'X' + m[2] };
    var toks = t.replace(/[^A-Z0-9.]+/g, ' ').split(' ').filter(function (w) {
      return w && !STOP[w] && !/^\d+(\.\d+)?M$/.test(w) && !/^\d+X\d+(\.\d+)?M$/.test(w) && !/^[SP]\d{3}/.test(w);
    });
    return { fam: 'TXT', key: 'TXT:' + toks.join(' '), base: 'TXT:' + toks.join(' '), toks: toks };
  }
  function jaccard(a, b) {
    var A = {}, B = {}, i, inter = 0, uni = 0;
    for (i = 0; i < a.length; i++) A[a[i]] = 1;
    for (i = 0; i < b.length; i++) B[b[i]] = 1;
    for (var k in A) { uni++; if (B[k]) inter++; }
    for (k in B) if (!A[k]) uni++;
    return uni ? inter / uni : 0;
  }

  function material(q, desig) {
    var tq = normText(q), t = normText((q || '') + ' ' + (desig || '')), m;
    if (/AISI|INOX|\b1\.4\d{3}\b/.test(t)) {
      m = t.match(/AISI\s*(\d{3}L?)/);
      return { cls: 'INOX', grade: m ? 'AISI ' + m[1] : 'INOX' };
    }
    m = tq.match(/\b([SP])\s*(235|275|355|420|460)/) || t.match(/\b([SP])\s*(235|275|355|420|460)/);
    if (m) return { cls: 'CARBONO', grade: m[1] + m[2] };
    m = t.match(/(?:\bCL\s*|\b)(4\.6|5\.6|8\.8|10\.9|12\.9)(?!\d)/);
    if (m) return { cls: 'CLASSE', grade: m[1] };
    return { cls: null, grade: null };
  }
  function qualityScore(a, b) {
    if (!a.cls || !b.cls) return 0.75;
    if (a.cls !== b.cls) return 0;
    return a.grade === b.grade ? 1 : 0.5;
  }

  /* ---------- Leitura de folha (array de linhas) ---------- */
  function rowNames(row) { return (row || []).map(function (c) { return typeof c === 'string' ? c.trim() : null; }); }
  function parseSheet(aoa) {
    var requisition = [], history = [], i, j;
    for (i = 0; i < aoa.length; i++) {
      var names = rowNames(aoa[i]);
      var isHist = names.indexOf('Fornecedor') >= 0 && names.indexOf('DataEncomenda') >= 0;
      var isReq = !isHist && names.indexOf('Designacao') >= 0 &&
        (names.indexOf('Quantidade') >= 0 || names.indexOf('Quantidade_Requisitada') >= 0);
      if (!isHist && !isReq) continue;
      if (isHist) {
        var fIdx = names.indexOf('Fornecedor');
        var first = function (n) { for (var k = fIdx; k < names.length; k++) if (names[k] === n) return k; return names.indexOf(n); };
        var linkCol = names.indexOf('Auto_ID') >= 0 && names.indexOf('Auto_ID') < fIdx ? names.indexOf('Auto_ID') : -1;
        var col = {};
        ['Fornecedor', 'DataEncomenda', 'ValorTabelaMinimo', 'Auto_ID', 'Numero_Encomenda', 'Requesicao_N', 'Designacao', 'Quantidade',
          'Peso_unitario', 'Unidade', 'Preco_unitario_euro', 'desconto', 'Unidade_Preco', 'Preco_Total_euro', 'Peso_total', 'Recebido',
          'Certificado', 'Preco_unitario_pvp', 'Preco_Total_pvp', 'Preco_tabela', 'data_recepcao', 'DataPrevisaoChegada', 'LINHA_ANULADA', 'AUTO_IDARTIGO',
          'PRECO_TABELA_NEGOCIADO', 'Qualidade', 'Requesicao_Item', 'N_Obra'].forEach(function (n) { col[n] = first(n); });
        var link = null;
        for (j = i + 1; j < aoa.length; j++) {
          var r = aoa[j] || [], nm = rowNames(r);
          if (nm.indexOf('Fornecedor') >= 0 && nm.indexOf('DataEncomenda') >= 0) break;
          if (nm.indexOf('Designacao') >= 0 && nm.indexOf('Quantidade') >= 0) break;
          var g = function (n) { return col[n] >= 0 ? clean(r[col[n]]) : null; };
          if (linkCol >= 0 && clean(r[linkCol]) !== null && num(r[linkCol]) !== null) link = num(r[linkCol]);
          if (!g('Fornecedor') || !g('Designacao')) continue;
          history.push({
            link: linkCol >= 0 ? link : (num(g('Requesicao_Item'))),
            fornecedor: String(g('Fornecedor')).trim(),
            data: toDay(g('DataEncomenda')),
            tabelaMin: num(g('ValorTabelaMinimo')),
            orderLineId: num(g('Auto_ID')),
            encomenda: g('Numero_Encomenda') !== null ? String(g('Numero_Encomenda')) : null,
            req: num(g('Requesicao_N')),
            designacao: String(g('Designacao')),
            qualidade: g('Qualidade') !== null ? String(g('Qualidade')).trim() : '',
            quantidade: num(g('Quantidade')),
            pesoUnit: num(g('Peso_unitario')),
            unidade: g('Unidade') || '',
            precoUnit: num(g('Preco_unitario_euro')),
            desconto: num(g('desconto')),
            unidadePreco: g('Unidade_Preco') || '',
            precoTotal: num(g('Preco_Total_euro')),
            pesoTotal: num(g('Peso_total')),
            recebido: num(g('Recebido')),
            certificado: g('Certificado') || '',
            precoPvp: num(g('Preco_unitario_pvp')),
            precoTotalPvp: num(g('Preco_Total_pvp')),
            precoTabela: num(g('Preco_tabela')),
            tabelaNeg: num(g('PRECO_TABELA_NEGOCIADO')),
            recepcao: toDay(g('data_recepcao')),
            previsao: toDay(g('DataPrevisaoChegada')),
            anulada: num(g('LINHA_ANULADA')) === 1,
            artigo: num(g('AUTO_IDARTIGO')),
            obra: g('N_Obra')
          });
        }
        i = j - 1;
      } else {
        var c2 = {};
        ['Auto_ID', 'Numero_Requesicao', 'Designacao', 'Qualidade', 'Dimensoes', 'Quantidade', 'Quantidade_Requisitada', 'Peso_unitario',
          'Unidade', 'Peso_Total', 'AUTO_IDARTIGO', 'NumLinha', 'N_Obra', 'Certificado'].forEach(function (n) { c2[n] = names.indexOf(n); });
        for (j = i + 1; j < aoa.length; j++) {
          var rr = aoa[j] || [], nn = rowNames(rr);
          if ((nn.indexOf('Fornecedor') >= 0 && nn.indexOf('DataEncomenda') >= 0) || nn.indexOf('Designacao') >= 0) break;
          if (!rr.some(function (v) { return clean(v) !== null; })) break;
          var h = function (n) { return c2[n] >= 0 ? clean(rr[c2[n]]) : null; };
          if (!h('Designacao')) continue;
          var q = num(h('Quantidade')); if (q === null) q = num(h('Quantidade_Requisitada'));
          var pu = num(h('Peso_unitario')), pt = num(h('Peso_Total'));
          if (pt === null && pu !== null && q !== null) pt = pu * q;
          requisition.push({
            id: num(h('Auto_ID')), req: num(h('Numero_Requesicao')), linha: num(h('NumLinha')) || requisition.length + 1,
            designacao: String(h('Designacao')).trim(), qualidade: h('Qualidade') ? String(h('Qualidade')).trim() : '',
            dimensoes: h('Dimensoes') ? String(h('Dimensoes')) : '', quantidade: q, pesoUnit: pu, unidade: h('Unidade') || '',
            pesoTotal: pt, artigo: num(h('AUTO_IDARTIGO')), obra: h('N_Obra'), certificado: h('Certificado') || ''
          });
        }
        i = j - 1;
      }
    }
    return { requisition: requisition, history: dedupe(history) };
  }
  function dedupe(hist) {
    var seen = {}, out = [];
    hist.forEach(function (h) {
      var k = h.orderLineId !== null ? 'id' + h.orderLineId : [h.fornecedor, h.data, h.encomenda, h.designacao, h.quantidade, h.precoUnit].join('|');
      if (seen[k]) { if (h.link !== null && seen[k].links.indexOf(h.link) < 0) seen[k].links.push(h.link); return; }
      h.links = h.link !== null ? [h.link] : [];
      seen[k] = h; out.push(h);
    });
    return out;
  }
  function parseWorkbook(sheets) { // sheets: array de aoa
    var res = { requisition: [], history: [] };
    sheets.forEach(function (a) { var p = parseSheet(a); res.requisition = res.requisition.concat(p.requisition); res.history = res.history.concat(p.history); });
    res.history = dedupe(res.history.map(function (h) { delete h.links; return h; }));
    return res;
  }

  /* ---------- Preço de cada linha de encomenda ---------- */
  function priceOf(h) {
    var net = null;
    if (h.precoTotal > 0 && h.quantidade > 0) net = h.precoTotal / h.quantidade;
    else if (h.precoUnit > 0) net = h.precoUnit * (1 - ((h.desconto || 0) >= 1 ? h.desconto : 0) / 100);
    return net;
  }

  function unitBasis(h) {
    var u = String(h.unidadePreco || h.unidade || '').toUpperCase().trim();
    if (u === 'ML' || u === 'M') return 'ML';
    if (u === 'KG' || u === 'TON' || u === 'T') return 'kg';
    return h.pesoUnit > 0 ? 'kg' : 'un';
  }
  function lineUnitIsML(line) { var u = String(line.unidade || '').toUpperCase(); return u === 'ML' || u === 'M' || (!u && /\bML\b|X\d+(\.\d+)?M\b/i.test(line.dimensoes || '') && /CHS|SHS|RHS|TUBO/i.test(line.designacao)); }
  var DEFAULT_W = { preco: 35, frequencia: 20, recencia: 20, prazo: 15, qualidade: 10 };
  var MATCH_LABEL = { exata: 'Artigo igual', proxima: 'Medida próxima', semelhante: 'Semelhante' };

  function analyse(requisition, history, opts) {
    opts = opts || {};
    var W = opts.weights || DEFAULT_W, refDay = opts.refDay !== undefined ? opts.refDay : Math.floor(Date.now() / DAY);
    var halfLife = opts.halfLife || 180;
    var wsum = W.preco + W.frequencia + W.recencia + W.prazo + W.qualidade || 1;
    var hk = history.map(function (h) { return itemKey(h.designacao); });
    var hm = history.map(function (h) { return material(h.qualidade, h.designacao); });

    return requisition.map(function (line) {
      var lk = itemKey(line.designacao), lm = material(line.qualidade, line.designacao);
      var rows = [];
      history.forEach(function (h, idx) {
        var how = [], k = hk[idx];
        if (line.id !== null && h.links && h.links.indexOf(line.id) >= 0) how.push('ligação');
        if (line.artigo !== null && h.artigo !== null && h.artigo === line.artigo) how.push('artigo');
        var mt = null;
        if (k.key === lk.key) mt = 'exata';
        else if (k.fam !== 'TXT' && k.base === lk.base) mt = 'proxima';
        else if (k.fam === 'TXT' && lk.fam === 'TXT' && jaccard(k.toks, lk.toks) >= 0.6) mt = 'semelhante';
        if (mt) how.push('designação');
        if (!how.length) return;
        if (!mt) mt = (how.indexOf('ligação') >= 0 || how.indexOf('artigo') >= 0) ? (k.fam === lk.fam ? 'proxima' : 'semelhante') : 'semelhante';
        var q = qualityScore(lm, hm[idx]);
        var price = priceOf(h);
        var est = null;
        if (price === null) est = h.precoTotalPvp > 0 && h.quantidade > 0 ? h.precoTotalPvp / h.quantidade : (h.precoPvp > 0 ? h.precoPvp : null);
        var r = {
          h: h, how: how, match: mt, matchFactor: mt === 'exata' ? 1 : mt === 'proxima' ? 0.85 : 0.7,
          key: k.key, mat: hm[idx], quality: q, price: price, estimated: est, ub: unitBasis(h),
          lead: h.recepcao !== null && h.data !== null && h.recepcao >= h.data ? h.recepcao - h.data : null,
          flags: [], excluded: false, priceOk: false
        };
        if (h.anulada) { r.flags.push('Linha anulada'); r.excluded = true; }
        if (line.req !== null && h.req === line.req) { r.flags.push('Encomenda desta requisição'); r.excluded = true; }
        if (q === 0) { r.flags.push('Material diferente (' + (hm[idx].grade || hm[idx].cls) + ')'); r.excluded = true; }
        if (price === null && !h.anulada) r.flags.push(r.estimated ? 'Preço pendente (estimado por tabela)' : 'Sem preço');
        rows.push(r);
      });
      // base de comparação: €/kg (barras/chapas ao peso), €/ML (tubos ao metro) ou €/unidade
      var priced = rows.filter(function (r) { return !r.excluded && r.price !== null; });
      var cnt = { kg: 0, ML: 0, un: 0 };
      priced.forEach(function (r) { cnt[r.ub]++; });
      var basis = !priced.length ? (lineUnitIsML(line) ? 'ML' : (/^(HEA|HEB|HEM|IPE|IPN|UPN|UPE|UAP|CHAPA|L|BCH)$/.test(lk.fam) ? 'kg' : 'un')) : (cnt.kg >= cnt.ML && cnt.kg >= cnt.un ? 'kg' : cnt.ML >= cnt.un ? 'ML' : 'un');
      var conv = function (p, h) {
        if (p === null) return null;
        var u = unitBasis(h);
        if (basis === 'kg') { if (u === 'kg' && /KG/i.test(h.unidadePreco || '')) return p; return h.pesoUnit > 0 ? p / h.pesoUnit : null; }
        if (basis === 'ML') return u === 'ML' ? p : null;
        return u === 'un' ? p : null;
      };
      rows.forEach(function (r) { r.cmp = conv(r.price, r.h); r.cmpEst = conv(r.estimated, r.h); });
      var vals = rows.filter(function (r) { return !r.excluded && r.cmp !== null; }).map(function (r) { return r.cmp; }).sort(function (a, b) { return a - b; });
      var med = vals.length ? (vals.length % 2 ? vals[(vals.length - 1) / 2] : (vals[vals.length / 2 - 1] + vals[vals.length / 2]) / 2) : null;
      rows.forEach(function (r) {
        if (r.excluded || r.cmp === null) return;
        if (vals.length >= 3 && (r.cmp < 0.6 * med || r.cmp > 1.8 * med)) { r.flags.push('Preço atípico (fora da mediana)'); return; }
        r.priceOk = true;
      });
      // agregação por fornecedor
      var map = {};
      rows.forEach(function (r) {
        if (r.excluded) return;
        var s = map[r.h.fornecedor] || (map[r.h.fornecedor] = { fornecedor: r.h.fornecedor, rows: [], orders: {}, lastDay: null, matchFactor: 0, q: [], leads: [] });
        s.rows.push(r);
        s.orders[r.h.encomenda || ('l' + r.h.orderLineId)] = 1;
        if (r.h.data !== null && (s.lastDay === null || r.h.data > s.lastDay)) s.lastDay = r.h.data;
        s.matchFactor = Math.max(s.matchFactor, r.matchFactor);
        s.q.push(r.quality);
        if (r.lead !== null) s.leads.push(r.lead);
      });
      var sups = Object.keys(map).map(function (k) {
        var s = map[k], ok = s.rows.filter(function (r) { return r.priceOk; });
        var wsumP = 0, acc = 0;
        ok.forEach(function (r) {
          var w = basis === 'kg' ? (r.h.pesoTotal > 0 ? r.h.pesoTotal : (r.h.quantidade || 1) * r.h.pesoUnit) : (r.h.quantidade || 1);
          w = w > 0 ? w : 1; acc += r.cmp * w; wsumP += w;
        });
        s.nOrders = Object.keys(s.orders).length;
        s.nLines = s.rows.length;
        s.avgPrice = wsumP ? acc / wsumP : null;
        var lastP = ok.slice().sort(function (a, b) { return (b.h.data || 0) - (a.h.data || 0); });
        s.lastPrice = lastP.length ? lastP[0].cmp : null;
        s.lastPriceUnit = lastP.length ? lastP[0].price : null;
        s.lastPriceUnitLabel = lastP.length ? (lastP[0].h.unidadePreco || lastP[0].h.unidade || 'un') : '';
        s.lastPriceDay = lastP.length ? lastP[0].h.data : null;
        s.minPrice = ok.length ? Math.min.apply(null, ok.map(function (r) { return r.cmp; })) : null;
        s.nPriced = ok.length;
        s.leadAvg = s.leads.length ? s.leads.reduce(function (a, b) { return a + b; }, 0) / s.leads.length : null;
        s.qualityAvg = s.q.reduce(function (a, b) { return a + b; }, 0) / s.q.length;
        return s;
      });
      var minAvg = Math.min.apply(null, sups.filter(function (s) { return s.avgPrice !== null; }).map(function (s) { return s.avgPrice; }).concat([Infinity]));
      var maxOrd = Math.max.apply(null, sups.map(function (s) { return s.nOrders; }).concat([1]));
      var minLead = Math.min.apply(null, sups.filter(function (s) { return s.leadAvg !== null; }).map(function (s) { return s.leadAvg; }).concat([Infinity]));
      sups.forEach(function (s) {
        var c = {
          preco: s.avgPrice !== null ? minAvg / s.avgPrice : 0.4,
          frequencia: s.nOrders / maxOrd,
          recencia: s.lastDay !== null ? Math.pow(0.5, Math.max(0, refDay - s.lastDay) / halfLife) : 0,
          prazo: s.leadAvg !== null ? Math.min(1, (minLead + 1) / (s.leadAvg + 1)) : 0.5,
          qualidade: s.qualityAvg
        };
        s.components = c;
        var base = (W.preco * c.preco + W.frequencia * c.frequencia + W.recencia * c.recencia + W.prazo * c.prazo + W.qualidade * c.qualidade) / wsum;
        s.score = Math.round(base * s.matchFactor * 1000) / 10;
        s.matchLabel = s.matchFactor === 1 ? MATCH_LABEL.exata : s.matchFactor >= 0.85 ? MATCH_LABEL.proxima : MATCH_LABEL.semelhante;
      });
      sups.sort(function (a, b) { return b.score - a.score || (a.avgPrice || 1e9) - (b.avgPrice || 1e9); });
      var top = sups[0] || null;
      var qtyBasis = basis === 'kg' ? line.pesoTotal : basis === 'ML' ? (lineUnitIsML(line) ? line.quantidade : null) : line.quantidade;
      rows.sort(function (a, b) { return (b.h.data || 0) - (a.h.data || 0); });
      return {
        qtyBasis: qtyBasis, line: line, key: lk.key, mat: lm, basis: basis, median: med, rows: rows, suppliers: sups, top: top,
        estimate: top && top.avgPrice !== null && qtyBasis ? top.avgPrice * qtyBasis : null,
        notes: rows.filter(function (r) { return r.flags.indexOf('Encomenda desta requisição') >= 0; })
      };
    });
  }

  return { parseSheet: parseSheet, parseWorkbook: parseWorkbook, analyse: analyse, itemKey: itemKey, material: material,
    dayToISO: dayToISO, dayToPT: dayToPT, toDay: toDay, DEFAULT_W: DEFAULT_W };
})();
if (typeof module !== 'undefined') module.exports = SFCore;
