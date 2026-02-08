using Godot;
using System;
using System.Collections.Generic;
using Lexmancer.Abilities.V2;

namespace Lexmancer.Abilities.Execution;

/// <summary>
/// Manages delayed and repeated action execution
/// </summary>
public partial class DelayedActionExecutor : Node
{
	private struct ScheduledAction
	{
		public List<EffectAction> Actions;
		public EffectContext Context;
		public float Delay;
		public int RepeatsLeft;
		public float Interval;
	}

	private List<ScheduledAction> scheduledActions = new();

	/// <summary>
	/// Schedule actions to execute multiple times with intervals
	/// </summary>
	public void ScheduleActions(List<EffectAction> actions, EffectContext context, int count, float interval)
	{
		if (actions == null || actions.Count == 0 || count <= 0)
			return;

		scheduledActions.Add(new ScheduledAction
		{
			Actions = actions,
			Context = context,
			Delay = interval, // First execution after interval
			RepeatsLeft = count,
			Interval = interval
		});

		GD.Print($"Scheduled {actions.Count} actions to repeat {count} times with {interval}s interval");
	}

	public override void _Process(double delta)
	{
		if (scheduledActions.Count == 0)
			return;

		// Process all scheduled actions
		for (int i = scheduledActions.Count - 1; i >= 0; i--)
		{
			var scheduled = scheduledActions[i];
			scheduled.Delay -= (float)delta;

			if (scheduled.Delay <= 0)
			{
				// Execute actions
				ExecuteScheduledActions(scheduled.Actions, scheduled.Context);

				// Reschedule if repeats remain
				scheduled.RepeatsLeft--;
				if (scheduled.RepeatsLeft > 0)
				{
					scheduled.Delay = scheduled.Interval;
					scheduledActions[i] = scheduled;
					GD.Print($"Executed scheduled actions. {scheduled.RepeatsLeft} repeats remaining.");
				}
				else
				{
					// All repeats complete, remove from list
					scheduledActions.RemoveAt(i);
					GD.Print("Scheduled actions completed.");
				}
			}
			else
			{
				// Update the delay
				scheduledActions[i] = scheduled;
			}
		}
	}

	/// <summary>
	/// Execute a set of scheduled actions
	/// </summary>
	private void ExecuteScheduledActions(List<EffectAction> actions, EffectContext context)
	{
		// Get world node (root of scene tree)
		var worldNode = GetTree().Root;

		// Create interpreter and execute all actions
		var interpreter = new EffectInterpreter(worldNode);
		foreach (var action in actions)
		{
			interpreter.Execute(action, context);
		}
	}

	/// <summary>
	/// Get number of currently scheduled action groups
	/// </summary>
	public int GetScheduledCount()
	{
		return scheduledActions.Count;
	}
}
