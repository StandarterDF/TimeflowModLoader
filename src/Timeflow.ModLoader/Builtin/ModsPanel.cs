using System.Collections.Generic;
using HarmonyLib;
using Timeflow.ModLoader.Logging;
using UnityEngine;
using UnityEngine.UI;
using UIText = UnityEngine.UI.Text;

namespace Timeflow.ModLoader.Builtin
{
    /// <summary>
    /// Native in-game Mods view. Instead of drawing an external overlay, it swaps the contents of
    /// the main menu's own <c>Paper</c> panel: the regular menu buttons are hidden and replaced
    /// with one menu-style button per mod plus a "Back" button. Because it reuses the menu's own
    /// panel and buttons, it is laid out, scaled and drawn exactly like the rest of the game.
    ///
    /// Clicking a mod's button enables/disables it immediately (hot toggle) and the choice is
    /// persisted by <see cref="ModManager.SetEnabled"/>.
    /// </summary>
    internal sealed class ModsPanel
    {
        public static ModsPanel Instance { get; } = new ModsPanel();

        private Transform _paper;
        private GameObject _template;

        private readonly List<GameObject> _originals = new List<GameObject>();
        private readonly List<bool> _originalActive = new List<bool>();
        private readonly List<GameObject> _spawned = new List<GameObject>();

        private bool _open;

        public bool IsOpen => _open;

        public void Toggle(Transform paper, GameObject template)
        {
            if (_open)
                Close();
            else
                Open(paper, template);
        }

        public void Open(Transform paper, GameObject template)
        {
            if (_open || paper == null || template == null)
                return;

            _paper = paper;
            _template = template;

            CaptureOriginals();
            SpawnHeader();
            SpawnModRows();
            SpawnBackButton();

            _open = true;
            ModLog.Info($"[mods] panel opened ({ModManager.All.Count} mod(s)).");
        }

        public void Close()
        {
            if (!_open)
                return;

            foreach (var spawned in _spawned)
            {
                if (spawned != null)
                    Object.Destroy(spawned);
            }
            _spawned.Clear();

            for (var i = 0; i < _originals.Count && i < _originalActive.Count; i++)
            {
                if (_originals[i] != null)
                    _originals[i].SetActive(_originalActive[i]);
            }

            _open = false;
            ModLog.Info("[mods] panel closed.");
        }

        // ----------------------------------------------------------------- build

        private void CaptureOriginals()
        {
            _originals.Clear();
            _originalActive.Clear();

            for (var i = 0; i < _paper.childCount; i++)
            {
                var child = _paper.GetChild(i).gameObject;
                _originals.Add(child);
                _originalActive.Add(child.activeSelf);
                child.SetActive(false);
            }
        }

        private void SpawnHeader()
        {
            var header = SpawnRow(Texts.Mods, null);
            var button = header.GetComponent<Button>();
            if (button != null)
                Object.Destroy(button);

            var pressing = FindBehaviour(header, "ButtonPressing");
            if (pressing != null)
                Object.Destroy(pressing);

            var image = header.GetComponent<Image>();
            if (image != null)
                image.color = new Color(0f, 0f, 0f, 0f); // text-only header

            var label = header.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (label != null)
                label.color = new Color(0.24f, 0.18f, 0.12f, 1f); // dark, readable on the paper
        }

        private void SpawnModRows()
        {
            var entries = ModManager.All;
            if (entries.Count == 0)
            {
                SpawnRow(Texts.NoMods, null);
                return;
            }

            foreach (var entry in entries)
            {
                var modId = entry.Info.Id;
                GameObject row = null;
                row = SpawnRow(RowLabel(modId), () =>
                {
                    ModManager.SetEnabled(modId, !IsLoaded(modId));
                    RefreshLabel(row, modId);
                });
            }
        }

        private void SpawnBackButton()
        {
            SpawnRow(Texts.Back, Close);
        }

        private GameObject SpawnRow(string label, System.Action onClick)
        {
            var row = Object.Instantiate(_template, _paper);
            row.name = "Timeflow_ModsRow";
            row.SetActive(true);
            StripGameComponents(row);

            SetLabel(row, label);

            var button = row.GetComponent<Button>();
            if (button != null)
            {
                button.onClick = new Button.ButtonClickedEvent();
                if (onClick != null)
                    button.onClick.AddListener(() => onClick());
            }

            _spawned.Add(row);
            return row;
        }

        // ----------------------------------------------------------------- labels

        private void RefreshLabel(GameObject row, string modId)
        {
            SetLabel(row, RowLabel(modId));
        }

        private static string RowLabel(string modId)
        {
            var entry = Find(modId);
            if (entry == null)
                return modId;

            return entry.Info.Name + (entry.Loaded ? Texts.On : Texts.Off)
                   + (string.IsNullOrEmpty(entry.Error) ? string.Empty : Texts.ErrorSuffix);
        }

        private static bool IsLoaded(string modId)
        {
            var entry = Find(modId);
            return entry != null && entry.Loaded;
        }

        private static ModManager.ModEntry Find(string modId)
        {
            foreach (var entry in ModManager.All)
            {
                if (string.Equals(entry.Info.Id, modId, System.StringComparison.OrdinalIgnoreCase))
                    return entry;
            }
            return null;
        }

        private static void SetLabel(GameObject go, string text)
        {
            var tmp = go.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = text;
                // Keep every row on a single line, exactly like the game's own menu buttons.
                // Long mod names simply shrink a little instead of wrapping onto two lines.
                var naturalSize = tmp.fontSize;
                tmp.enableWordWrapping = false;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 8f;
                tmp.fontSizeMax = naturalSize;
                return;
            }

            var uiText = go.GetComponentInChildren<UIText>(true);
            if (uiText != null)
                uiText.text = text;
        }

        /// <summary>
        /// Removes components that would fight our custom behaviour: the game's localisation
        /// writer, its hint bubble and its EventTrigger (which can re-fire clicks).
        /// </summary>
        private static void StripGameComponents(GameObject go)
        {
            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;
                var name = behaviour.GetType().Name;
                if (name == "Localization" || name.EndsWith("Localization", System.StringComparison.Ordinal)
                    || name == "EventTrigger" || name == "HintBubble")
                {
                    Object.Destroy(behaviour);
                }
            }
        }

        private static MonoBehaviour FindBehaviour(GameObject go, string typeName)
        {
            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour != null && behaviour.GetType().Name == typeName)
                    return behaviour;
            }
            return null;
        }
    }
}
