/// <summary>
/// Behaviour states of a Capoo. The logic for each state lives in CapooController.Behavior.cs.
/// </summary>
public enum CapooState
{
	Appear,       // summon effect
	Idle,
	Walk,
	Run,
	Wild,         // sprint
	React,        // one-shot reaction (hit, stackDown, ...) then resume
	Pet,          // being rubbed by the cursor
	Enjoy,        // after petting
	Dragged,
	Air,          // falling
	Roll,         // thrown upwards
	Land,
	Dizzy,
	Hop,          // small jump off a ledge or a stack
	Hungry,       // waiting for food (bubble)
	Eating,
	Pooing,
	WaitCleanPoo, // waiting for cleanup (bubble)
	PooRemoving,
	Sleepy,
	Sleeping,
	Waking,
	Stacked,      // standing on another Capoo's head
	Recycle
}

public static class CapooStateExtensions
{
	/// <summary>Standing on the ground and free to start something new.</summary>
	public static bool IsGroundIdle(this CapooState s) =>
		s is CapooState.Idle or CapooState.Walk or CapooState.Run or CapooState.Wild or CapooState.Enjoy;

	public static bool IsMoving(this CapooState s) =>
		s is CapooState.Walk or CapooState.Run or CapooState.Wild;

	public static bool IsAirborne(this CapooState s) =>
		s is CapooState.Air or CapooState.Roll;

	/// <summary>States where a long press may pick the Capoo up.</summary>
	public static bool CanBeDragged(this CapooState s) =>
		s is not (CapooState.Appear or CapooState.Recycle or CapooState.Pooing
			or CapooState.Eating or CapooState.PooRemoving or CapooState.Dragged);

	/// <summary>States where another Capoo may land on this one's head.</summary>
	public static bool CanCarryStack(this CapooState s) =>
		s is CapooState.Idle or CapooState.Walk or CapooState.Enjoy or CapooState.React
			or CapooState.Stacked or CapooState.Sleepy or CapooState.Sleeping or CapooState.Hungry
			or CapooState.Land or CapooState.Dizzy;
}
