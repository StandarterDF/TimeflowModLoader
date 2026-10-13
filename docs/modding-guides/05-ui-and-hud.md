# 05 — UI: HUDs and native menus

Two ways to put something on screen:

1. **IMGUI** (`OnGUI`) — instant, private, great for debug HUDs. Used by the trainer and the F10
   console.
2. **Native uGUI** — clone the game's own widgets so your menu looks exactly like the rest of
   Timeflow. This is how the **Моды / Mods** menu is built.

---

## Part 1 — IMGUI HUD (`OnGUI`)

Every loaded mod gets `OnGUI()` called each frame (the loader's own `MonoBehaviour` forwards it).
`GUILayout` handles layout for you.

```csharp
using UnityEngine;

public override void OnGUI()
{
    if (!_visible) return;

    GUILayout.BeginArea(new Rect(10, 10, 320, 170), GUI.skin.box);
    GUILayout.Label("<b>My HUD</b>  (F4 to hide)");

    var p = GameManager.instance?.currentPlayerScript;
    if (p == null) GUILayout.Label("no active player");
    else
    {
        GUILayout.Label($"Money : {p.money:N0}");
        GUILayout.Label($"Mood  : {p.mood}");
        GUILayout.Label($"Time  : {p.time}");
    }
    GUILayout.EndArea();
}
```

**Verified:** `example.hud` draws exactly this and toggles with **F4**; the trainer draws a second
HUD simultaneously.

Notes:

* IMGUI does **not** scale with the game's UI scale, which is usually desirable for debug info.
* Cache strings where possible; `OnGUI` runs multiple times per frame (layout + repaint).
* Rich text (`<b>`, `<color=...>`) works in `GUILayout.Label`.
* Use `KeyCode` hotkeys in `OnUpdate`, not `OnGUI`.

---

## Part 2 — Native uGUI (the "looks built-in" approach)

### Golden rule: clone a *live widget from the same screen*

Cloning the whole **Settings window** does not render (observed: the clone never appeared). What
does work reliably is cloning a **button that is already part of the visible panel** and wiring a
new click handler.

The loader does this in two steps:

1. `ModMenuButton` patches `MenuView.SetupMenuView` and, when it runs, clones the existing
   `_settingsButton` and inserts a **"Моды"** button right after it.
2. `ModsPanel` hides the menu panel's children and spawns clones of that button for each mod, plus
   a header and a **"Назад"** button.

```csharp
// Clone the settings button into the same parent (the menu "Paper").
var clone = Object.Instantiate(settings.gameObject, settings.transform.parent, false);
clone.name = "Timeflow_ModsButton";
StripGameComponents(clone);                         // see below

var button = clone.GetComponent<Button>();
button.onClick = new Button.ButtonClickedEvent();   // drop the game's listeners
button.onClick.AddListener(() => ModsPanel.Instance.Toggle(paper, template));
```

### Strip the game components from the clone

Cloned buttons carry behaviours that fight your custom behaviour:

| Component (by type name) | Why remove |
|--------------------------|------------|
| `Localization` / `*Localization` | It would overwrite your label text every frame with a localisation key. |
| `EventTrigger` | It can re-fire clicks / hover logic. |
| `HintBubble` | It shows the original button's tooltip. |

```csharp
foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true))
{
    var n = b.GetType().Name;
    if (n == "Localization" || n.EndsWith("Localization") || n == "EventTrigger" || n == "HintBubble")
        Object.Destroy(b);
}
```

### Set the label (TMP first, legacy fallback)

```csharp
var tmp = go.GetComponentInChildren<TMPro.TMP_Text>(true);
if (tmp != null)
{
    tmp.text = text;
    // keep long labels on one line, like the game's own buttons
    var natural = tmp.fontSize;
    tmp.enableWordWrapping = false;
    tmp.enableAutoSizing = true;
    tmp.fontSizeMin = 8f;
    tmp.fontSizeMax = natural;
}
else
{
    var t = go.GetComponentInChildren<UnityEngine.UI.Text>(true);
    if (t != null) t.text = text;
}
```

### Swap a panel's contents

`ModsPanel` "opens" by hiding all children of the menu's `Paper` and spawning its own rows; it
"closes" by destroying them and restoring the originals' active state. This keeps the game's
layout component in charge, so spacing is automatically native.

```csharp
// open
for (var i = 0; i < paper.childCount; i++) { var c = paper.GetChild(i).gameObject; originals.Add(c); originalActive.Add(c.activeSelf); c.SetActive(false); }
// ... spawn rows into paper ...
// close
foreach (var o in spawned) Object.Destroy(o);
for (var i = 0; i < originals.Count; i++) originals[i].SetActive(originalActive[i]);
```

**Verified:** the **Моды** menu opens in both the main menu and the in-game pause menu, shows one
row per mod ("Lucky Dice — вкл." / "— выкл."), toggles live, and **Назад** restores the menu
exactly.

---

## Part 3 — Opening the game's own windows

Instead of building UI, drive the game's UI via `UIManager.instance` (methods are mostly
`async UniTask`):

```csharp
var ui = UIManager.instance;

// A toast
await ui.ShowNotification("MyMod", "Hello!", someCancellationToken);

// A Yes/No dialog with a callback
await ui.ShowDialogYesNo("MyMod", "Do the thing?", result => { if (result) DoIt(); });

// The card popup, the time editor, settings, ...
await ui.ShowEvent(card);
await ui.ShowTime(editMode: true, fromLoan: false, callCard: null, mayClose: true);
await ui.ShowCardView(ct, card);
ui.ShowSettings();
```

`CancellationToken`: reuse the game's per-player token where possible
(`player.GetCancellationTokenOnDestroy()`), or keep a `CancellationTokenSource` in your mod and
cancel it in `OnUnload`.

For your own text/log line in the game's event feed:

```csharp
LogManager.instance.Message("MyMod did something");   // supports <color=...> rich text
```

---

## Part 4 — Localisation

* `SettingsManager.instance.currentLanguage` is `"ru"` or `"en"` (the build enables those two).
* `Localization.currentLanguage` mirrors it.
* Freelance mod UI should read `Localization.currentLanguage` and pick a string, or use
  `Localization.StringForKey("key")` for existing keys.

The loader's own built-ins use a tiny bilingual helper (`Builtin/Texts.cs`):

```csharp
internal static class Texts
{
    public static string Mods  => Lang("Моды", "Mods");
    public static string Back  => Lang("Назад", "Back");
    public static string On    => Lang(" — вкл.", " — on");
    public static string Off   => Lang(" — выкл.", " — off");
    private static bool Ru => Localization.currentLanguage == "ru";
    private static string Lang(string ru, string en) => Ru ? ru : en;
}
```

For **user-authored** bilingual text (like a custom card), use the card's dictionary setters:
`card.SetTitleDictForAllLangs(...)` or `card.SetTitleDict(new Dictionary<string,string>{ {"ru","..."},{"en","..."} })`.

---

## Automation note (for agents)

When driving the game with a desktop-automation tool:

* Screenshots from Windows MCP are 1280×720 while the screen is 2560×1440 — **multiply image
  coordinates by 2.0** before clicking.
* Move the cursor to a neutral spot before focusing the window: activating a Unity window with the
  cursor over a button generates a real click (`StandaloneInputModule.ReleaseMouse`).
* Keyboard shortcuts sent by window-level automation may not reach Unity; prefer focusing the
  window and sending keys through the OS input queue (e.g. `SendKeys`), which was verified to
  work with the mod hotkeys.

Next: [06 — Config & save data](06-config-and-save-data.md).
