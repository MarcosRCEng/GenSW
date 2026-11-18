# Protótipos comparáveis — pesquisa #440

Abra [index.html](index.html). O HTML, CSS e JavaScript são independentes do frontend do GenSW e funcionam sem compilação, bibliotecas externas ou APIs. Um servidor HTTP local estático pode facilitar a revisão. Nenhuma porta da aplicação existente deve ser substituída.

Os nomes, identificações, valores, quantidades e perfis são sintéticos. As URLs com hash pertencem somente ao protótipo; não propõem substituir as rotas reais da aplicação. A amostra usa o inventário de módulos de `modules.ts` e os destinos de `AppRoutes.tsx` da main integrada, sem recursos F02–F06. Módulos planejados são textos estáticos. Destinos implementados fora dos cenários demonstrados são identificados como “fora da amostra”.

## Estruturas

- **A — em camadas:** trilho global de 56 px + lista de módulos de 208 px. O painel secundário de até 314 px abre sob demanda e agrupa **Páginas** e **Ações**, cobrindo o conteúdo temporariamente. Não há terceira coluna permanente.
- **B — sidebar única:** navegação de 232 px, organizada pelas áreas reais. O módulo atual expõe destinos locais, complementados por seções e retornos no conteúdo.
- **Até 820 px neste protótipo:** a navegação vira drawer. Em A, a seleção do módulo abre seu painel; em B, o mapa fica no mesmo drawer. A área útil da tabela permanece integral. O seletor superior oferece desktop, 768 px e 390 px. O limiar é demonstrativo: a implementação deve adotar o orçamento de largura útil definido no contrato, testando também aproximadamente 1.100 px.

As alternativas compartilham paleta, dados, densidade, ações, estados e contrato de retorno. Alternar A/B mantém a tela, seus filtros e ordenação. A preferência registrada na comparação é temporária e não autoriza alterações na aplicação real.

## Roteiro de revisão

1. **Início e menus:** abra cada alternativa, explore os módulos e verifique o estado ativo. No celular, abra/feche o drawer e selecione um destino. Os planejados não são links. Os destinos reais fora da amostra não abrem telas fictícias.
2. **Animais:** clique em “Animais e retorno”. A lista começa filtrada por **Matriz / Ativos / página 2 / Nome A–Z**. Abra um animal, entre em **Editar animal**, altere o nome e cancele. Teste **Continuar editando** e **Descartar e continuar**. Edite novamente, salve e retorne à lista. O contexto permanece na URL. Abra a seção **Pesagens**, um subregistro e **Voltar ao animal**. Teste o link de **URL direta**; o subregistro retorna ao pai e este retorna à lista padrão se não houver origem.
3. **Financeiro:** clique em “Caixa e painel”. A amostra começa em outubro/2026, **Efetivos**, todas as categorias. Selecione mês, situação e categoria, aplique e abra um lançamento pelo botão **Abrir painel**. **Corrigir lançamento** abre o editor no painel, limitado à descrição na amostra. Cancelar/salvar volta ao detalhe; **Fechar painel** mantém o caixa filtrado e restaura foco à linha de origem. Teste Escape e alterações não salvas. O link de detalhe direto tem **Voltar ao fluxo de caixa**, pois é navegação de página. O drawer é uma alternativa visual aos painéis inline existentes; o caixa mantém dinheiro efetivo e cancelados, sem previsão, competência, vencimentos ou contas futuras.
4. **Estoque:** clique em “Estoque e comando”. A tabela e o detalhe mantêm **saldo físico**, **utilizável**, **elegibilidade**, **situação** e **validade não informada** separados; ausência de validade não comprova aptidão. Em celular, todas as colunas ficam disponíveis na região de rolagem interna. Abra o lote e volte aos saldos, conservando material/local/ordenação. Abra **Registrar recebimento**, preencha quantidade/data/referência/local e siga para **Revisar prévia**. Volte a **Ajustar dados** e confira a conservação dos campos. Use **Confirmar recebimento**; o diálogo pede uma ação explícita antes do evento sintético. O retorno aos saldos conserva os filtros. O protótipo não modifica o saldo mostrado na tabela nem cria movimentos reais.
5. **Receita e versão:** abra a receita, depois a versão, seu editor e cancele/salve. A edição retorna ao detalhe da versão; o retorno da versão aponta para a receita pai. Teste o acesso direto.
6. **Catálogo e perfil:** abra o item, depois o perfil e seu editor. O detalhe do perfil retorna ao item pai; cancelar/salvar a edição retorna ao detalhe do perfil.
7. **Estados:** no seletor superior, revise carregamento, vazio, erro, sessão expirada, papel insuficiente, conflito e confirmação. São demonstrações visuais. Use a ação de recuperação e verifique que o contexto continua na URL. Em um formulário com alterações, selecione **Conflito de edição** e depois **Manter minha edição**; os campos do rascunho reaparecem. O seletor permanece disponível também em tela física estreita.
8. **Teclado e responsividade:** percorra com Tab, ative links com Enter e use Escape para fechar drawers e diálogos. O foco fica dentro de overlays modais e volta a um controle previsível. Em 390 e 768 px, a tabela tem rolagem interna, todas as colunas e uma orientação; a página não precisa rolar horizontalmente.
9. **Recarga:** recarregue lista/detalhe/painel após aplicar filtros. Os parâmetros são mantidos. Alterações ainda não salvas acionam o aviso nativo antes de sair/recarregar. Dados “salvos” na amostra existem somente em memória e voltam ao conjunto sintético na recarga.

## Semântica e limites

Links levam a páginas, pais e listas. Botões abrem/fecham painéis, aplicam filtros, salvam e confirmam comandos. Não há uso indiscriminado de histórico anterior. Retorno à lista, ao pai, ao caixa e fechamento de painel possuem rótulos diferentes e destinos explícitos.

O protótipo demonstra o contrato, a responsividade, foco visível, links ativos (`aria-current`), expansão, Escape, contenção de foco e proteção de edição. Não certifica conformidade WCAG: leitores de tela, zoom, contraste automatizado e todo o conjunto de rotas reais exigem revisão na implementação futura. Ícones de texto possuem rótulos acessíveis nos controles; na aplicação real, devem usar o repertório de ícones já existente.

O resumo financeiro é sintético e representa apenas efetivos do mês completo, independentemente dos filtros da lista. O painel mostra o registro sintético selecionado. A ação de correção só aparece nos exemplos **efetivos, ordinários e com caixa aberto**; cancelados são somente consulta, inclusive em link direto com `panel=edit`. Na amostra de estoque, somente LOT-DEMO-01 tem detalhe navegável; os demais lotes são identificados como fora da amostra. Composição, nutrientes, cálculos, permissões e regras de domínio não são reproduzidos. Versão e perfil editáveis são identificados como rascunho. O estado de conflito demonstra apresentação e preservação dos campos, sem implementar sincronização. A recuperação do destino após reautenticação é uma dependência futura a validar; o botão demonstrativo não executa login. Os formulários do catálogo/receita demonstram navegação, não todos os campos existentes. Não há upload, fotos privadas, dados operacionais, login real, backend, banco, publicação ou ambiente compartilhado.

## Arquivos

- [index.html](index.html): apresentação, seletores, cenários e aplicação demonstrativa.
- [prototype.css](prototype.css): tokens locais, estruturas A/B, tabelas, formulários, overlays e regras responsivas.
- [prototype.js](prototype.js): dados sintéticos, roteamento por hash, contexto na URL, retorno e interações.

Validações executadas e capturas sintéticas são registradas no relatório da pesquisa; o uso destes arquivos não substitui os testes futuros da aplicação real.
