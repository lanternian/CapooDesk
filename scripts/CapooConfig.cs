using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
public partial class CapooConfig : Node
{
	public List<CapooInfo> infos;

	public List<CapooAction> actions;

	public GameParameter parameters = new GameParameter();

	private readonly Dictionary<string, CapooAction> actionMap = new Dictionary<string, CapooAction>();

	public override void _Ready()
	{
		infos = LoadJson<List<CapooInfo>>("res://assets/json/Capoo_Info.json") ?? new List<CapooInfo>();
		foreach (var info in infos) TrimStrings(info);

		actions = LoadJson<List<CapooAction>>("res://assets/json/Capoo_Action.json") ?? new List<CapooAction>();
		// Our own actions composed from existing layer animations (sleep, hungry, ...).
		var extra = LoadJson<List<CapooAction>>("res://assets/json/Capoo_Action_Extra.json");
		if (extra != null) actions.AddRange(extra);
		foreach (var action in actions)
		{
			TrimStrings(action);
			actionMap[action.action_name] = action;
		}

		var gameParameters = LoadJson<List<GameParameter>>("res://assets/json/Game_Parameter.json");
		if (gameParameters != null && gameParameters.Count > 0) parameters = gameParameters[0];
	}

	public CapooAction GetAction(string actionName)
	{
		if (actionName != null && actionMap.TryGetValue(actionName, out var action)) return action;
		GD.PushWarning($"Unknown Capoo action: {actionName}");
		return null;
	}

	public bool HasAction(string actionName) => actionName != null && actionMap.ContainsKey(actionName);

	private static T LoadJson<T>(string path) where T : class
	{
		if (!FileAccess.FileExists(path)) return null;
		var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file == null)
		{
			GD.PrintErr($"Failed to open {path}.");
			return null;
		}
		string jsonText = file.GetAsText();
		file.Close();
		return JsonSerializer.Deserialize<T>(jsonText);
	}

	/// <summary>The tables contain values like " " that should count as empty.</summary>
	private static void TrimStrings(object obj)
	{
		foreach (var prop in obj.GetType().GetProperties())
		{
			if (prop.PropertyType == typeof(string) && prop.CanWrite)
				prop.SetValue(obj, ((string)prop.GetValue(obj))?.Trim() ?? "");
		}
	}
}

public class GameParameter
{
	public float idleTimeMin { get; set; } = 2;
	public float idleTimeMax { get; set; } = 5;
	public float hungryProbability { get; set; } = 1;
	public float pooProbability { get; set; } = 1;
	public float idleProbability { get; set; } = 1;
	public float walkProbability { get; set; } = 5;
	public float runProbability { get; set; } = 1;
	public float wildProbability { get; set; } = 1;
	public float walkSpeed { get; set; } = 1;
	public float runSpeed { get; set; } = 2;
	public float wildSpeed { get; set; } = 4;
	public float walkRadius { get; set; } = 200;
	public float runRadius { get; set; } = 400;
	public float wildRadius { get; set; } = 600;
	public float stayActionDuration { get; set; } = 10;
}

public class CapooInfo
{

	public string id { get; set; }
	public string name { get; set; }

	[JsonConverter(typeof(ToFloatConverter))]
	public float collider_Adjust { get; set; } = 1;
	public string body { get; set; }
	public string body_Anim { get; set; }
	[JsonConverter(typeof(ToBoolConverter))]
	public bool handIndex { get; set; }
	public string face { get; set; }
	public string face_Anim { get; set; }
	public string face_decoration { get; set; }
	public string face_decoration_Anim { get; set; }
	public string hat { get; set; }
	public string hat_Anim { get; set; }
	public string clothes { get; set; }	
	public string clothes_Anim { get; set; }
	public string back { get; set; }
	public string back_hierarchy { get; set; }
	public string back_Anim { get; set; }
	public string right_hand_item { get; set; }
	public string right_hand_item_Anim { get; set; }
	public string capoo_acc_top { get; set; }
	public string capoo_acc_top_Anim { get; set; }
	public string capoo_acc_body { get; set; }
	public string capoo_acc_body_Anim { get; set; }
	public string capoo_acc_bot { get; set; }
	public string capoo_acc_bot_Anim { get; set; }
	public string mood { get; set; }
	public string mood_Anim { get; set; }
	public string efx_bot { get; set; }
	public string efx_bot_Anim { get; set; }
	public string efx_top { get; set; }
	public string efx_top_Anim { get; set; }
	public string efx_appears { get; set; }
	public string efx_appears_Anim { get; set; }
	public string talk { get; set; }
	public string talk_Anim { get; set; }
	public string food { get; set; }
	public string food_Anim { get; set; }
	public string poo { get; set; }
	public string poo_Anim { get; set; }
}

public class CapooAction
{
	public string action_name { get; set; }
	public string body { get; set; }
	[JsonConverter(typeof(ToBoolConverter))]
	public bool body_loop { get; set; }
	public string face { get; set; }
	[JsonConverter(typeof(ToBoolConverter))]
	public bool face_loop { get; set; }
	public string mood { get; set; }
	[JsonConverter(typeof(ToBoolConverter))]
	public bool mood_loop { get; set; }
	public string effectTop { get; set; }
	[JsonConverter(typeof(ToBoolConverter))]
	public bool effectTop_loop { get; set; }
	public string effectBot { get; set; }
	[JsonConverter(typeof(ToBoolConverter))]
	public bool effectBot_loop { get; set; }
	public string talk { get; set; }
	[JsonConverter(typeof(ToBoolConverter))]
	public bool talk_loop { get; set; }
	public string appear { get; set; }
	[JsonConverter(typeof(ToBoolConverter))]
	public bool appear_loop { get; set; }
	public string food { get; set; }
	[JsonConverter(typeof(ToBoolConverter))]
	public bool food_loop { get; set; }
	public string poo { get; set; }
	[JsonConverter(typeof(ToBoolConverter))]
	public bool poo_loop { get; set; }
}


public class ToFloatConverter : JsonConverter<float>
{
	public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType == JsonTokenType.Number) return reader.GetSingle();
		if (reader.TokenType == JsonTokenType.String
			&& float.TryParse(reader.GetString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float value))
			return value;
		return 1;
	}

	public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
	}
}

public class ToBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var str = reader.GetString();
            return str == "1" || str?.ToLower() == "true";
        }
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetDouble() != 0;
        }
        if (reader.TokenType == JsonTokenType.True) return true;
        if (reader.TokenType == JsonTokenType.False) return false;
        throw new JsonException();
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value ? "1" : "0");
    }
}
