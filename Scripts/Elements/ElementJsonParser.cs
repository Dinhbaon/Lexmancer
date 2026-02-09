using System;
using System.Text.Json;
using Lexmancer.Abilities.V2;

namespace Lexmancer.Elements
{
public static class ElementJsonParser
{
	public static bool TryParseElement(
		string jsonText,
		out string name,
		out string description,
		out string colorHex,
		out AbilityV2 ability,
		out string error)
	{
		name = "";
		description = "";
		colorHex = "#808080";
		ability = null;
		error = "";

		try
		{
			using var doc = JsonDocument.Parse(jsonText);
			var root = doc.RootElement;

			name = GetStringProperty(root, "name", "");
			description = GetStringProperty(root, "description", "");

			colorHex = GetStringProperty(root, "colorHex", "");
			if (string.IsNullOrWhiteSpace(colorHex))
			{
				colorHex = GetStringProperty(root, "color", "#808080");
			}

			if (TryGetPropertyIgnoreCase(root, "ability", out var abilityElement))
			{
				ability = AbilityBuilder.FromLLMResponse(abilityElement.GetRawText());
			}

			return true;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return false;
		}
	}

	private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
	{
		if (element.ValueKind == JsonValueKind.Object)
		{
			foreach (var prop in element.EnumerateObject())
			{
				if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
				{
					value = prop.Value;
					return true;
				}
			}
		}

		value = default;
		return false;
	}

	private static string GetStringProperty(JsonElement element, string name, string defaultValue)
	{
		return TryGetPropertyIgnoreCase(element, name, out var prop) ? prop.GetString() : defaultValue;
	}
}
}
