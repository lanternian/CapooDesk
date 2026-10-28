using Godot;
using System;

public partial class test : Node2D
{
	private double _accum = 0.0;

	public override void _Ready()
	{
		// Capoo is already in the scene (test.tscn), don't create another one
	}

	public override void _Process(double delta)
	{
		_accum += delta;
		if (_accum < 2.0) return;
		_accum = 0.0;

		double fps = Performance.GetMonitor(Performance.Monitor.TimeFps);
		double tProcess = Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000.0;
		double tPhysics = Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000.0;
		double drawCalls = Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
		double nodeCount = Performance.GetMonitor(Performance.Monitor.ObjectNodeCount);

		GD.Print($"FPS={fps:0}  Process={tProcess:0.00}ms  Physics={tPhysics:0.00}ms  DrawCalls={drawCalls:0}  Nodes={nodeCount:0}");
	}
}
