using System;

/// <summary>
/// Gentle care timers: a Capoo may get hungry or need to poo once enough time has passed
/// since it last ate / pooed. Ignoring it has no consequences, it just asks again later.
/// </summary>
public class CapooNeeds
{
	// Minutes between requests for care levels 0 (off), 1 (low), 2 (normal), 3 (often).
	private static readonly double[] HungerMinutes = { double.PositiveInfinity, 40, 15, 5 };
	private static readonly double[] PooMinutes = { double.PositiveInfinity, 50, 20, 7 };

	private double sinceFed;
	private double sincePoo;

	public bool JustAte { get; set; }

	public CapooNeeds(int careLevel)
	{
		// Stagger the first request so a freshly restored desktop doesn't get hungry all at once.
		double hunger = Interval(HungerMinutes, careLevel);
		double poo = Interval(PooMinutes, careLevel);
		sinceFed = double.IsInfinity(hunger) ? 0 : Random.Shared.NextDouble() * 0.8 * hunger;
		sincePoo = double.IsInfinity(poo) ? 0 : Random.Shared.NextDouble() * 0.8 * poo;
	}

	public void Tick(double delta)
	{
		sinceFed += delta;
		sincePoo += delta;
	}

	public bool CanBeHungry(int careLevel) => sinceFed >= Interval(HungerMinutes, careLevel);
	public bool CanPoo(int careLevel) => sincePoo >= Interval(PooMinutes, careLevel);

	public void Fed()
	{
		sinceFed = 0;
		JustAte = true;
		// Eating makes a poo come a bit sooner.
		sincePoo += 60;
	}

	/// <summary>Nobody answered the bubble: ask again after half an interval.</summary>
	public void HungerIgnored(int careLevel) => sinceFed = Interval(HungerMinutes, careLevel) * 0.5;

	public void Pooed() => sincePoo = 0;

	private static double Interval(double[] table, int careLevel) => table[Math.Clamp(careLevel, 0, 3)] * 60.0;
}
