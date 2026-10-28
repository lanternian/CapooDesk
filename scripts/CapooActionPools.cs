using Godot;
using System;

/// <summary>
/// Maps CapooState to pools of action names for random animation selection.
/// </summary>
public static class CapooActionPools
{
	public static readonly string[] Idle =
		{ "idle_idle1", "idle_idle2", "idle_idle3", "idle_idle4", "idle_idle5", "idle_idle6" };

	public static readonly string[] Walking =
		{ "walk1", "walk2", "walk3", "walk4", "walk5", "walk6" };

	public static readonly string[] Running =
		{ "run1", "run2", "run3", "run4", "run5", "run6", "run7", "run8", "run9" };

	public static readonly string[] Hit =
		{ "hit1", "hit2", "hit3", "hit4", "hit5", "hit6", "hit7" };

	public static readonly string[] Pet =
		{ "pet1", "pet2", "pet3", "pet4", "pet5" };

	public static readonly string[] CatchUp =
		{ "catchUp1", "catchUp2", "catchUp3", "catchUp4" };

	public static readonly string[] Prone =
		{ "prone1", "prone2", "prone3", "prone4" };

	public static readonly string[] Dizzy =
		{ "dizzy1", "dizzy2", "dizzy3", "dizzy4", "dizzy_dizzy", "dizzy_soul" };

	public static readonly string[] Enjoy =
		{ "enjoy1", "enjoy2", "enjoy3" };

	public static readonly string[] FoodAppear =
		{ "foodAppear1", "foodAppear2", "foodAppear3", "foodAppear4" };

	public static readonly string[] Roll =
		{ "roll1", "roll2", "roll3", "roll4", "roll5" };

	public static readonly string[] StackDown =
		{ "stackDown1", "stackDown2", "stackDown3" };

	public static string RandomFrom(string[] pool)
	{
		return pool[Random.Shared.Next(pool.Length)];
	}

	public static string[] GetPoolForState(CapooState state)
	{
		return state switch
		{
			CapooState.Idle => Idle,
			CapooState.Walking => Walking,
			CapooState.Running => Running,
			CapooState.Sleeping => new[] { "idle_idle5" },
			CapooState.Dragged => CatchUp,
			CapooState.Falling => new[] { "air" },
			CapooState.Petting => Pet,
			CapooState.Eating => FoodAppear,
			CapooState.Pooping => new[] { "pooing" },
			CapooState.Stacked => Prone,
			CapooState.Dizzy => Dizzy,
			_ => Idle
		};
	}

	public static string RandomActionForState(CapooState state)
	{
		return RandomFrom(GetPoolForState(state));
	}
}
