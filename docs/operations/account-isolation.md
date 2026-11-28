# Isolamento de contas no ERP

A sessão ERP contém o SubjectId central, o ID da sessão, a conta, o vínculo, a revisão e a geração de acesso. A API confere esses valores no catálogo, verifica o destino e fixa o ator local antes de criar o contexto ERP. O JWT de plataforma tem uma audiência diferente e usa exclusivamente os papéis do catálogo. A política de gestão da plataforma protege `GET /api/v1/platform/accounts/{id}/diagnostics`. A rota `/api/v1/ready` mede apenas a prontidão do processo e do catálogo.

## Preparação de destinos

- Forneça `ConnectionStrings__GenSWCatalog` com a credencial de runtime do catálogo. Resolva as referências de cada vínculo em `AccountRuntime__<referência>`, `AccountMaintenance__<referência>` e `AccountVolumes__<referência>`, a partir do provedor externo de configuração. Forneça a chave de assinatura pelo mesmo mecanismo de segredos. Monte em `DataProtection__KeyRingDirectory` o anel de chaves gerenciado externamente, com acesso restrito e proteção no armazenamento. A API usa esse anel em leitura e os testes usam chaves efêmeras em memória.
- Use um papel PostgreSQL exclusivo de runtime por base. Conceda CONNECT, USAGE e DML apenas no próprio destino. A verificação do runtime recusa papéis com CONNECT a qualquer outra base acessível do cluster. Revogue concessões herdadas de PUBLIC, inclusive em bases administrativas e templates que aceitam conexões, no cluster autorizado. Conceda SELECT em `__EFMigrationsHistory` e EXECUTE em `gensw_scope.identity()` e `gensw_scope.verify_generation()`. Restrinja DML às tabelas de negócio necessárias e preserve as projeções de autoria. O papel do runtime não tem propriedade do esquema nem CREATE.
- Use um papel independente para a barreira. Ele precisa de CONNECT ao seu destino, USAGE em `gensw_scope` e EXECUTE em `gensw_scope.advance_fence(uuid, uuid, uuid, bigint, bigint, boolean)`. O papel de migração que possui os objetos e o de backup têm credenciais próprias. O runtime ERP e o papel da barreira não precisam de DDL.
- A raiz privada é um volume independente e contém diretamente as chaves PNG e o arquivo `.gensw-volume-id` com o UUID do volume no formato D. Proíba links e junções no caminho e mantenha o marcador protegido contra troca. O sistema de arquivos deve honrar bloqueios compartilhados e exclusivos de arquivo entre as instâncias que servem o mesmo volume. Valide isso no ambiente da #583.
- A tabela `gensw_scope.destination` recebe uma única linha com os IDs verificados da base, conta e vínculo, a revisão e a geração. A verificação exige o histórico EF exato da versão da API, a projeção local ativa e a correspondência dos marcadores. Depois de restaurar ou adotar dados, prepare o marcador e a nova geração sob autorização antes de publicar o destino.
- A revisão atual expõe conteúdo PNG pelas rotas autenticadas de animais e variedades. Ao integrar uma futura exportação, execute a consulta no escopo ERP fixado, armazene o artefato na raiz privada do vínculo, valide a propriedade e a permissão no download e mantenha a proteção de geração durante toda a entrega. O controlador de imagens demonstra o uso de `AccountFileAccess` para esse limite.

## Migrations

Gere SQL separado para os contextos. Os comandos abaixo partem da raiz do repositório e servem para revisão.

```powershell
dotnet tool restore
dotnet tool run dotnet-ef migrations script --idempotent --project src/Backend/GenSW.Infrastructure --startup-project src/Backend/GenSW.API --context CatalogDbContext --output catalogo.sql
dotnet tool run dotnet-ef migrations script 20261112205407_AddFormulationOptimization --idempotent --project src/Backend/GenSW.Infrastructure --startup-project src/Backend/GenSW.API --context GenSWDbContext --output erp-isolamento.sql
```

Para bases novas, gere o SQL ERP desde a versão inicial. Antes de aplicar qualquer revisão, confirme o destino, o papel, o histórico `__EFMigrationsHistory`, a versão-alvo e a autorização para a operação. A migration de isolamento remove verificadores e sessões antigas do ERP enquanto preserva UUIDs, papéis locais e FKs. A recuperação dessa transição exige backup autorizado com validação da identidade e da geração na restauração.

Para atualizar um destino marcado e já publicado, o host de manutenção usa `ErpSchemaMaintenance` com a conexão separada do migrador e a aprovação de intervalo e SHA-256 obtida por `CreateReviewSql` e `Fingerprint`. O método bloqueia apenas o vínculo escolhido, revoga as sessões, drena a geração anterior e toma um lock exclusivo por destino. Ele confere se o histórico é um prefixo válido no intervalo aprovado. Retentativas começam no último passo aplicado. Uma falha mantém o vínculo em manutenção com categoria sanitizada no diagnóstico. O serviço reabre somente após confirmar histórico, guardas, marcadores e ator no destino.

## Integração da #582 e ensaio da #583

| Operação | Interface |
| --- | --- |
| Ativar após confirmação por código e provisionamento | Reserve conta e destino no catálogo como pendentes. Depois de verificar exclusividade, esquema, identidade, volume e ator, prepare o marcador para a geração de publicação e chame `AccountLifecycle.ActivateAsync` com conta, vínculo, revisão e geração. Na reativação, prepare uma geração seguinte e emita sessões novas. |
| Redefinir credencial central | Na transação que valida e consome o código da redefinição, chame `CatalogCredentialRevocation.LockAndRevokeSessionsAsync` antes de alterar a senha. Conclua o consumo, alteração e revogação juntos. |
| Desativar e reconciliar | Chame `AccountLifecycle.DeactivateAsync` sob autorização de plataforma. `Pending` mantém novas admissões bloqueadas e permite repetir a mesma operação. `Inactive` confirma o bloqueio central e a barreira no banco e no volume. |
| Verificar os dois destinos | Execute `AccountIsolationApiTests` e `AccountJobIsolationTests` com PostgreSQL efêmero. Para validar a UI, use a opção de manifesto de `AccountIsolationApiTests`, credenciais sintéticas e dois perfis com os mesmos IDs de animais e imagens. O script de teste encerra o Kestrel quando recebe o sinal de conclusão. |

O processo de otimização recebe somente o modelo numérico. A integração da #583 deve exercitar a execução em Linux com o isolamento de processo e de rede do solver instalado, além de validar os papéis do PostgreSQL, os bloqueios de volume e a rotatividade dos segredos no host de ensaio. A adoção e o corte da base existente seguem nas #584 e #585.
