# Neck

Utilitário de manutenção periódica para Windows. Ele analisa antes de apagar, trabalha apenas com locais conhecidos e usa as ferramentas nativas do Windows para manutenção do sistema.

O projeto ainda está em fase inicial. Revise o relatório apresentado pelo aplicativo e mantenha backup dos arquivos importantes antes de qualquer manutenção do sistema.

## Proteções adotadas

- Não modifica o Registro para prometer desempenho.
- Não apaga documentos, fotos, downloads, senhas ou dados de navegadores.
- Temporários recentes são preservados; arquivos em uso são ignorados.
- Lixeira só é esvaziada se a opção for marcada explicitamente.
- Drivers são consultados e encaminhados para os canais oficiais da Microsoft, Intel, NVIDIA e HP.
- Toda execução gera um relatório em `Documentos\Neck\Relatorios`.

## Experiência guiada

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
