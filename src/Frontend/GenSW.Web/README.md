# GenSW Web

Aplicação React do GenSW. Em desenvolvimento, o frontend e a API usam HTTPS para
preservar o fluxo real do refresh cookie `Secure` e `HttpOnly`.

## Pré-requisitos

- Node.js 20 ou 22+ e npm;
- SDK .NET com suporte a `dotnet dev-certs`;
- certificado HTTPS de desenvolvimento confiável;
- PostgreSQL do GenSW disponível;
- `ConnectionStrings__GenSW` e `Authentication__Jwt__SigningKey` configurados
  fora do repositório para a API.

## Configuração local

Na raiz do repositório, o caminho recomendado prepara tudo sem copiar segredos
para arquivos versionados:

```powershell
.\setup-gensw.bat
.\start-gensw.bat
```

Para a execução manual, instale as dependências e crie a configuração local a
partir do exemplo:

```powershell
cd src/Frontend/GenSW.Web
npm ci
Copy-Item .env.example .env.local
```

O valor de desenvolvimento esperado é:

```dotenv
VITE_API_BASE_URL=https://localhost:7443/api/v1
```

A URL não é um segredo. Somente variáveis prefixadas com `VITE_` ficam
disponíveis no código executado pelo navegador; não coloque credenciais nelas.

## Certificado HTTPS do Vite

O Node.js não lê diretamente o certificado mantido no repositório de
certificados do sistema pelo ASP.NET Core. Confie no certificado .NET e exporte
uma cópia PEM para o diretório local de desenvolvimento do ASP.NET:

```powershell
dotnet dev-certs https --trust

$genswHttpsDir = Join-Path $env:APPDATA 'ASP.NET\https'
New-Item -ItemType Directory -Force -Path $genswHttpsDir | Out-Null
dotnet dev-certs https `
  --export-path (Join-Path $genswHttpsDir 'GenSW.Web.pem') `
  --format Pem `
  --no-password
```

O último comando cria o par `GenSW.Web.pem` e `GenSW.Web.key`. O Vite procura
esse par em `%APPDATA%\ASP.NET\https` no Windows e em `~/.aspnet/https` nos
demais ambientes.

Se o par estiver em outro local, defina ambos os caminhos apenas no
`.env.local`:

```dotenv
GENSW_HTTPS_CERT_PATH=C:/caminho/local/GenSW.Web.pem
GENSW_HTTPS_KEY_PATH=C:/caminho/local/GenSW.Web.key
```

O arquivo `.key` contém uma chave privada sem senha. Mantenha-o fora do
repositório, protegido pelas permissões da conta local e nunca o compartilhe.
As extensões de certificado e chave, assim como `.env.local`, são ignoradas pelo
Git. O Vite encerra com uma instrução clara caso não encontre os dois arquivos;
ele não cria nem exporta chaves automaticamente.

## Execução integrada

Na raiz do repositório, inicie a API no perfil HTTPS:

```powershell
dotnet run --launch-profile https --project src/Backend/GenSW.API
```

Em outro terminal, inicie o frontend:

```powershell
cd src/Frontend/GenSW.Web
npm run dev
```

As origens locais são fixas para manter CORS e cookies previsíveis:

- frontend: `https://localhost:7441`;
- API HTTPS: `https://localhost:7443`;
- API HTTP: `http://localhost:7442`;
- base REST: `https://localhost:7443/api/v1`.

A porta do Vite é estrita: se `7441` estiver ocupada, libere-a em vez de usar
outra porta não configurada no CORS da API.

## Validação

Os testes frontend usam Vitest, jsdom e Testing Library. `npm test` é
deliberadamente não interativo para funcionar em CI:

```powershell
npm test
npm run lint
npm run build
```

Para validar manualmente a sessão, confirme que API e frontend estão nas URLs
acima, acesse a rota de login com um usuário real, recarregue a página para
testar a recuperação da sessão e finalize com **Sair**. O navegador deve confiar
no certificado para aceitar o refresh cookie seguro.

A criação/bootstrap e a administração de usuários pertencem à A5. Enquanto não
houver um usuário real no banco, a validação manual completa do login ficará
pendente; a A4 não adiciona seed ou usuário artificial para contornar essa
dependência.
