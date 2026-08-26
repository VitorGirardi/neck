# Neck

Utilitário de manutenção periódica para Windows. Ele analisa antes de apagar, trabalha apenas com locais conhecidos e usa as ferramentas nativas do Windows para manutenção do sistema.

## Neck Guard (v0.3)

O diagnóstico inteligente observa o uso de memória, o espaço livre no disco do Windows e os grupos de processos que mais consomem RAM. Ele classifica o momento como estável, atenção ou crítico e explica a causa em linguagem simples. Nesta primeira versão o Guard é estritamente somente leitura: nenhum aplicativo é encerrado ou alterado.

## Modo Reunião (v0.4)

Antes de uma apresentação, o Neck verifica RAM, disco, reinicialização pendente, rede, energia e aplicativos pesados. Durante o período escolhido, ele impede que o computador ou a tela entrem em suspensão e pausa as próprias rotinas de manutenção. A proteção é temporária e totalmente reversível.

## Guard contínuo (v0.5)

O monitoramento na bandeja é opcional e mede o computador a cada 30 segundos. O Neck mantém somente as últimas 24 horas em `%LOCALAPPDATA%\Neck`, exibe alertas após pressão persistente — nunca por um pico isolado — e reconhece crescimento anormal do maior consumidor de memória. As notificações podem ser silenciadas por duas horas e são evitadas quando outro aplicativo está em tela cheia.

Nenhuma métrica é enviada pela internet. O histórico contém horário, uso geral de RAM e disco e o nome/consumo agregado do processo mais pesado.

Durante uma manutenção ativa, fechar a janela oferece continuar a tarefa na bandeja. O Neck impede apenas o encerramento completo até a ferramenta do Windows terminar e envia uma notificação com o resultado.

## SOS Neck (v0.6)

O SOS apresenta somente aplicativos com janela visível, ordenados pelo consumo aproximado de memória. Com confirmação explícita, ele pode enviar um pedido normal de fechamento — equivalente ao botão `X` — e aguarda o próprio aplicativo responder. Processos críticos do Windows e encerramento forçado são excluídos por projeto. O SOS também oferece limpeza segura de temporários antigos e acesso ao Gerenciador de Tarefas.

O projeto ainda está em fase inicial. Revise o relatório apresentado pelo aplicativo e mantenha backup dos arquivos importantes antes de qualquer manutenção do sistema.

## Proteções adotadas

- Não modifica o Registro para prometer desempenho.
- Não apaga documentos, fotos, downloads, senhas ou dados de navegadores.
- Temporários recentes são preservados; arquivos em uso são ignorados.
- Lixeira só é esvaziada se a opção for marcada explicitamente.
- Drivers são consultados e encaminhados para os canais oficiais da Microsoft, Intel, NVIDIA e HP.
- Toda execução gera um relatório em `Documentos\Neck\Relatorios`.

## Experiência guiada

- **Neck Guard:** apresenta uma pontuação de saúde e identifica os maiores consumidores de memória.
- **Modo Reunião:** executa uma checagem prévia e mantém computador e tela acordados por 30, 60 ou 120 minutos.
- **Guard contínuo:** pode continuar na bandeja, mantém histórico local de 24 horas e alerta apenas sobre problemas persistentes.
- **SOS Neck:** reúne ações imediatas e confirmadas para aliviar uma sobrecarga sem forçar processos.
- **Limpeza rápida:** remove somente temporários seguros e relatórios antigos.
- **Manutenção completa:** mantém as opções técnicas em uma tela separada e explicada.
- **Drivers e Windows Update:** mostra versões instaladas e abre apenas fontes oficiais.
- **Atividade e histórico:** explica o que está acontecendo e salva os relatórios localmente.

## Frequência sugerida

Uma análise a cada 30 dias é suficiente para a maioria dos computadores. A verificação de integridade pode ser usada quando houver falhas, travamentos ou corrupção do Windows.

## Compilação

Execute `build.ps1` no PowerShell. O resultado é criado em `dist\Neck.exe`. O compilador do .NET Framework 4.8 que acompanha o Windows é utilizado, sem dependências de terceiros.

Para executar os autotestes de análise e gerar uma versão de verificação visual, use `test.ps1`. Os arquivos de teste são criados em `test-output` e não são versionados.

Cada envio e pull request para a branch `main` também executa os autotestes e compila o programa no GitHub Actions. O executável resultante fica disponível como artefato da execução do workflow.
