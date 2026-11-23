# Commands

Command names follow the panel language: **Turkish** names when the panel is in Turkish, **English** names in every other language. The Turkish names always keep working, and every command can be renamed in the panel (Plugins → open a plugin → *Commands*, or the *General commands* tile). A command whose plugin is off answers "this feature is off"; `!help` only lists the commands of enabled plugins.

Commands work in the bot's channel chat, in a private chat with the bot and in the server chat.

| Command | Turkish name | Plugin | What it does |
|---|---|---|---|
| `!play <song or link>` | `!ara` | music | Search YouTube or open a link and play / queue it |
| `!link <link>` | `!link` | music | Play a link directly (YouTube, SoundCloud, Twitch, stream URL…) |
| `!file <name>` | `!dosya` | music | Queue a file from the panel's music library |
| `!radio [name]` | `!radyo` | radio | Play a radio station from the panel, or list them |
| `!next` | `!gec` | music / radio | Next song (starts a vote when vote skip is on) |
| `!skip` | `!skip` | music / radio | Same as next |
| `!pause` | `!dur` | music / radio | Pause / resume (toggle) |
| `!hold` | `!duraklat` | music / radio | Pause |
| `!resume` | `!devam` | music / radio | Resume |
| `!stop` | `!bitir` | music / radio | Stop and clear the queue |
| `!end` | `!kapat` | music / radio | Same as stop |
| `!vol [0-100]` | `!ses` | music / radio | Show or set the volume |
| `!np` | `!calan` | music / radio | Now playing |
| `!queue` | `!siradaki` | music / radio | Show the queue |
| `!clear` | `!temizle` | music / radio | Clear the queue (the playing song keeps going) |
| `!playlist [save\|play\|add\|show\|delete] <name>` | `!liste` | music | Playlists: save the queue, play, add the current song, show, delete; alone it lists them |
| `!fav [del <no>]` | `!fav` | favorites | Add the playing song to your favourites (or remove one) |
| `!favs [list]` | `!favs` | favorites | Queue your favourites (or list them) |
| `!stats` | `!istatistik` | music | Most played songs and top requesters of the last 30 days |
| `!lyrics [song]` | `!soz` | fun (lyrics) | Lyrics of the playing (or another) song, sent privately |
| `!poll question \| a \| b` | `!anket` | fun (polls) | Start a poll (`end` / `bitir` ends it); posted in the channel |
| `!vote <no>` | `!oy` | fun (polls) | Vote in the poll; the current percentages are shared in the channel |
| `!weather <city>` | `!hava` | fun (weather) | Weather |
| `!fx <code> [amount]` | `!doviz` | fun (currency) | Exchange rate |
| `!usd [amount]` | `!dolar` | fun (currency) | US dollar rate |
| `!eur [amount]` | `!euro` | fun (currency) | Euro rate |
| `!dice [2d6]` | `!zar` | fun (dice) | Roll dice |
| `!coin` | `!yazitura` | fun (dice) | Flip a coin |
| `!pick a \| b \| c` | `!sec` | fun (dice) | Pick one of the options |
| `!register <name>` | `!kayit` | registration | Register a waiting user (staff groups only) |
| `!time` | `!sure` | ranks | Your total time on the server and your place |
| `!top` | `!top` | ranks | Most active users |
| `!come` | `!gel` | — | Bring the bot to your channel |
| `!lastseen <name>` | `!gorulme` | — | Whether someone is online, or when they were last seen |
| `!help` | `!yardim` | — | All commands of the enabled plugins |

## Examples

- `!play tarkan kuzu kuzu` — searches YouTube and queues the first result
- `!playlist save rock` · `!playlist play rock` · `!playlist add rock` · `!playlist show rock` · `!playlist delete rock`
- `!poll What do we play tonight? | CS2 | LoL | Valorant` → everyone types `!vote 2`; `!poll end` ends it
- `!weather istanbul` · `!fx EUR 50` · `!dice 2d6` · `!pick pizza | burger | sushi`
- `!fav` · `!favs` · `!favs list` · `!fav del 3`
- `!lastseen John` · `!come` · `!file intro`

TS3AudioBot's own commands (`!play`, `!add`, `!list`, `!volume`, …) also exist; they follow the same plugin switches.
