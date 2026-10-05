# Propriedades operacionais — evidências da Evolução #404

Data: 05/10/2026, America/Sao_Paulo. [Evolução #404](https://devops-lab.tailaf9418.ts.net/issues/404); coordenação [#406](https://devops-lab.tailaf9418.ts.net/issues/406), backend [#407](https://devops-lab.tailaf9418.ts.net/issues/407), frontend [#408](https://devops-lab.tailaf9418.ts.net/issues/408), API/concorrência [#409](https://devops-lab.tailaf9418.ts.net/issues/409) e finalização [#410](https://devops-lab.tailaf9418.ts.net/issues/410). Acesso pela API autenticada do Redmine, associação do usuário ao papel dedicado Codex no projeto GenSW. Aceite humano e merge continuam pendentes.

## Base, investigação e modelo

Branch `codex/404-propriedades-operacionais`, criada de `origin/main` no commit `9783be5d5055c6340ffef207ab07400539f329e5`, merge do PR #17. A investigação reutilizou o padrão Domain/Application/Infrastructure/API dos catálogos, paginação/busca autenticada, painéis independentes de Animal, lock transacional de Animal e os componentes de Visualizar e retorno com filtros introduzidos no PR #17.

Propriedade é uma unidade física com nome, localização textual opcional, observação, status e auditoria UTC. Vínculos ficam em tabela própria com início/fim, sem coluna obrigatória nem backfill de Animal. Inativação conserva vínculos e bloqueia novas entradas. Transferência fecha e abre vínculos atomicamente, confere o vínculo esperado dentro da transação e trava o destino contra inativação concorrente. Um índice único parcial impede dois vínculos abertos por Animal.

Datas sem futuro, considerando hoje UTC; intervalos de início inclusivo e fim exclusivo, admitindo movimentações no mesmo dia. Reassociação após encerramento não pode anteceder o último término. Filiação permanece a fonte do pedigree; nenhum serviço ou regra de Cruzamentos, Ciclos, Proles ou Fluxo de caixa foi alterado. As exclusões de escopo estão no [contrato operacional](../operations/propriedades.md).

Migration `20261005132012_AddOperationalProperties`: duas tabelas novas, checks, FKs Restrict e índices; snapshot conferido contra o modelo PostgreSQL. Um teste migra uma base até a versão financeira anterior, grava animais Operacional/Referência e Filiação, aplica a nova migration e compara os dados e todas as colunas preexistentes. Propriedades e vínculos continuam vazios após a migração.

A validação detectou comparação de nomes acentuados dependente do locale PostgreSQL. A correção usa chave interna em maiúsculas invariantes com índice único, mantendo o nome original no DTO público. O teste que encontrou o problema passou na execução final. A promoção de Propriedades também exigiu atualizar a expectativa antiga de módulo planejado na navegação.

## Verificações automatizadas

Backend: **500 aprovados**, zero falhas/ignorados — Domain 116, Application 132, API 138, Infrastructure 114. Execução completa final:

```powershell
dotnet test GenSW.sln --artifacts-path .gensw/properties-404-check-build --logger trx --results-directory .gensw/properties-404/final-test-results --verbosity minimal
dotnet build GenSW.sln --configuration Release --artifacts-path .gensw/properties-404-check-build --verbosity minimal
```

O build Release passou com zero avisos/erros. A cobertura nova inclui:

- API autenticada em todas as rotas, usuário comum, cadastro/edição/status, validação, nome duplicado inclusive acentuado, busca/paginação e recursos ausentes.
- Animal Operacional e Referência criado/editado sem Propriedade; vínculo, transferência, encerramento e reassociação com histórico; pedigree antes/depois idêntico.
- Inativação conservando o vínculo atual; destino inativo, comando desatualizado, datas inválidas, destino atual e ausência de recurso rejeitados sem modificar o histórico.
- Duas requisições simultâneas de primeiro vínculo ou transferência, com barreira de lock observada no PostgreSQL: uma confirma e outra recebe `409 vinculo_desatualizado`; permanece um único atual.
- Falha induzida no insert após fechamento do vínculo: rollback integral; outro leitor continua vendo o vínculo anterior durante a transação e após a falha.
- Índice parcial rejeitando gravação direta de segundo atual; transferência esperando inativação concorrente e rejeitando o destino após seu commit.
- Migration preservando dados/colunas preexistentes e teste de igualdade entre modelo e snapshot.

Os testes PostgreSQL usam instâncias efêmeras e foram executados, sem skips. Logs e TRX locais em `.gensw/properties-404/`; nenhum segredo foi adicionado ao repositório.

Frontend: **408 testes aprovados em 37 arquivos**, ESLint sem warnings e build TypeScript/Vite aprovado. Cobertura nova de contratos, vínculo esperado/conflitos, períodos preservados, ausência de Propriedade, Visualizar sem formulário nem mutação, catálogo além da primeira página, recuperação de falhas, cadastro/edição/status, retorno com busca/página e proteção das quatro rotas de Propriedades. Após ajuste de tipagem no teste das páginas, os cinco testes desse arquivo foram reexecutados e passaram.

```powershell
cd src/Frontend/GenSW.Web
npm test -- --run
npm run lint
npm run build
```

Logs finais: `frontend-final-tests.log`, `frontend-final-lint.log`, `frontend-final-build.log`, `frontend-page-final.log`. `git diff --check` aprovado.

## Navegador real

Chromium integrado, desktop padrão 1280×720 e viewport móvel 390×844. API HTTPS em `localhost:7002`, frontend em `localhost:5174` e PostgreSQL isolado com banco descartável de validação. Cadastro e dados de teste exclusivos desse ambiente; a base operacional existente não recebeu a migration nem os registros de teste.

1. Login de usuário de teste comum; home → Propriedades; cadastro, busca, edição, Visualizar e retorno preservando filtro de busca.
2. Duas unidades físicas cadastradas; campos opcionais ausentes aceitos. Consulta individual recarregada pela URL direta.
3. Animal Operacional inicialmente sem Propriedade: primeiro vínculo, inativação da unidade com vínculo preservado, transferência para a segunda unidade no mesmo dia e dois períodos visíveis.
4. Encerramento do segundo vínculo: Animal permanece sem Propriedade e com ambos os períodos fechados. Visualizar não monta formulários nem botões de movimentação.
5. Animal de Referência consultado sem Propriedade e sem histórico, válido e navegável. Reativação da primeira unidade confirmada pela lista e detalhe.
6. Detalhe de Propriedade e histórico de Animal em tela estreita; sem overflow horizontal do documento. A tabela usa rolagem interna para as colunas, verificadas também horizontalmente.
7. Teclado: Tab do título focado alcança o retorno, com foco visível. Nenhum erro de console durante os fluxos verificados.

Screenshots locais, não versionados, em `output/playwright/properties-404/`: `propriedade-detalhe-desktop.jpg`, `propriedade-detalhe-mobile.jpg`, `animal-transferencia-desktop.jpg`, `animal-historico-mobile.jpg`, `propriedades-lista-desktop.jpg`. As imagens foram inspecionadas. Emulação móvel não substitui aparelho físico ou leitor de tela. Erros de API, concorrência e rollback foram verificados por testes automatizados.

## Entrega e pendências

Commit, confirmação de push, URL do PR e resultado do CI são registrados nas Tarefas do Redmine, evitando referência circular ao hash neste documento. `AGENTS.md`, diretórios locais preexistentes, logs, fixtures de navegador e screenshots ficam fora do commit.

Publicação da migration e das novas versões no ambiente operacional, revisão do PR, aceite humano e merge continuam pendentes. As tarefas são deixadas **Em validação**, sem conclusão antecipada. O procedimento de publicação e rollback está em `docs/operations/propriedades.md`.
