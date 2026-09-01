# Testes e critérios de lançamento

Este documento separa o que foi comprovado automaticamente do que ainda exige validação manual. O objetivo é impedir que uma build seja chamada de estável apenas porque compilou.

## Estado da versão 1.19.0

| Verificação | Estado | Evidência |
| --- | --- | --- |
| Compilação no Windows | Aprovada | Workflow público `build` no GitHub Actions |
| Suíte funcional local | Aprovada | `SELF_TEST_OK` em 1 de setembro de 2026 |
| Interface normal, mínima e 1052 × 759 | Aprovada | Capturas geradas pelo self-test |
| Menu simplificado da bandeja | Aprovada | Teste fixa as sete linhas essenciais e oculta ações inativas |
| Adaptive, Turbo, Escudo de Foco e Autopilot | Aprovada | Testes com processos descartáveis e restauração do estado original |
| Recuperação após interrupção | Aprovada | Diário transacional testado com PID, nome e horário de início |
| Privacidade do relatório de suporte | Aprovada | Usuário, computador e caminhos pessoais removidos no teste |
| Binários e checksums da release | Aprovada | Arquivos públicos baixados novamente e comparados por SHA-256 |
| Microsoft Defender | Aprovada | Nenhuma ameaça encontrada no portátil ou instalador em 1 de setembro de 2026 |
| Assinatura Authenticode | Pendente | Aguardando aprovação da SignPath Foundation |
| Instalação limpa no Windows 10 | Pendente | Exige VM ou segundo computador |
| Instalação limpa no Windows 11 | Pendente | Exige VM ou segundo computador |
| Cura Bluetooth em múltiplos adaptadores | Pendente | Exige hardware real de fabricantes diferentes |
| Proteção anti-loop do Bluetooth | Automatizado | Simula timeout, descarga do BTHUSB, repetição e cooldown |
| Reset elétrico guiado | Automatizado e visual | Recusa `/f`, `/hybrid` e reinicialização; confere layout normal e mínimo sem executar desligamento |

Enquanto qualquer linha crítica estiver pendente, o Neck deve ser apresentado como **beta pública**, não como software certificado ou garantia universal de desempenho.

## Teste automatizado

Em um PowerShell comum, sem privilégios administrativos:

```powershell
git clone https://github.com/VitorGirardi/neck.git
cd neck
.\test.ps1
```

O resultado esperado termina com `SELF_TEST_OK`. O teste não apaga arquivos pessoais, não executa DISM, não otimiza a unidade e não reinicia o adaptador Bluetooth real.

Para validar o pacote já montado:

```powershell
.\verify-package.ps1 -AllowUnsigned
```

O parâmetro `-AllowUnsigned` é temporário e deve ser removido da validação da release assim que a assinatura oficial estiver ativa.

## Checklist em Windows limpo

Execute esta lista em uma VM ou computador secundário, nunca em uma máquina crítica sem backup.

### Instalação e ciclo de vida

- [ ] Instalar pelo `Neck-Setup-1.19.0.exe`.
- [ ] Confirmar atalho no menu Iniciar e ícone opcional na Área de Trabalho.
- [ ] Abrir uma segunda instância e confirmar que a primeira recebe o foco.
- [ ] Ativar a inicialização com o Windows, reiniciar a sessão e confirmar a bandeja.
- [ ] Desinstalar e confirmar a remoção do executável e da entrada de inicialização.
- [ ] Confirmar que dados locais remanescentes correspondem ao que está descrito em `PRIVACY.md`.

### Diagnóstico e interface

- [ ] Conferir CPU, RAM, disco, GPU e temperaturas contra ferramentas do próprio Windows ou do fabricante.
- [ ] Redimensionar todas as janelas até o tamanho mínimo sem texto cortado ou controles sobrepostos.
- [ ] Testar 100%, 125%, 150% e 200% de escala de exibição.
- [ ] Testar tema claro do Windows e contraste alto quando disponível.
- [ ] Confirmar que reduzir animações interrompe os movimentos contínuos.

### Operações reversíveis

- [ ] Acelerar um aplicativo descartável e confirmar a restauração ao perder foco, parar e fechar o Neck.
- [ ] Encerrar o Neck durante uma otimização de teste e confirmar a recuperação na próxima abertura.
- [ ] Ativar Modo Reunião e confirmar a restauração da política de suspensão ao sair.
- [ ] Testar Autopilot somente com aplicativos descartáveis e conferir o diário de recuperação vazio ao final.

### Limpeza e manutenção administrativa

- [ ] Criar arquivos artificiais antigos no diretório temporário e confirmar que apenas os elegíveis são removidos.
- [ ] Confirmar que temporários recentes, Downloads, Documentos e dados de navegadores permanecem intactos.
- [ ] Executar DISM ScanHealth e SFC VerifyOnly dentro da VM.
- [ ] Executar otimização da unidade e verificar o relatório produzido.
- [ ] Cancelar o UAC e confirmar que o Neck explica o cancelamento sem travar.

### Bluetooth

- [ ] Diagnosticar um adaptador saudável sem executar reparo desnecessário.
- [ ] Desligar apenas a chave do Bluetooth e confirmar que o Neck abre as configurações, sem reiniciar o driver.
- [ ] Testar a cura em falha real e confirmar que pareamentos continuam cadastrados.
- [ ] Repetir, quando possível, com adaptadores Intel, MediaTek e Realtek.

## Critério para sair de beta

Uma release pode ser considerada candidata a estável quando:

1. a suíte e o workflow público estiverem verdes;
2. não houver falha crítica aberta;
3. Windows 10 e Windows 11 tiverem completado o checklist limpo;
4. executável e instalador mostrarem Authenticode `Valid` com timestamp;
5. o resultado e as limitações forem registrados na release.
