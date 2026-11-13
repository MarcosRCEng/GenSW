# Distribuição do HiGHS na F05

O worker .NET 8 usa o pacote oficial `Highs.Native` 1.15.1, com versão e hash de conteúdo fixados no `packages.lock.json`. `dotnet nuget verify --all` validou a assinatura de repositório NuGet.org do pacote. O código nativo é carregado por caminho absoluto e conferido por SHA-256, versão `1.15.1` e largura `HighsInt` de 4 bytes. A aplicação aceita Windows e Linux x64. [Pacote NuGet](https://www.nuget.org/packages/Highs.Native/1.15.1), [release](https://github.com/ERGO-Code/HiGHS/releases/tag/v1.15.1), [assinaturas C](https://github.com/ERGO-Code/HiGHS/blob/v1.15.1/highs/interfaces/highs_c_api.h).

| Artefato do pacote | SHA-256 |
| --- | --- |
| `highs.native.1.15.1.nupkg` | `E46ECB150654B9AECDAD12CBB42275DD8567552B9F76497EF688A8940FD01163` |
| `runtimes/win-x64/native/highs.dll` | `43ddf43bbbcc18e024731f17db3369c379e2fa6f1cf61aa7e2e2f3fd8bb03058` |
| `runtimes/linux-x64/native/highs.so` | `640eb397c0b106e3c0cbe37175eee6c76048a3f873827e187e9610a0c2a17912` |

O mantenedor monta o pacote a partir de builds próprios para cada plataforma. A tag compila o artefato Linux x64 em contêiner Debian 11 e usa runtime MSVC estático no Windows. A inspeção ELF do `.so` encontrou x86-64 e dependências do sistema `libdl`, `libpthread`, `libstdc++`, `libm`, `libgcc_s`, `libc` e o carregador `ld-linux-x86-64`. A presença do arquivo no pacote é conferida separadamente da execução em CI. [Workflow do mantenedor](https://github.com/ERGO-Code/HiGHS/blob/v1.15.1/.github/workflows/build-nuget-package.yml).

O núcleo tem licença MIT. Os textos de licença e avisos desta pasta vêm da mesma tag e seguem nos artefatos de build e publicação. A configuração padrão do upstream mantém HiPO desativado e a F05 usa simplex. A licença de pdqsort e a de zstr acompanham a distribuição. [Licença](https://github.com/ERGO-Code/HiGHS/blob/v1.15.1/LICENSE.txt), [avisos](https://github.com/ERGO-Code/HiGHS/blob/v1.15.1/THIRD_PARTY_NOTICES.md), [configuração de build](https://github.com/ERGO-Code/HiGHS/blob/v1.15.1/CMakeLists.txt).
