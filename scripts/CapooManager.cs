using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Manages all Capoo instances: spawning, despawning, stacking, and merging.
/// Autoload or add to scene.
/// </summary>
public partial class CapooManager : Node2D
{
	public const int MaxCapoos = 12;

	private List<CapooController> capoos = new List<CapooController>();

	public IReadOnlyList<CapooController> AllCapoos => capoos;

	public CapooController SpawnCapoo(int infoIndex, Vector2 position)
	{
		if (capoos.Count >= MaxCapoos) return null;

		var config = GetNode<CapooConfig>("/root/CapooConfig");
		if (infoIndex < 0 || infoIndex >= config.infos.Count) return null;

		var controller = new CapooController();
		controller.Name = $"Capoo_{infoIndex}_{capoos.Count}";
		controller.SetInfoIndex(infoIndex);
		controller.Position = position;
		AddChild(controller);
		capoos.Add(controller);
		return controller;
	}

	public void RemoveCapoo(CapooController capoo)
	{
		if (!capoos.Contains(capoo)) return;

		capoos.Remove(capoo);
		capoo.QueueFree();
	}

	public void RemoveAllCapoos()
	{
		foreach (var capoo in new List<CapooController>(capoos))
		{
			RemoveCapoo(capoo);
		}
	}

	public int GetCapooCount() => capoos.Count;

	public override void _Process(double delta)
	{
		// Clean up destroyed capoos
		capoos.RemoveAll(c => !IsInstanceValid(c));
	}
}
