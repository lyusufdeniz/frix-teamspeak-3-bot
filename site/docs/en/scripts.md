# Scripts

Scripts are small JavaScript programs that add your own chat commands and react to events. Write them in
Panel → Plugins → **Scripts** → *New script*. A new script starts from a commented example.

- Every enabled script runs on **every bot**.
- Scripts only see the `frix` object: no file, network or system access.
- Each call is limited to **2 seconds**, **32 MB** of memory and 500,000 steps; endless loops are stopped.
- Saving checks the syntax first; a script with a syntax error is not saved.
- Runtime errors appear next to the script and in **Script logs**, together with your `frix.log` lines.
- Built-in command names cannot be taken by a script.

## API

| Call | What it does |
|---|---|
| `frix.command(name, (args, ctx) => text)` | Registers `!name`. `args` is the text after the command. The returned text is the reply, sent where the command was typed (channel, private or server chat). Return nothing for no reply. |
| `frix.on(event, (e) => { … })` | Listens to an event (see below). |
| `frix.sendChannel(text)` | Message to the bot's channel. |
| `frix.sendServer(text)` | Message to the whole server. |
| `frix.sendPrivate(id, text)` | Private message to a client (client ID). |
| `frix.poke(id, text)` | Pokes a client. |
| `frix.clients()` | Online users: `id`, `uid`, `name`, `channel`, `channelId`, `away`, `groups`. |
| `frix.store.get(key)` / `frix.store.set(key, value)` | Data that survives restarts, per script (JSON, up to 10 KB per key). |
| `frix.log(value)` | Writes to the script log. |
| `frix.botName`, `frix.serverName` | Name of the bot and of the TeamSpeak server. |

Messages are sent with the default colour of [Bot texts](#customization); BBCode works.

`ctx` of a command: `id`, `uid`, `user`, `channel`, `target` (`channel`, `private` or `server`).

## Events

| Event | When | `e` |
|---|---|---|
| `connect` | Someone connects to the server | `id`, `uid`, `user` |
| `channel` | Someone enters a channel | `id`, `uid`, `user`, `channelId`, `channel` |
| `message` | Any chat message the bot sees | `id`, `uid`, `user`, `text` |
| `song` | A song or radio starts | `title`, `radio` |
| `tick` | Every 10 seconds | `time` |

Joins in the first 15 seconds after the bot connects (people who were already online) are ignored.

## Examples

**Discord link and a dice command**

```js
frix.command('discord', () => 'Join us: [url=https://discord.gg/example]discord.gg/example[/url]');

frix.command('roll', (args, ctx) => {
  const max = parseInt(args) || 100;
  return ctx.user + ' rolled ' + (1 + Math.floor(Math.random() * max)) + ' (1-' + max + ')';
});
```

**Greet people who enter the bot's lobby**

```js
frix.on('channel', (e) => {
  if (e.channel === 'Lobby') frix.sendPrivate(e.id, 'Welcome ' + e.user + '! Type !help for commands.');
});
```

**Count songs and announce every 100th**

```js
frix.on('song', (e) => {
  const n = (frix.store.get('songs') || 0) + 1;
  frix.store.set('songs', n);
  if (n % 100 === 0) frix.sendChannel('🎉 Song number ' + n + ': ' + e.title);
});
```

**List who is away**

```js
frix.command('away', () => {
  const away = frix.clients().filter((c) => c.away).map((c) => c.name);
  return away.length ? 'Away: ' + away.join(', ') : 'Nobody is away.';
});
```
