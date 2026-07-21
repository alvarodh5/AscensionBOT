# AscensionBot

An in-process grinding bot for **[Project Ascension](https://ascension.gg/)** (a custom WoW 3.3.5a server with a classless / "build your own hero" system and level-scaled zones).

It is a heavily trimmed and adapted fork of **[DrewKestell/BloogBot](https://github.com/DrewKestell/BloogBot)** — all the low-level memory/injection groundwork comes from that project.

> 🌐 **[English](#english)** · **[Español](#español)**

---

## English

### ⚠️ Disclaimer

This is a personal, educational project about reverse engineering and game memory. Using a bot **violates the server's Terms of Service and will get your account banned** if detected — Project Ascension has its own anti-cheat and bans in waves. **Use a throwaway account you don't mind losing.** It does **not** work on retail WoW. You are responsible for what you do with this.

### What it does

A simple, autonomous grinder: it finds a nearby mob, walks to it, kills it, and repeats — indefinitely. Because Ascension is classless and zones scale to your level, the bot doesn't need class logic or travel routes; it just uses whatever abilities you put on your action bar.

### Profiles

| Profile | Behaviour |
|---|---|
| **Caster** | Ranged. Engages from 20 yds, casts random abilities from action slots **1/2/3**, self-heals with slot **4** below 30% HP, and recasts a self-buff on slot **5** every ~30 min. Only starts a fight at ≥75% HP and ≥50% mana (but defends itself if attacked), rests after every fight. Looting is **off by default** (enable with `CasterLootEnabled`). Sends a Telegram session report. |
| **Melee** | Classless melee. Walks into melee range and spams action slots **1–6** + auto-attack, loots and skins. Includes a `Test` diagnostic that dumps player/offset info. |

### Features

- **In-process DLL injection** into the running `Ascension.exe` (attach to a client already launched by the Ascension launcher).
- **Classless combat** — no spell names, no class assumptions; it just presses your action-bar buttons.
- **Straight-line movement** — no navmesh/mmaps required (best on open terrain).
- **Death recovery** — resurrects at the Spirit Healer and waits out Resurrection Sickness before fighting again.
- **Telegram notifications** — level ups, deaths/resurrects, rare loot, a stats report every 30 minutes, and on stop.
- **Disconnect handling** — detects a lost connection (≥5s) and stops with a final report; a **watchdog in the Bootstrapper** alerts you on Telegram if the game process itself crashes or closes.
- **Minimal UI** — pick a profile, then Start / Stop / Test.
- **No database** — runs with `DatabaseType: "none"`, no SQL setup needed.

### Requirements

- Windows
- Visual Studio 2022 with **.NET Framework 4.6.1** and the **C++ build tools** (for the native Loader/FastCall/Navigation projects)
- A Project Ascension client + launcher (reports client build **3.3.5.12340**)

### Build & run

1. Open `AscensionBot.sln` in Visual Studio 2022.
2. **Restore NuGet packages** and **Rebuild Solution** (this also builds the native C++ projects). Output lands in the `Bot\` folder.
3. Launch Ascension through its own launcher and log in to the character screen (or into the world).
4. Run **`Bootstrapper.exe` as Administrator** (injecting into a process you didn't create needs admin rights). It injects the bot and then stays open as a **watchdog** — keep that console window open.
5. In the AscensionBot window: select a profile, place your abilities on the action bar, and press **Start**.

### Configuration (`botSettings.json`)

Sensible defaults live in code, so the file is tiny — you only override what you need:

```json
{
  "CurrentBotName": "Caster",
  "TelegramEnabled": false,
  "TelegramBotToken": "",
  "TelegramChatId": "",
  "Food": "",
  "Drink": ""
}
```

**Telegram** (optional): create a bot with [@BotFather](https://t.me/BotFather) for the token, get your chat id from [@userinfobot](https://t.me/userinfobot), set `TelegramEnabled` to `true`, and **message your bot once** so it's allowed to write to you.

### Action bar setup

- **Caster** → attacks on slots **1, 2, 3**; heal on slot **4**; self-buff (recast every ~30 min) on slot **5**.
- **Melee** → your abilities on slots **1–6**.

### Credits & License

Built on top of **[BloogBot](https://github.com/DrewKestell/BloogBot)** by Drew Kestell. Licensed under **MIT** (see [LICENSE](LICENSE)). This fork keeps the same license.

---

## Español

### ⚠️ Aviso

Este es un proyecto personal y educativo sobre ingeniería inversa y memoria de juegos. Usar un bot **viola los Términos de Servicio del servidor y te banearán la cuenta** si te detectan — Project Ascension tiene su propio anticheat y banea en oleadas. **Usa una cuenta desechable que no te importe perder.** **No** funciona en el WoW oficial. Eres responsable del uso que le des.

### Qué hace

Un farmeador simple y autónomo: busca un bicho cercano, va hacia él, lo mata y repite — sin parar. Como Ascension es classless y las zonas escalan a tu nivel, el bot no necesita lógica de clases ni rutas de viaje; solo usa las habilidades que pongas en tu barra de acción.

### Perfiles

| Perfil | Comportamiento |
|---|---|
| **Caster** | A distancia. Ataca desde 20 yardas, lanza habilidades aleatorias de los slots **1/2/3**, se cura con el slot **4** por debajo del 30% de vida y relanza un buff propio en el slot **5** cada ~30 min. Solo inicia combate con ≥75% de vida y ≥50% de maná (pero se defiende si le atacan), descansa tras cada pelea. El loot está **desactivado por defecto** (actívalo con `CasterLootEnabled`). Envía un informe de sesión por Telegram. |
| **Melee** | Cuerpo a cuerpo classless. Se acerca y machaca los slots **1–6** + auto-ataque, lootea y desuella. Incluye un `Test` de diagnóstico que vuelca info del jugador/offsets. |

### Características

- **Inyección de DLL en el proceso** `Ascension.exe` (se engancha a un cliente ya abierto por el launcher de Ascension).
- **Combate classless** — sin nombres de hechizos ni suposiciones de clase; solo pulsa los botones de tu barra.
- **Movimiento en línea recta** — no requiere navmesh/mmaps (ideal en terreno abierto).
- **Recuperación tras morir** — resucita en el Sanador de Espíritus y espera a que pase la enfermedad por resurrección antes de volver a pelear.
- **Avisos por Telegram** — subidas de nivel, muertes/resurrecciones, botín raro, un informe de estadísticas cada 30 minutos y al detenerse.
- **Gestión de desconexión** — detecta pérdida de conexión (≥5s) y se detiene con informe final; un **vigilante en el Bootstrapper** te avisa por Telegram si el proceso del juego crashea o se cierra.
- **UI mínima** — eliges perfil y Start / Stop / Test.
- **Sin base de datos** — funciona con `DatabaseType: "none"`, sin configurar SQL.

### Requisitos

- Windows
- Visual Studio 2022 con **.NET Framework 4.6.1** y las **C++ build tools** (para los proyectos nativos Loader/FastCall/Navigation)
- Un cliente + launcher de Project Ascension (reporta build **3.3.5.12340**)

### Compilar y ejecutar

1. Abre `AscensionBot.sln` en Visual Studio 2022.
2. **Restaura los paquetes NuGet** y haz **Rebuild Solution** (compila también los proyectos C++ nativos). La salida va a la carpeta `Bot\`.
3. Abre Ascension con su launcher y entra a la pantalla de personajes (o al mundo).
4. Ejecuta **`Bootstrapper.exe` como Administrador** (inyectar en un proceso que no creaste tú requiere permisos de admin). Inyecta el bot y se queda abierto como **vigilante** — deja esa ventana de consola abierta.
5. En la ventana de AscensionBot: elige un perfil, coloca tus habilidades en la barra y pulsa **Start**.

### Configuración (`botSettings.json`)

Los valores por defecto están en el código, así que el archivo es diminuto — solo sobrescribes lo que necesitas:

```json
{
  "CurrentBotName": "Caster",
  "TelegramEnabled": false,
  "TelegramBotToken": "",
  "TelegramChatId": "",
  "Food": "",
  "Drink": ""
}
```

**Telegram** (opcional): crea un bot con [@BotFather](https://t.me/BotFather) para el token, saca tu chat id de [@userinfobot](https://t.me/userinfobot), pon `TelegramEnabled` en `true` y **escríbele un mensaje a tu bot una vez** para que pueda escribirte.

### Barra de acción

- **Caster** → ataques en los slots **1, 2, 3**; cura en el slot **4**; buff propio (se relanza cada ~30 min) en el slot **5**.
- **Melee** → tus habilidades en los slots **1–6**.

### Créditos y licencia

Construido sobre **[BloogBot](https://github.com/DrewKestell/BloogBot)** de Drew Kestell. Licencia **MIT** (ver [LICENSE](LICENSE)). Este fork mantiene la misma licencia.
