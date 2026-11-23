# Texts & commands

## Language

There is one language setting: Panel → **Settings → General → Language** (also asked in the setup wizard). It sets the
panel, the bot's replies and the default command names. Available: Türkçe, English, Deutsch, Español, Français, Русский,
Português, Polski, Italiano.

Command names are Turkish when Turkish is chosen and English in every other language. Changing the language resets
renamed commands to these defaults. The Turkish names always keep working.

## Bot texts

Panel → Plugins → **Bot texts** lists every reply the bot can send, with its preview. Texts of plugins that are off on
every bot are dimmed, marked *Plugin off* and listed last.

- **Message look** (top card): the default colour of all replies, the error colour and bold. A reply that has its own
  `[color]` tag keeps its colour.
- **Edit** opens the editor. It shows the BBCode the bot really sends, including the default colour and bold, so you can
  change them. The toolbar adds bold, italic, underline, strike-through, links and colours; the buttons below insert the
  variables this text supports; the preview shows how it looks in TeamSpeak.
- **Reset** brings back the default text. Saving the unchanged default also keeps the text on its default.

TeamSpeak BBCode: `[b]`, `[i]`, `[u]`, `[s]`, `[color=#FF0000]`, `[url=https://…]text[/url]`.

## Variables

| Where | Variables |
|---|---|
| Welcome message | `{user}` `{server}` `{channel}` `{time}` `{date}` |
| Announcements | `{server}` `{channel}` `{time}` |
| Song → bot name / description | `{title}` `{artist}` `{song}` `{user}` `{bot}` |
| Channel name info | `{online}` `{record}` `{max}` `{time}` `{date}` |
| Temporary channel name | `{user}` `{n}` |
| Rank-up message | `{user}` `{hours}` |
| Custom text commands | `{user}` `{args}` `{time}` `{date}` |
| Bot texts | shown as buttons in the editor of each text |

`{artist}` and `{song}` are split from titles like *Artist - Song (Official Video)*; without a separator `{artist}` is
empty and `{song}` is the whole title. `{record}` is the most users ever seen online at once on that server.

## Custom commands

Panel → Plugins → **Custom commands**. Two kinds:

- **Text** — replies with a fixed text, e.g. `!discord` → `Join us: https://discord.gg/…`. BBCode and the variables above
  work.
- **Alias** — runs another command, e.g. `!song` → `!play`, or `!lofi` → `!radio Lofi Hip Hop`.

Each command can be switched off without deleting it and can have a description, which is shown on the avatar card. Custom names cannot be the same as built-in commands.

## Renaming built-in commands

Open a plugin → its feature card → **Commands**, or the **General commands** tile. Type the new name; it is saved when you
leave the field or press Enter. The old name keeps working. Leave the field empty to go back to the default. Replies,
`!help` and the avatar card show the new names automatically.

## Avatar

The *Avatar* plugin sets the bot's TeamSpeak avatar separately for three states:

| State | Choices |
|---|---|
| Idle | command card · own image · no avatar |
| Music playing | song cover · own image · idle avatar |
| Radio playing | idle avatar · own image |

The **command card** lists the commands of the enabled plugins, your custom commands and `!radio` when you have stations.
Its descriptions are the `avatar.*` lines in Bot texts. **Own images** are uploaded in the plugin dialog and scaled to
300 px. TeamSpeak servers limit the avatar size (`i_client_max_avatar_filesize`); if an upload is refused the log says so
and the old avatar stays.
