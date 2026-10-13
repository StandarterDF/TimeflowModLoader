using System;
using System.Reflection;
using HarmonyLib;
using Timeflow.ModLoader.Logging;
using Timeflow.ModLoader.Patching;
using UnityEngine;
using UnityEngine.UI;

namespace Timeflow.ModLoader.Builtin
{
    /// <summary>
    /// Adds a "Mods" entry to the game's main menu (<c>MenuView</c>) that switches the menu panel
    /// into the native mod list (see <see cref="ModsPanel"/>). Implemented via reflection + Harmony
    /// so the loader keeps no compile-time dependency on Assembly-CSharp.
    ///
    /// The button is a clone of the existing "Settings" button, so it inherits the game's exact look.
    /// </summary>
    internal static class ModMenuButton
    {
        private static bool _installed;
        private static FieldInfo _settingsField;
        private static Button _button;
        private static Transform _paper;
        private static GameObject _template;

        public static void Install()
        {
            if (_installed)
                return;
            _installed = true;

            var menuViewType = AccessTools.TypeByName("MenuView");
            var setupMethod = menuViewType != null ? AccessTools.Method(menuViewType, "SetupMenuView") : null;
            _settingsField = menuViewType != null ? AccessTools.Field(menuViewType, "_settingsButton") : null;

            if (setupMethod == null || _settingsField == null)
            {
                ModLog.Warn("MenuView.SetupMenuView/_settingsButton not found; the Mods button will not be added.");
                return;
            }

            var postfix = new HarmonyMethod(typeof(ModMenuButton).GetMethod(
                nameof(Postfix), BindingFlags.Static | BindingFlags.NonPublic));

            try
            {
                HarmonyProvider.Root.Patch(setupMethod, postfix: postfix);
                ModLog.Info("Main-menu 'Mods' button installed.");
            }
            catch (Exception ex)
            {
                ModLog.Error($"Failed to patch MenuView.SetupMenuView: {ex}");
            }
        }

        [HarmonyPostfix]
        private static void Postfix(object __instance)
        {
            try
            {
                EnsureButton(__instance);
            }
            catch (Exception ex)
            {
                ModLog.Error("Mods button: " + ex);
            }
        }

        private static void EnsureButton(object menuView)
        {
            if (menuView == null || _settingsField == null)
                return;

            var settings = _settingsField.GetValue(menuView) as Button;
            if (settings == null)
                return;

            var parent = settings.transform.parent;
            if (parent == null)
                return;

            if (_button == null)
            {
                if (!Application.isPlaying)
                    return;

                _paper = parent;
                _template = settings.gameObject;

                var clone = UnityEngine.Object.Instantiate(settings.gameObject, parent, false);
                clone.name = "Timeflow_ModsButton";
                StripGameComponents(clone);

                _button = clone.GetComponent<Button>();
                if (_button == null)
                {
                    UnityEngine.Object.Destroy(clone);
                    return;
                }

                _button.onClick = new Button.ButtonClickedEvent();
                _button.onClick.AddListener(() => ModsPanel.Instance.Toggle(_paper, _template));
                ModLog.Info("Mods button created in the main menu.");
            }

            SetLabel(_button.gameObject, Texts.Mods);
            _button.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1);

            if (_button.gameObject.activeSelf != settings.gameObject.activeSelf)
                _button.gameObject.SetActive(settings.gameObject.activeSelf);
        }

        private static void SetLabel(GameObject go, string text)
        {
            var tmp = go.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = text;
                return;
            }

            var uiText = go.GetComponentInChildren<Text>(true);
            if (uiText != null)
                uiText.text = text;
        }

        private static void StripGameComponents(GameObject go)
        {
            foreach (var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;
                var name = behaviour.GetType().Name;
                if (name == "Localization" || name.EndsWith("Localization", StringComparison.Ordinal)
                    || name == "EventTrigger" || name == "HintBubble")
                {
                    UnityEngine.Object.Destroy(behaviour);
                }
            }
        }
    }
}
