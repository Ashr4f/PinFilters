using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace PinFilters
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class PinFiltersPlugin : BaseUnityPlugin
    {
        public const string ModGuid = "ashr4f.pinfilters";
        public const string ModName = "PinFilters";
        public const string ModVersion = "1.0.4";

        internal static ManualLogSource Log = null!;

        internal static ConfigEntry<bool> Enabled = null!;
        internal static ConfigEntry<string> Hidden = null!;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey = null!;
        internal static ConfigEntry<bool> GroupByIcon = null!;
        internal static ConfigEntry<float> MaxPanelHeight = null!;
        internal static ConfigEntry<float> ButtonOffsetX = null!;
        internal static ConfigEntry<float> ButtonOffsetY = null!;

        private void Awake()
        {
            Log = Logger;

            Enabled = Config.Bind("General", "Enabled", true,
                "Master switch.");

            Hidden = Config.Bind("General", "Hidden Groups", "",
                "Comma-separated groups currently unchecked in the panel. Filled automatically, edit only if you want to preset it.");

            ToggleKey = Config.Bind("General", "Panel Key", new KeyboardShortcut(KeyCode.F),
                "Key that shows or hides the filter panel while the large map is open.");

            GroupByIcon = Config.Bind("General", "Group By Icon", true,
                "Group pins by their icon, so every mod pin type gets its own line even when the mod uses no distinct pin type.");

            MaxPanelHeight = Config.Bind("Panel", "Max Height", 420f,
                "Maximum panel height in pixels. The list scrolls beyond that.");

            ButtonOffsetX = Config.Bind("Panel", "Button Offset X", 0f,
                "Horizontal offset in pixels of the map toggle, relative to the public position toggle.");

            ButtonOffsetY = Config.Bind("Panel", "Button Offset Y", 64f,
                "Vertical offset in pixels of the map toggle, relative to the public position toggle. Raise it if the label overlaps another one.");

            new Harmony(ModGuid).PatchAll();
        }

        // The panel is drawn from the plugin itself: Minimap has no OnGUI to
        // hook into.
        private void OnGUI()
        {
            if (!Enabled.Value || !FilterPanel.Visible) return;
            if (Minimap.instance == null || Minimap.instance.m_mode != Minimap.MapMode.Large)
            {
                FilterPanel.Visible = false;
                return;
            }
            FilterPanel.Draw();
        }
    }

    // ------------------------------------------------------------------
    // Pins are grouped by what the player actually sees: the icon, with the
    // pin type as fallback. Any mod that adds pins is covered, no mod
    // specific knowledge needed.
    // ------------------------------------------------------------------
    internal static class PinGroups
    {
        internal static readonly Dictionary<string, Sprite?> Icons = new Dictionary<string, Sprite?>();
        internal static readonly Dictionary<string, string> Labels = new Dictionary<string, string>();
        internal static readonly HashSet<string> HiddenGroups = new HashSet<string>();

        // The label shown to the player is the pin name itself, localized by
        // the game, so it follows the user language. Nameless pins fall back to
        // the vanilla name of their type.
        internal static string LabelOf(string group)
        {
            if (Labels.TryGetValue(group, out string label) && label.Length > 0) return label;
            return group;
        }

        private static string TypeLabel(Minimap.PinType type)
        {
            switch (type)
            {
                case Minimap.PinType.Death: return FilterPanel.T("Death", "Mort");
                case Minimap.PinType.Bed: return Loc("$piece_bed", FilterPanel.T("Bed", "Lit"));
                case Minimap.PinType.Shout: return FilterPanel.T("Shout", "Cri");
                case Minimap.PinType.Boss: return FilterPanel.T("Boss", "Boss");
                case Minimap.PinType.Player: return FilterPanel.T("Players", "Joueurs");
                case Minimap.PinType.Ping: return FilterPanel.T("Ping", "Ping");
                case Minimap.PinType.RandomEvent: return FilterPanel.T("Event", "Événement");
                case Minimap.PinType.EventArea: return FilterPanel.T("Event area", "Zone d'événement");
                default: return "";
            }
        }

        private static string Loc(string token, string fallback)
        {
            if (Localization.instance == null) return fallback;
            string s = Localization.instance.Localize(token);
            return string.IsNullOrEmpty(s) || s.StartsWith("[") || s == token ? fallback : s;
        }
        private static string _hiddenRaw = "";
        private static bool _synced;

        internal static string GroupOf(Minimap.PinData pin)
        {
            if (PinFiltersPlugin.GroupByIcon.Value && pin.m_icon != null)
            {
                string name = pin.m_icon.name;
                if (!string.IsNullOrEmpty(name)) return name;
            }
            return pin.m_type.ToString();
        }

        internal static void Track(Minimap.PinData pin)
        {
            string group = GroupOf(pin);
            if (!Icons.ContainsKey(group)) Icons[group] = pin.m_icon;

            if (Labels.TryGetValue(group, out string current) && current.Length > 0) return;

            string label = "";
            if (!string.IsNullOrEmpty(pin.m_name) && Localization.instance != null)
                label = Localization.instance.Localize(pin.m_name);
            if (string.IsNullOrEmpty(label)) label = TypeLabel(pin.m_type);
            Labels[group] = label;
        }

        internal static bool IsHidden(string group)
        {
            SyncFromConfig();
            return HiddenGroups.Contains(group);
        }

        internal static void SetHidden(string group, bool hidden)
        {
            SyncFromConfig();
            if (hidden) HiddenGroups.Add(group);
            else HiddenGroups.Remove(group);
            _hiddenRaw = string.Join(", ", new List<string>(HiddenGroups).ToArray());
            PinFiltersPlugin.Hidden.Value = _hiddenRaw;
        }

        private static void SyncFromConfig()
        {
            string raw = PinFiltersPlugin.Hidden.Value;
            if (_synced && raw == _hiddenRaw) return;
            _synced = true;
            _hiddenRaw = raw;
            HiddenGroups.Clear();
            foreach (string part in raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string p = part.Trim();
                if (p.Length > 0) HiddenGroups.Add(p);
            }
        }
    }

    // ------------------------------------------------------------------
    // Soft link to a portal destination picker, if one is installed. Nothing
    // is required, the check simply returns false when absent.
    // ------------------------------------------------------------------
    internal static class PortalPicker
    {
        private static PropertyInfo? _isOpen;
        private static bool _searched;

        internal static bool IsOpen
        {
            get
            {
                if (!_searched)
                {
                    _searched = true;
                    try
                    {
                        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            Type? t = asm.GetType("Bifrost.PortalGui");
                            if (t == null) continue;
                            _isOpen = t.GetProperty("IsOpen", AccessTools.all);
                            break;
                        }
                    }
                    catch
                    {
                        _isOpen = null;
                    }
                }
                if (_isOpen == null) return false;
                try
                {
                    return _isOpen.GetValue(null) is bool b && b;
                }
                catch
                {
                    return false;
                }
            }
        }
    }

    // ------------------------------------------------------------------
    // Hiding is visual and reapplied every frame, so mods that recreate or
    // restamp their pins never fight with the filter. Nothing is deleted.
    // ------------------------------------------------------------------
    internal static class PinHider
    {
        private static readonly List<FieldInfo> _goFields = new List<FieldInfo>();
        private static readonly List<KeyValuePair<FieldInfo, FieldInfo>> _nestedGoFields =
            new List<KeyValuePair<FieldInfo, FieldInfo>>();
        private static bool _searched;
        private static readonly HashSet<CanvasGroup> _touched = new HashSet<CanvasGroup>();

        internal static void Apply(Minimap map)
        {
            if (!PinFiltersPlugin.Enabled.Value || map == null) return;
            // A portal destination picker owns the map while it is open: its
            // markers must stay visible whatever the filters say.
            if (PortalPicker.IsOpen) return;

            foreach (Minimap.PinData pin in map.m_pins)
            {
                try
                {
                    PinGroups.Track(pin);
                    SetVisible(pin, !PinGroups.IsHidden(PinGroups.GroupOf(pin)));
                }
                catch
                {
                    // A single broken pin must never abort the pass.
                }
            }
        }

        private static void SetVisible(Minimap.PinData pin, bool visible)
        {
            if (!_searched) CacheFields();
            foreach (FieldInfo f in _goFields) SetAlpha(f.GetValue(pin), visible);
            foreach (KeyValuePair<FieldInfo, FieldInfo> kv in _nestedGoFields)
            {
                object? mid = kv.Key.GetValue(pin);
                if (mid != null) SetAlpha(kv.Value.GetValue(mid), visible);
            }
        }

        private static void CacheFields()
        {
            _searched = true;
            foreach (FieldInfo f in typeof(Minimap.PinData).GetFields(AccessTools.all))
            {
                if (typeof(GameObject).IsAssignableFrom(f.FieldType) || typeof(Component).IsAssignableFrom(f.FieldType))
                {
                    _goFields.Add(f);
                }
                else if (f.FieldType.IsClass && f.FieldType != typeof(string)
                    && !typeof(UnityEngine.Object).IsAssignableFrom(f.FieldType))
                {
                    foreach (FieldInfo nested in f.FieldType.GetFields(AccessTools.all))
                    {
                        if (typeof(GameObject).IsAssignableFrom(nested.FieldType) || typeof(Component).IsAssignableFrom(nested.FieldType))
                            _nestedGoFields.Add(new KeyValuePair<FieldInfo, FieldInfo>(f, nested));
                    }
                }
            }
        }

        // Alpha through a CanvasGroup survives everything the game and other
        // mods do to the pin object, unlike deactivating it.
        private static void SetAlpha(object? value, bool visible)
        {
            GameObject? go = null;
            if (value is GameObject g && g) go = g;
            else if (value is Component c && c) go = c.gameObject;
            if (go == null) return;

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                if (visible) return;
                cg = go.AddComponent<CanvasGroup>();
            }
            float target = visible ? 1f : 0f;
            if (cg.alpha != target)
            {
                cg.alpha = target;
                cg.blocksRaycasts = visible;
            }
            _touched.Add(cg);
        }
    }

    // ------------------------------------------------------------------
    // Panel drawn with the immediate mode GUI: no prefab, no asset bundle,
    // and no interference with the game UI hierarchy.
    // ------------------------------------------------------------------
    internal static class FilterPanel
    {
        internal static bool Visible;
        internal static bool WantsPanel;
        internal static Rect LastRect;
        private static Vector2 _scroll;
        private static string _search = "";
        private static GUIStyle? _rowStyle;

        internal static void Draw()
        {
            List<string> groups = new List<string>(PinGroups.Icons.Keys);
            groups.Sort(StringComparer.OrdinalIgnoreCase);

            List<string> shownGroups = new List<string>();
            foreach (string g in groups)
            {
                if (_search.Length == 0 || Display(g).IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                    shownGroups.Add(g);
            }

            const float rowH = 26f;
            float w = 280f;
            float maxH = Mathf.Min(Screen.height * 0.6f, PinFiltersPlugin.MaxPanelHeight.Value);
            float wanted = 96f + shownGroups.Count * rowH;
            float h = Mathf.Min(maxH, wanted);

            // Anchored just above the map toggle, so the panel appears where it
            // was opened from.
            Vector2 anchor = MapButton.ScreenPosition();
            float x = anchor.x >= 0f ? Mathf.Clamp(anchor.x - w * 0.5f, 8f, Screen.width - w - 8f) : 24f;
            float y = anchor.y >= 0f ? Mathf.Max(8f, Screen.height - anchor.y - h - 24f) : 90f;
            Rect area = new Rect(x, y, w, h);
            LastRect = area;

            GUI.Box(area, T("Map filters", "Filtres de carte"));
            GUILayout.BeginArea(new Rect(area.x + 8f, area.y + 26f, area.width - 16f, area.height - 34f));

            _search = GUILayout.TextField(_search, GUILayout.Height(22f));

            if (_rowStyle == null)
            {
                _rowStyle = new GUIStyle(GUI.skin.toggle);
                _rowStyle.fixedHeight = rowH - 4f;
                _rowStyle.alignment = TextAnchor.MiddleLeft;
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (string group in shownGroups)
            {
                GUILayout.BeginHorizontal(GUILayout.Height(rowH));
                bool shown = !PinGroups.IsHidden(group);
                bool now = GUILayout.Toggle(shown, "", _rowStyle, GUILayout.Width(18f));
                DrawIcon(PinGroups.Icons[group], rowH - 6f);
                GUILayout.Label(Display(group), GUILayout.Height(rowH - 6f));
                GUILayout.EndHorizontal();
                if (now != shown) PinGroups.SetHidden(group, !now);
            }
            GUILayout.EndScrollView();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(T("All", "Tout")))
            {
                foreach (string g in shownGroups) PinGroups.SetHidden(g, false);
            }
            if (GUILayout.Button(T("None", "Aucun")))
            {
                foreach (string g in shownGroups) PinGroups.SetHidden(g, true);
            }
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        // Sprites are atlas regions, so the icon is drawn through its own uv
        // rectangle instead of the whole texture.
        private static void DrawIcon(Sprite? sprite, float size)
        {
            Rect slot = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            if (sprite == null || sprite.texture == null) return;
            Rect tr = sprite.textureRect;
            Rect uv = new Rect(tr.x / sprite.texture.width, tr.y / sprite.texture.height,
                               tr.width / sprite.texture.width, tr.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(slot, sprite.texture, uv);
        }

        // The pin label comes from the game, so it is already in the user
        // language. Only the fallback on asset names needs cleaning up.
        internal static string Display(string group)
        {
            string label = PinGroups.LabelOf(group);
            return label == group ? Pretty(group) : label;
        }

        // Icon names are asset names, so they are cleaned up for display.
        private static string Pretty(string raw)
        {
            string s = raw.Replace("_", " ").Replace("mapicon", "").Replace("MapIcon", "").Trim();
            return s.Length == 0 ? raw : char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        internal static string T(string en, string fr)
        {
            return Localization.instance != null && Localization.instance.GetSelectedLanguage() == "French" ? fr : en;
        }
    }

    // ------------------------------------------------------------------
    // A native looking toggle is cloned from the map's own public position
    // toggle and placed above it, so the panel is discoverable without
    // knowing any key.
    // ------------------------------------------------------------------
    internal static class MapButton
    {
        private static GameObject? _clone;
        private static Component? _toggle;
        private static PropertyInfo? _isOn;
        private static bool _failed;

        internal static void Ensure(Minimap map)
        {
            if (_clone != null || _failed) return;
            try
            {
                // The UI assemblies are not referenced, so the vanilla toggle is
                // cloned and driven through reflection.
                FieldInfo? field = AccessTools.Field(typeof(Minimap), "m_publicPosition");
                Component? source = field?.GetValue(map) as Component;
                if (source == null || source.transform == null) return;

                _clone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
                _clone.name = "PinFilters_Toggle";

                RectTransform? src = source.GetComponent<RectTransform>();
                RectTransform? rt = _clone.GetComponent<RectTransform>();
                if (src != null && rt != null)
                {
                    rt.anchorMin = src.anchorMin;
                    rt.anchorMax = src.anchorMax;
                    rt.pivot = src.pivot;
                    rt.anchoredPosition = src.anchoredPosition + new Vector2(PinFiltersPlugin.ButtonOffsetX.Value, PinFiltersPlugin.ButtonOffsetY.Value);
                }

                string label = FilterPanel.T("Map filters", "Filtres de carte");
                foreach (Component c in _clone.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) continue;
                    string type = c.GetType().Name;
                    if (type.Contains("TextMeshPro") || type == "Text")
                    {
                        PropertyInfo? text = c.GetType().GetProperty("text");
                        text?.SetValue(c, label, null);
                    }
                }

                foreach (Component c in _clone.GetComponents<Component>())
                {
                    if (c != null && c.GetType().Name == "Toggle")
                    {
                        _toggle = c;
                        _isOn = c.GetType().GetProperty("isOn");
                        break;
                    }
                }

                // The clone keeps the vanilla listeners, which would toggle the
                // public position. They are cleared before use.
                if (_toggle != null)
                {
                    PropertyInfo? evt = _toggle.GetType().GetProperty("onValueChanged");
                    object? handler = evt?.GetValue(_toggle, null);
                    handler?.GetType().GetMethod("RemoveAllListeners")?.Invoke(handler, null);
                    _isOn?.SetValue(_toggle, FilterPanel.Visible, null);
                }

                if (_toggle == null) throw new Exception("toggle component not found");

                ReplaceCheckmark();
            }
            catch (Exception e)
            {
                PinFiltersPlugin.Log.LogWarning($"PinFilters: map toggle not created ({e.Message}). The panel key still works.");
                if (_clone != null) UnityEngine.Object.Destroy(_clone);
                _clone = null;
                _failed = true;
            }
        }


        // The clone inherits the checkmark of the public position toggle, a
        // player with a sword, which means nothing for a pin filter. It is
        // swapped for a map pin icon taken from the minimap itself.
        private static void ReplaceCheckmark()
        {
            if (_clone == null) return;
            Sprite? icon = PickIcon();
            if (icon == null) return;

            foreach (Component c in _clone.GetComponentsInChildren<Component>(true))
            {
                if (c == null || c.GetType().Name != "Image") continue;
                // The checkmark is the graphic under the toggle background, so
                // only children deeper than the root are replaced.
                if (c.transform == _clone.transform) continue;
                PropertyInfo? sprite = c.GetType().GetProperty("sprite");
                if (sprite == null) continue;
                sprite.SetValue(c, icon, null);
                PropertyInfo? color = c.GetType().GetProperty("color");
                color?.SetValue(c, Color.white, null);
            }
        }

        private static Sprite? PickIcon()
        {
            Minimap map = Minimap.instance;
            if (map == null || map.m_icons == null) return null;
            foreach (Minimap.SpriteData sd in map.m_icons)
            {
                if (sd.m_name == Minimap.PinType.Icon3 && sd.m_icon != null) return sd.m_icon;
            }
            foreach (Minimap.SpriteData sd in map.m_icons)
            {
                if (sd.m_icon != null) return sd.m_icon;
            }
            return null;
        }
        // Screen position of the toggle, used to place the panel. Negative
        // when the toggle could not be created.
        internal static Vector2 ScreenPosition()
        {
            if (_clone == null) return new Vector2(-1f, -1f);
            RectTransform? rt = _clone.GetComponent<RectTransform>();
            if (rt == null) return new Vector2(-1f, -1f);
            Vector3 p = rt.position;
            return new Vector2(p.x, p.y);
        }

        // Offsets can be changed while playing, so they are reapplied.
        internal static void ApplyOffsets(Minimap map)
        {
            if (_clone == null) return;
            FieldInfo? field = AccessTools.Field(typeof(Minimap), "m_publicPosition");
            Component? source = field?.GetValue(map) as Component;
            RectTransform? src = source?.GetComponent<RectTransform>();
            RectTransform? rt = _clone.GetComponent<RectTransform>();
            if (src == null || rt == null) return;
            Vector2 wanted = src.anchoredPosition + new Vector2(PinFiltersPlugin.ButtonOffsetX.Value, PinFiltersPlugin.ButtonOffsetY.Value);
            if (rt.anchoredPosition != wanted) rt.anchoredPosition = wanted;
        }

        // The toggle state is polled instead of hooking its event, which keeps
        // the reflection minimal and cannot leak listeners.
        internal static void Sync()
        {
            if (_toggle == null || _isOn == null) return;
            try
            {
                bool on = _isOn.GetValue(_toggle, null) is bool b && b;
                if (on != FilterPanel.Visible)
                {
                    if (FilterPanel.WantsPanel != FilterPanel.Visible) _isOn.SetValue(_toggle, FilterPanel.Visible, null);
                    else FilterPanel.Visible = on;
                }
                FilterPanel.WantsPanel = FilterPanel.Visible;
            }
            catch
            {
                _toggle = null;
            }
        }
    }

    [HarmonyPatch(typeof(Minimap), "UpdatePins")]
    internal static class Minimap_UpdatePins_Patch
    {
        private static void Postfix(Minimap __instance)
        {
            PinHider.Apply(__instance);
        }
    }

    [HarmonyPatch(typeof(Minimap), "Update")]
    internal static class Minimap_Update_Patch
    {
        private static FieldInfo? _zoomField;
        private static float _zoomBefore;
        private static bool _guard;

        // Scrolling inside the panel would zoom the map at the same time. The
        // zoom is captured before the update and restored when the cursor is
        // over the panel.
        private static void Prefix(Minimap __instance)
        {
            _guard = false;
            if (!PinFiltersPlugin.Enabled.Value || !FilterPanel.Visible) return;
            if (__instance.m_mode != Minimap.MapMode.Large) return;

            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (!FilterPanel.LastRect.Contains(mouse)) return;

            if (_zoomField == null) _zoomField = AccessTools.Field(typeof(Minimap), "m_largeZoom");
            if (_zoomField == null) return;
            _zoomBefore = (float)_zoomField.GetValue(__instance);
            _guard = true;
        }

        private static void Postfix(Minimap __instance)
        {
            if (_guard && _zoomField != null)
            {
                _zoomField.SetValue(__instance, _zoomBefore);
                _guard = false;
            }

            if (!PinFiltersPlugin.Enabled.Value) return;

            if (__instance.m_mode != Minimap.MapMode.Large)
            {
                FilterPanel.Visible = false;
                MapButton.Sync();
                return;
            }

            MapButton.Ensure(__instance);
            MapButton.ApplyOffsets(__instance);
            MapButton.Sync();

            if (Minimap.InTextInput()) return;
            if (PinFiltersPlugin.ToggleKey.Value.IsDown()) FilterPanel.Visible = !FilterPanel.Visible;
        }
    }

}
