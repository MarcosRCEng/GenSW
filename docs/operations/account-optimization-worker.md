# Isolamento do solver de otimização

A API valida o vínculo e prepara o modelo numérico na base da conta. O solver recebe esse modelo por entrada padrão e devolve seu resultado por saída padrão. Conta, vínculo, ator, credenciais, conexões PostgreSQL e chaves de arquivos ficam na API. Ela confere o vínculo e a lease antes de publicar o resultado.

## Ensaio da #583

Use o runtime Linux x64 e o Bubblewrap em `/usr/bin/bwrap`. O usuário da API precisa poder criar namespaces de usuário, PID, rede, IPC e UTS no ambiente de ensaio. O Bubblewrap prepara uma raiz própria, descarta capacidades e impede namespaces de usuário adicionais. O processo recebe as bibliotecas de sistema, o runtime .NET e os arquivos próprios do solver por montagens de leitura. O espaço temporário é local ao processo. [Referência do Bubblewrap](https://github.com/containers/bubblewrap/blob/main/bwrap.xml)

Antes de executar o ensaio, confira o pacote e as permissões no host ou contêiner sintético. A imagem atual do DEVOPS-TI precisa dessa preparação para habilitar o solver. Preserve os limites de privilégio do contêiner e instale a ferramenta somente no ambiente autorizado.

1. Use a conta de sistema da API para confirmar que o launcher cria os namespaces com os mesmos parâmetros que `HighsProcessSolver` usa.
2. Execute um cálculo sintético que exija o solver nativo. Confirme a solução, o encerramento do processo e o resultado na base da conta.
3. Confira os mounts e o namespace de rede do processo. Use alvos sintéticos para confirmar a recusa de acesso à rede do PostgreSQL, à raiz privada, à configuração da API e ao arquivo de segredo do ensaio.
4. Interrompa um cálculo em andamento por desativação da conta e confira a barreira no banco. Após a retomada da conta, verifique a quarentena do cálculo anterior e peça um reprocessamento com nova intenção.

Em host incompatível, ou quando o launcher não está instalado, o solver retorna erro de disponibilidade e o processamento segue o contrato de resultado terminal. Uma falha ao preparar o namespace também encerra o cálculo. Os testes de regressão com o executável nativo direto usam exclusivamente dados sintéticos e registram essa seleção na configuração de teste.
