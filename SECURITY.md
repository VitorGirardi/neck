# Política de segurança

## Versões suportadas

A versão mais recente publicada em [Releases](https://github.com/VitorGirardi/neck/releases/latest) recebe correções de segurança. Versões anteriores podem ser utilizadas para comparação, mas não recebem suporte garantido.

## Como relatar uma vulnerabilidade

Não publique detalhes exploráveis, dados pessoais, credenciais ou relatórios sensíveis em uma issue aberta. Use **Report a vulnerability** na aba [Security](https://github.com/VitorGirardi/neck/security) do repositório para iniciar um aviso privado do GitHub.

Inclua, quando possível:

- versão do Neck e do Windows;
- impacto observado e pré-condições;
- passos mínimos para reprodução;
- arquivos ou funções envolvidos;
- uma sugestão de correção, se houver.

O projeto é mantido de forma independente e não promete um prazo fixo de resposta. Relatos serão triados com prioridade proporcional ao impacto e permanecerão privados até existir uma correção ou mitigação adequada.

## Limites de confiança

- O Neck inicia como usuário comum.
- Operações elevadas aceitam apenas tarefas previstas em uma lista fechada no código.
- Parâmetros de dispositivos são validados antes de chegar a ferramentas do Windows.
- Nenhum token de GitHub, certificado ou chave de assinatura deve ser salvo no repositório.
- Segredos de assinatura pertencem ao cofre do GitHub e ao HSM do provedor de assinatura.
- Releases assinadas devem passar pela verificação descrita na [política de assinatura](CODE_SIGNING_POLICY.md).

Mudanças que ampliem privilégios, removam confirmações, executem comandos arbitrários ou enfraqueçam validações exigem revisão explícita de segurança.
