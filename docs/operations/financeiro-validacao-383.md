# Evidências do MVP financeiro — execução #383

Data: 02/10/2026, America/Sao_Paulo. Evolução #376; design aprovado em #377; entregas #378–#382. A aprovação foi registrada pela API antes da investigação; esta execução possui a Tarefa #383. Todas as entregas requerem revisão e aceite humano.

## Git e escopo

Branch `codex/383-financeiro-mvp`, derivada de `codex/377-planejamento-financeiro`. Preserva `9fcf789` (planejamento) e a base integrada `29ea8f5` (main no início da execução).

| Commit | Entrega |
| --- | --- |
| `2a98048` | Base financeira BRL, regras e migration aditiva |
| `45eb3eb` | Caixa auditável, idempotência, versões, fechamento e ajustes atômicos |
| `d168c7c` | Frontend navegável, referências pesquisáveis e prévias |
| `2551ff0` | Precisão explícita, testes de concorrência, rollback e autorização |

Documentação operacional: [financeiro-caixa.md](financeiro-caixa.md). O commit desta documentação e o resultado de push/revisão são registrados no Redmine. Não houve merge nem publicação em produção. `AGENTS.md`, `.gensw/` e evidências locais não são versionados. Arquivos e serviços locais preexistentes foram preservados; #363 não foi alterada.

## Validação automatizada efetivamente executada

| Check | Resultado |
| --- | --- |
| Backend completo, Release | **464 aprovados**, zero falhas/ignorados: Domain 112, Application 124, API 116, Infrastructure 112 |
| Build backend Release | Zero erros e warnings |
| Frontend completo | **326 aprovados**, 31 arquivos |
| ESLint | Aprovado |
| Build frontend TypeScript/Vite | Aprovado |
| `git diff --check` | Aprovado |

Comando final backend, na raiz:

```powershell
dotnet test GenSW.sln --configuration Release --artifacts-path .gensw/financial-build-383 --logger trx --results-directory .gensw/financial-test-results/verified
dotnet build GenSW.sln --configuration Release --artifacts-path .gensw/financial-build-383 --no-restore -v quiet
```

Comandos frontend, em `src/Frontend/GenSW.Web`:

```powershell
npm test -- --run
npm run lint
npm run build
```

`dotnet test GenSW.sln` também passou antes dos últimos casos adicionados (460 testes). A tentativa posterior no diretório Release padrão foi bloqueada por assemblies usados pela API preexistente na porta 7001. O resultado final de 464 usa diretório de artefatos isolado, sem interromper esse serviço. Uma tentativa intermediária de artefatos mais profundos falhou em dois testes preexistentes que localizam arquivos pela profundidade do caminho; o comando final acima mantém a profundidade esperada e passou integralmente.

Logs locais: `.gensw/finance-383/backend-verified-tests.log`, `backend-build.log`, `frontend-final-tests.log`, `frontend-final-lint.log`, `frontend-final-build.log`; TRX em `.gensw/financial-test-results/verified/`.

PostgreSQL **18 real**, com clusters efêmeros do helper existente. As suítes financeiras verificam:

- Migration up em base nova e em base existente, sem reconstruir movimentos financeiros ou alterar Pessoa/históricos dos demais módulos.
- Decimal exato, precisão excessiva inclusive `1.000`, limites individuais/agregados, saldo negativo, abertura explícita e ausência de receita artificial.
- Exemplo 100 + 250 − 80 = 270, transporte mensal, cancelamento auditável, filtros/paginação independentes dos totais e referências inativas preservadas.
- Duas correções com a mesma versão; criação e mudança de mês concorrendo com fechamento; dois fechamentos simultâneos; ajustes e idempotência concorrentes. Não há gravação posterior ao fechamento vencedor.
- Idempotência persistida conserva a resposta original após correção; mesma chave/payload diferente retorna conflito.
- Rollback de lançamento, auditoria, idempotência e snapshot, inclusive falha real injetada por trigger PostgreSQL na auditoria/fechamento; timeout de lock sem gravação parcial.
- JWT, 401/403, configuração e fechamento exclusivos de Admin, ProblemDetails, paginação defensiva, mês fechado sem reabertura e venda sem alteração de status do Animal.
- Frontend: dinheiro exato, envio repetido, retry com a mesma chave, filtros/totais e acesso de usuário comum.

## Verificação visual e funcional real

Chrome via Playwright CLI: desktop **1440×1000** e contexto móvel com toque **360×732**. Imagens foram abertas e inspecionadas. A tabela usa rolagem própria; a página móvel não apresentou overflow horizontal. Labels, foco na prévia e confirmação por Tab/Enter foram exercitados.

Fluxos executados pela interface em banco isolado:

1. Configuração Admin de agosto/2026 com saldo inicial 100; estado inicial vazio.
2. Despesa de ração 80 e receita de venda 250, Animal escolhido pela pesquisa; resumo 170 de resultado e 270 de saldo.
3. Correção da despesa para 90, com motivo; cancelamento posterior, preservando registro e histórico antes/depois em UTC; saldo 350.
4. Fechamento de agosto com conferência 349 e diferença −1 apenas informativa; confirmação explícita de irreversibilidade. Setembro vazio fechado em sequência, abertura/final 350.
5. Ajuste do recebimento original 250 para 200, com motivo e prévia de impacto −50; outubro ficou com abertura 350, receita 200, despesa compensatória 250 e saldo 300. Agosto/setembro continuam com snapshot 350.
6. Nova reversão do mesmo original recebeu 409 e mensagem de conflito, preservando a prévia. Anotação posterior apareceu no histórico sem movimentar saldo.
7. Usuário comum em celular criou despesa 10 com Pessoa selecionada pela pesquisa e depois a cancelou com motivo; saldo voltou de 290 a 300. Não há botão de fechamento nem configuração Admin para esse papel.
8. Pesquisa sem resultados mostrou estado vazio e manteve os totais; categoria criada, renomeada, inativada e reativada pela interface.

Screenshots locais em `output/playwright/financial-383/`:

| Arquivo | Evidência |
| --- | --- |
| `01-desktop-setup.png` | Configuração inicial |
| `02-desktop-balance.png` | Exemplo de saldo 270 |
| `03-desktop-closing-preview.png` | Conferência e fechamento irreversível |
| `04-desktop-adjustment-preview.png` | Prévia da reversão/substituição |
| `05-desktop-conflict.png` | Reversão duplicada bloqueada |
| `06-mobile-form.png` | Formulário e pesquisa em celular |
| `07-mobile-summary.png` | Resumo e lista em celular |
| `08-desktop-final.png` | Resultado final em outubro |

O console registrou 401 esperados na expiração/renovação da sessão e após reinício da API; o interceptor renovou a sessão e as operações foram concluídas. O 409 de reversão duplicada foi exercitado deliberadamente. Não foi observado erro de execução JavaScript nesses fluxos.

## Instância local de homologação

- Aplicação: **https://localhost:5183/financeiro**.
- Swagger: **https://localhost:7083/swagger**; API `/api/v1`.
- PostgreSQL isolado: loopback na porta 55383, em `.gensw/finance-383/pgdata/`. Não é o banco de trabalho preexistente.
- Contas locais: `financeiro.admin` (Admin) e `financeiro.user` (usuário comum). A credencial está protegida pelo Windows DPAPI em `.gensw/finance-383/credential.xml`; nenhum valor de senha é incluído nesta documentação. O helper local `.gensw/finance-383/copiar-senha-homologacao.ps1` copia a senha para a área de transferência do Windows sem imprimi-la. Não versionar esse diretório.
- A API usa o build Release verificado; frontend Vite usa os arquivos finais. Os serviços permanecem executando nas sessões locais da execução para homologação.

## Limitações e aceite pendente

Emulação móvel Chrome não substitui aparelho físico, Safari ou leitor de tela; estes não foram testados. A verificação humana deve revisar interação, regras de negócio e dados da demonstração. Acessibilidade foi conferida por semântica, teclado e contraste visual, sem certificar auditoria WCAG completa. Serialização global das mutações é deliberada para o MVP e não constitui teste de carga.

Revisão do código, CI remoto e aceite humano são registrados com seu resultado real no Redmine/PR. A entrega permanece **Em validação** enquanto faltar aceite, revisão ou merge. Nenhuma dessas pendências autoriza produção.
