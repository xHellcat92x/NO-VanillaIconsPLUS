using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using NuclearOption.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VanillaIconsPLUS;

[BepInPlugin("com.hellcat92.vanillaiconsplus", "Vanilla Icons PLUS", PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    // HUD name tints
    public ConfigEntry<Color> FriendlyNameHUD;
    public ConfigEntry<Color> EnemyNameHUD;
    
    // HUD icon tints
    private ConfigEntry<Color> _friendlyUnitsHUD;
    private ConfigEntry<Color> _enemyUnitsHUD;
    private ConfigEntry<Color> _neutralUnitsHUD;
    
    // AA unit tint (HUD + MAP, enemy only)
    public ConfigEntry<Color> AAUnitsHUD;
    public ConfigEntry<Color> SpecialAAUnitsHUD;
    
    // MAP name tints
    public ConfigEntry<Color> FriendlyNameMap;
    public ConfigEntry<Color> EnemyNameMap;
    
    // Toggles
    public ConfigEntry<bool> ShowHUDNames;
    public ConfigEntry<bool> ShowMapNames;
    public static ConfigEntry<bool> DisableAllyInfoHover;
    
    // Name label customization
    public ConfigEntry<int> HUDNameFontSize;
    public ConfigEntry<float> HUDNameOffset;
    public ConfigEntry<int> MapNameFontSize;
    public ConfigEntry<float> MapNameOffset;
    
    private static ManualLogSource _log;
    private Harmony _harmony;
    internal static Plugin Instance;
    
    private void Awake()
    {
        Instance = this;
        _log = Logger;
        
        // ============================================================
        // CONFIG
        // ============================================================
        
        ShowHUDNames = Config.Bind("Settings", "Show Player Names", true, "Toggle HUD player names");
        
        FriendlyNameHUD = Config.Bind("Settings", "Friendly Player Names", new Color(0.19f, 0.58f, 1f, 1f),
            "Friendly HUD player names");
        
        DisableAllyInfoHover = Config.Bind("Settings", "Disable Vanilla Friendly Hover Names",
            true, "Disable the new 0.34 vanilla feature showing the name of a friendly player you hover over.");
        
        EnemyNameHUD = Config.Bind("Settings", "Enemy Player Names", new Color(1f, 0.13f, 0.05f, 1f),
            "Enemy HUD player names");
        
        _friendlyUnitsHUD = Config.Bind("Settings", "Friendly Units", new Color(0.19f, 0.58f, 1f, 1f),
            "Friendly HUD unit icons");
        
        _enemyUnitsHUD = Config.Bind("Settings", "Enemy Units", new Color(1f, 0.13f, 0.05f, 1f),
            "Enemy HUD unit icons");
        
        _neutralUnitsHUD = Config.Bind("Settings", "Neutral Units", new Color(0.6f, 0.6f, 0.6f, 1f),
            "Neutral HUD unit icons");
        
        AAUnitsHUD = Config.Bind("Settings", "Enemy AA Units", new Color(1f, 0.369f, 1f, 1f),
            "Tint for enemy AA/SAM/CIWS units on HUD & Map");
        
        SpecialAAUnitsHUD = Config.Bind("Settings", "Enemy AA (Special) Units", new Color(0f, 1f, 1f, 1f),
            "Tint for enemy Special AA units on HUD & Map (CRAM/LADS/HEL/Radar/Boltstrike)");
        
        ShowMapNames = Config.Bind("Settings", "Show Map Player Names", true, "Toggle map player names");
        
        FriendlyNameMap = Config.Bind("Settings", "Friendly Player Names (MAP)", new Color(0.19f, 0.58f, 1f, 1f),
            "Friendly map player names");
        
        EnemyNameMap = Config.Bind("Settings", "Enemy Player Names (MAP)", new Color(1f, 0.13f, 0.05f, 1f),
            "Enemy map player names");
        
        HUDNameFontSize = Config.Bind("Settings", "HUD Player Name Font Size", 14, "Font size for HUD player names");
        
        HUDNameOffset = Config.Bind("Settings", "HUD Player Name Vertical Offset", 25f,
            "Vertical offset above HUD icons");
        
        MapNameFontSize = Config.Bind("Settings", "MAP Player Name Font Size", 14, "Font size for MAP player names");
        
        MapNameOffset = Config.Bind("Settings", "MAP Player Name Vertical Offset", 5f,
            "Vertical offset above MAP icons");
        
        var aaWhiteList = new AAConfigReadWrite(Path.Combine(Paths.ConfigPath,
            "com.hellcat92.vanillaiconsplus_AA_Whitelist.cfg"), Logger);
        
        aaWhiteList.ReadAAList();
        
        // ============================================================
        // PATCHING
        // ============================================================
        
        _harmony = new Harmony("com.hellcat92.vanillaiconsplus");
        _harmony.PatchAll();
        
        // HUD unit colours → HUD + map refresh
        _friendlyUnitsHUD.SettingChanged += delegate
        {
            ApplyHUDTints();
            RefreshHUDIcons();
            RefreshMapIcons();
        };
        _enemyUnitsHUD.SettingChanged += delegate
        {
            ApplyHUDTints();
            RefreshHUDIcons();
            RefreshMapIcons();
        };
        _neutralUnitsHUD.SettingChanged += delegate
        {
            ApplyHUDTints();
            RefreshHUDIcons();
            RefreshMapIcons();
        };
        
        // AA recolour refresh (HUD + map)
        AAUnitsHUD.SettingChanged += delegate
        {
            RefreshHUDIcons();
            RefreshMapIcons();
        };
        SpecialAAUnitsHUD.SettingChanged += delegate
        {
            RefreshHUDIcons();
            RefreshMapIcons();
        };
        
        // Initial application
        ApplyHUDTints();
        RefreshHUDIcons();
        RefreshMapIcons();
        
        _log.LogInfo($"{Info.Metadata.Name} v{Info.Metadata.Version} loaded.");
    }
    
    
    internal static void ApplyHUDTints()
    {
        var ga = Resources.FindObjectsOfTypeAll<GameAssets>().FirstOrDefault() ?? GameAssets.i;
        if (ga == null)
        {
            _log.LogWarning("GameAssets not found.");
            return;
        }
        
        ga.HUDFriendly = Instance._friendlyUnitsHUD.Value;
        ga.HUDHostile = Instance._enemyUnitsHUD.Value;
        ga.HUDNeutral = Instance._neutralUnitsHUD.Value;
    }
    
    internal static void RefreshHUDIcons()
    {
        var hud = SceneSingleton<CombatHUD>.i;
        if (hud == null || hud.aircraft == null)
            return;
        
        var field = AccessTools.Field(typeof(CombatHUD), "markers");
        var markers = field.GetValue(hud) as List<HUDUnitMarker>;
        if (markers == null)
            return;
        
        foreach (var marker in markers)
        {
            if (marker?.unit == null || marker.image == null)
                continue;
            
            bool hasHQ = marker.unit.NetworkHQ != null;
            var sameHQ = hasHQ && hud.aircraft.NetworkHQ != null &&
                         marker.unit.NetworkHQ == hud.aircraft.NetworkHQ;
            bool isNeutral = !hasHQ;
            bool isEnemy = hasHQ && !sameHQ;
            
            // Preserve selection colour
            if (marker.selected)
                continue;
            
            Color baseTint;
            if (isNeutral)
                baseTint = Instance._neutralUnitsHUD.Value;
            else if (isEnemy)
                baseTint = Instance._enemyUnitsHUD.Value;
            else
                baseTint = Instance._friendlyUnitsHUD.Value;
            
            float a = marker.image.color.a;
            Color result = new Color(baseTint.r, baseTint.g, baseTint.b, a);
            
            switch (isEnemy)
            {
                case true when AAUnitHelper.IsAA(marker.unit):
                {
                    var aa = Instance.AAUnitsHUD.Value;
                    result = new Color(aa.r, aa.g, aa.b, a);
                    break;
                }
                case true when AAUnitHelper.IsSpecialAA(marker.unit):
                {
                    var saa = Instance.SpecialAAUnitsHUD.Value;
                    result = new Color(saa.r, saa.g, saa.b, a);
                    break;
                }
            }
            
            marker.image.color = result;
        }
    }
    
    internal static void RefreshMapIcons()
    {
        var map = SceneSingleton<DynamicMap>.i;
        if (map == null) return;
        
        var field = AccessTools.Field(typeof(DynamicMap), "iconLookup");
        var dict = field.GetValue(map) as Dictionary<Unit, UnitMapIcon>;
        
        if (dict == null) return;
        
        foreach (var kvp in dict)
            kvp.Value.UpdateColor();
    }
}

// ============================================================
// JAM STATE (RadarWarning-based, player-centric)
// ============================================================

public static class JamState
{
    public static readonly HashSet<Unit> JammedUnits = new();
    
    public static bool PlayerIsJammed;
}

[HarmonyPatch(typeof(RadarWarning), nameof(RadarWarning.Update))]
public static class Patch_RadarWarning_Update
{
    static readonly FieldInfo JammingLookupField =
        AccessTools.Field(typeof(RadarWarning), "jammingIconLookup");
    
    private static void Postfix(RadarWarning __instance)
    {
        if (__instance == null || JammingLookupField == null)
            return;
        
        var lookup = JammingLookupField.GetValue(__instance) as IDictionary;
        if (lookup == null)
            return;
        
        JamState.JammedUnits.Clear();
        
        foreach (DictionaryEntry entry in lookup)
        {
            var unit = entry.Key as Unit;
            if (unit != null)
                JamState.JammedUnits.Add(unit);
        }
        
        // Player-centric: if there is any jamming icon, the player's aircraft is being jammed
        JamState.PlayerIsJammed = JamState.JammedUnits.Count > 0;
    }
}

// ============================================================
// HUD PLAYER NAME LABEL SYSTEM
// ============================================================

public static class HUDUnitMarkerExtensions
{
    public class NameHolder
    {
        public TextMeshProUGUI label;
        public float spawnTime;
        public TMP_FontAsset font;
    }
    
    public static readonly ConditionalWeakTable<HUDUnitMarker, NameHolder> table
        = new ConditionalWeakTable<HUDUnitMarker, NameHolder>();
    
    public static NameHolder GetHolder(this HUDUnitMarker marker)
        => table.GetOrCreateValue(marker);
    
    public static TextMeshProUGUI GetLabel(this HUDUnitMarker marker)
        => marker.GetHolder().label;
    
    public static void SetLabel(this HUDUnitMarker marker, TextMeshProUGUI label)
        => marker.GetHolder().label = label;
    

}

[HarmonyPatch(typeof(HUDUnitMarker), nameof(HUDUnitMarker.UpdateVisibility))]
public static class Patch_HUD_UpdateVisibility
{
    private static void Postfix(HUDUnitMarker __instance)
    {
        if (__instance == null || __instance.unit == null)
            return;
        
        Aircraft ac = __instance.unit as Aircraft;
        if (ac == null || ac.Player == null)
            return;
        
        var plugin = Plugin.Instance;
        
        var holder = __instance.GetHolder();
        TextMeshProUGUI label = holder.label;
        
        if (label == null)
        {
            GameObject go = new GameObject("HUD_PlayerName");
            go.transform.SetParent(SceneSingleton<CombatHUD>.i.iconLayer, false);
            
            label = go.AddComponent<TextMeshProUGUI>();
            
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableWordWrapping = false;
            label.enableAutoSizing = false;
            
            if (holder.font == null)
            {
                TextMeshProUGUI hudText = SceneSingleton<CombatHUD>.i.GetComponentInChildren<TextMeshProUGUI>(true);
                holder.font = hudText != null ? hudText.font : TMP_Settings.defaultFontAsset;
            }
            
            label.font = holder.font;
            label.fontSize = plugin.HUDNameFontSize.Value;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            
            __instance.SetLabel(label);
            holder.spawnTime = Time.timeSinceLevelLoad;
        }
        
        label.text = ac.Player.GetDisplayName(PlayerNameContext.ChatOrLeaderboard);
        
        if (Time.timeSinceLevelLoad - holder.spawnTime < 0.01f)
        {
            label.enabled = false;
            return;
        }
    }
}

[HarmonyPatch(typeof(HUDUnitMarker), nameof(HUDUnitMarker.UpdatePosition))]
public static class Patch_HUD_UpdatePosition
{
    private static void Postfix(HUDUnitMarker __instance)
    {
        var plugin = Plugin.Instance;
        TextMeshProUGUI label = __instance.GetLabel();
        
        if (label == null)
            return;
        
        bool hideBySelection = __instance.selected;
        bool hideByToggle = !plugin.ShowHUDNames.Value;
        
        label.fontSize = plugin.HUDNameFontSize.Value;
        float offset = plugin.HUDNameOffset.Value;
        label.transform.position = __instance.image.transform.position + new Vector3(0f, offset, 0f);
        
        bool friendly = __instance.unit.NetworkHQ ==
                        SceneSingleton<CombatHUD>.i.aircraft.NetworkHQ;
        
        label.color = friendly
            ? plugin.FriendlyNameHUD.Value
            : plugin.EnemyNameHUD.Value;
        
        // Name hiding only when the *player's aircraft* is actively being jammed
        bool visible =
            __instance.image.enabled &&
            !JamState.PlayerIsJammed &&
            !hideBySelection &&
            !hideByToggle;
        
        label.enabled = visible;
    }
}

[HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.OnRemoveIcon))]
public static class Patch_Map_RemoveIcon
{
    private static void Prefix(UnitMapIcon __instance)
    {
        TextMeshProUGUI label = __instance.GetLabel();
        if (label != null)
        {
            Object.Destroy(label.gameObject);
            __instance.SetLabel(null);
        }
    }
}

// ============================================================
// HUD AA UNIT RECOLOUR
// ============================================================

[HarmonyPatch(typeof(CombatHUD), nameof(CombatHUD.UpdateMarkers))]
public static class Patch_HUD_AAColour
{
    static void Postfix(CombatHUD __instance)
    {
        if (__instance == null || __instance.aircraft == null)
            return;
        
        var plugin = Plugin.Instance;
        
        var field = AccessTools.Field(typeof(CombatHUD), "markers");
        var markers = field.GetValue(__instance) as List<HUDUnitMarker>;
        if (markers == null)
            return;
        
        foreach (var marker in markers)
        {
            if (marker?.unit == null || marker.image == null)
                continue;
            
            bool hasHQ = marker.unit.NetworkHQ != null;
            bool sameHQ = hasHQ && __instance.aircraft.NetworkHQ != null &&
                          marker.unit.NetworkHQ == __instance.aircraft.NetworkHQ;
            bool isEnemy = hasHQ && !sameHQ;
            
            if (!isEnemy)
                continue;
            
            if (marker.selected)
                continue;
            
            if (AAUnitHelper.IsAA(marker.unit))
            {
                var color = marker.image.color;
                var value = plugin.AAUnitsHUD.Value;
                marker.image.color = new Color(value.r, value.g, value.b, color.a);
            }
            else if (AAUnitHelper.IsSpecialAA(marker.unit))
            {
                var color = marker.image.color;
                var value = plugin.SpecialAAUnitsHUD.Value;
                marker.image.color = new Color(value.r, value.g, value.b, color.a);
            }
        }
    }
}

// ============================================================
// MAP PLAYER NAME LABEL SYSTEM
// ============================================================

public static class MapIconExtensions
{
    public class NameHolder
    {
        public TextMeshProUGUI label;
        public TMP_FontAsset font;
    }
    
    public static readonly ConditionalWeakTable<UnitMapIcon, NameHolder> table
        = new ConditionalWeakTable<UnitMapIcon, NameHolder>();
    
    public static NameHolder GetHolder(this UnitMapIcon icon)
        => table.GetOrCreateValue(icon);
    
    public static TextMeshProUGUI GetLabel(this UnitMapIcon icon)
        => icon.GetHolder().label;
    
    public static void SetLabel(this UnitMapIcon icon, TextMeshProUGUI label)
        => icon.GetHolder().label = label;
}

public static class MapIconHelpers
{
    static readonly FieldInfo ImageField =
        AccessTools.Field(typeof(UnitMapIcon), "iconImage");
    
    public static Image GetImage(this UnitMapIcon icon)
        => ImageField?.GetValue(icon) as Image;
}

[HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.UpdateIcon))]
public static class Patch_Map_UpdateIcon
{
    private static void Postfix(UnitMapIcon __instance, float mapDisplayFactor, float mapInverseScale, Transform mapTransform, bool mapMaximized)
    {
        if (__instance == null || __instance.unit == null)
            return;
        
        var plugin = Plugin.Instance;
        
        Aircraft ac = __instance.unit as Aircraft;
        if (ac == null || ac.Player == null)
            return;
        
        Image img = __instance.GetImage();
        if (img == null)
            return;
        
        var holder = __instance.GetHolder();
        TextMeshProUGUI label = holder.label;
        
        if (label == null)
        {
            GameObject go = new GameObject("MAP_PlayerName");
            go.transform.SetParent(img.transform.parent, false);
            
            label = go.AddComponent<TextMeshProUGUI>();
            
            label.overflowMode = TextOverflowModes.Overflow;
            label.enableWordWrapping = false;
            label.enableAutoSizing = false;
            
            if (holder.font == null)
            {
                TextMeshProUGUI hudText = SceneSingleton<CombatHUD>.i.GetComponentInChildren<TextMeshProUGUI>(true);
                holder.font = hudText != null ? hudText.font : TMP_Settings.defaultFontAsset;
            }
            
            label.font = holder.font;
            label.fontSize = plugin.MapNameFontSize.Value;
            label.alignment = TextAlignmentOptions.Center;
            label.raycastTarget = false;
            
            __instance.SetLabel(label);
        }
        
        label.text = ac.Player.GetDisplayName(PlayerNameContext.ChatOrLeaderboard);
        
        bool hideByToggle = !plugin.ShowMapNames.Value;
        
        label.fontSize = plugin.MapNameFontSize.Value;
        float offset = plugin.MapNameOffset.Value;
        label.transform.localPosition = img.transform.localPosition + new Vector3(0f, offset, 0f);
        label.transform.localScale = Vector3.one * mapInverseScale;
        
        bool friendly = false;
        var hq = __instance.unit.NetworkHQ;
        if (hq != null)
        {
            var mode = DynamicMap.GetFactionMode(hq, true);
            friendly = mode == FactionMode.Friendly;
        }
        
        label.color = friendly
            ? plugin.FriendlyNameMap.Value
            : plugin.EnemyNameMap.Value;
        
        // Name hiding only when the *player's aircraft* is actively being jammed
        bool visible =
            mapMaximized &&
            __instance.gameObject.activeInHierarchy &&
            !JamState.PlayerIsJammed &&
            !hideByToggle;
        
        label.enabled = visible;
    }
}

[HarmonyPatch(typeof(HUDUnitMarker), nameof(HUDUnitMarker.RemoveIcon))]
public static class Patch_HUD_RemoveIcon
{
    static void Prefix(HUDUnitMarker __instance)
    {
        TextMeshProUGUI label = __instance.GetLabel();
        if (label != null)
        {
            Object.Destroy(label.gameObject);
            __instance.SetLabel(null);
        }
    }
}

// ============================================================
// MAP AA UNIT RECOLOUR
// ============================================================

[HarmonyPatch(typeof(UnitMapIcon), nameof(UnitMapIcon.UpdateIcon))]
public static class Patch_Map_AAColour
{
    static void Postfix(UnitMapIcon __instance)
    {
        if (__instance == null || __instance.unit == null)
            return;
        
        var plugin = Plugin.Instance;
        var map = SceneSingleton<DynamicMap>.i;
        if (map == null || (map.selectedIcons != null && map.selectedIcons.Contains(__instance)))
            return;
        
        var playerHQ = map.HQ;
        if (playerHQ == null)
            return;
        
        bool isEnemy = __instance.unit.NetworkHQ != null &&
                       __instance.unit.NetworkHQ != playerHQ;
        
        if (!isEnemy)
            return;
        
        if (AAUnitHelper.IsAA(__instance.unit))
        {
            Image image = __instance.GetImage();
            if (image != null)
                image.color = plugin.AAUnitsHUD.Value;
        }
        else if (AAUnitHelper.IsSpecialAA(__instance.unit))
        {
            Image image = __instance.GetImage();
            if (image != null)
                image.color = plugin.SpecialAAUnitsHUD.Value;
        }
    }
}

// ============================================================
// MAP FIX — Reapply tints after SetFaction()
// ============================================================

[HarmonyPatch(typeof(DynamicMap), nameof(DynamicMap.SetFaction))]
public static class Patch_Map_SetFaction
{
    static void Postfix(DynamicMap __instance)
    {
        Plugin.Instance.StartCoroutine(Delayed());
    }
    
    private static IEnumerator Delayed()
    {
        yield return null;
        Plugin.ApplyHUDTints();
        Plugin.RefreshHUDIcons();
        Plugin.RefreshMapIcons();
    }
}

// ============================================================
// Disable overlapping AllyInfo hover based on config
// ============================================================

[HarmonyPatch(typeof(AllyInfo), nameof(AllyInfo.UpdateAllyInfoOnHover))]
[HarmonyPriority(Priority.First)]
public static class Patch_AllyInfoHover
{
    private static bool Prefix(AllyInfo __instance)
    {
        if (!Plugin.DisableAllyInfoHover.Value)
            return true;
        
        __instance.hoveredAllyInfo.enabled = false;
        return false;
    }
}