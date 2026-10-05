# Prompt executável futuro — MVP-1 da Evolução #413

**Condição de execução:** este prompt só pode implementar após aprovação humana explícita da especificação e do plano abaixo. A presença deste arquivo, um PR draft ou decisões de recorte não constituem aprovação. Se ainda não houver aceite verificável, apresente os documentos para revisão e pare antes de modificar código funcional/migrations.

Trabalhe no repositório `C:\Users\Marcos\Documents\ChatGPT\GenSW`. Siga integralmente o `AGENTS.md` local, mantendo-o fora do Git. Leia:

- `docs/superpowers/specs/2026-10-05-producao-insumos-produtos-413-design.md`.
- `docs/superpowers/plans/2026-10-05-producao-insumos-produtos-413-implementation.md`.

Use a [Evolução #413](https://devops-lab.tailaf9418.ts.net/issues/413). A Tarefa [#415](https://devops-lab.tailaf9418.ts.net/issues/415) registra o planejamento, não a execução futura; #414 registrou apenas a preparação do prompt de planejamento. Crie Tarefa própria filha da #413 para este prompt autorizado, registre objetivo, escopo, restrições/aceite e coloque Em andamento antes da investigação técnica. Tarefas P01–P05 do plano são propostas: crie somente as que forem efetivamente executadas, com dependências e evidências. Não criar tarefas F01–F06 como se autorizadas.

Autentique Redmine pela variável de ambiente `REDMINE_API_KEY` no cabeçalho `X-Redmine-API-Key`, URL base `https://devops-lab.tailaf9418.ts.net`, projeto gensw/23. Valide leitura antes de qualquer solicitação de login. Não imprimir, gravar ou versionar a chave; não alterar banco Redmine nem papéis globais.

Atualize e inspecione `origin/main`; o planejamento partiu de `37908e22d0ec5110dfd395bc91ac41d6e7a1df2f` com PR #18 integrado. Registre base real e mudanças pertinentes. Verifique status e preserve arquivos locais/untracked alheios. Use branch `codex/<tarefa>-catalogo-formulacao` derivada da main atual, ou continuação adequada explicitamente autorizada; não implementar em branch documental por conveniência.

## Escopo autorizado quando o planejamento for aceito

O usuário decidiu: **primeiro somente catálogo, receitas e simulações**; **adiar vínculo com ovos**, iniciando futura fase de estoque por entradas manuais; **adiar custos**. Implemente somente P01–P05 do plano:

1. Catálogo único de materiais com capacidades cumulativas, classes alimentares/embalagens/consumíveis, categorias, unidades e conversões explícitas/versionadas, status/versão/histórico, API e interface completa.
2. Perfis nutricionais versionados com fonte/método/amostra, data, base/unidade, origem medida/declarada/estimada, zero conhecido/desconhecido/não aplicável e aplicabilidade; componentes prioritários definidos semanticamente. Lote do laudo é referência textual, sem lote físico antecipado.
3. Receitas versionadas, quantidades/percentuais, saídas/rendimento/perdas esperados, etapas/embalagens, escalonamento e dependências acíclicas por versão/saída específica. Sem disparar ordens.
4. Simulações/comparações manuais com motor decimal no backend, BN/MS, energia compatível e kcal/MJ sem troca de modalidade, metas min/max/faixa com origem, contribuições e completude, processamento com hipóteses explícitas ou resultado não calculável. Snapshots imutáveis e idempotência.
5. Validação integrada, migrations aditivas isoladas, preservação dos dados/contratos existentes, documentação, commit/push/PR e evidências.

Não implementar Estoque/Local/Lote operacional/Reserva/Ordem/Movimento/PonteOvos/custos/preços/rateio/solver/compras/vendas/fiscal/rotulagem/WIP. Os capítulos físicos da especificação são orientação futura, não lista de entidades a criar. Não mostrar disponibilidade física, execução, margem ou selo de dieta balanceada. Nenhuma receita gera fato animal, financeiro ou sanitário.

## Forma de trabalhar

Investigue padrões atuais antes de editar: README/arquitetura, Propriedades e catálogos, `shared/details`, `shared/http`, AppRoutes e navigation/modules, autenticação, EF/migrations, Financial para padrões de versão/idempotência/auditoria sem acoplamento, testes PostgreSQL/CI. Preserve Animal, Filiação como fonte do pedigree, Cruzamentos, Ciclos, Proles, ovos individuais, Propriedades e Caixa.

Siga Domain/Application/Infrastructure/API e React features; manter Domain sem EF/ASP.NET, Application por interfaces específicas, novas exceções sem acoplamento a Animal. Use Guid, restrições/índices PostgreSQL, precisão conforme especificação, decimais novos como strings invariantes e validação canônica no backend. Publicados são imutáveis; atualizar gera versão explícita. Não reutilize genericamente o lock de caixa/genealogia; concorrência de publicações e idempotência exige mecanismo próprio proporcional.

Cada vertical entrega domínio, API e UI verificáveis; promova somente rotas reais no menu. Catálogo “Insumos e produtos”; Produção — transformações com Receitas e Formulação/Comparação, sem Ordens no MVP-1. Produção agrícola/animal/genética preservadas como planejadas; não estreitar agricultura ao conceito de mistura. Visualizar separado de Editar, filtro/paginação preservados, seletores pesquisáveis no servidor, estados acessíveis e telas estreitas.

Não invente nutrientes, densidades, massas por unidade, requisitos por espécie, limites de inclusão, retenção ou segurança alimentar. Fontes do planejamento apoiam conceitos/proveniência, não são dados operacionais prontos. Dados reais exigem contexto/fonte e compatibilidade; fixtures sintéticas nunca são seed operacional. Embalagens não entram no denominador nutricional; material incorporado com dado faltante não pode sumir da massa para melhorar completude.

## Verificações obrigatórias

Aplicar a matriz do plano para unidades incompatíveis, faltantes/zero/NA, BN/MS, modalidades energéticas, versões históricas, metas incompatíveis/impossíveis, rendimentos/perdas, mesma identidade em etapas, sub-receita sem dupla contagem e ciclos/limites. Testar código/publicação/idempotência concorrentes e rollback de snapshot/auditoria em PostgreSQL real. Casos físicos de saldo/consumo/dupla confirmação/correção pós-consumo são futuros e não justificam criar estoque agora.

Reproduzir exemplos sintéticos da especificação: F1 60/40 kg → PB 220 g/kg BN, fibra 44 g/kg, energia 10,4 MJ/kg, MS 86 kg; F2 30/70 kg → PB 310, fibra 62, energia 9,2, MS 83 kg. PB ausente de B deve deixar total indeterminado e cobertura 60%, sem meta atingida. Conversões e escalonamento não arredondam silenciosamente.

Execute gates proporcionais por vertical e, ao finalizar: restore/build Release/test da solução; frontend npm ci/test/lint/build; diff --check; migrations em banco novo/cópia **isolados** com prova de preservação; navegador HTTPS/autenticado em desktop/celular e revisão de console/API; regressões existentes. Não aplicar migrations em ambiente compartilhado nem publicar sem autorização específica. Não declarar PASS de teste não executado.

Revise diff, escopo e segredos antes do commit. Commit rastreável, push e PR revisável com validações reais; anexar o PR ao chat pela ferramenta apropriada. CI Backend/Frontend aprovados e pendências explicitadas. Não realizar merge automaticamente. Registrar evidências, base/branch/commit/push/PR e limitações na Tarefa; mover **Em validação** enquanto faltar aceite/revisão/merge manual. Não concluir #415 ou #413 como efeito colateral desta execução.

Se o uso real exigir decisão que altere modelo/escopo, investigue alternativas e formule pergunta material com proposta concreta, continuando trabalho independente; não ampliar para fases F sem autorização. Entregue resultado, riscos restantes e estado de validação sem recomendar dieta/processo de conserva.
