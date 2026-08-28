# Política de privacidade do Neck

Vigente a partir de 28 de agosto de 2026.

O Neck não possui telemetria, publicidade, contas de usuário ou coleta de inventário remoto. Diagnósticos de CPU, memória, armazenamento, hardware, temperatura, processos e Bluetooth são executados localmente.

> Este programa não transfere informações para outros sistemas em rede, exceto quando isso é solicitado explicitamente pela pessoa que está usando o Neck.

## Quando existe acesso à internet

O Neck acessa a internet somente nestas situações iniciadas pelo usuário:

- **Verificar atualizações:** envia uma requisição `GET` para `https://api.github.com/repos/VitorGirardi/neck/releases/latest`. O cabeçalho `User-Agent` informa o nome e a versão do Neck. Como em qualquer conexão, o GitHub recebe metadados técnicos normais da rede, como endereço IP.
- **Abrir GitHub ou uma página oficial:** o Neck entrega a URL ao navegador padrão. A navegação passa a seguir a política do site aberto e do navegador.
- **Abrir Windows Update:** o Neck abre uma tela do próprio Windows; download, diagnóstico e política de dados são controlados pela Microsoft e pelas configurações do sistema.

O Neck não baixa nem instala silenciosamente atualizações próprias, aplicativos ou drivers. Também não envia relatórios, lista de processos, especificações do computador ou histórico de uso ao GitHub.

## Dados armazenados localmente

- Preferências e histórico recente do Neck Guard: `%LOCALAPPDATA%\Neck`
- Relatórios de manutenção escolhidos pelo usuário: `Documentos\Neck\Relatorios`
- Histórico do Guard: limitado às últimas 24 horas

Esses dados permanecem no computador. Para removê-los, desinstale o Neck e apague as duas pastas acima, caso ainda existam. A versão portátil utiliza os mesmos locais para manter as preferências entre execuções.

## Operações administrativas

Quando o usuário confirma uma manutenção que exige administrador, o Neck inicia uma segunda instância local com uma lista fechada de tarefas permitidas. Nenhuma credencial é lida ou armazenada, e nenhum resultado administrativo é enviado pela internet.

## Compilação e assinatura

GitHub Actions e, após aprovação, SignPath.io são utilizados pelos mantenedores para compilar e assinar releases. Esses serviços fazem parte do processo de publicação e não são acessados pelo aplicativo instalado durante o uso normal.

Questões públicas sobre privacidade podem ser abertas nas [issues](https://github.com/VitorGirardi/neck/issues). Vulnerabilidades ou exposições de dados devem ser informadas conforme a [política de segurança](SECURITY.md).
