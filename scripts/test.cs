using Godot;
using System;

public partial class test : Node2D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var characterBody = new testCharacter();

		AddChild(characterBody);


	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
