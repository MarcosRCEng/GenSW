# GenSW — duas alternativas para escolha de navegação

Pesquisa de 11/10/2026 (America/Sao_Paulo), [Tarefa #440](https://devops-lab.tailaf9418.ts.net/issues/440), filha da [Evolução #386](https://devops-lab.tailaf9418.ts.net/issues/386). A [#439](https://devops-lab.tailaf9418.ts.net/issues/439) registra apenas a preparação. Base inspecionada: `origin/main`, commit `022450bbd651fa3d0571d9eb00fe532292544bb6`, merge de F01. Branch documental: `codex/440-pesquisa-ui-ux`.

## Abrir e comparar

Abra [o protótipo navegável](index.html). Selecione **A · Em camadas** ou **B · Sidebar única**, use **Comparar propostas** e alterne **Desktop / 768 px / 390 px**. Os mesmos cenários e dados sintéticos estão disponíveis nas duas estruturas. A escolha no protótipo muda apenas a demonstração; não concede autorização de implementação.

Servidor local de revisão: [http://127.0.0.1:5188/](http://127.0.0.1:5188/), exclusivamente loopback e sem APIs. Não usa 5179/7009. Para iniciar em outra sessão, estando neste diretório:

```powershell
node serve.mjs 5188
```

O processo informa seu PID e falha se a porta estiver ocupada; não substitui processos existentes. `index.html` também pode ser aberto diretamente no navegador, pois não busca dados nem depende de serviços. O servidor só serve este diretório de artefatos.

## Resultado e recomendação

**Recomendo B**, a sidebar única com grupos e navegação contextual. O mapa existente tem poucos níveis e fluxos locais distintos; B preserva mais largura para tabelas e exige menos estados de abertura no celular. **A** explora a hierarquia da referência com trilho, módulos e painel secundário transitório, favorecendo a descoberta através de descrições e agrupamentos. A recomendação é uma inferência de adequação ao GenSW, não uma medição de produtividade ou superioridade de produto.

O principal problema não é somente aparência: `history.state` já preserva a origem em vários detalhes, mas há interrupções dessa cadeia em formulários e comandos. Financeiro fecha painéis locais; versões, perfis e subregistros retornam ao pai; detalhes retornam à lista. O contrato propõe controles consistentes para esses destinos diferentes, sem substituir retornos por histórico imprevisível.

Na demonstração, A ocupa 56 px de trilho + 208 px de módulos, com painel transitório de 314 px (orçamento aproximado de 320 px); B ocupa 232 px. Ambas usam drawer de navegação quando o contêiner da aplicação tem até 820 px. Aproximadamente 1100 px é apenas o orçamento de largura útil a avaliar para o futuro frontend, depois da escolha. A/B compartilham a paleta atual: verde `#176348`, texto `#24362f`, secundário `#53645a` e foco `#287cd0`.

O Financeiro aparece como **drawer modal nas duas alternativas**, uma proposta visual e de foco para os painéis inline existentes, sujeita ao seu aceite. Fechar continua uma ação por callback, mantendo caixa/mês/filtros/página; não cria rota de painel nem confirma operação. O fluxo representa dinheiro realizado e não adiciona contas a pagar/receber ou funções financeiras futuras.

## Entrega

| Artefato | Conteúdo |
| --- | --- |
| [Diagnóstico verificável](diagnostico.md) | Inventário de rotas/fluxos, semântica, destinos/fallbacks, contexto, ações, estados, testes existentes e problemas priorizados; links ao código da main inspecionada |
| [Benchmark](benchmark.md) | Seis referências oficiais, três ERPs Web, matrizes comparativas, fontes/versionamento/data, observações e inferências separadas, análise da imagem fornecida |
| [Verificação das fontes](fontes-verificacao.md) | URLs públicas, status HTTP, conteúdo recuperado e limites de extração |
| [Contrato e plano](contrato-e-plano.md) | Mapa real de menus, retornos/cabeçalhos/ações, tokens/estados, arquitetura A/B, recomendação, riscos, dependências e plano por etapas com critérios de aceite |
| [Protótipos A/B](index.html) | HTML/CSS/JS independente, dados sintéticos, desktop/móvel, cenários comparáveis e estados demonstrativos |
| [Roteiro dos protótipos](README-prototipos.md) | Fluxos, links diretos, controles de avaliação e limites de simulação |
| [Validação desta pesquisa](validacao.md) | Verificações realmente executadas, capturas sintéticas e pendências de revisão |

## Roteiro curto de decisão

1. Abra Início e os menus. Compare descoberta de outro módulo, estado atual e espaço útil com menu aberto/fechado.
2. Em Animais, aplique/observe filtro, página e ordenação; abra detalhe, pesagem e edição; cancele/salve; volte à lista. Abra também o link direto, sem origem.
3. Em Caixa, mantenha mês/filtro, abra o lançamento/painel e feche. Compare **Fechar painel** com retorno de detalhe por URL.
4. Em Estoque, abra lote a partir de Saldos filtrados e retorne. Execute apenas a simulação de recebimento → prévia → confirmação → evento → retorno. Navegação não confirma recebimento.
5. Em Receita e Catálogo, consulte versão/perfil, diferencie detalhe de edição e retorne ao pai.
6. Repita em 390 px e 768 px; use Tab, Enter, Escape e observe foco. Experimente os estados demonstrativos, sem inferir autorização ou integração real a partir da simulação.

## Decisões para autorizar a continuidade

- **Estrutura:** A ou B (recomendada).
- **Retornos/cabeçalhos:** aceitar o contrato de origem/pai/painel, preservação de filtros e proteção de alterações não salvas, incluindo o destino contextual após edição e o drawer modal financeiro com callback/contexto conservados.
- **Direção visual:** tema claro com o verde e os neutros demonstrados, densidade operacional única, filtros próximos à lista e tabelas completas com rolagem interna.

Após escolha e autorização expressa, criar Tarefa própria antes de cada etapa executável de implementação e seguir o plano. Esta entrega permanece **Em validação** enquanto houver escolha/revisão humana pendente. Não há código de remodelagem no frontend real, mudança de regra/permissão/rota/dado, migration, avanço F02–F06, merge ou publicação.

## Ambiente e privacidade

O checkout solicitado em `codex/415-planejamento-producao` e seus arquivos locais foram preservados. A pesquisa ocorreu em worktree isolado da main. Na verificação desta etapa, não havia listeners em 5179/7009 nem processo de API identificado; a homologação não foi recriada ou interrompida. O diagnóstico é de código/documentação da versão integrada, complementado por browser nos protótipos sintéticos. Não foi realizada inspeção visual positiva da aplicação autenticada em execução nesta etapa.

Não foram consultados/exportados cadastros operacionais, valores financeiros reais, imagens privadas, credenciais ou caminhos do volume privado. Os parâmetros de hash dos protótipos são sintéticos; a proposta para a aplicação real conserva o contrato privado de `history.state`. Testes históricos de F01 são contexto documental, não validação desta pesquisa.
