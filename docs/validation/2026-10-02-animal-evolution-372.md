# Evidências — Animal: pesos, imagens e árvore (#372)

Data: 2026-10-02. Evolução #364; tarefas #366–#371 e tarefa deste prompt #372. Planejamento aprovado em #365, preservado no commit `f8d5195`. Branch `codex/372-animal-pesos-imagens-arvore`, a partir do planejamento sobre `93fdb79` (PR #14 integrada em main). Sem merge ou liberação de produção.

## Implementação

- #366: entidade de pesagem, seis marcos controlados, precisão decimal, filtros/paginação, correção com data preservada, idade observada calculada separadamente da idade alvo. Correção de nascimento e pesagens compartilham lock de Animal.
- #367: entidades/tabelas distintas para imagens de Animal e Variedade; volume privado, assinatura/decodificação/reorientação/remoção de perfis, limites e cotas; preferência explícita única, ordem atômica e fallback determinístico; conteúdo autenticado e compensação de falhas.
- #368: busca paginada por nome/código, predicado compartilhado com gravação para espécie, raça condicional, sexo, ativo e auto-filiação; exclusão de descendentes antes de paginar; serialização PostgreSQL da genealogia contra ciclos concorrentes e alterações de cadastro/status. Substituição mantém histórico.
- #369: projeção própria de árvore, snapshot por resposta, ascendentes/descendentes limitados, nós deduplicados, alertas de legado inconsistente/cíclico, expansão paginada e imagem preferencial de Animal. Pedigree legado preservado.
- #370: painel de pesos, galerias separadas, combobox com teclado/cancelamento de respostas obsoletas, árvore com fotos circulares, conectores e relações em texto acessível, expansão/recentragem/voltar e limites acumulados. Multipart/Blob utilizam o mesmo cliente de sessão; URLs de fotos são revogadas.
- #371: migrations aditivas, testes com PostgreSQL real, teste de backup/restauração, relatório somente leitura e documentação operacional. CI verifica e expõe pg_dump/pg_restore no diretório de diagnóstico dos testes.

## Validações executadas

Ambiente local Windows 10.0.26100, processo 64 bits, 28 CPUs lógicas, .NET 8, PostgreSQL 18 instalado. Cada fixture cria banco/cluster descartável. Nenhuma migration foi aplicada a banco compartilhado.

| Validação | Resultado e evidência local |
| --- | --- |
| `dotnet build GenSW.sln -c Release` | Êxito, zero warnings/erros; `.test-output/372/final-build.log` |
| `dotnet test GenSW.sln -c Release` | Domain 100, Application 124, API 115 e Infrastructure 93 aprovados, nenhum ignorado; `.test-output/372/final-backend.log` e TRX em `final/` |
| `AnimalEvolutionTests` após ampliar a matriz | 8 aprovados: inclui duas verificações adicionais à rodada completa; `.test-output/372/evolution-final.trx` |
| API após separar DTOs de resposta | 1 integração aprovada; `.test-output/372/evolution-api-final.trx` |
| Frontend build e lint | Êxito, zero erro de TypeScript/ESLint |
| `npm test` após ajustes finais | 322 aprovados em 30 arquivos; `.test-output/372/frontend-final.log` |
| EF `has-pending-model-changes` Release | Sem mudanças de modelo pendentes |
| `git diff --check` | Sem erro de whitespace após normalizar EOF |

A rodada completa cobre cadastro/taxonomia, autenticação, filiação histórica, cruzamentos/ciclos/proles e Produção de Ovos pelas suítes já existentes. Não equivale a nova validação manual de todos esses módulos.

As oito integrações específicas verificam migrations sobre Animal preexistente, datas/idade 30 alvo e 31 real, medições duplicadas no dia, nascimento concorrente, ciclos simultâneos de dois/três animais, substituição histórica, classificação/status concorrentes, raça condicional na busca e gravação, unicidade concorrente de representativa, isolamento por proprietário, reordenação obsoleta, fallback, reativação, cotas de quantidade/bytes, falhas compensadas de banco/storage, arquivo falso/truncado/grande/animado/APNG, limite de área/dimensão e EXIF removido. O teste de restauração utiliza pg_dump/pg_restore, restaura derivados e compara SHA-256 de conteúdo.

O teste de escala usa 10.000 animais e 19.992 vínculos, ancestrais compartilhados, árvore profunda e alta descendência, com 25 amostras alternando raiz/folha. Na rodada completa: árvore p95 197,6 ms; candidatos p95 180,1 ms; 9 comandos SQL no recorte máximo medido (orçamento 12). Teto de 100 nós/200 arestas e expansão de 20 itens confirmados. Medidas locais não incluem rede de produção nem representam SLA do ambiente alvo.

## Verificação no navegador

Playwright CLI com Chromium real, frontend Vite e API autenticada sobre PostgreSQL descartável, sem respostas de negócio simuladas. Verificados: criar/editar pesagem com data e idade alvo/real, upload, preferência, ordenação, inativação e reativação sem restaurar preferência; seleção de pai com ArrowDown/Enter e manutenção do vínculo histórico; bloqueio da busca com classificação não salva; expansão explícita, recentragem e retorno da árvore; galeria independente de Variedade e edição de legenda.

Inspeção visual em 1280×900 e 390×844: cartões/fotos circulares e conectores legíveis, estados sem foto e pais ausentes, rolagem confinada ao diagrama, ausência de transbordamento horizontal da página. Centralização se ajusta à largura da janela. O desenho de referência `arvore.webp` foi somente consultado, sem cópia para o repositório.

Capturas locais, geradas com dados descartáveis:

- `output/playwright/372/tree-desktop.png`
- `output/playwright/372/tree-mobile.png`
- `output/playwright/372/variety-gallery.png`

Capturas e relatórios brutos permanecem locais, fora dos commits. O console do ambiente final apresentou apenas o 404 preexistente de favicon, sem erro funcional nos fluxos verificados. Houve correções durante a execução (tradução LINQ, sincronização de teste assíncrono e centralização responsiva); resultados acima correspondem às repetições aprovadas, não às tentativas intermediárias.

## Pendências para aceite humano

Revisar implementação e CI remoto; validar convenções de marcos e usabilidade com dados reais; provisionar volume/ACL e validar o processador no host de destino; executar auditoria somente leitura e revisar inconsistências legadas; ensaiar backup/restauração do ambiente alvo; planejar janela de atualização de todos os escritores. Não executados: auditoria em dados de produção, backup real de produção, deploy, teste de leitor de tela externo ou homologação com usuários finais. O teste local de PostgreSQL e o navegador não substituem essas etapas.

Tarefas devem permanecer Em validação enquanto revisão/aceite estiverem pendentes. Tarefa #363 não foi alterada nem ampliada. Não há reconhecimento visual, DNA, dispositivos, automações ou rastreamento.

## Commits de implementação

- `be1007e`: backend, migrations, concorrência, segurança, projeção e testes (#366–#369/#371).
- `2875271`: frontend e cliente HTTP/sessão (#370).
- Documentação operacional e evidências em commit separado (#371/#372); o hash final e a publicação da branch são registrados no Redmine e na PR.

O planejamento `f8d5195` permanece ancestral da implementação. Artefatos locais preexistentes foram preservados; AGENTS.md e .gensw não entram nos commits.
