# Privacy / Privacidade

*Português abaixo.*

## English

Insanos App is a small Windows tray app for the INSANOS guild. It has no ads, no analytics, no telemetry and no
crash reporting.

**What stays on your computer**

`%APPDATA%\InsanosApp\config.json` holds the game's AddOns folder, which addon folder to use, the check interval, the
time of the last raid change and its label, and your app key. The key is encrypted with Windows DPAPI, so only your
Windows user can read it. Nothing in this file is sent anywhere.

**What the app sends**

The app talks to one address only: `https://insanos.discloud.app`, the guild's Discord bot. Each request carries your
app key (`Authorization` header) and the app version (`User-Agent`). Like any web request, it also reveals your IP
address to the server and its hosting provider (Discloud). The app sends no files, screenshots, passwords or other
personal data. It never reads game memory.

**What the guild's bot stores**

When you run `/vincular-app` on Discord, the bot stores:

- a SHA-256 hash of the key (never the key itself);
- your Discord user ID, the server ID and your display name on that server;
- whether you were an officer when you linked the app;
- when the key was created and, at most once an hour, when it was last used.

The raids the app receives (raid name, time and the in-game names of the players who signed up) are the same
sign-ups already posted on the guild's Discord. Request counters used to block abuse are kept in memory for one minute
and are not saved. The hosting provider may keep its own standard connection logs.

**Deleting your data**

Run `/desvincular-app` on Discord: the key is deleted at once and stops working. Then choose "Sair" in the tray icon
menu and delete the `%APPDATA%\InsanosApp` folder.

**Contact**

Open an issue at https://github.com/sklta/insanos-app/issues or talk to a guild officer on Discord.

## Português

O App Insanos é um app pequeno para Windows, da guilda INSANOS. Não tem propaganda, análise de uso, telemetria nem
envio de relatório de erro.

**O que fica no seu computador**

`%APPDATA%\InsanosApp\config.json` guarda a pasta AddOns do jogo, qual pasta do addon usar, o intervalo das
verificações, a hora e o nome da última raid que mudou e a sua chave do app. A chave é protegida pelo Windows (DPAPI)
e só o seu usuário do Windows consegue abrir. Nada desse arquivo é enviado para lugar nenhum.

**O que o app envia**

O app conversa com um endereço só: `https://insanos.discloud.app`, o bot da guilda no Discord. Cada pedido leva a sua
chave do app (cabeçalho `Authorization`) e a versão do app (`User-Agent`). Como qualquer acesso à internet, o servidor
e a hospedagem (Discloud) veem o seu IP. O app não envia arquivos, imagens da tela, senhas nem outros dados pessoais.
Ele nunca lê a memória do jogo.

**O que o bot da guilda guarda**

Quando você usa `/vincular-app` no Discord, o bot guarda:

- o hash SHA-256 da chave (nunca a chave);
- o seu ID do Discord, o ID do servidor e o seu nome no servidor;
- se você era oficial quando vinculou o app;
- quando a chave foi criada e, no máximo uma vez por hora, quando foi usada pela última vez.

As raids que o app recebe (nome da raid, horário e os nomes no jogo de quem se inscreveu) são as mesmas inscrições que
já estão no Discord da guilda. Os contadores que bloqueiam abuso ficam na memória por um minuto e não são salvos. A
hospedagem pode guardar os registros de conexão dela.

**Como apagar os seus dados**

Use `/desvincular-app` no Discord: a chave é apagada na hora e para de funcionar. Depois clique em "Sair" no menu do
ícone e apague a pasta `%APPDATA%\InsanosApp`.

**Contato**

Abra uma issue em https://github.com/sklta/insanos-app/issues ou fale com um oficial da guilda no Discord.
