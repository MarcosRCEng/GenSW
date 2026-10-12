# Navegação A — convenções aprovadas

Continuidade da Evolução [#386](https://devops-lab.tailaf9418.ts.net/issues/386). A pesquisa [#440](https://devops-lab.tailaf9418.ts.net/issues/440) apresentou duas alternativas no [PR documental #23](https://github.com/MarcosRCEng/GenSW/pull/23). Em 11/10/2026, o usuário escolheu **A** com os ajustes abaixo e confirmou expressamente a autorização para implementar. Em 12/10/2026, declarou a entrega da [PR #24](https://github.com/MarcosRCEng/GenSW/pull/24) validada e aceita. A [#441](https://devops-lab.tailaf9418.ts.net/issues/441) registra a preparação; #442–#447 rastreiam estrutura, contexto, adoção, revisão responsiva, gates e entrega. A [#448](https://devops-lab.tailaf9418.ts.net/issues/448) registra o aceite e a preparação da integração. Veja a [validação da implementação](../validation/2026-10-12-navegacao-a-447.md).

## Decisão e organização

- Trilho global de 56 px, módulos de 208 px e painel secundário transitório de aproximadamente 314 px. O painel cobre temporariamente o conteúdo, sem terceira coluna permanente.
- **Ordem alfabética pt-BR** das áreas, dos módulos dentro de cada área e dos destinos dentro do módulo. A hierarquia funcional existente continua reconhecível.
- **Sem seção “Ações” no menu.** O menu oferece páginas de consulta; Criar, Editar, Salvar, prévias e comandos permanecem nas páginas apropriadas, com as permissões e confirmações existentes.
- Planejados continuam sem links, apresentados na home. Não criar novos destinos administrativos ou de negócio.
- Navegação compacta em drawer até 1100 px; nunca três colunas no celular. Menu e painéis modais contêm foco, fecham com Escape e restauram o acionador.
- Tema claro, verde `#176348`, texto `#24362f`, secundário `#53645a`, foco `#287cd0` e superfícies neutras, com densidade operacional única e alvos móveis de 44 px.

## Cabeçalhos, ações e retornos

Cabeçalho compartilha título, contexto, breadcrumb quando há hierarquia real, retorno contextual e ações. Links representam destinos; botões representam ações, abertura/fechamento de painel ou comandos. Início pertence à navegação global, evitando controles duplicados nas páginas.

Listas preservam busca, filtros, ordenação e paginação em `history.state`, reutilizando os helpers existentes. Não mover dados privados para query/hash ou armazenamento do navegador. URL copiada/aba nova usa defaults e relações que já fazem parte da URL.

Detalhe → edição retorna ao detalhe; edição iniciada pela lista mantém a origem da lista. Criação conserva o destino operacional da feature. Sem origem válida, o retorno é determinístico: lista canônica ou detalhe do ID. Subregistros retornam ao pai e, quando aplicável, à seção/página de origem. Perfil nutricional pertence ao Item; versão pertence à Receita. Estoque continua aceitando somente as cinco listas permitidas por `inventoryOrigin`.

A origem de pais encadeados conserva o percurso Cruzamento → Ciclo → Prole sem substituir o ancestral pelo filho. O feedback fixo **Registro salvo.** só aparece no destino interno esperado; mensagens arbitrárias não são transportadas entre páginas.

O Financeiro usa drawer modal para os painéis existentes, mantendo seus callbacks e estado local. Fechar painel conserva mês/filtros/página e restaura foco. Detalhes que já possuem rota continuam páginas com retorno ao caixa. Essa mudança de apresentação não cria previsão, competência ou contas futuras, nem amplia permissões de correção/ajuste/fechamento.

Um formulário alterado protege navegação interna, Voltar do navegador, saída/reload e fechamento local. O diálogo inicialmente foca **Continuar editando**; Escape conserva o rascunho. **Descartar e sair** é explícito. Sucesso libera a navegação; falha conserva campos e proteção. Nada é repetido automaticamente ou persistido como rascunho privado.

## Implementação e limites

O `RouterProvider` com `createBrowserRouter` mantém a árvore `AppRoutes` e as URLs existentes. Essa integração mínima fornece o [bloqueio suportado de navegação](https://api.reactrouter.com/v7/functions/react-router.useBlocker.html), inclusive histórico, sem interceptar/reescrever `pushState`. Um único provedor coordena os formulários e usa `beforeunload` para saída externa/reload; [a API do router](https://reactrouter.com/api/data-routers/createBrowserRouter) não substitui esse aviso nativo.

`TableRegion` conserva informação operacional em regiões horizontais internas. Somente quando existe overflow, acrescenta nome acessível, aviso de rolagem e parada de teclado; sem overflow, evita uma parada adicional. O foco retornado a seções fica abaixo do cabeçalho fixo. Não esconder overflow global como correção de layout. Estados de carregamento, vazio, erro, ausência, permissão, conflito e confirmação conservam a semântica e as regras de cada recurso; não criar simulações desses estados na aplicação real.

Não alterar backend, regras, permissões, URLs, dados ou migrations. F01 permanece integrada e não é reimplementada; F02–F06 ficam fora do escopo. O aceite manual da entrega foi registrado em 12/10/2026; a integração à main depende da autorização específica de merge exigida na instrução original. A entrega deve registrar testes realmente executados, ambiente isolado, branch/commit/push/PR e limitações, sem reutilizar números históricos como evidência atual.
