using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
public partial class CapooConfig : Node
{
	public List<CapooInfo> infos;

	public List<CapooAction> actions;

	public override void _Ready()
	{

        var file = FileAccess.Open("res://assets/json/Capoo_Info.json", FileAccess.ModeFlags.Read);
        if (file != null)
        {
            string jsonText = file.GetAsText();
            file.Close();
            infos = JsonSerializer.Deserialize<List<CapooInfo>>(jsonText);
        }
        else
        {
            GD.PrintErr("Failed to open Capoo_Info.json file.");
        }

        file = FileAccess.Open("res://assets/json/Capoo_Action.json", FileAccess.ModeFlags.Read);
        if (file != null)
        {
            string jsonText = file.GetAsText();
            file.Close();
            actions = JsonSerializer.Deserialize<List<CapooAction>>(jsonText);
        }
        else
        {
            GD.PrintErr("Failed to open Capoo_Action.json file.");
        }
	}

	public override void _Process(double delta)
	{
	}
}

public class CapooInfo
{

	public string id { get; set; }
	public string name { get; set; }

	[JsonConverter(typeof(ToBoolConverter))]
	public bool collider_Adjust { get; set; }
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

			// GD.Print(JsonSerializer.Serialize(infos[0], new JsonSerializerOptions { WriteIndented = true }));
			// usage example:
			// var config = GetNode<CapooConfig>("/root/CapooConfig");
			// var infos = config.infos;
			// var capooInfo = infos.Find(infos => infos.name == "鴨帽咖波");
			// GD.Print(capooInfo.id);
/*
	"id": "capoo_001",
	"name": "咖波",
	"collider_Adjust": "1",
	"body": "capoo_basic_001",
	"body_Anim": "",
	"handIndex": 0.0,
	"face": "capoo_basic_face_001",
	"face_Anim": "",
	"face_decoration": "",
	"face_decoration_Anim": "",
	"hat": "capoo_hat_009",
	"hat_Anim": "animation",
	"clothes": "",
	"clothes_Anim": "",
	"back": "",
	"back_hierarchy": "",
	"back_Anim": "",
	"right_hand_item": "",
	"right_hand_item_Anim": "",
	"capoo_acc_top": "",
	"capoo_acc_top_Anim": "",
	"capoo_acc_body": "",
	"capoo_acc_body_Anim": "",
	"capoo_acc_bot": "",
	"capoo_acc_bot_Anim": "",
	"mood": "capoo_mood",
	"mood_Anim": "",
	"efx_bot": "capoo_efx_bot",
	"efx_bot_Anim": "",
	"efx_top": "capoo_efx_top",
	"efx_top_Anim": "",
	"efx_appears": "efx_capoo_appears_01",
	"efx_appears_Anim": "",
	"talk": "char_talk",
	"talk_Anim": "",
	"food": "capoo_food",
	"food_Anim": "",
	"poo": "capoo_poo",
	"poo_Anim": ""
*/
