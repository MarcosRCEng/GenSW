# Evidências e limites da pesquisa #440

Validação em 11/10/2026, America/Sao_Paulo. Base integrada: `022450bbd651fa3d0571d9eb00fe532292544bb6`; `origin/main` reconsultada ao encerrar, sem mudança. Branch: `codex/440-pesquisa-ui-ux`. A [Tarefa #440](https://devops-lab.tailaf9418.ts.net/issues/440), filha da [Evolução #386](https://devops-lab.tailaf9418.ts.net/issues/386), registra commit, push, PR e resultado dos checks. O aceite das alternativas permanece humano.

## O que foi verificado

O diagnóstico examinou código e documentação da main integrada, inventariando **28 fluxos**. Foram conferidas **57 referências de código, 54 âncoras e 56 arquivos**, sem referência inválida ou indefinida. A leitura dos testes existentes identifica contratos e cobertura declarada; não equivale a executar esses testes nesta etapa.

O benchmark possui seis referências principais, incluindo Odoo, Frappe/ERPNext e Business Central, mais SAP Fiori, W3C e a imagem indicada pelo usuário. As 23 URLs únicas tiveram 21 respostas HTTP 200 e duas 403, com limites de extração registrados em [fontes-verificacao.md](fontes-verificacao.md). Não foram acessadas contas ou instâncias operacionais de ERPs. A imagem pública foi inspecionada visualmente; não foi incorporada aos artefatos nem copiada como identidade do GenSW.

Os protótipos foram executados no navegador por **Playwright CLI 0.1.22**, sessão própria `gensw-ux-440`, navegador com user agent Chrome 153 no Windows. Usou-se um servidor estático exclusivo em `127.0.0.1:5188`, sem backend. Resultados estruturados estão em [evidencias-browser.json](evidencias-browser.json).

| Grupo executado em A e B | Resultado |
| --- | --- |
| Animais: lista filtrada/paginada/ordenada → detalhe → editar → cancelar protegido/salvar → pesagem → pai → lista; recarga e acesso direto | 16 verificações passaram |
| Financeiro: linha correta → painel → edição protegida → salvar/cancelar → fechar/Escape; mês/categoria e foco à segunda linha; detalhe direto → caixa | 16 verificações passaram |
| Estoque: consulta filtrada → lote → retorno; recebimento com local/quantidade/data/referência → prévia → ajustar dados → confirmação → evento sintético → consulta | 24 verificações passaram |
| Receita/versão e Item/perfil: detalhe/edição separados, cancelar protegido, salvar, retorno ao filho e ao pai, inclusive por URL direta | 20 verificações passaram |
| Layout em **390, 768, 1100 e 1440 px**: dez telas por alternativa; ausência de overflow horizontal global/do conteúdo e comportamento do menu; teclado em drawer móvel; campos essenciais de Estoque e consulta de cancelado financeiro | 219 verificações passaram, incluindo 80 combinações de tela/largura; sem erro JavaScript de página nessa execução |
| Percursos físicos em **390 px**: Animais, Caixa, Estoque, Receita e Catálogo; estados loading/vazio/erro/sessão/permissão/confirmação; conflito, troca A/B e Voltar do navegador com rascunho | 38 verificações passaram |
| Revisão após último ajuste: um único Início no menu móvel de cada alternativa e retorno de foco; requisições e erros durante essa execução | 4 verificações passaram; zero requisições externas, respostas HTTP ≥400 ou erros JavaScript |
| Capturas e foco adicional | Nove capturas finais inspecionadas; Escape no menu A desktop devolve foco ao módulo; 12 avanços de Tab permanecem no painel financeiro |

Esses números descrevem verificações do protótipo sintético, não testes de regras de negócio nem cobertura da aplicação. Os nomes de controles foram conferidos nas snapshots; falhas iniciais de seletores do roteiro foram corrigidas antes dos resultados finais. As verificações recarregaram o documento após trocar URLs com hash, evitando executar JavaScript de uma versão anterior.

Correções resultantes da revisão incluíram menu desktop/móvel, contexto dos campos de comando, retorno de foco ao lançamento selecionado, consulta exclusiva de cancelados, semântica de listas versus registros de exemplo, conservação de rascunhos e duplicação de Início no menu móvel B. Todas ocorreram exclusivamente nos protótipos.

`node --check prototype.js` e `node --check serve.mjs` passaram. A revisão estática confirmou que HTML/CSS/JS não importam o frontend, não usam APIs de domínio e não contêm credenciais. Links internos dos relatórios e capturas foram conferidos. O diff da entrega fica restrito a `output/ux/440/`; AGENTS.md, scripts de QA locais, logs de navegador e arquivos de ambientes não fazem parte do commit.

## Capturas sintéticas finais

As capturas recortam o quadro da aplicação demonstrativa. O protótipo completo também oferece controles de comparação, largura e estado. Em celular, as tabelas continuam completas em regiões internas de rolagem; uma captura estática mostra apenas parte de suas colunas.

| Comparação | A | B |
| --- | --- | --- |
| Início desktop | [A desktop](capturas/a-desktop-inicio.png) | [B desktop](capturas/b-desktop-inicio.png) |
| Animais em 390 px, mesmo contexto | [A celular](capturas/a-celular-animais.png) | [B celular](capturas/b-celular-animais.png) |
| Menus | [A: painel secundário desktop](capturas/a-desktop-menu.png) | [B: drawer celular](capturas/b-celular-menu.png) |

Outros exemplos: [Caixa e painel desktop](capturas/b-desktop-caixa-painel.png), [Estoque celular](capturas/b-celular-estoque.png), [Versão e retorno ao pai celular](capturas/a-celular-versao.png).

Contraste calculado dos pares centrais: texto `#24362f`/branco 12,78:1; secundário `#53645a`/branco 6,29:1 e superfície `#eaf4ed` 5,59:1; branco/verde `#176348` 7,20:1; branco/verde escuro `#173c30` 12,17:1; aviso `#735119`/`#fff5dd` 6,63:1; perigo `#943d25`/`#fff7f3` 6,72:1. Isso verifica somente esses pares; não certifica todo o CSS ou conformidade WCAG. Alvos móveis essenciais têm pelo menos 44 px de altura na amostra.

## Ambiente preservado e limites

O checkout solicitado continua em `codex/415-planejamento-producao`, commit `286d81f835d4b228c3f95fffc2903e66c7025e7b`. Seus arquivos locais anteriores foram preservados. O trabalho versionado está em worktree próprio da main. Uma cópia nova dos artefatos de revisão foi colocada em `output/ux/440` do diretório solicitado, sem substituir diretórios de trabalhos anteriores.

Na inspeção do ambiente, não havia listeners em 5179/7009 nem processo de API identificado. Não se recriou a homologação ou seu banco/volume. Portanto, **o diagnóstico integrado é estático e documental; a inspeção positiva em navegador foi dos protótipos**, sem sessão autenticada da aplicação real. Nenhum cadastro, valor financeiro, foto ou dado operacional foi usado. F01 não foi reimplementada; F02–F06 não foram iniciadas.

Não foram executadas localmente as suítes .NET, Vitest, lint ou build do GenSW, pois nenhum código da aplicação mudou. O PR documental aciona os checks existentes do GitHub; seu resultado é registrado na Tarefa. Resultados históricos de F01 não são apresentados como validação desta entrega.

Permanecem para a implementação após autorização: navegador sobre aplicação integrada; todos os destinos reais e permissões; leitor de tela, zoom e aparelhos físicos; resolução real de concorrência/sessão; testes de negócio existentes e persistência privada do contexto; ambiente isolado de revisão e validação manual. O aviso nativo de saída/recarregamento está implementado na amostra, mas não foi homologado entre navegadores. Salvar na demonstração não persiste dados além da memória, e Receita/Catálogo demonstram sobretudo o destino de retorno.

O servidor de revisão serve somente artefatos públicos/sintéticos em loopback. Para encerrar ou iniciar posteriormente, use o processo próprio identificado no terminal ou `node serve.mjs 5188` neste diretório, verificando antes se a porta já está ocupada. A entrega aguarda **escolha A/B e autorização do contrato/direção visual**, incluindo o drawer financeiro proposto; não há merge, publicação ou alteração de banco compartilhado.
