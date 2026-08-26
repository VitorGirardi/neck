<p align="center">
  <img src="assets/neck-icon.svg" width="112" alt="Ícone do Neck">
</p>

<h1 align="center">Neck</h1>

<p align="center">
  <strong>Cuide do Windows sem complicação.</strong><br>
  Diagnóstico claro, manutenção segura e prioridades personalizadas para computadores lentos ou sobrecarregados.
</p>

<p align="center">
  <a href="https://github.com/VitorGirardi/neck/actions/workflows/build.yml"><img src="https://github.com/VitorGirardi/neck/actions/workflows/build.yml/badge.svg" alt="Build"></a>
  <a href="https://github.com/VitorGirardi/neck/releases/latest"><img src="https://img.shields.io/github/v/release/VitorGirardi/neck?display_name=tag&sort=semver" alt="Última versão"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows&logoColor=white" alt="Windows 10 e 11">
  <img src="https://img.shields.io/badge/.NET%20Framework-4.8-512BD4" alt=".NET Framework 4.8">
</p>

<p align="center">
  <a href="https://github.com/VitorGirardi/neck/releases/latest"><strong>Baixar a versão mais recente</strong></a>
  ·
  <a href="#instalação">Como instalar</a>
  ·
  <a href="#segurança-por-padrão">Proteções</a>
</p>

![Tela principal do Neck](assets/screenshots/neck-dashboard.png)

## Por que o Neck existe?

Quando um computador começa a travar, é comum encontrar ferramentas que prometem “liberar RAM”, aplicam alterações obscuras no Registro ou apagam arquivos sem explicar o impacto. O Neck segue outro caminho: **mede primeiro, explica a causa e mantém a decisão com você**.

Ele foi criado para quem quer responder três perguntas simples:

1. O que está deixando meu computador lento agora?
2. O que posso fazer sem arriscar meus arquivos ou o Windows?
3. Qual ação devo executar primeiro?

## Principais recursos

| Recurso | O que faz | Proteção principal |
| --- | --- | --- |
| **Meu Plano Neck** | Cruza RAM, disco, temporários, inicialização e Windows Update para escolher três prioridades. | Não executa nenhuma ação automaticamente. |
| **Neck Guard** | Mostra a saúde do computador, CPU, RAM e os maiores consumidores de memória. | Diagnóstico somente leitura. |
| **Guard contínuo** | Monitora a cada 30 segundos e alerta apenas sobre pressão persistente de CPU ou RAM. | Histórico local limitado às últimas 24 horas. |
| **Neck Turbo** | Prioriza temporariamente a família do aplicativo que está em foco. | Usa somente prioridade Acima do normal e restaura tudo automaticamente. |
| **Neck Adaptive + RAM Park** | Otimiza a família completa de um aplicativo e retira páginas ociosas da RAM física. | Mantém o aplicativo aberto e recarrega dados sob demanda. |
| **SOS Neck** | Lista aplicativos visíveis que podem aliviar uma sobrecarga. | Solicita fechamento normal; nunca força processos. |
| **Neck Boot** | Explica o que inicia com o Windows e quais itens opcionais merecem revisão. | Encaminha mudanças para a tela oficial do Windows. |
| **Modo Reunião** | Verifica o computador e impede suspensão durante uma apresentação. | Temporário, reversível e sem manutenção concorrente. |
| **Limpeza segura** | Remove temporários antigos e relatórios de erro conhecidos. | Preserva arquivos recentes, documentos, downloads e navegadores. |
| **Manutenção completa** | Reúne DISM, SFC e otimização da unidade. | Solicita administrador somente para a tarefa escolhida. |
| **Drivers e atualizações** | Mostra versões instaladas e abre fontes oficiais. | Não instala drivers silenciosamente. |

## Meu Plano Neck

Em vez de apresentar dezenas de métricas, o plano personalizado transforma o diagnóstico em três próximos passos. Cada recomendação informa o motivo, a urgência e o destino da ação.

![Meu Plano Neck com três prioridades](assets/screenshots/neck-plan.png)

O plano pode encaminhar para SOS Neck, limpeza segura, Neck Boot, diagnóstico ou Windows Update. Confirmações e proteções continuam valendo em todas as etapas.

## Neck Adaptive

Quando um aplicativo pesado precisa continuar aberto — como Claude, Chrome, Teams ou um editor — o Neck Adaptive reduz seu impacto enquanto ele está em segundo plano, sem encerrar janelas ou perder trabalho. A análise considera a **família inteira de processos**, inclusive filhos com nomes diferentes como `chrome`, `node` ou `msedgewebview2`.

1. Abra o **SOS Neck**.
2. Selecione o aplicativo.
3. Clique em **Ativar Adaptive** e confirme.
4. Para desfazer, selecione-o novamente e clique em **Desativar Adaptive**.

O estado muda sozinho conforme o uso:

- **Em uso:** CPU, memória e energia permanecem no estado original.
- **Aguardando:** após você trocar de janela, o Neck espera 15 segundos para evitar mudanças durante um Alt+Tab rápido.
- **Otimizado:** aplica prioridade de CPU abaixo do normal, EcoQoS, baixa prioridade de memória e RAM Park a todos os processos da família.
- Ao retornar à janela, o Neck restaura os três controles em até 2 segundos.

O **RAM Park** pede ao Windows para retirar o máximo possível de páginas ociosas do working set — a parte do aplicativo que está residente na RAM física. O Neck mede a memória antes e depois e informa uma estimativa liberada. Subprocessos novos também são detectados. Cada valor anterior é preservado individualmente e todos os ajustes restantes são restaurados ao encerrar o Neck.

A implementação utiliza as APIs documentadas [`SetProcessInformation`](https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-setprocessinformation), [`MEMORY_PRIORITY_INFORMATION`](https://learn.microsoft.com/windows/win32/api/processthreadsapi/ns-processthreadsapi-memory_priority_information) e [`EmptyWorkingSet`](https://learn.microsoft.com/windows/win32/api/psapi/nf-psapi-emptyworkingset) do Windows.

![Neck Adaptive no SOS Neck](assets/screenshots/neck-sos.png)

> [!NOTE]
> O RAM Park reduz memória física residente, não a memória privada comprometida pelo aplicativo. Nenhum estado é apagado, mas o primeiro retorno pode ficar mais lento enquanto o Windows recarrega páginas da memória standby, de arquivos ou do pagefile.

## Neck Turbo

O Neck Turbo melhora a resposta do aplicativo que não pode travar — uma apresentação, navegador, editor ou ferramenta de IA — quando existe disputa por CPU. Ele não cria capacidade de processamento: diz ao agendador do Windows qual tarefa deve passar na frente durante o período escolhido.

1. Abra o **SOS Neck** e selecione o aplicativo principal.
2. Clique em **Turbo 60 min** e confirme.
3. Feche o SOS e volte ao aplicativo.
4. Para interromper antes, use **Encerrar Turbo** no SOS ou na bandeja.

Enquanto a janela do aplicativo ou de um subprocesso da família estiver em primeiro plano, o Neck aplica `ABOVE_NORMAL_PRIORITY_CLASS`. Ao trocar de janela, expirar o período ou fechar o Neck, cada processo volta à sua prioridade anterior. Processos que já usam prioridade Alta ou Tempo real nunca são alterados; componentes críticos do Windows e o próprio Neck permanecem excluídos.

Turbo e Adaptive podem trabalhar juntos: o aplicativo recebe prioridade quando está em uso e, depois de 15 segundos em segundo plano, volta a receber o alívio configurado pelo Adaptive. O plano de energia do Windows não é alterado.

A implementação segue as APIs documentadas [`SetPriorityClass`](https://learn.microsoft.com/windows/win32/api/processthreadsapi/nf-processthreadsapi-setpriorityclass) e [Scheduling Priorities](https://learn.microsoft.com/windows/win32/procthread/scheduling-priorities) do Windows.

## Segurança por padrão

- Não usa “limpeza de RAM” artificial nem encerra processos em massa.
- Não aplica o Neck Adaptive a componentes críticos do Windows nem ao próprio Neck.
- Não usa prioridade Alta ou Tempo real no Neck Turbo e não altera o plano de energia.
- Não confunde RAM estacionada com memória definitivamente liberada e apresenta o resultado como estimativa.
- Não aplica ajustes genéricos no Registro para prometer desempenho.
- Não apaga documentos, fotos, downloads, senhas ou dados de navegadores.
- Não esvazia a Lixeira sem uma seleção explícita.
- Não baixa nem executa atualizações do Neck silenciosamente.
- Não armazena tokens ou credenciais do GitHub.
- Não reinicia o computador automaticamente.
- Não instala drivers automaticamente; utiliza Windows Update e páginas oficiais.
- Mantém uma lista fechada de tarefas administrativas permitidas.
- Salva relatórios de manutenção em `Documentos\Neck\Relatorios`.

O Neck normalmente é executado como usuário comum. A janela do UAC aparece somente quando você escolhe uma operação do Windows que realmente exige privilégios elevados, como DISM, SFC, otimização da unidade ou criação de ponto de restauração.

## Instalação

### Instalador recomendado

1. Abra a página de [releases](https://github.com/VitorGirardi/neck/releases/latest).
2. Baixe `Neck-Setup-1.4.0.exe` e o arquivo correspondente `.sha256`.
3. Opcionalmente, confira a integridade no PowerShell:

```powershell
Get-FileHash .\Neck-Setup-1.4.0.exe -Algorithm SHA256
```

4. Execute o instalador e siga as instruções. O Neck será adicionado ao menu Iniciar e poderá ser removido normalmente pelas configurações de Aplicativos do Windows.

### Versão portátil

Baixe `Neck.exe` na mesma release e execute-o diretamente. Nenhuma instalação é necessária. As preferências e o histórico continuam armazenados no perfil local do Windows.

> [!IMPORTANT]
> Os binários ainda não possuem assinatura digital. Até que o projeto tenha um certificado de assinatura de código, o Windows pode exibir “Editor desconhecido” ou uma proteção do SmartScreen. Sempre baixe o Neck desta página de releases e compare o SHA-256 publicado.

## Privacidade

O Neck não possui telemetria e não envia métricas do computador para servidores externos.

Dados mantidos localmente:

- Preferências e histórico do Guard: `%LOCALAPPDATA%\Neck`
- Relatórios de manutenção: `Documentos\Neck\Relatorios`
- Histórico do Guard: somente as últimas 24 horas

A internet é utilizada apenas quando você solicita uma verificação de versão ou abre uma fonte oficial, como GitHub, Windows Update ou a página de um fabricante.

## Requisitos

- Windows 10 ou Windows 11 de 64 bits
- .NET Framework 4.8
- Aproximadamente 10 MB de espaço para instalação
- Permissão de administrador somente para operações avançadas selecionadas

## Compilação

O projeto utiliza o compilador C# do .NET Framework disponível no Windows e não depende de pacotes externos para gerar o aplicativo.

```powershell
git clone https://github.com/VitorGirardi/neck.git
cd neck
.\test.ps1
.\build.ps1
```

Saída principal: `dist\Neck.exe`.

Para gerar o instalador, instale o [Inno Setup 6](https://jrsoftware.org/isinfo.php) e execute:

```powershell
.\build-installer.ps1
```

O comando produz o instalador, o executável portátil e os respectivos checksums SHA-256. Cada push e pull request para `main` também executa os autotestes, compila o aplicativo e gera os artefatos no GitHub Actions.

## Estrutura do projeto

```text
Program.cs                 Interface principal e manutenção segura
SystemMonitoring.cs        Diagnóstico de CPU, memória, disco e reunião
GuardMonitoring.cs         Monitoramento contínuo, histórico e bandeja
SosMode.cs                 Alívio seguro de sobrecarga
EfficiencyMode.cs          Otimização adaptativa de CPU, memória e EcoQoS
TurboMode.cs               Prioridade de foco temporária e reversível
ProcessFamily.cs           Descoberta de processos-filhos e RAM real da família
StartupAnalysis.cs         Análise somente leitura da inicialização
PersonalPlan.cs            Motor e interface das três prioridades
ElevatedOperations.cs      Executor administrativo com lista fechada
PreferencesAndUpdates.cs   Preferências e consulta manual de versão
SelfTest.cs                Testes funcionais e verificações visuais
installer/Neck.iss         Definição do instalador para Windows
```

## Como contribuir

Relatos de bugs, ideias e pull requests são bem-vindos.

- Antes de abrir uma issue, verifique se o problema já foi relatado.
- Explique a versão do Windows, a versão do Neck e como reproduzir o comportamento.
- Mudanças que apagam dados, encerram processos à força ou enfraquecem as confirmações de segurança não serão aceitas.
- Execute `.\test.ps1` antes de enviar um pull request.

Use as [issues do GitHub](https://github.com/VitorGirardi/neck/issues) para bugs e sugestões. Para uma vulnerabilidade que não deve ser divulgada publicamente, utilize o recurso **Report a vulnerability** na aba Security do repositório quando ele estiver disponível.

## Limites honestos

O Neck ajuda a diagnosticar e reduzir sobrecarga, mas não substitui backup, antivírus, suporte técnico ou uma atualização de hardware. Ele não consegue transformar falta física de RAM em memória adicional e não promete acelerar todos os computadores.

Resultados dependem do estado do Windows, dos aplicativos abertos e do hardware. Leia cada recomendação e mantenha backup dos arquivos importantes antes de qualquer manutenção do sistema.

## Roadmap

- [x] Diagnóstico inteligente e histórico local
- [x] Modo Reunião e monitoramento na bandeja
- [x] SOS Neck e privilégios sob demanda
- [x] Instalador, preferências e checksums
- [x] Neck Boot e Meu Plano Neck
- [x] Neck Adaptive com foco, EcoQoS e prioridade de memória
- [x] RAM Park e otimização por família de processos
- [x] Neck Turbo e detecção de pressão persistente de CPU
- [ ] Assinatura digital dos binários
- [ ] Aprimorar classificações de aplicativos com contribuições da comunidade
- [ ] Internacionalização da interface

---

<p align="center">
  Feito com cuidado para tornar a manutenção do Windows mais compreensível, segura e auditável.
</p>
