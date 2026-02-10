using Godot;
using System;
using Lexmancer.Services;

namespace Lexmancer.Core;

/// <summary>
/// Global service locator providing access to core game services.
/// This is a Godot autoload singleton - configure in Project Settings > Autoload.
///
/// Usage:
///   var services = ServiceLocator.Instance;
///   var element = services.Elements.GetElement(42);
///   services.Config.SetUseLLM(true);
/// </summary>
public partial class ServiceLocator : Node
{
	// Singleton instance (set by Godot autoload system)
	private static ServiceLocator _instance;
	public static ServiceLocator Instance
	{
		get
		{
			if (_instance == null)
			{
				GD.PrintErr("ServiceLocator not initialized! Make sure it's configured as an autoload in Project Settings.");
			}
			return _instance;
		}
	}

	// Core Services (lazily initialized from autoloads)
	private EventBus _eventBus;
	private ElementService _elementService;
	private ConfigService _configService;
	private CombatService _combatService;

	public EventBus Events => _eventBus ??= GetNode<EventBus>("/root/EventBus");
	public ElementService Elements => _elementService ??= GetNode<ElementService>("/root/ElementService");
	public ConfigService Config => _configService ??= GetNode<ConfigService>("/root/ConfigService");
	public CombatService Combat => _combatService ??= GetNode<CombatService>("/root/CombatService");

	// TODO: Add more services as we create them
	// public CombatService Combat { get; private set; }
	// public AbilityService Abilities { get; private set; }

	public override void _EnterTree()
	{
		if (_instance != null && _instance != this)
		{
			GD.PrintErr("Multiple ServiceLocator instances detected! Only one should exist as autoload.");
			QueueFree();
			return;
		}
		_instance = this;
		GD.Print("ServiceLocator initialized");
	}

	public override void _Ready()
	{
		// Verify services are available
		InitializeServices();
	}

	public override void _ExitTree()
	{
		if (_instance == this)
		{
			_instance = null;
		}
	}

	private void InitializeServices()
	{
		// Verify all required services are available
		bool allServicesReady = true;

		if (Events == null)
		{
			GD.PrintErr("✗ EventBus not found! Make sure it's configured as autoload.");
			allServicesReady = false;
		}
		else
		{
			GD.Print("✓ EventBus ready");
		}

		if (Elements == null)
		{
			GD.PrintErr("✗ ElementService not found! Make sure it's configured as autoload.");
			allServicesReady = false;
		}
		else
		{
			GD.Print("✓ ElementService ready");
		}

		if (Config == null)
		{
			GD.PrintErr("✗ ConfigService not found! Make sure it's configured as autoload.");
			allServicesReady = false;
		}
		else
		{
			GD.Print("✓ ConfigService ready");
		}

		if (Combat == null)
		{
			GD.PrintErr("✗ CombatService not found! Make sure it's configured as autoload.");
			allServicesReady = false;
		}
		else
		{
			GD.Print("✓ CombatService ready");
		}

		if (allServicesReady)
		{
			GD.Print("✓ All services initialized successfully");
		}
		else
		{
			GD.PrintErr("✗ Some services failed to initialize! Check autoload configuration.");
		}
	}

	/// <summary>
	/// Get a service by type (for generic access)
	/// </summary>
	public T GetService<T>() where T : Node
	{
		var serviceName = typeof(T).Name;

		// Try to get from autoload by name
		var service = GetNodeOrNull<T>($"/root/{serviceName}");
		if (service != null)
			return service;

		throw new InvalidOperationException($"Service {serviceName} not found in autoloads");
	}

	/// <summary>
	/// Print debug info about all services
	/// </summary>
	public void PrintServiceStatus()
	{
		GD.Print("=== ServiceLocator Status ===");
		GD.Print($"EventBus: {(Events != null ? "✓" : "✗")}");
		GD.Print($"ElementService: {(Elements != null ? "✓" : "✗")}");
		GD.Print($"ConfigService: {(Config != null ? "✓" : "✗")}");
		GD.Print($"CombatService: {(Combat != null ? "✓" : "✗")}");
		GD.Print("============================");
	}

}
