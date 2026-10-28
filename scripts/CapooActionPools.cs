using System;

/// <summary>
/// Pools of action names (Capoo_Action.json + Capoo_Action_Extra.json) for random selection.
/// </summary>
public static class CapooActionPools
{
	public static readonly string[] Idle =
		{ "idle_idle1", "idle_idle2", "idle_idle3", "idle_idle4", "idle_idle5", "idle_idle6" };

	public static readonly string[] IdleFull =
		{ "idle_full1", "idle_full2" };

	public static readonly string[] Walking =
		{ "walk1", "walk2", "walk3", "walk4", "walk5", "walk6" };

	public static readonly string[] Running =
		{ "run1", "run2", "run3", "run4", "run5", "run6", "run7", "run8", "run9" };

	public static readonly string[] Wild =
		{ "wild1", "wild2", "wild3", "wild4", "wild5" };

	public static readonly string[] Hit =
		{ "hit1", "hit2", "hit3", "hit4", "hit5", "hit6", "hit7" };

	public static readonly string[] Pet =
		{ "pet1", "pet2", "pet3", "pet4", "pet5" };

	public static readonly string[] CatchUp =
		{ "catchUp1", "catchUp2", "catchUp3", "catchUp4" };

	public static readonly string[] Prone =
		{ "prone1", "prone2", "prone3" };

	/// <summary>Bumped by a thrown Capoo.</summary>
	public static readonly string[] Dizzy =
		{ "dizzy1", "dizzy2", "dizzy3", "dizzy4" };

	public static readonly string[] Enjoy =
		{ "enjoy1", "enjoy2", "enjoy3", "comfortable1", "comfortable2" };

	public static readonly string[] AfterEat =
		{ "enjoy1", "enjoy2", "enjoy3" };

	public static readonly string[] FoodAppear =
		{ "foodAppear1", "foodAppear2", "foodAppear3", "foodAppear4" };

	public static readonly string[] Roll =
		{ "roll1", "roll2", "roll3", "roll4", "roll5" };

	public static readonly string[] StackDown =
		{ "stackDown1", "stackDown2", "stackDown3" };

	public static readonly string[] Hungry =
		{ "hungry1", "hungry2" };

	public static readonly string[] Sleep =
		{ "sleep1", "sleep2" };

	public static readonly string[] WakeUp =
		{ "wakeUp1", "wakeUp2" };

	public static string RandomFrom(string[] pool)
	{
		return pool[Random.Shared.Next(pool.Length)];
	}
}
