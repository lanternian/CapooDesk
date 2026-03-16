using Godot;
using System;

public partial class test : Node2D
{
	private double _accum = 0.0;
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		var characterBody = new testCharacter();

		AddChild(characterBody);


	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		return;
		_accum += delta;
		if (_accum < 1.0) return; // 每秒打印一次，避免刷屏
		_accum = 0.0;

		double fps = Performance.GetMonitor(Performance.Monitor.TimeFps);
		double tProcess = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0f;
		double tPhysics = Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0f;
		double drawCalls = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);

		GD.Print($"FPS={fps:0}  Process={tProcess:0.00}ms  Physics={tPhysics:0.00}ms  DrawCalls={drawCalls:0}");
	}
}
