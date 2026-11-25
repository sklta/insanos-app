# App Insanos (PC)

App da guilda **INSANOS** (WoW Forever) para Windows. Ele leva as raids marcadas no Discord direto para o addon
[Insanos Guild Sync](https://www.curseforge.com/wow/addons/insanos-guild-sync), sem precisar copiar e colar a chave
`!NILBAS`. É para oficiais e líderes de raid. Usar é opcional: colar a chave no addon continua funcionando igual.

> **Ainda em testes.** Quando sair, o `.exe` fica em [Releases](../../releases/latest).

## Como funciona

1. No Discord da guilda, use `/vincular-app` e copie a chave (ela aparece só para você).
2. Abra o app, confira a pasta do WoW Forever e cole a chave.
3. O app fica perto do relógio. A cada 5 minutos ele pergunta ao bot quais raids você lidera e deixa a próxima
   pronta no addon. Entre no jogo ou dê `/reload`: a raid já aparece carregada. Ninguém é convidado sozinho.

Para desligar: `/desvincular-app` no Discord, e "Sair" no menu do ícone.

## Segurança

O código deste repositório é todo o app: dá para ler cada linha. O resumo do que ele faz e do que não faz:

**O que o app faz**

- **Conversa com um lugar só:** `https://insanos.discloud.app`, o bot da guilda
  ([`InboxApi.cs`](InsanosApp/Core/InboxApi.cs)). Ele manda a sua chave e recebe as chaves `!NILBAS` das raids que
  você lidera.
- **Escreve um arquivo só no jogo:** `Interface\AddOns\NilbasForever\Inbox.lua`, com essas chaves
  ([`InboxFile.cs`](InsanosApp/Core/InboxFile.cs)). Antes de escrever, joga fora tudo que não for uma chave válida
  (só letras, números e `+/=`, até 10 chaves): nada que vem da internet vira código no jogo. O addon confere tudo de
  novo antes de usar.
- **Guarda as configurações** em `%APPDATA%\InsanosApp\config.json`: a pasta do jogo e a sua chave. A chave fica
  protegida pelo Windows (DPAPI), e só o seu usuário consegue abrir ([`KeyVault.cs`](InsanosApp/KeyVault.cs)).
- **"Abrir junto com o Windows"** só liga se você marcar, e é só para o seu usuário
  ([`Startup.cs`](InsanosApp/Startup.cs)).

**O que o app não faz**

- Não pede, não lê e não guarda senha nenhuma (Battle.net, WoW ou Discord).
- Não lê a memória do jogo, não injeta nada, não aperta teclas e não automatiza nada no WoW.
- Não manda nada do seu PC: nem tela, nem arquivos, nem dados pessoais. Só a chave do `/vincular-app`.
- Não roda como administrador.
- A chave só lê as raids que você lidera. Ela não dá acesso à sua conta do Discord, e `/desvincular-app` desliga na hora.

**Como conferir que o .exe é deste código**

Cada versão é montada pelo próprio GitHub, a partir deste código, no workflow
[`release.yml`](.github/workflows/release.yml), e não no computador de ninguém. Em cada release:

- **Atestação do GitHub:** prova assinada de que o `.exe` saiu deste repositório e deste workflow. Para conferir:
  `gh attestation verify .\InsanosApp-X.Y.Z.exe -R sklta/insanos-app`
- **SHA-256:** a impressão digital do arquivo. No PowerShell: `Get-FileHash .\InsanosApp-X.Y.Z.exe -Algorithm SHA256`
  (tem que dar o mesmo valor publicado no release).
- **VirusTotal:** o link da análise de dezenas de antivírus vai junto no release.

O `.exe` ainda não tem assinatura digital paga, então na primeira vez o Windows pode mostrar "O Windows protegeu o
computador". Clique em "Mais informações" e "Executar assim mesmo", depois de conferir o arquivo como acima.

Achou um problema de segurança? Avise um oficial da guilda no Discord.

## Para quem programa

- `InsanosApp/`: o app (.NET 10, Windows Forms). `Core/` é o núcleo, sem tela.
- `InsanosApp.Tests/`: testes do núcleo, sem pacotes de fora: `dotnet run --project InsanosApp.Tests`
- Gerar o `.exe` como o GitHub faz: veja o passo "Gerar o .exe" em [`release.yml`](.github/workflows/release.yml).

## Links

- Site da guilda: https://insanos.discloud.app
- Addon no CurseForge: https://www.curseforge.com/wow/addons/insanos-guild-sync

Site e app de fã, sem ligação com a Blizzard Entertainment.
