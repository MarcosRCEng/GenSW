# Operação do GenSW no DEVOPS-TI

O ensaio da #576 usa o projeto Compose `gensw-ensaio-576` no DEVOPS-TI. O systemd acompanha o estado do host. O Nginx serve a SPA e encaminha `/api/v1` em `http://127.0.0.1:3086`. O PostgreSQL e a API usam redes Docker sem publicação direta de portas. A #577 descreve o procedimento de [acesso móvel privado](devops-ti-mobile.md).

## Preparar a revisão

1. Parta de um commit aprovado e obtenha o SHA completo. Exporte uma cópia do código com finais de linha Linux. O pacote de implantação pode ser criado com `git -c core.autocrlf=false archive -o gensw-release.tar.gz HEAD .dockerignore deploy/devops-ti src/Backend src/Frontend/GenSW.Web`. Confira o SHA-256 no envio. Extraia em `/srv/apps/gensw-ensaio-576/releases/<sha>`.
2. Confirme a montagem `/srv/dados` e o UUID esperado no fstab. Provisione os três diretórios da base sintética em `/srv/dados/gensw-ensaio-576`. O PostgreSQL inicializa `postgres` no bind de `/var/lib/postgresql`. A API precisa criar, renomear e remover arquivos em `images` e persistir suas chaves ASP.NET em `data-protection`. Use UID/GID `1654:1654` nos dois diretórios da API, com permissão `0700`. A raiz de persistência precisa ser acessível à conta que executa o Compose.
3. Use `compose.env.example` para criar `stack.env` no diretório da aplicação. Preencha o SHA, o UUID e as referências absolutas aos arquivos de configuração externos. Gere senhas e a chave de assinatura por fonte criptográfica. Grave a mesma senha no arquivo do PostgreSQL e na configuração privada da API. Restrinja os arquivos ao operador e à conta de serviço. Para a imagem do PostgreSQL 18, carregue a variante Debian e registre a referência por digest em `GENSW_POSTGRES_IMAGE`. O bind da API aponta para `/var/lib/gensw/images`, que deve conter diretamente os arquivos referenciados pelos registros.
4. Aponte `current` para `releases/<sha>`. A partir do diretório da aplicação, execute:

```sh
stack=current/deploy/devops-ti/systemd/gensw-stack
conf="$PWD/stack.env"
sh "$stack" check "$conf"
sh "$stack" compose "$conf" build api web
sh "$stack" compose "$conf" up --detach --no-build --pull never --wait db
```

5. Confira na instância dedicada o nome `gensw_ensaio_576`, a versão PostgreSQL, o volume e a tabela `__EFMigrationsHistory`. Gere o SQL da mesma revisão no ambiente de build, com o CLI EF da versão indicada em `.config/dotnet-tools.json`.

```sh
dotnet tool restore
ConnectionStrings__GenSW='Host=127.0.0.1;Port=1;Database=gensw_ensaio_576;Username=gensw' \
Authentication__Jwt__SigningKey='chave-ficticia-apenas-para-o-cli-ef' \
dotnet ef migrations script --idempotent --context GenSWDbContext \
  --project src/Backend/GenSW.Infrastructure \
  --startup-project src/Backend/GenSW.API \
  --output migrations.idempotent.sql
```

Revise as operações do SQL e confira seu hash após a transferência. Aplique somente à base de ensaio confirmada. Volte a consultar o histórico após a execução.

```sh
sh "$stack" compose "$conf" exec -T db psql -X -v ON_ERROR_STOP=1 \
  -U gensw -d gensw_ensaio_576 < migrations.idempotent.sql
```

Em uma base vazia, crie o administrador sintético com a ferramenta existente, depois das migrations. Forneça `InitialAdminBootstrap__Name`, `InitialAdminBootstrap__Username` e `InitialAdminBootstrap__Password` em um arquivo privado temporário. Remova o arquivo após o uso.

```sh
sh "$stack" compose "$conf" run --rm --no-deps \
  --env-from-file "$BOOTSTRAP_ENV" --entrypoint dotnet api /app/admin/GenSW.AdminBootstrap.dll
sh "$stack" stop "$conf"
```

## Instalar e operar a unidade de sistema

No DEVOPS-TI, um administrador instala a unidade e habilita o boot. Antes de habilitar, pare qualquer execução manual ou unidade de usuário do mesmo projeto. A conta de serviço tem acesso ao Docker para administrar os contêineres GenSW e usa um shell de login desabilitado. Conceda ao grupo dela leitura de `stack.env` e dos dois arquivos externos e acesso de travessia à pasta de configuração. Preserve as permissões dos demais arquivos.

```sh
cd /srv/apps/gensw-ensaio-576
getent passwd gensw-ensaio >/dev/null ||
  sudo useradd --system --user-group --no-create-home --shell /usr/sbin/nologin gensw-ensaio
# Para a configuração no subdiretório config da aplicação
sudo chgrp gensw-ensaio stack.env config config/api.env config/postgres-password
chmod 0750 config
chmod 0640 stack.env config/api.env config/postgres-password
sudo install -m 0644 current/deploy/devops-ti/systemd/gensw-ensaio-576.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now gensw-ensaio-576.service
systemctl is-enabled gensw-ensaio-576.service
systemctl status gensw-ensaio-576.service
```

Use `sudo systemctl start gensw-ensaio-576.service` e `sudo systemctl stop gensw-ensaio-576.service` para operar somente essa pilha. A unidade aguarda a rede, o Docker e a montagem. O script confere o UUID e os diretórios antes de iniciar o Compose. O banco aguarda o próprio healthcheck, depois a API verifica migrations, catálogo e escrita no volume privado. O Nginx inicia após a API pronta. O systemd passa para `active` após a resposta de prontidão pela origem web. Em caso de falha, consulta novamente as dependências ao reiniciar após 20 segundos.

Consulte `journalctl -u gensw-ensaio-576.service -b` para eventos do ciclo de vida. Para detalhes dos contêineres, use `sh "$stack" compose "$conf" ps` e `sh "$stack" compose "$conf" logs --tail 100 db api web`. Os logs têm rotação por contêiner. Use `curl -fsS http://127.0.0.1:3086/api/v1/ready` para conferir a prontidão atual. Evite exibir a configuração resolvida do Compose ou o ambiente dos processos, que podem conter segredos.

A parada do Compose ocorre na ordem web, API e PostgreSQL. A API recebe tempo para finalizar trabalhos. O mecanismo existente de recuperação de otimização conserva o recibo e registra como interrompidas as execuções pendentes cujo prazo expirou. Os binds e as imagens de contêiner permanecem disponíveis para retomada. Reinicie o servidor inteiro somente em uma janela coordenada com o Redmine e as rotinas de backup. Pare a unidade GenSW antes de uma manutenção do Docker que exija manter o daemon desligado. A #578 trata os agendamentos e a recuperação dos backups.

## Atualizar ou reverter

1. Prepare e teste a nova revisão em ambiente isolado. Retenha a pasta e as imagens Docker da revisão anterior, assim como a referência exata do PostgreSQL. Registre IDs dos artefatos e o histórico de migrations. Antes de mudanças de esquema, guarde um conjunto validado do banco, das imagens privadas e das chaves do mesmo ponto de parada.
2. Pare a unidade GenSW e confirme seus três contêineres parados. Troque o alvo de `current` e `GENSW_RELEASE` em `stack.env` enquanto a unidade estiver parada. Instale novamente e recarregue a unidade se o arquivo de serviço tiver mudado.
3. Se a revisão exige migrations, inicie só o banco e aplique o SQL previamente revisado no ambiente autorizado. Inicie a unidade e confirme prontidão, login, leitura e conteúdo das imagens do ensaio.
4. Se precisar reverter, pare a unidade e volte o symlink e o SHA de `stack.env` para a revisão anterior. Reutilize os binds quando o esquema continuar compatível. Quando o esquema for incompatível, restaure o conjunto anterior em diretórios dedicados segundo o procedimento aprovado de banco e imagens. Preserve o conjunto pós-falha para inspeção. Inicie e repita as verificações. Para restaurar, prepare e confira o conjunto isoladamente antes de promovê-lo à raiz esperada pela unidade. A primeira publicação permite reverter ao estado de serviço ausente por `sudo systemctl disable --now gensw-ensaio-576.service`, preservando a persistência do ensaio.

## Repetir o ensaio

Acesse a origem de loopback via encaminhamento SSH autorizado. Execute o script `deploy/devops-ti/validation/smoke.py` com Python 3 e uma conta sintética. O comando `create` pede confirmação da identidade do banco e gera um recibo com os IDs e hashes dos PNGs. A opção `--solver` executa o HiGHS Linux. Depois de parar e retomar, use `verify` com o mesmo recibo.

```sh
python3 deploy/devops-ti/validation/smoke.py create --username "$TEST_USER" \
  --receipt "$TEST_RECEIPT" --synthetic-db-confirmed --solver
python3 deploy/devops-ti/validation/smoke.py verify --username "$TEST_USER" \
  --receipt "$TEST_RECEIPT"
```

Abra o cadastro pela SPA, confira a miniatura e o original e compare os hashes do recibo aos dois arquivos na raiz privada, pelas chaves registradas no banco. Registre somente resultados e identificadores sintéticos.

Para ensaiar o atraso, use uma cópia de teste do Compose com atraso no processo do PostgreSQL, mantendo sua checagem de saúde. Observe a API e o Nginx aguardarem o banco e confira a prontidão pela origem web. No reboot autorizado, verifique a nova sessão de boot, o início do serviço antes de uma sessão de login, a prontidão e o mesmo recibo. Confira também a disponibilidade do Redmine após a janela.
