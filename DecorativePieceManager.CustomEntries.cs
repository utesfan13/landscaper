using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace Landscaper;

/// <summary>Custom entries from the config.</summary>
public sealed partial class DecorativePieceManager
{
    /// <summary>
    /// Parses entries of the form
    /// Display Name|Prefab|Tool|Category[|Rotation X,Y,Z[|Item:Amount,...[|Scale X,Y,Z or Scale]]].
    /// </summary>
    private List<DecorativePieceDefinition> ParseCustomEntries(string customEntries, bool logErrors = true)
    {
        void Warn(string message)
        {
            if (logErrors)
            {
                _log.LogWarning(message);
            }
        }

        var definitions = new List<DecorativePieceDefinition>();
        foreach (var line in customEntries.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split('|').Select(field => field.Trim()).ToArray();
            if (fields.Length < 4 || !Enum.TryParse(fields[2], true, out BuildTool tool))
            {
                Warn($"Ignoring invalid custom entry: {line}");
                continue;
            }

            var definition = new DecorativePieceDefinition
            {
                DisplayName = fields[0],
                PrefabName = fields[1],
                Tool = tool,
                Category = fields[3]
            };

            if (fields.Length >= 5 && fields[4].Length > 0)
            {
                var values = fields[4].Split(',');
                if (values.Length == 3 &&
                    float.TryParse(values[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                    float.TryParse(values[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                    float.TryParse(values[2].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
                {
                    definition.PlacementRotation = new Vector3(x, y, z);
                }
                else
                {
                    Warn($"Ignoring invalid rotation in custom entry: {line}");
                }
            }

            if (fields.Length >= 6 && fields[5].Length > 0)
            {
                foreach (var requirement in fields[5].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = requirement.Split(':');
                    if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var amount) && amount > 0)
                    {
                        definition.Requirements.Add(new PieceRequirement { ItemName = parts[0].Trim(), Amount = amount });
                    }
                    else
                    {
                        Warn($"Ignoring invalid requirement '{requirement}' in custom entry: {line}");
                    }
                }
            }

            if (fields.Length >= 7 && fields[6].Length > 0)
            {
                if (TryParseScale(fields[6], out var scale))
                {
                    definition.Scale = scale;
                }
                else
                {
                    Warn($"Ignoring invalid scale in custom entry (use one number or X,Y,Z, all above 0): {line}");
                }
            }

            definitions.Add(definition);
        }

        return definitions;
    }

    private static bool TryParseScale(string text, out Vector3 scale)
    {
        scale = Vector3.one;
        var values = text.Split(',')
            .Select(value => float.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : float.NaN)
            .ToArray();

        if (values.Length == 1)
        {
            values = new[] { values[0], values[0], values[0] };
        }

        if (values.Length != 3 || values.Any(value => float.IsNaN(value) || value <= 0f))
        {
            return false;
        }

        scale = new Vector3(values[0], values[1], values[2]);
        return true;
    }
}
