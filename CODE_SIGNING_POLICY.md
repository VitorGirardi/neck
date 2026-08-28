# Code signing policy

Esta é a política pública de assinatura de código do Neck.

**Free code signing provided by SignPath.io, certificate by SignPath Foundation.**

O projeto está preparado para a integração, mas os binários só devem ser anunciados como assinados depois da aprovação da SignPath Foundation e de uma verificação Authenticode válida na release correspondente.

## Escopo

A política cobre os binários oficiais produzidos por este repositório:

- `Neck.exe`, versão portátil e aplicativo instalado;
- `Neck-Setup-<versão>.exe`, instalador para Windows.

Checksums `.sha256`, código-fonte e imagens não recebem Authenticode. Os checksums são recalculados somente depois da assinatura final.

## Origem e cadeia de compilação

1. O código é obtido de um commit ou tag público deste repositório.
2. GitHub Actions executa `test.ps1` em uma máquina Windows limpa.
3. `build.ps1` gera o `Neck.exe` sem assinatura.
4. SignPath assina o executável portátil.
5. O instalador é criado contendo o `Neck.exe` já assinado.
6. SignPath assina o instalador.
7. O workflow exige assinatura Authenticode válida, publicador esperado e timestamp antes de gerar os checksums e disponibilizar o pacote assinado.

O compilador legado do .NET Framework inclui metadados variáveis no executável; portanto, o projeto não afirma que builds locais são byte a byte reproduzíveis. A garantia oferecida é uma compilação automatizada, rastreável e auditável a partir do repositório público.

## Algoritmos e verificação

- Digest Authenticode: SHA-256
- Timestamp: RFC 3161 com SHA-256, conforme a configuração da SignPath
- Chave privada: criada e mantida pelo provedor em HSM; nunca é exportada para o repositório

Verificação manual:

```powershell
Get-AuthenticodeSignature .\Neck.exe | Format-List Status,StatusMessage,SignerCertificate,TimeStamperCertificate
Get-FileHash .\Neck.exe -Algorithm SHA256
```

Uma release assinada deve mostrar `Status: Valid`. Nome de arquivo ou checksum, isoladamente, não substituem a validação da assinatura.

## Ativação depois da aprovação

O mantenedor deve solicitar a participação do projeto pelo [formulário oficial da SignPath Foundation](https://signpath.org/apply.html). Depois da aprovação, os identificadores fornecidos pela SignPath são configurados no GitHub em **Settings > Secrets and variables > Actions**; nenhum token deve ser commitado.

Secret obrigatório:

- `SIGNPATH_API_TOKEN`

Variables obrigatórias:

- `SIGNPATH_ORGANIZATION_ID`
- `SIGNPATH_PROJECT_SLUG`
- `SIGNPATH_POLICY_SLUG`
- `SIGNPATH_PORTABLE_ARTIFACT_CONFIGURATION_SLUG`
- `SIGNPATH_INSTALLER_ARTIFACT_CONFIGURATION_SLUG`

Variable opcional:

- `SIGNPATH_EXPECTED_SUBJECT` — nome do publicador esperado; o padrão é `SignPath Foundation`.

São usadas duas configurações de artefato porque o executável portátil e o instalador possuem estruturas diferentes. Os slugs exatos devem vir da configuração aprovada no painel da SignPath; para trocá-los, basta atualizar as variables, sem alterar o workflow.

Com a configuração concluída, o mantenedor executa manualmente o workflow `sign-windows-release` a partir de `main` ou de uma tag `v*`. O artefato `neck-windows-signed-<commit>` só está pronto para publicação se todas as verificações do workflow terminarem com sucesso. A publicação da release permanece uma decisão humana separada.

## Papéis da equipe

- **Committer e reviewer:** [Vitor Girardi (@VitorGirardi)](https://github.com/VitorGirardi)
- **Approver de assinatura:** [Vitor Girardi (@VitorGirardi)](https://github.com/VitorGirardi)

Contribuições externas passam por revisão do mantenedor. Contas com permissão de escrita ou aprovação devem utilizar autenticação multifator. Solicitações de assinatura são limitadas ao `main` e a tags oficiais do próprio projeto.

## Privacidade

Consulte a [política de privacidade](PRIVACY.md). O aplicativo não se conecta à SignPath durante o uso; a SignPath participa somente da cadeia de publicação.

## Incidentes e revogação

Se uma chave, conta ou release assinada for comprometida, o mantenedor deve interromper novas assinaturas, retirar os artefatos afetados, avisar a SignPath e publicar uma análise no repositório quando isso puder ser feito sem ampliar o risco. Uma assinatura válida não transforma comportamento inseguro em seguro e pode ser revogada.
