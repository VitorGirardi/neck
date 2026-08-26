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
| **Neck Guard** | Mostra a saúde do computador e os maiores consumidores de memória. | Diagnóstico somente leitura. |
| **Guard contínuo** | Monitora a cada 30 segundos e alerta apenas sobre pressão persistente. | Histórico local limitado às últimas 24 horas. |
| **Modo Leve** | Mantém um aplicativo aberto com menor prioridade de CPU e eficiência energética do Windows. | Temporário, reversível e restaurado ao sair do Neck. |
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

## Modo Leve

Quando um aplicativo pesado precisa continuar aberto — como Claude, Chrome, Teams ou um editor — o Modo Leve reduz temporariamente sua disputa por CPU sem encerrar janelas ou perder trabalho.

1. Abra o **SOS Neck**.
2. Selecione o aplicativo.
3. Clique em **Ativar Modo Leve** e confirme.
4. Para desfazer, selecione-o novamente e clique em **Restaurar normal**.

O Neck aplica prioridade abaixo do normal e o mecanismo nativo de eficiência energética do Windows (EcoQoS) aos processos atuais. O Guard acompanha subprocessos novos a cada 30 segundos. O estado anterior é preservado e restaurado individualmente; ao encerrar o Neck, todos os Modos Leves restantes também são removidos.

![Modo Leve no SOS Neck](assets/screenshots/neck-sos.png)

> [!NOTE]
> O Modo Leve alivia disputa por processamento, consumo e temperatura. Ele não “cria RAM” nem reduz imediatamente toda a memória que o aplicativo já reservou, e pode deixá-lo um pouco menos responsivo enquanto estiver ativo.

## Segurança por padrão

- Não usa “limpeza de RAM” artificial nem encerra processos em massa.
- Não aplica o Modo Leve a componentes críticos do Windows nem ao próprio Neck.
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
2. Baixe `Neck-Setup-1.1.0.exe` e o arquivo correspondente `.sha256`.
3. Opcionalmente, confira a integridade no PowerShell:

```powershell
Get-FileHash .\Neck-Setup-1.1.0.exe -Algorithm SHA256
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
SystemMonitoring.cs        Diagnóstico de memória, disco e reunião
GuardMonitoring.cs         Monitoramento contínuo, histórico e bandeja
SosMode.cs                 Alívio seguro de sobrecarga
EfficiencyMode.cs          Prioridade reversível e EcoQoS por aplicativo
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
- [x] Modo Leve reversível por aplicativo
- [ ] Assinatura digital dos binários
- [ ] Aprimorar classificações de aplicativos com contribuições da comunidade
- [ ] Internacionalização da interface

---

<p align="center">
  Feito com cuidado para tornar a manutenção do Windows mais compreensível, segura e auditável.
</p>
