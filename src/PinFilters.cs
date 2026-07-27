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
        public const string ModVersion = "1.0.14";

        internal static ManualLogSource Log = null!;

        internal static ConfigEntry<bool> Enabled = null!;
        internal static ConfigEntry<string> Hidden = null!;
        internal static ConfigEntry<KeyboardShortcut> ToggleKey = null!;
        internal static ConfigEntry<bool> GroupByIcon = null!;
        internal static ConfigEntry<string> SearchAliases = null!;
        internal static ConfigEntry<string> ExcludedGroups = null!;
        internal static ConfigEntry<float> PanelWidth = null!;
        internal static ConfigEntry<float> RowHeight = null!;
        internal static ConfigEntry<float> MaxPanelHeight = null!;
        internal static ConfigEntry<float> ButtonWidth = null!;
        internal static ConfigEntry<float> ButtonHeight = null!;
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

            ExcludedGroups = Config.Bind("General", "Excluded Groups", "Shout",
                "Comma-separated groups never listed in the panel. They stay visible on the map, they are just not filterable.");

            SearchAliases = Config.Bind("General", "Search Aliases", "",
                "Extra search words per group, so everyone finds a pin with the word they know.\n" +
                "Format: group=word1|word2, separated by commas. Example: Myrt=Myrtille|Blueberry, Chard=Thistle|Chardon");

            GroupByIcon = Config.Bind("General", "Group By Icon", true,
                "Group pins by their icon, so every mod pin type gets its own line even when the mod uses no distinct pin type.");

            PanelWidth = Config.Bind("Panel", "Width", 340f,
                "Panel width in pixels.");

            RowHeight = Config.Bind("Panel", "Row Height", 30f,
                "Height of a list row in pixels.");

            MaxPanelHeight = Config.Bind("Panel", "Max Height", 480f,
                "Maximum panel height in pixels. The list scrolls beyond that.");

            ButtonWidth = Config.Bind("Panel", "Button Width", 150f,
                "Width of the map button in pixels.");

            ButtonHeight = Config.Bind("Panel", "Button Height", 36f,
                "Height of the map button in pixels.");

            ButtonOffsetX = Config.Bind("Panel", "Button Offset X", 0f,
                "Fine tuning of the button position, added to the built in placement. Leave at 0 unless another interface mod moves the map controls.");

            ButtonOffsetY = Config.Bind("Panel", "Button Offset Y", 0f,
                "Fine tuning of the button position, added to the built in placement. Leave at 0 unless another interface mod moves the map controls.");

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
        internal static readonly Dictionary<string, string> Aliases = new Dictionary<string, string>();

        internal static string AliasOf(string group)
        {
            return Aliases.TryGetValue(group, out string a) ? a : "";
        }
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

        // Grouping is done on the pin label, stripped of its counter, because
        // several unrelated families can share the same icon. Pins whose name
        // changes at runtime, like pings and shouts, are grouped by type so the
        // list never fills up with player messages.
        internal static string GroupOf(Minimap.PinData pin)
        {
            if (IsDynamicName(pin.m_type))
                return pin.m_type.ToString();

            string label = Normalize(pin.m_name);
            if (label.Length > 0) return label;

            if (PinFiltersPlugin.GroupByIcon.Value && pin.m_icon != null && !string.IsNullOrEmpty(pin.m_icon.name))
                return pin.m_icon.name;

            return pin.m_type.ToString();
        }

        private static bool IsDynamicName(Minimap.PinType type)
        {
            return type == Minimap.PinType.Ping || type == Minimap.PinType.Shout
                || type == Minimap.PinType.Player || type == Minimap.PinType.Death;
        }

        // Discovery pins carry a count, so "Myrt 6" and "Myrt 3" are the same
        // family. Trailing digits and separators are removed.
        internal static string Normalize(string? raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string s = Localization.instance != null ? Localization.instance.Localize(raw) : raw;
            s = s.Trim();
            int end = s.Length;
            while (end > 0)
            {
                char c = s[end - 1];
                if (char.IsDigit(c) || c == ' ' || c == 'x' || c == 'X' || c == ':' || c == '(' || c == ')') end--;
                else break;
            }
            return s.Substring(0, end).Trim();
        }

        internal static void Track(Minimap.PinData pin)
        {
            string group = GroupOf(pin);
            if (!Icons.ContainsKey(group)) Icons[group] = pin.m_icon;

            if (Labels.TryGetValue(group, out string current) && current.Length > 0) return;

            string label = IsDynamicName(pin.m_type) ? TypeLabel(pin.m_type) : Normalize(pin.m_name);
            if (label.Length == 0) label = TypeLabel(pin.m_type);
            Labels[group] = label;

            // The prefab or icon name is kept as a search alias, so both the
            // server label and the original name find the group.
            string alias = pin.m_icon != null ? pin.m_icon.name : "";
            if (!string.IsNullOrEmpty(alias)) Aliases[group] = alias;
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
        internal static bool Interacting;
        private static Vector2 _scroll;
        private static string _search = "";
        private static GUIStyle? _labelStyle;
        private static GUIStyle? _placeholderStyle;
        private static GUIStyle? _titleStyle;
        private static GUIStyle? _buttonStyle;
        private static Texture2D? _fill;

        internal static void Draw()
        {
            List<string> groups = new List<string>();
            foreach (string g in PinGroups.Icons.Keys)
            {
                if (!IsExcluded(g)) groups.Add(g);
            }
            groups.Sort(StringComparer.OrdinalIgnoreCase);

            List<string> shownGroups = new List<string>();
            foreach (string g in groups)
            {
                if (_search.Length == 0 || Matches(g, _search)) shownGroups.Add(g);
            }

            float rowH = PinFiltersPlugin.RowHeight.Value;
            float w = PinFiltersPlugin.PanelWidth.Value;
            float maxH = Mathf.Min(Screen.height * 0.6f, PinFiltersPlugin.MaxPanelHeight.Value);
            float wanted = 120f + shownGroups.Count * rowH;
            float h = Mathf.Min(maxH, wanted);

            // Right edge aligned with the button, growing to the left and
            // upwards, so the panel never covers the map controls.
            Rect btn = MapButton.ScreenRect();
            float x, y;
            if (btn.width > 0f)
            {
                x = Mathf.Clamp(btn.xMax - w, 8f, Screen.width - w - 8f);
                y = Mathf.Clamp(Screen.height - btn.yMax - h - 8f, 8f, Screen.height - h - 8f);
            }
            else
            {
                x = Screen.width - w - 24f;
                y = 90f;
            }
            Rect area = new Rect(x, y, w, h);
            LastRect = area;

            int hiddenCount = 0;
            foreach (string g in groups) if (PinGroups.IsHidden(g)) hiddenCount++;
            string title = T("Map filters", "Filtres de carte");
            if (hiddenCount > 0) title += hiddenCount == 1
                ? T(" (1 hidden)", " (1 masqué)")
                : T($" ({hiddenCount} hidden)", $" ({hiddenCount} masqués)");
            // Own background: the default box is too transparent to read over
            // the map.
            FillRect(area, new Color(0.05f, 0.05f, 0.06f, 0.94f));
            FillRect(new Rect(area.x, area.y, area.width, 2f), new Color(1f, 1f, 1f, 0.25f));
            FillRect(new Rect(area.x, area.yMax - 2f, area.width, 2f), new Color(1f, 1f, 1f, 0.25f));
            FillRect(new Rect(area.x, area.y, 2f, area.height), new Color(1f, 1f, 1f, 0.25f));
            FillRect(new Rect(area.xMax - 2f, area.y, 2f, area.height), new Color(1f, 1f, 1f, 0.25f));
            FillRect(new Rect(area.x + 2f, area.y + 2f, area.width - 4f, 28f), new Color(1f, 1f, 1f, 0.08f));

            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label);
                _titleStyle.alignment = TextAnchor.MiddleCenter;
                _titleStyle.fontStyle = FontStyle.Bold;
                _titleStyle.fontSize = 16;
                _titleStyle.normal.textColor = new Color(1f, 0.85f, 0.55f);
            }
            GUI.Label(new Rect(area.x, area.y + 3f, area.width, 24f), title, _titleStyle);

            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 34f, area.width - 20f, area.height - 44f));

            Rect searchRow = GUILayoutUtility.GetRect(area.width - 16f, 22f);
            Rect searchField = new Rect(searchRow.x, searchRow.y, searchRow.width - (_search.Length > 0 ? 24f : 0f), 22f);
            GUI.SetNextControlName("PinFiltersSearch");
            _search = GUI.TextField(searchField, _search);

            if (_search.Length == 0 && GUI.GetNameOfFocusedControl() != "PinFiltersSearch")
            {
                if (_placeholderStyle == null)
                {
                    _placeholderStyle = new GUIStyle(GUI.skin.label);
                    _placeholderStyle.alignment = TextAnchor.MiddleLeft;
                    _placeholderStyle.fontStyle = FontStyle.Italic;
                    _placeholderStyle.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
                }
                GUI.Label(new Rect(searchField.x + 6f, searchField.y, searchField.width - 8f, 22f),
                    T("Search a pin type", "Rechercher un type de pin"), _placeholderStyle);
            }
            else if (_search.Length > 0)
            {
                if (GUI.Button(new Rect(searchRow.xMax - 22f, searchRow.y, 22f, 22f), "x")) _search = "";
            }
            GUILayout.Space(4f);

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label);
                _labelStyle.alignment = TextAnchor.MiddleLeft;
                _labelStyle.wordWrap = false;
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (string group in shownGroups)
            {
                // Explicit rects instead of automatic layout: the checkbox, the
                // icon and the label are centred on the same row.
                Rect row = GUILayoutUtility.GetRect(w - 44f, rowH);
                Rect box = new Rect(row.x, row.y + (rowH - 20f) * 0.5f, 20f, 20f);
                Rect ico = new Rect(row.x + 28f, row.y + (rowH - 24f) * 0.5f, 24f, 24f);
                Rect lab = new Rect(row.x + 58f, row.y, row.width - 58f, rowH);

                bool shown = !PinGroups.IsHidden(group);
                bool hover = row.Contains(Event.current.mousePosition);

                if (hover) FillRect(row, new Color(1f, 1f, 1f, 0.12f));

                bool now = GUI.Toggle(box, shown, "");
                DrawIcon(ico, PinGroups.Icons[group]);

                Color previous = GUI.color;
                if (!shown) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                GUI.Label(lab, Display(group), _labelStyle);
                GUI.color = previous;

                // The whole row is clickable, not just the small checkbox.
                if (hover && Event.current.type == EventType.MouseDown && Event.current.button == 0
                    && !box.Contains(Event.current.mousePosition))
                {
                    now = !shown;
                    Event.current.Use();
                }

                if (now != shown) PinGroups.SetHidden(group, !now);
            }
            GUILayout.EndScrollView();

            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button);
                _buttonStyle.fontSize = 15;
                _buttonStyle.fontStyle = FontStyle.Bold;
                _buttonStyle.normal.textColor = new Color(1f, 0.85f, 0.55f);
                _buttonStyle.hover.textColor = Color.white;
                _buttonStyle.fixedHeight = 28f;
            }

            GUILayout.Space(6f);
            Rect buttons = GUILayoutUtility.GetRect(area.width - 20f, 28f);
            float half = (buttons.width - 8f) * 0.5f;
            Rect bAll = new Rect(buttons.x, buttons.y, half, 28f);
            Rect bNone = new Rect(buttons.x + half + 8f, buttons.y, half, 28f);
            FillRect(bAll, new Color(1f, 1f, 1f, 0.10f));
            FillRect(bNone, new Color(1f, 1f, 1f, 0.10f));
            if (GUI.Button(bAll, T("All", "Tout"), _buttonStyle))
            {
                foreach (string g in shownGroups) PinGroups.SetHidden(g, false);
            }
            if (GUI.Button(bNone, T("None", "Aucun"), _buttonStyle))
            {
                foreach (string g in shownGroups) PinGroups.SetHidden(g, true);
            }

            GUILayout.EndArea();
        }

        // Sprites are atlas regions, so the icon is drawn through its own uv
        // rectangle instead of the whole texture.
        private static void DrawIcon(Rect slot, Sprite? sprite)
        {
            if (sprite == null || sprite.texture == null) return;
            Rect tr = sprite.textureRect;
            Rect uv = new Rect(tr.x / sprite.texture.width, tr.y / sprite.texture.height,
                               tr.width / sprite.texture.width, tr.height / sprite.texture.height);
            GUI.DrawTextureWithTexCoords(slot, sprite.texture, uv);
        }

        private static void FillRect(Rect r, Color color)
        {
            if (_fill == null)
            {
                _fill = new Texture2D(1, 1);
                _fill.SetPixel(0, 0, Color.white);
                _fill.Apply();
            }
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(r, _fill);
            GUI.color = previous;
        }

        private static bool IsExcluded(string group)
        {
            foreach (string part in PinFiltersPlugin.ExcludedGroups.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string p = part.Trim();
                if (p.Length > 0 && (string.Equals(p, group, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(p, Display(group), StringComparison.OrdinalIgnoreCase))) return true;
            }
            return false;
        }

        // Any of the label, the group key or the icon name matching is enough,
        // so both a server label and an original name find the group.
        private static bool Matches(string group, string needle)
        {
            if (Contains(Display(group), needle) || Contains(group, needle) || Contains(PinGroups.AliasOf(group), needle))
                return true;

            // Words added by the server, so a French player finds "Myrtille"
            // while the pin label is an abbreviation.
            foreach (string entry in PinFiltersPlugin.SearchAliases.Value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] kv = entry.Split('=');
                if (kv.Length != 2) continue;
                if (!Contains(group, kv[0].Trim()) && !Contains(Display(group), kv[0].Trim())) continue;
                foreach (string word in kv[1].Split('|'))
                {
                    if (Contains(word.Trim(), needle)) return true;
                }
            }
            return false;
        }

        private static bool Contains(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack)
                && haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
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
        // Placement measured on the vanilla map, above the cartography row.
        // Canvas units, so it scales with the resolution like the rest of the UI.
        private const float BaseX = -53f;
        private const float BaseY = 97f;

        private static GameObject? _clone;
        private static Component? _control;
        private static PropertyInfo? _isOn;
        private static bool _isToggle;
        private static bool _failed;

        internal static void Ensure(Minimap map)
        {
            if (_clone != null || _failed) return;
            try
            {
                FieldInfo? field = AccessTools.Field(typeof(Minimap), "m_publicPosition");
                Component? anchor = field?.GetValue(map) as Component;
                if (anchor == null || anchor.transform == null) return;

                // A real button with a background is cloned from the crafting
                // panel, which is what the vanilla UI uses for actions.
                Component? source = FindCraftButton();
                _isToggle = source == null;
                if (source == null) source = anchor;

                _clone = UnityEngine.Object.Instantiate(source.gameObject, anchor.transform.parent);
                _clone.name = "PinFilters_Button";

                // The crafting panel is inactive while the inventory is closed,
                // so the clone inherits that state and would stay invisible.
                _clone.SetActive(true);
                foreach (Transform child in _clone.GetComponentsInChildren<Transform>(true))
                    child.gameObject.SetActive(true);

                RectTransform? src = anchor.GetComponent<RectTransform>();
                RectTransform? rt = _clone.GetComponent<RectTransform>();
                if (src != null && rt != null)
                {
                    rt.anchorMin = src.anchorMin;
                    rt.anchorMax = src.anchorMax;
                    rt.pivot = src.pivot;
                    rt.anchoredPosition = src.anchoredPosition + new Vector2(BaseX + PinFiltersPlugin.ButtonOffsetX.Value, BaseY + PinFiltersPlugin.ButtonOffsetY.Value);
                    rt.localScale = Vector3.one;
                    // The crafting button is much wider than the map controls.
                    if (!_isToggle) rt.sizeDelta = new Vector2(PinFiltersPlugin.ButtonWidth.Value, PinFiltersPlugin.ButtonHeight.Value);
                }

                string label = FilterPanel.T("Map filters", "Filtres de carte");
                foreach (Component c in _clone.GetComponentsInChildren<Component>(true))
                {
                    if (c == null) continue;
                    string type = c.GetType().Name;
                    if (type.Contains("TextMeshPro") || type == "Text")
                        c.GetType().GetProperty("text")?.SetValue(c, label, null);
                }

                foreach (Component c in _clone.GetComponents<Component>())
                {
                    if (c == null) continue;
                    string type = c.GetType().Name;
                    if (type == "Button" || type == "Toggle")
                    {
                        _control = c;
                        break;
                    }
                }
                if (_control == null) throw new Exception("no button or toggle on the clone");

                string eventName = _isToggle ? "onValueChanged" : "onClick";
                PropertyInfo? evt = _control.GetType().GetProperty(eventName);
                object? handler = evt?.GetValue(_control, null);
                handler?.GetType().GetMethod("RemoveAllListeners")?.Invoke(handler, null);

                if (_isToggle)
                {
                    _isOn = _control.GetType().GetProperty("isOn");
                    _isOn?.SetValue(_control, FilterPanel.Visible, null);
                    ReplaceCheckmark();
                }
                else
                {
                    AddClickListener(handler);
                }
            }
            catch (Exception e)
            {
                PinFiltersPlugin.Log.LogWarning($"PinFilters: map button not created ({e.Message}). The panel key still works.");
                if (_clone != null) UnityEngine.Object.Destroy(_clone);
                _clone = null;
                _control = null;
                _failed = true;
            }
        }

        private static Component? FindCraftButton()
        {
            try
            {
                if (InventoryGui.instance == null) return null;
                foreach (string name in new[] { "m_craftButton", "m_repairButton", "m_upgradeButton" })
                {
                    FieldInfo? f = AccessTools.Field(typeof(InventoryGui), name);
                    if (f?.GetValue(InventoryGui.instance) is Component c && c != null) return c;
                }
            }
            catch
            {
                // Falls back to cloning the map toggle.
            }
            return null;
        }

        // onClick takes a UnityAction, which lives in the core module, so the
        // listener can be built without referencing the UI assembly.
        private static void AddClickListener(object? handler)
        {
            if (handler == null) return;
            foreach (MethodInfo mi in handler.GetType().GetMethods())
            {
                if (mi.Name != "AddListener") continue;
                ParameterInfo[] ps = mi.GetParameters();
                if (ps.Length != 1) continue;
                Delegate action = Delegate.CreateDelegate(ps[0].ParameterType, typeof(MapButton).GetMethod(nameof(OnClick), AccessTools.all));
                mi.Invoke(handler, new object[] { action });
                return;
            }
        }

        private static void OnClick()
        {
            FilterPanel.Visible = !FilterPanel.Visible;
            FilterPanel.WantsPanel = FilterPanel.Visible;
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
        // Screen rect of the button, used to align the panel on its right
        // edge and place it above. Zero sized when unavailable.
        internal static Rect ScreenRect()
        {
            if (_clone == null) return Rect.zero;
            RectTransform? rt = _clone.GetComponent<RectTransform>();
            if (rt == null) return Rect.zero;
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float minX = Mathf.Min(corners[0].x, corners[2].x);
            float maxX = Mathf.Max(corners[0].x, corners[2].x);
            float minY = Mathf.Min(corners[0].y, corners[1].y);
            float maxY = Mathf.Max(corners[0].y, corners[1].y);
            return new Rect(minX, minY, maxX - minX, maxY - minY);
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
            Vector2 wanted = src.anchoredPosition + new Vector2(BaseX + PinFiltersPlugin.ButtonOffsetX.Value, BaseY + PinFiltersPlugin.ButtonOffsetY.Value);
            if (rt.anchoredPosition != wanted) rt.anchoredPosition = wanted;
            if (!_isToggle)
            {
                Vector2 size = new Vector2(PinFiltersPlugin.ButtonWidth.Value, PinFiltersPlugin.ButtonHeight.Value);
                if (rt.sizeDelta != size) rt.sizeDelta = size;
            }
            if (!_clone.activeSelf) _clone.SetActive(true);
        }

        // The toggle state is polled instead of hooking its event, which keeps
        // the reflection minimal and cannot leak listeners.
        internal static void Sync()
        {
            if (!_isToggle || _control == null || _isOn == null) return;
            try
            {
                bool on = _isOn.GetValue(_control, null) is bool b && b;
                if (on != FilterPanel.Visible)
                {
                    if (FilterPanel.WantsPanel != FilterPanel.Visible) _isOn.SetValue(_control, FilterPanel.Visible, null);
                    else FilterPanel.Visible = on;
                }
                FilterPanel.WantsPanel = FilterPanel.Visible;
            }
            catch
            {
                _control = null;
            }
        }
    }


    // The map reads the mouse in its own update, and the method that takes an
    // input flag does not exist in every game version. The whole update is
    // skipped instead while the panel is being used, which blocks panning,
    // zooming and clicks in one go.
    [HarmonyPatch(typeof(Minimap), "Update")]
    internal static class Minimap_Update_Block_Patch
    {
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Minimap __instance)
        {
            if (!PinFiltersPlugin.Enabled.Value || !FilterPanel.Visible) return true;
            if (__instance.m_mode != Minimap.MapMode.Large) return true;

            if (FilterPanel.Interacting) return false;

            Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            return !FilterPanel.LastRect.Contains(mouse);
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
        // Runs even when the update above is skipped, so the key and the
        // outside click keep working while the panel has the mouse.
        [HarmonyPriority(Priority.Last)]
        private static void Prefix(Minimap __instance)
        {

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

            // A click outside closes the panel, like any menu, and still
            // reaches the map. The button itself is ignored so opening the
            // panel does not close it in the same frame.
            if (FilterPanel.Visible && Input.GetMouseButtonDown(0) && !FilterPanel.Interacting)
            {
                Vector2 mouse = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                Rect btn = MapButton.ScreenRect();
                bool onButton = btn.width > 0f
                    && new Rect(btn.x, Screen.height - btn.yMax, btn.width, btn.height).Contains(mouse);
                if (!FilterPanel.LastRect.Contains(mouse) && !onButton) FilterPanel.Visible = false;
            }
        }
    }

}
