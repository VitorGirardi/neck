# Como contribuir com o Neck

Obrigado por ajudar a tornar a manutenção do Windows mais clara e segura. Bugs, ideias, melhorias de acessibilidade, traduções e revisões de segurança são bem-vindos.

## Antes de começar

- Procure uma issue existente antes de abrir outra.
- Vulnerabilidades não devem ser publicadas em issues; use o relato privado na aba **Security**.
- Não inclua nomes de usuário, computador, dispositivos pareados, relatórios pessoais, credenciais ou capturas com dados privados.
- Mudanças que apagam dados, encerram processos à força ou removem confirmações exigem uma justificativa de segurança explícita.

## Ambiente

- Windows 10 ou 11 de 64 bits
- .NET Framework 4.8
- PowerShell
- Inno Setup 6 somente para gerar o instalador

```powershell
git clone https://github.com/VitorGirardi/neck.git
cd neck
.\test.ps1
.\build.ps1
```

## Fluxo recomendado

1. Crie uma branch curta a partir de `main`.
2. Faça uma mudança focada e mantenha o comportamento reversível.
3. Atualize testes e documentação quando o comportamento público mudar.
4. Execute `test.ps1` e confirme `SELF_TEST_OK`.
5. Abra um pull request explicando problema, solução, risco e como foi testado.

## Proteções obrigatórias

- O Neck deve iniciar como usuário comum.
- Elevação deve aceitar somente operações previstas em lista fechada.
- Entradas usadas em comandos, caminhos e identificadores de dispositivo precisam ser validadas.
- Alterações de prioridade, EcoQoS, energia ou memória devem registrar e restaurar o estado anterior.
- Limpeza deve permanecer limitada a raízes conhecidas, ignorar pontos de nova análise e respeitar idade mínima.
- Nenhum relatório ou inventário pode ser enviado automaticamente.
- Ausência de sensor, permissão ou suporte deve ser exibida como indisponível, nunca inventada.

## Pull requests

Um pull request está pronto para análise quando:

- [ ] possui escopo e motivação claros;
- [ ] preserva arquivos pessoais e configurações não relacionadas;
- [ ] inclui teste proporcional ao risco;
- [ ] não contém binários, relatórios locais ou segredos;
- [ ] mantém README, privacidade e segurança coerentes;
- [ ] termina com a suíte completa aprovada.

Consulte também [TESTING.md](TESTING.md), [SECURITY.md](SECURITY.md) e [PRIVACY.md](PRIVACY.md).
