using Godot;
using System;

public partial class CapooNeeds : Node
{
	// Current need levels (0.0 - 1.0)
	public float HungerLevel { get; private set; } = 0.0f;
	public float PoopUrgency { get; private set; } = 0.0f; // increases after eating
	public float MoodLevel { get; private set; } = 1.0f;   // starts happy

	// Configurable rates
	public float HungerRate { get; set; } = 0.01f;       // per second
	public float PoopRate { get; set; } = 0.005f;        // per second after eating
	public float MoodDecayRate { get; set; } = 0.002f;   // natural mood decay per second
	public float PetMoodBoost { get; set; } = 0.15f;
	public float HitMoodPenalty { get; set; } = 0.05f;
	public float FeedHungerReduction { get; set; } = 0.3f;
	public float FeedPoopIncrease { get; set; } = 0.2f;

	// Thresholds
	public float HungryThreshold { get; set; } = 0.7f;
	public float PoopThreshold { get; set; } = 0.8f;

	public void Tick(double delta)
	{
		float dt = (float)delta;

		// Hunger increases over time
		HungerLevel = Clamp01(HungerLevel + HungerRate * dt);

		// Poop urgency increases slowly over time, faster when hungry just ate
		PoopUrgency = Clamp01(PoopUrgency + PoopRate * dt);

		// Mood naturally decays, faster when hungry
		float decayMultiplier = IsHungry() ? 2.0f : 1.0f;
		MoodLevel = Clamp01(MoodLevel - MoodDecayRate * decayMultiplier * dt);
	}

	public bool IsHungry()
	{
		return HungerLevel > HungryThreshold;
	}

	public bool NeedsPoop()
	{
		return PoopUrgency >= PoopThreshold;
	}

	public void Feed(float amount)
	{
		float feedAmount = amount > 0 ? amount : FeedHungerReduction;
		HungerLevel = Clamp01(HungerLevel - feedAmount);
		PoopUrgency = Clamp01(PoopUrgency + FeedPoopIncrease);
		// Eating improves mood a little
		MoodLevel = Clamp01(MoodLevel + 0.05f);
	}

	public void CleanPoop()
	{
		PoopUrgency = 0.0f;
		// Relief improves mood
		MoodLevel = Clamp01(MoodLevel + 0.1f);
	}

	public void Pet()
	{
		MoodLevel = Clamp01(MoodLevel + PetMoodBoost);
	}

	public void Hit()
	{
		MoodLevel = Clamp01(MoodLevel - HitMoodPenalty);
	}

	private static float Clamp01(float value)
	{
		return Mathf.Clamp(value, 0.0f, 1.0f);
	}
}
