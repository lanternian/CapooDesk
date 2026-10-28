using Godot;
using System;

public enum CapooState
{
	Idle,
	Walking,
	Running,
	Sleeping,
	Dragged,
	Falling,
	Petting,
	Eating,
	Pooping,
	Stacked,
	Dizzy
}

public partial class CapooStateMachine : Node
{
	private CapooState _currentState = CapooState.Idle;
	private CapooState _previousState = CapooState.Idle;

	public CapooState CurrentState => _currentState;
	public CapooState PreviousState => _previousState;

	public double StateTimer { get; private set; } = 0.0;
	public double StateDuration { get; private set; } = 0.0;

	public event Action<CapooState, CapooState> OnStateChanged;

	public override void _Process(double delta)
	{
		StateTimer += delta;
	}

	public bool CanTransitionTo(CapooState newState)
	{
		return newState switch
		{
			CapooState.Idle => _currentState != CapooState.Idle,
			CapooState.Walking => _currentState is CapooState.Idle or CapooState.Running or CapooState.Walking,
			CapooState.Running => _currentState is CapooState.Idle or CapooState.Walking or CapooState.Running,
			CapooState.Sleeping => _currentState is CapooState.Idle,
			CapooState.Dragged => _currentState is not (CapooState.Dragged or CapooState.Falling),
			CapooState.Falling => _currentState is CapooState.Dragged or CapooState.Stacked,
			CapooState.Petting => _currentState is CapooState.Idle or CapooState.Walking,
			CapooState.Eating => _currentState is CapooState.Idle or CapooState.Walking,
			CapooState.Pooping => _currentState is CapooState.Idle or CapooState.Walking,
			CapooState.Stacked => _currentState is not CapooState.Dragged,
			CapooState.Dizzy => _currentState is CapooState.Idle or CapooState.Walking,
			_ => false
		};
	}

	public bool TransitionTo(CapooState newState)
	{
		if (!CanTransitionTo(newState)) return false;

		_previousState = _currentState;
		_currentState = newState;
		StateTimer = 0.0;
		StateDuration = 0.0;
		OnStateChanged?.Invoke(_previousState, _currentState);
		return true;
	}

	public void SetStateDuration(double duration)
	{
		StateDuration = duration;
	}

	public bool IsStateTimerExpired()
	{
		return StateDuration > 0 && StateTimer >= StateDuration;
	}

	public void ForceState(CapooState state)
	{
		_previousState = _currentState;
		_currentState = state;
		StateTimer = 0.0;
		StateDuration = 0.0;
		OnStateChanged?.Invoke(_previousState, _currentState);
	}
}
