"""Generates the reference pages of the documentation (plugins, commands) in site/docs/{en,tr}/.

The plugin settings come from a snapshot of the panel API (GET /panel/api/plugins/<bot>), saved in
packaging/tools/docs-data/plugins.json. Refresh it after changing plugin settings:

    curl -b cookies.txt http://localhost:58913/panel/api/plugins/<bot> > packaging/tools/docs-data/plugins.json
    python packaging/tools/gen_docs.py
"""
import json
import os
import re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
DATA = os.path.join(ROOT, "packaging", "tools", "docs-data", "plugins.json")
OUT = os.path.join(ROOT, "site", "docs")

# Turkish built-in name -> English default name (FrixCommands.LanguageDefaults["en"])
EN_NAMES = {
    "ara": "play", "radyo": "radio", "gec": "next", "dur": "pause", "devam": "resume", "bitir": "stop", "ses": "vol",
    "calan": "np", "siradaki": "queue", "temizle": "clear", "yardim": "help", "kayit": "register", "liste": "playlist",
    "soz": "lyrics", "anket": "poll", "oy": "vote", "hava": "weather", "doviz": "fx", "zar": "dice", "yazitura": "coin",
    "sec": "pick", "istatistik": "stats", "sure": "time", "duraklat": "hold", "kapat": "end", "dolar": "usd", "euro": "eur",
    "gel": "come", "gorulme": "lastseen", "dosya": "file",
}


def en_cmds(text):
    """!ara -> !play in English pages."""
    return re.sub(r"!([a-zçğıöşü]+)", lambda m: "!" + EN_NAMES.get(m.group(1), m.group(1)), text)


# TeamSpeak permissions a feature needs (shown under each feature)
PERMS = {
    "music": "`b_client_use_channel_commander` (only if channel commander is on in the bot settings)",
    "npstatus": "`b_client_modify_own_description` (for the description)",
    "avatar": "`i_client_max_avatar_filesize` high enough for the image (the command card is about 30 KB)",
    "welcome": "`i_client_poke_power` (only for the *poke* delivery)",
    "advertisement": "`b_client_server_textmessage_send` (only for messages to the whole server)",
    "afkmover": "`i_client_move_power` higher than the users' `i_client_needed_move_power`",
    "namefilter": "`i_client_poke_power`, and `i_client_kick_from_server_power` for *kick*",
    "channelguard": "`i_client_move_power` (move) or `i_client_kick_from_channel_power` (kick)",
    "antiflood": "`i_client_poke_power`, `i_client_kick_from_channel_power` / `i_client_kick_from_server_power`",
    "registration": "`i_group_member_add_power`, `i_group_member_remove_power`, `i_client_move_power`, `i_client_poke_power`",
    "tempchannel": "`b_channel_create_temporary`, `i_client_move_power`",
    "channelinfo": "`b_channel_modify_name` on that channel",
    "supportnotify": "`i_client_poke_power`",
    "ranks": "`i_group_member_add_power` (and `i_group_member_remove_power` to remove the previous rank)",
    "autoreconnect": "—",
}
PERMS_TR = {
    "music": "`b_client_use_channel_commander` (yalnızca bot ayarlarında kanal komutanı açıksa)",
    "npstatus": "`b_client_modify_own_description` (açıklama için)",
    "avatar": "resme yetecek `i_client_max_avatar_filesize` (komut kartı yaklaşık 30 KB)",
    "welcome": "`i_client_poke_power` (yalnızca *dürtme* ile gönderimde)",
    "advertisement": "`b_client_server_textmessage_send` (yalnızca tüm sunucuya mesajda)",
    "afkmover": "kullanıcıların `i_client_needed_move_power` değerinden yüksek `i_client_move_power`",
    "namefilter": "`i_client_poke_power`, *at* için `i_client_kick_from_server_power`",
    "channelguard": "`i_client_move_power` (taşıma) ya da `i_client_kick_from_channel_power` (atma)",
    "antiflood": "`i_client_poke_power`, `i_client_kick_from_channel_power` / `i_client_kick_from_server_power`",
    "registration": "`i_group_member_add_power`, `i_group_member_remove_power`, `i_client_move_power`, `i_client_poke_power`",
    "tempchannel": "`b_channel_create_temporary`, `i_client_move_power`",
    "channelinfo": "o kanalda `b_channel_modify_name`",
    "supportnotify": "`i_client_poke_power`",
    "ranks": "`i_group_member_add_power` (önceki rütbeyi kaldırmak için `i_group_member_remove_power`)",
    "autoreconnect": "—",
}

OPT = {
    "en": {"quiet": "quiet", "normal": "normal", "loud": "loud", "server": "server", "channel": "channel", "private": "private message",
           "poke": "poke", "both": "both", "sequential": "in order", "random": "random", "kick": "kick", "move": "move",
           "card": "command card", "custom": "own image", "none": "none", "cover": "song cover", "idle": "idle avatar"},
    "tr": {"quiet": "kısık", "normal": "normal", "loud": "yüksek", "server": "sunucu", "channel": "kanal", "private": "özel mesaj",
           "poke": "dürtme", "both": "ikisi de", "sequential": "sırayla", "random": "rastgele", "kick": "at", "move": "taşı",
           "card": "komut kartı", "custom": "kendi resmim", "none": "yok", "cover": "şarkı kapağı", "idle": "boştaki avatar"},
}

L = {
    "en": {"title": "Plugins", "setting": "Setting", "default": "Default", "notes": "Notes", "perm": "TeamSpeak permission",
           "on": "on", "off": "off", "empty": "empty", "feature": "Feature", "defaultOn": "on by default", "defaultOff": "off by default",
           "image": "uploaded image", "plan": "plan editor", "ranks": "rank editor", "channels": "channel list", "channel": "channel",
           "intro": ("Plugins are switched on **per bot**: Panel → **Plugins** → pick the bot in the sidebar → click a plugin. "
                     "Plugins with several features show each feature as a card with its own switch, settings and commands. "
                     "Channel fields offer the bot's channels as a list once the bot has connected; you can also type a channel name or ID (`5` or `/5`). "
                     "Group fields take comma-separated server group IDs (TeamSpeak → *Permissions → Server Groups*). "
                     "Message fields support BBCode and open an editor with a live preview.")},
    "tr": {"title": "Eklentiler", "setting": "Ayar", "default": "Varsayılan", "notes": "Açıklama", "perm": "TeamSpeak yetkisi",
           "on": "açık", "off": "kapalı", "empty": "boş", "feature": "Özellik", "defaultOn": "varsayılan açık", "defaultOff": "varsayılan kapalı",
           "image": "yüklenen resim", "plan": "plan düzenleyici", "ranks": "rütbe düzenleyici", "channels": "kanal listesi", "channel": "kanal",
           "intro": ("Eklentiler **her bot için ayrı** açılır: Panel → **Eklentiler** → kenar çubuğundan botu seç → bir eklentiye tıkla. "
                     "Birden çok özelliği olan eklentilerde her özellik kendi anahtarı, ayarları ve komutlarıyla bir karttır. "
                     "Kanal alanları, bot bir kez bağlandıktan sonra botun kanallarını liste olarak gösterir; kanal adını ya da ID'sini (`5` veya `/5`) yazabilirsin de. "
                     "Grup alanlarına virgülle ayrılmış sunucu grubu ID'leri yazılır (TeamSpeak → *İzinler → Sunucu Grupları*). "
                     "Mesaj alanları BBCode destekler ve canlı önizlemeli bir editör açar.")},
}


def cell(s):
    return str(s).replace("|", "\\|").replace("\n", " ")


def default_text(f, lang):
    t, d = f["type"], f.get("default")
    l = L[lang]
    if t == "bool":
        return l["on"] if d in (True, "true") else l["off"]
    if t == "avatarimg":
        return "—"
    if t == "schedule":
        return l["plan"]
    if t == "ranks":
        return l["ranks"]
    if d in (None, ""):
        return l["empty"]
    if t == "select":
        return OPT[lang].get(str(d), str(d))
    if t in ("textarea", "text") and isinstance(d, str) and len(d) > 48:
        return "`" + cell(d.split("\n")[0][:45]) + "…`"
    return "`" + cell(d) + "`"


def notes(f, lang):
    parts = []
    t = f["type"]
    if t == "select" and f.get("options"):
        parts.append(" / ".join(OPT[lang].get(o, o) for o in f["options"]))
    h = (f.get("help") or {}).get(lang)
    if h:
        parts.append(h)
    s = " — ".join(parts)
    return cell(en_cmds(s) if lang == "en" else s)


def plugins_page(data, lang):
    l = L[lang]
    out = [f"# {l['title']}", "", l["intro"] if lang == "tr" else en_cmds(l["intro"]), ""]
    for g in data:
        name = g["name"][lang]
        desc = g["description"][lang]
        out += [f"## {name}", "", en_cmds(desc) if lang == "en" else desc, ""]
        # split into features
        feats, cur = [], None
        multi = any(f["type"] == "section" for f in g["schema"])
        for f in g["schema"]:
            if f["type"] == "section":
                cur = {"f": f, "fields": []}
                feats.append(cur)
            else:
                if not multi:
                    if not feats:
                        cur = {"f": None, "fields": []}
                        feats.append(cur)
                cur["fields"].append(f)
        for ft in feats:
            pid = ft["f"]["key"] if ft["f"] else (g["parts"][0]["id"] if g.get("parts") else "")
            if ft["f"]:
                fl = ft["f"]
                state = l["defaultOn"] if fl.get("default") in (True, "true") else l["defaultOff"]
                out += [f"### {fl['label'][lang]}", "", f"*{state}*", ""]
                h = (fl.get("help") or {}).get(lang)
                if h:
                    out += [en_cmds(h) if lang == "en" else h, ""]
            if ft["fields"]:
                out += [f"| {l['setting']} | {l['default']} | {l['notes']} |", "|---|---|---|"]
                for f in ft["fields"]:
                    lab = f["label"][lang]
                    lab = en_cmds(lab) if lang == "en" else lab
                    out.append(f"| {cell(lab)} | {default_text(f, lang)} | {notes(f, lang)} |")
                out.append("")
            perm = (PERMS if lang == "en" else PERMS_TR).get(pid)
            if perm and perm != "—":
                out += [f"**{l['perm']}:** {perm}", ""]
    return "\n".join(out).rstrip() + "\n"


# ------------------------------------------------------------------ commands
# (turkish name, usage args, plugin, en description, tr description)
CMDS = [
    ("ara", "<song or link>", "<şarkı ya da link>", "music", "Search YouTube or open a link and play / queue it", "YouTube'da arar ya da linki açar, çalar veya kuyruğa ekler"),
    ("link", "<link>", "<link>", "music", "Play a link directly (YouTube, SoundCloud, Twitch, stream URL…)", "Linki doğrudan çalar (YouTube, SoundCloud, Twitch, yayın adresi…)"),
    ("dosya", "<name>", "<ad>", "music", "Queue a file from the panel's music library", "Panel kütüphanesinden bir dosyayı kuyruğa ekler"),
    ("radyo", "[name]", "[isim]", "radio", "Play a radio station from the panel, or list them", "Paneldeki bir radyoyu çalar ya da radyoları listeler"),
    ("gec", "", "", "music / radio", "Next song (starts a vote when vote skip is on)", "Sonraki şarkı (oylamayla geç açıksa oy verir)"),
    ("skip", "", "", "music / radio", "Same as next", "Geç ile aynı"),
    ("dur", "", "", "music / radio", "Pause / resume (toggle)", "Duraklat / devam ettir"),
    ("duraklat", "", "", "music / radio", "Pause", "Duraklat"),
    ("devam", "", "", "music / radio", "Resume", "Devam ettir"),
    ("bitir", "", "", "music / radio", "Stop and clear the queue", "Durdurur ve kuyruğu temizler"),
    ("kapat", "", "", "music / radio", "Same as stop", "Bitir ile aynı"),
    ("ses", "[0-100]", "[0-100]", "music / radio", "Show or set the volume", "Ses seviyesini gösterir ya da ayarlar"),
    ("calan", "", "", "music / radio", "Now playing", "Çalan şarkı"),
    ("siradaki", "", "", "music / radio", "Show the queue", "Kuyruğu gösterir"),
    ("temizle", "", "", "music / radio", "Clear the queue (the playing song keeps going)", "Kuyruğu temizler (çalan şarkı devam eder)"),
    ("liste", "[save|play|add|show|delete] <name>", "[kaydet|cal|ekle|goster|sil] <ad>", "music", "Playlists: save the queue, play, add the current song, show, delete; alone it lists them", "Çalma listeleri: kuyruğu kaydet, çal, çalanı ekle, göster, sil; tek başına listeleri gösterir"),
    ("fav", "[del <no>]", "[sil <no>]", "favorites", "Add the playing song to your favourites (or remove one)", "Çalan şarkıyı favorilerine ekler (ya da birini siler)"),
    ("favs", "[list]", "[liste]", "favorites", "Queue your favourites (or list them)", "Favorilerini kuyruğa ekler (ya da listeler)"),
    ("istatistik", "", "", "music", "Most played songs and top requesters of the last 30 days", "Son 30 günün en çok çalınanları ve en çok şarkı açanları"),
    ("soz", "[song]", "[şarkı]", "fun (lyrics)", "Lyrics of the playing (or another) song, sent privately", "Çalan (ya da başka) şarkının sözleri, özel mesajla"),
    ("anket", "question | a | b", "soru | a | b", "fun (polls)", "Start a poll (`end` / `bitir` ends it); posted in the channel", "Anket başlatır (`bitir` ile biter); kanala gönderilir"),
    ("oy", "<no>", "<no>", "fun (polls)", "Vote in the poll; the current percentages are shared in the channel", "Ankette oy ver; güncel yüzdeler kanalda paylaşılır"),
    ("hava", "<city>", "<şehir>", "fun (weather)", "Weather", "Hava durumu"),
    ("doviz", "<code> [amount]", "<kod> [miktar]", "fun (currency)", "Exchange rate", "Döviz kuru"),
    ("dolar", "[amount]", "[miktar]", "fun (currency)", "US dollar rate", "Dolar kuru"),
    ("euro", "[amount]", "[miktar]", "fun (currency)", "Euro rate", "Euro kuru"),
    ("zar", "[2d6]", "[2d6]", "fun (dice)", "Roll dice", "Zar atar"),
    ("yazitura", "", "", "fun (dice)", "Flip a coin", "Yazı tura atar"),
    ("sec", "a | b | c", "a | b | c", "fun (dice)", "Pick one of the options", "Seçeneklerden birini seçer"),
    ("kayit", "<name>", "<isim>", "registration", "Register a waiting user (staff groups only)", "Bekleyen kullanıcıyı kaydeder (yalnızca yetkili gruplar)"),
    ("sure", "", "", "ranks", "Your total time on the server and your place", "Sunucudaki toplam süren ve sıran"),
    ("top", "", "", "ranks", "Most active users", "En aktif kullanıcılar"),
    ("gel", "", "", "—", "Bring the bot to your channel", "Botu kanalına çağırır"),
    ("gorulme", "<name>", "<isim>", "—", "Whether someone is online, or when they were last seen", "Biri şu an çevrimiçi mi, değilse en son ne zaman görüldü"),
    ("yardim", "", "", "—", "All commands of the enabled plugins", "Açık eklentilerin tüm komutları"),
]

CMD_INTRO = {
    "en": ("Command names follow the panel language: **Turkish** names when the panel is in Turkish, **English** names in every "
           "other language. The Turkish names always keep working, and every command can be renamed in the panel "
           "(Plugins → open a plugin → *Commands*, or the *General commands* tile). A command whose plugin is off answers "
           "\"this feature is off\"; `!help` only lists the commands of enabled plugins.\n\n"
           "Commands work in the bot's channel chat, in a private chat with the bot and in the server chat."),
    "tr": ("Komut adları panel diline göre gelir: panel Türkçe ise **Türkçe**, diğer tüm dillerde **İngilizce** adlar. "
           "Türkçe adlar her zaman çalışır ve her komutun adı panelden değiştirilebilir (Eklentiler → bir eklenti → *Komutlar*, "
           "ya da *Genel komutlar* kartı). Eklentisi kapalı bir komut \"bu özellik kapalı\" cevabını verir; `!yardim` yalnızca açık "
           "eklentilerin komutlarını listeler.\n\n"
           "Komutlar botun kanalında, botla özel sohbette ve sunucu sohbetinde çalışır."),
}
PLUGIN_TR = {"music": "müzik", "radio": "radyo", "music / radio": "müzik / radyo", "favorites": "favoriler", "registration": "kayıt",
             "ranks": "rütbeler", "fun (lyrics)": "eğlence (sözler)", "fun (polls)": "eğlence (anket)", "fun (weather)": "eğlence (hava)",
             "fun (currency)": "eğlence (döviz)", "fun (dice)": "eğlence (zar)", "—": "—"}


def commands_page(lang):
    if lang == "en":
        out = ["# Commands", "", CMD_INTRO["en"], "", "| Command | Turkish name | Plugin | What it does |", "|---|---|---|---|"]
        for tr, a_en, _a_tr, plugin, d_en, _d in CMDS:
            name = EN_NAMES.get(tr, tr)
            out.append(f"| `!{name}{(' ' + cell(a_en)) if a_en else ''}` | `!{tr}` | {plugin} | {cell(d_en)} |")
    else:
        out = ["# Komutlar", "", CMD_INTRO["tr"], "", "| Komut | İngilizce adı | Eklenti | Ne yapar |", "|---|---|---|---|"]
        for tr, _a_en, a_tr, plugin, _d_en, d_tr in CMDS:
            out.append(f"| `!{tr}{(' ' + cell(a_tr)) if a_tr else ''}` | `!{EN_NAMES.get(tr, tr)}` | {PLUGIN_TR.get(plugin, plugin)} | {cell(d_tr)} |")
    ex = {
        "en": ["", "## Examples", "",
               "- `!play tarkan kuzu kuzu` — searches YouTube and queues the first result",
               "- `!playlist save rock` · `!playlist play rock` · `!playlist add rock` · `!playlist show rock` · `!playlist delete rock`",
               "- `!poll What do we play tonight? | CS2 | LoL | Valorant` → everyone types `!vote 2`; `!poll end` ends it",
               "- `!weather istanbul` · `!fx EUR 50` · `!dice 2d6` · `!pick pizza | burger | sushi`",
               "- `!fav` · `!favs` · `!favs list` · `!fav del 3`",
               "- `!lastseen John` · `!come` · `!file intro`",
               "",
               "TS3AudioBot's own commands (`!play`, `!add`, `!list`, `!volume`, …) also exist; they follow the same plugin switches."],
        "tr": ["", "## Örnekler", "",
               "- `!ara tarkan kuzu kuzu` — YouTube'da arar, ilk sonucu kuyruğa ekler",
               "- `!liste kaydet rock` · `!liste cal rock` · `!liste ekle rock` · `!liste goster rock` · `!liste sil rock`",
               "- `!anket Bu akşam ne oynayalım? | CS2 | LoL | Valorant` → herkes `!oy 2` yazar; `!anket bitir` ile biter",
               "- `!hava istanbul` · `!doviz EUR 50` · `!zar 2d6` · `!sec pizza | burger | sushi`",
               "- `!fav` · `!favs` · `!favs liste` · `!fav sil 3`",
               "- `!gorulme Ahmet` · `!gel` · `!dosya intro`",
               "",
               "TS3AudioBot'un kendi komutları (`!play`, `!add`, `!list`, `!volume`…) da vardır; onlar da aynı eklenti anahtarlarına bağlıdır."],
    }
    out += ex[lang]
    return "\n".join(out) + "\n"


def main():
    data = json.load(open(DATA, encoding="utf-8"))
    for lang in ("en", "tr"):
        os.makedirs(os.path.join(OUT, lang), exist_ok=True)
        open(os.path.join(OUT, lang, "plugins.md"), "w", encoding="utf-8", newline="\n").write(plugins_page(data, lang))
        open(os.path.join(OUT, lang, "commands.md"), "w", encoding="utf-8", newline="\n").write(commands_page(lang))
    print("ok")


if __name__ == "__main__":
    main()
