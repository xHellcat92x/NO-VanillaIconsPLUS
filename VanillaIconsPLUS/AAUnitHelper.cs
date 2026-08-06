using System;
using System.Collections.Generic;

namespace VanillaIconsPLUS;

// ============================================================
// AA UNIT HELPER
// ============================================================

public static class AAUnitHelper
{
    private static readonly HashSet<string> DefaultAAUnitNames = new(StringComparer.Ordinal)
    {
        "23mm AAA Emplacement",
        "AFV-6 AA",
        "AFV-8 SAM",
        "AFV6 AA",
        "AFV8 Mobile Air Defense",
        "AeroSentry SPAAG",
        "Argus Class Frigate",
        "Dynamo Class Destroyer",
        "FGA-57 Anvil",
        "HLT R9 Launcher",
        "Hexhound SAM",
        "IRM-S1 Emplacement",
        "LCV-25 AA",
        "LCV25 AA",
        "Linebreaker SAM",
        "Linebreaker SPG",
        "MSV R9 Launcher",
        "MSV R9 Stratolance Launcher",
        "RAM45 SAM Launcher",
        "SLMMR-A3 SAM",
        "SPG-30 Aerosentry",
        "Sky Sentry AAA",
        "StratoLance R9 Launcher",
        "Type-14 LRAA"
    };
    
    private static readonly HashSet<string> DefaultSpecialAAUnitNames = new(StringComparer.Ordinal)
    {
        "HLT CRAM",
        "HLT LADS",
        "HLT Radar Truck",
        "HLT-CRAM",
        "HLT-HEL",
        "MSV CRAM",
        "MSV LADS",
        "MSV Radar",
        "T9K41 Boltstrike"
    };
    
    public static HashSet<string> AAUnitNames { get; private set; } = new(DefaultAAUnitNames, StringComparer.Ordinal);
    
    public static HashSet<string> SpecialAAUnitNames { get; private set; } =
        new(DefaultSpecialAAUnitNames, StringComparer.Ordinal);
    
    public static void RestoreDefaultAAUnitNames()
    {
        AAUnitNames = new HashSet<string>(DefaultAAUnitNames, StringComparer.Ordinal);
    }
    
    public static void RestoreDefaultSpecialAAUnitNames()
    {
        SpecialAAUnitNames = new HashSet<string>(DefaultSpecialAAUnitNames, StringComparer.Ordinal);
    }
    
    public static void SetAAUnitLists(IEnumerable<string> regularUnitNames, IEnumerable<string> specialUnitNames)
    {
        var regularNames = new HashSet<string>(
            regularUnitNames,
            StringComparer.Ordinal);
        
        var specialNames = new HashSet<string>(
            specialUnitNames,
            StringComparer.Ordinal);
        
        // Prevent overlap. A unit listed as special takes precedence.
        regularNames.ExceptWith(specialNames);
        
        AAUnitNames = regularNames;
        SpecialAAUnitNames = specialNames;
    }
    
    public static bool IsAA(Unit u) =>
        u != null &&
        !string.IsNullOrEmpty(u.unitName) &&
        AAUnitNames.Contains(u.unitName);
    
    public static bool IsSpecialAA(Unit u) =>
        u != null &&
        !string.IsNullOrEmpty(u.unitName) &&
        SpecialAAUnitNames.Contains(u.unitName);
}