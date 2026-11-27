# Acesso móvel privado do GenSW no DEVOPS-TI

A #577 usa o pacote de implantação da #576 com PostgreSQL, contas e imagens sintéticos. O desenho segue o runbook do Raspberry do IncubControl-IoT-Hub, com o Tailscale Serve terminando HTTPS, Nginx local servindo a SPA na raiz e API em `/api/v1` na mesma origem. O GenSW continua responsável pela sessão e pelas permissões da aplicação. [Operação do GenSW no DEVOPS-TI](devops-ti.md) · [Runbook do Raspberry](https://github.com/MarcosRCEng/IncubControl-IoT-Hub/blob/bd4a9fd1dae319bf1d9ef65a46c74d29c298e3fb/docs/raspberry-installation.md)

## Conferência antes da publicação

1. O responsável pelo DEVOPS-TI aprova o destino, a coexistência e o ensaio de reversão. O administrador da tailnet aprova o destino, o público e a alteração de política. Registre na #577 os responsáveis, o escopo aprovado e o resultado das verificações. Use a base sintética durante o ensaio.
2. Confira a unidade `gensw-ensaio-576.service` instalada, habilitada e ativa. Use o procedimento da #576 se a instalação ainda precisar ser feita. Compare o SHA da revisão aprovada ao alvo de `current`, a `GENSW_RELEASE`, aos rótulos `org.opencontainers.image.revision` das imagens em uso e à revisão registrada no status da unidade. A revisão própria do ingresso deve incluir a configuração de proxies confiáveis da #577.
3. Confira a identidade `gensw_ensaio_576`, o histórico `__EFMigrationsHistory` e as migrations exigidas pela revisão. Siga o procedimento da #576 para qualquer migration pertinente, com SQL revisado. Confirme as chaves do original e da miniatura no diretório exato montado em `Images__PrivateRoot` e repita o ensaio funcional com o recibo sintético.
4. Antes de instalar a revisão, compare o prefixo da nova rede Docker `proxy`, `172.30.77.0/29`, com as rotas e redes locais, Docker e tailnet. Se houver sobreposição, alinhe um prefixo livre e ajuste juntos o Compose, o Nginx e a configuração da API. A rede `frontend` da #576 pode permanecer sem uso durante a troca da revisão. Se for removê-la, use `gensw-stack compose` da revisão anterior com `down`, antes de ativar a nova revisão, mantendo os volumes e os artefatos das duas revisões.
5. Confira que o alvo `127.0.0.1:3086` entrega prontidão HTTP 200, a SPA e a API da revisão pretendida. Inspecione as escutas IPv4 e IPv6 e a publicação Docker. O Nginx deve publicar apenas em loopback e a API e o PostgreSQL devem ficar sem publicação direta.

```sh
systemctl is-enabled gensw-ensaio-576.service
systemctl is-active gensw-ensaio-576.service
systemctl show gensw-ensaio-576.service -p FragmentPath -p StatusText
curl --noproxy '*' -fsS http://127.0.0.1:3086/api/v1/ready
ss -ltn '( sport = :443 or sport = :8443 or sport = :3086 or sport = :8080 or sport = :5432 )'
tailscale serve status
tailscale funnel status
```

O Nginx recebe o tráfego do Serve pela publicação Docker de loopback e aceita esquema e IP encaminhados do endereço exato do gateway `172.30.77.1`. A API aceita os cabeçalhos encaminhados somente do Nginx em `172.30.77.2`. Confirme em Linux que a conexão via loopback chega ao Nginx por esse gateway e que um par fora dele não consegue influenciar esquema ou IP. O Serve 1.102.2 substitui `X-Forwarded-For` e `X-Forwarded-Proto` pelos valores que observa. Confira que uma requisição HTTPS autorizada chega como HTTPS e com o IP Tailscale observado, inclusive quando o cliente envia outros valores nesses cabeçalhos. Os endereços de cliente Tailscale usam `100.64.0.0/10` em IPv4 e `fd7a:115c:a1e0::/48` em IPv6. A garantia vem do salto confiável e da substituição dos cabeçalhos pelo Serve. Processos locais com acesso ao loopback fazem parte dessa fronteira de confiança. [Implementação do Serve 1.102.2](https://github.com/tailscale/tailscale/blob/v1.102.2/ipn/ipnlocal/serve.go) · [Prefixos reservados pelo Tailscale](https://tailscale.com/docs/reference/reserved-ip-addresses)

## Destino e política da tailnet

O candidato é `https://devops-lab.tailaf9418.ts.net:8443/`, separado do Redmine em TCP 443. Confira a resolução MagicDNS e que TCP 8443 está livre nas escutas do host e nas configurações do Serve e do Funnel. Registre a configuração anterior do Serve e do Funnel e os resultados dos acessos existentes em um local restrito. A #575 confirma o Serve do Redmine em 443 e registra como pendentes a revisão efetiva da política e a conferência da exposição de 3000 por interface. O responsável pelo DEVOPS-TI confirma a linha de base do Redmine e dos demais serviços antes da mudança.

O administrador revisa a união de `grants` e `acls`, inclusive curingas, intervalos de portas, grupos, autogrupos, tags, aliases, faixas IPv4/IPv6 e rotas que incluam o destino. Uma regra mais estreita só acrescenta permissões. Registre testes `accept` para o grupo GenSW e `deny` para identidades representativas sem acesso à TCP 8443, mais regressões para 443 e serviços coexistentes. Confirme que a identidade usada no teste negativo está na tailnet, mas fora do público efetivo dessa porta. [Grants aditivos e testes](https://tailscale.com/docs/features/access-control/grants/grants-explained) · [Sintaxe da política](https://tailscale.com/docs/reference/syntax/policy-file#tests)

O navegador seleciona cookies por host e caminho, independentemente da porta. O responsável pela coexistência deve conferir o escopo dos cookies nos dois serviços. Com o mesmo nome DNS, um cookie do Redmine com `Path=/` pode acompanhar requisições ao GenSW. O proxy da #577 envia à API apenas `GenSW.RefreshSession`, cujo caminho é `/api/v1/auth`. Mantenha os logs sem o valor do cabeçalho `Cookie`. Se a política não isolar o par destino/porta, ou se o navegador precisar separar cookies por host, valide com o administrador uma identidade DNS Tailscale dedicada, como um nó próprio ou `svc:gensw` em um host com identidade por tag e anúncio aprovado. Revise o grant e os testes com o destino escolhido. Preserve a identidade do nó do Redmine. [Tailscale Services](https://tailscale.com/docs/features/tailscale-services) · [RFC 6265](https://www.rfc-editor.org/rfc/rfc6265#section-8.5)

## Publicação do candidato

Na janela aprovada, aplique primeiro o grant restrito à TCP 8443 e valide os testes de política. Com o proxy e a revisão do GenSW confirmados, o operador autorizado aplica somente o novo mapeamento.

```sh
sudo tailscale serve --bg --https=8443 http://127.0.0.1:3086
tailscale serve status
tailscale serve status --json
tailscale funnel status
```

Confira `/ → http://127.0.0.1:3086` em TCP 8443 com acesso pela tailnet, a entrada anterior do Redmine em 443 e o Funnel inativo. De um cliente autorizado use a URL exata abaixo, com verificação normal de TLS. Repita a prontidão após encerrar a sessão administrativa e, em janela aprovada, após reinício do host. [CLI do Tailscale Serve](https://tailscale.com/docs/reference/tailscale-cli/serve)

```sh
curl --noproxy '*' --fail --show-error --silent --max-time 10 \
  https://devops-lab.tailaf9418.ts.net:8443/api/v1/ready
```

## Aceite no celular

| Cenário | Resultado esperado |
| --- | --- |
| Dados móveis, Wi-Fi desligado, Tailscale conectado com identidade autorizada | A URL final abre com certificado aceito, a tela de login aparece e as chamadas da SPA usam `/api/v1` na mesma origem. |
| Login e renovação | Login e `POST /api/v1/auth/refresh` retornam 200. A renovação troca `GenSW.RefreshSession`; confira `Secure`, `HttpOnly`, `SameSite=Lax`, caminho `/api/v1/auth` e origem HTTPS, sem registrar valores de tokens. |
| Rota interna | Abra a ficha sintética em `/animais/<id>` e recarregue a URL diretamente. A SPA volta à mesma ficha com a sessão renovada. |
| Imagens privadas | Abra a ficha e inspecione miniatura e original pela interface. Ambos renderizam e retornam 200 com `Content-Type: image/png` e `Cache-Control: no-store`. Compare com o recibo do cadastro sintético. Sem sessão, os dois endpoints retornam 401. |
| Atalho | Adicione a URL final à tela inicial pelo navegador e abra pelo ícone após fechar o navegador. Confira que a URL abre no celular e que a sessão segue as mesmas regras da navegação. Confirme com Marcos se o atalho satisfaz o uso esperado. |
| Logout | `POST /api/v1/auth/logout` retorna 204 e apaga o cookie. Recarregar a ficha ou abrir o atalho exige login. Renovação e acesso a recursos privados sem sessão retornam 401. |
| Tailscale desligado ou identidade da tailnet fora do grant | A origem do GenSW fica inacessível em cada cenário. Teste em aba nova, sem usar conteúdo já carregado. Confirme do mesmo celular que a rede móvel segue disponível. |
| Identidade Tailscale autorizada sem sessão ou sem papel no GenSW | A tela de login abre e o endpoint protegido retorna 401 sem sessão. Em sessão sintética com papel sem acesso administrativo, verifique 403 num endpoint que exige Admin, como `GET /api/v1/producao/ponte-ovos/conciliacao` quando esse módulo estiver habilitado. |
| Coexistência e portas internas | Repita o acesso ao Redmine pela origem 443 e os testes de acesso dos demais serviços conforme a linha de base. Confirme que 3086, a porta interna da API e 5432 ficam inacessíveis por conexão direta da tailnet e das demais interfaces externas aplicáveis. |

Se Marcos precisar de PWA instalável, confirme instalação e abertura no dispositivo-alvo. O service worker deve guardar somente os recursos públicos estáticos necessários. Exclua respostas de `/api/v1`, dados de sessão, imagens autenticadas e blobs privados. Confira o Cache Storage e tente reabrir a ficha ou a galeria offline, após logout e em outro perfil.

## Reversão do ingresso

Em janela aprovada, retire apenas o mapeamento da raiz HTTPS do GenSW em TCP 8443. Use a mesma porta e o mesmo modo aplicados na publicação. Para um destino dedicado, reverta somente o mapeamento e o anúncio do serviço aprovado conforme o procedimento desse destino. Preserve o Serve geral, a porta 443, o Funnel inativo e a unidade GenSW.

```sh
sudo tailscale serve --bg --https=8443 off
tailscale serve status
tailscale serve status --json
tailscale funnel status
curl --noproxy '*' -fsS http://127.0.0.1:3086/api/v1/ready
```

De um cliente anteriormente autorizado, confirme que 8443 deixou de responder e que o Redmine em 443 e os outros serviços mantêm os resultados anteriores. Compare o Serve e o Funnel com a linha de base. O administrador reverte apenas o grant e os testes introduzidos para o GenSW e revalida a política efetiva. Se o ensaio for bem-sucedido e a publicação continuar aprovada, reaplique o grant e o mapeamento e repita os testes de acesso. [Comportamento do desligamento por porta](https://tailscale.com/docs/reference/tailscale-cli/serve#disable-tailscale-serve) · [Testes do CLI 1.102.2](https://github.com/tailscale/tailscale/blob/v1.102.2/cmd/tailscale/cli/serve_v2_test.go)

Registre na #577 a revisão e os identificadores dos artefatos ativos, endpoint aplicado, aprovações, resultados da política, navegador e rede usados, verificação de reversão, branch, commit, push e pendências de aceite. Inclua apenas evidências sintéticas e omita valores de cookies, tokens e detalhes privados de conexão.
