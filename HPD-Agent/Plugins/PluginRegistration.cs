using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.AI;

/// <summary>
/// Represents a plugin registration that can be used to create AIFunctions.
/// </summary>
public class PluginRegistration
{
    /// <summary>
    /// The type of the plugin class.
    /// </summary>
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    public Type PluginType { get; }
    
    /// <summary>
    /// Optional instance of the plugin. If null, a new instance will be created.
    /// </summary>
    public object? Instance { get; }
    
    /// <summary>
    /// Filters to apply to all functions in this plugin.
    /// </summary>
    public IAiFunctionFilter[] Filters { get; }
    
    /// <summary>
    /// Whether this registration uses a pre-created instance.
    /// </summary>
    public bool IsInstance => Instance != null;
    
    /// <summary>
    /// Factory method to register a plugin by type (will be instantiated when needed).
    /// </summary>
    public static PluginRegistration FromType<T>(params IAiFunctionFilter[] filters) where T : class, new()
    {
        return new PluginRegistration(typeof(T), null, filters);
    }
    
    /// <summary>
    /// Factory method to register a plugin by type (backward compatible - no filters).
    /// </summary>
    public static PluginRegistration FromType<T>() where T : class, new()
    {
        return new PluginRegistration(typeof(T), null);
    }
    
    /// <summary>
    /// Factory method to register a plugin by type with a factory function.
    /// </summary>
    public static PluginRegistration FromType(Type pluginType, params IAiFunctionFilter[] filters)
    {
        return new PluginRegistration(pluginType, null, filters);
    }
    
    /// <summary>
    /// Factory method to register a plugin by type (backward compatible - no filters).
    /// </summary>
    public static PluginRegistration FromType(Type pluginType)
    {
        return new PluginRegistration(pluginType, null);
    }
    
    /// <summary>
    /// Factory method to register a plugin using a pre-created instance.
    /// </summary>
    public static PluginRegistration FromInstance<T>(T instance, params IAiFunctionFilter[] filters) where T : class
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        return new PluginRegistration(typeof(T), instance, filters);
    }
    
    /// <summary>
    /// Factory method to register a plugin using a pre-created instance (backward compatible - no filters).
    /// </summary>
    public static PluginRegistration FromInstance<T>(T instance) where T : class
    {
        if (instance == null) throw new ArgumentNullException(nameof(instance));
        return new PluginRegistration(typeof(T), instance);
    }
    
    /// <summary>
    /// Internal constructor to ensure valid state.
    /// </summary>
    private PluginRegistration(Type pluginType, object? instance, params IAiFunctionFilter[] filters)
    {
        PluginType = pluginType ?? throw new ArgumentNullException(nameof(pluginType));
        Instance = instance;
        Filters = filters ?? Array.Empty<IAiFunctionFilter>();
        
        // If instance is provided, validate it matches the type
        if (instance != null && !pluginType.IsInstanceOfType(instance))
        {
            throw new ArgumentException(
                $"Instance type {instance.GetType().Name} does not match plugin type {pluginType.Name}");
        }
    }
    
    /// <summary>
    /// Internal constructor for backward compatibility (no filters).
    /// </summary>
    private PluginRegistration(Type pluginType, object? instance) : this(pluginType, instance, Array.Empty<IAiFunctionFilter>())
    {
    }
    
    /// <summary>
    /// Creates an instance of the plugin if one is not already provided.
    /// </summary>
    public object GetOrCreateInstance()
    {
        if (Instance != null)
            return Instance;
        
        try
        {
            return Activator.CreateInstance(PluginType) 
                ?? throw new InvalidOperationException($"Failed to create instance of {PluginType.Name}");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create instance of plugin {PluginType.Name}. " +
                $"Ensure the plugin has a parameterless constructor.", ex);
        }
    }
    
    /// <summary>
    /// Converts this plugin registration to a list of AIFunctions using the generated registration code.
    /// </summary>
    [RequiresUnreferencedCode("This method uses reflection to call generated plugin registration code.")]
    public List<AIFunction> ToAIFunctions(IPluginMetadataContext? context = null)
    {
        var instance = GetOrCreateInstance();
        
        // Use reflection to find and call the generated registration method
        var registrationTypeName = $"{PluginType.Name}Registration";
        var registrationType = PluginType.Assembly.GetType($"{PluginType.Namespace}.{registrationTypeName}");
        
        if (registrationType == null)
        {
            throw new InvalidOperationException(
                $"Generated registration class {registrationTypeName} not found. " +
                $"Ensure the plugin has been processed by the source generator.");
        }
        
        var createPluginMethod = registrationType.GetMethod("CreatePlugin", 
            BindingFlags.Public | BindingFlags.Static);
        
        if (createPluginMethod == null)
        {
            throw new InvalidOperationException(
                $"CreatePlugin method not found in {registrationTypeName}. " +
                $"Ensure the source generator ran successfully.");
        }
        
        try
        {
            var result = createPluginMethod.Invoke(null, new[] { instance, context });
            return result as List<AIFunction> ?? new List<AIFunction>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create AIFunctions for plugin {PluginType.Name}", ex);
        }
    }
    
    /// <summary>
    /// Converts this plugin registration to a list of AIFunctions with filters applied.
    /// Note: This method is deprecated in favor of conversation-level orchestration.
    /// </summary>
    [RequiresUnreferencedCode("This method uses reflection to call generated plugin registration code.")]
    public List<AIFunction> ToAIFunctions(IPluginMetadataContext? context, params IAiFunctionFilter[] globalFilters)
    {
        // For now, ignore filters and use the basic ToAIFunctions method
        // Filters will be applied at the conversation level
        return ToAIFunctions(context);
    }
}

/// <summary>
/// Manager for plugin registrations and AIFunction creation.
/// </summary>
public class PluginManager
{
    private readonly List<PluginRegistration> _registrations = new();
    private readonly IPluginMetadataContext? _defaultContext;
    
    /// <summary>
    /// Initializes a new instance of the PluginManager.
    /// </summary>
    public PluginManager(IPluginMetadataContext? defaultContext = null)
    {
        _defaultContext = defaultContext;
    }
    
    /// <summary>
    /// Registers a plugin by type.
    /// </summary>
    public PluginManager RegisterPlugin<T>() where T : class, new()
    {
        _registrations.Add(PluginRegistration.FromType<T>());
        return this;
    }
    
    /// <summary>
    /// Registers a plugin by type with filters applied to all its functions.
    /// </summary>
    public PluginManager RegisterPlugin<T>(params IAiFunctionFilter[] filters) where T : class, new()
    {
        _registrations.Add(PluginRegistration.FromType<T>(filters));
        return this;
    }
    
    /// <summary>
    /// Registers a plugin by type.
    /// </summary>
    public PluginManager RegisterPlugin(Type pluginType)
    {
        _registrations.Add(PluginRegistration.FromType(pluginType));
        return this;
    }
    
    /// <summary>
    /// Registers a plugin by type with filters applied to all its functions.
    /// </summary>
    public PluginManager RegisterPlugin(Type pluginType, params IAiFunctionFilter[] filters)
    {
        _registrations.Add(PluginRegistration.FromType(pluginType, filters));
        return this;
    }
    
    /// <summary>
    /// Registers a plugin using an instance.
    /// </summary>
    public PluginManager RegisterPlugin<T>(T instance) where T : class
    {
        _registrations.Add(PluginRegistration.FromInstance(instance));
        return this;
    }
    
    /// <summary>
    /// Registers a plugin using an instance with filters applied to all its functions.
    /// </summary>
    public PluginManager RegisterPlugin<T>(T instance, params IAiFunctionFilter[] filters) where T : class
    {
        _registrations.Add(PluginRegistration.FromInstance(instance, filters));
        return this;
    }
    
    /// <summary>
    /// Creates all AIFunctions from registered plugins without filters (for conversation-level orchestration).
    /// </summary>
    [RequiresUnreferencedCode("This method calls plugin registration methods that use reflection.")]
    public List<AIFunction> CreateAllFunctions(IPluginMetadataContext? context = null)
    {
        var effectiveContext = context ?? _defaultContext;
        var allFunctions = new List<AIFunction>();
        
        foreach (var registration in _registrations)
        {
            try
            {
                // Call the ToAIFunctions without filters since orchestration is now at conversation level
                var functions = registration.ToAIFunctions(effectiveContext);
                allFunctions.AddRange(functions);
            }
            catch (Exception ex)
            {
                // Log error but continue with other plugins
                Console.WriteLine($"Failed to create functions for plugin {registration.PluginType.Name}: {ex.Message}");
            }
        }
        
        return allFunctions;
    }
    
    /// <summary>
    /// Creates all AIFunctions from registered plugins with global filters applied.
    /// </summary>
    [RequiresUnreferencedCode("This method calls plugin registration methods that use reflection.")]
    public List<AIFunction> CreateAllFunctions(IPluginMetadataContext? context, params IAiFunctionFilter[] globalFilters)
    {
        var effectiveContext = context ?? _defaultContext;
        var allFunctions = new List<AIFunction>();
        
        foreach (var registration in _registrations)
        {
            try
            {
                // Call the ToAIFunctions with global filters (legacy behavior)
                var functions = registration.ToAIFunctions(effectiveContext, globalFilters);
                allFunctions.AddRange(functions);
            }
            catch (Exception ex)
            {
                // Log error but continue with other plugins
                Console.WriteLine($"Failed to create functions for plugin {registration.PluginType.Name}: {ex.Message}");
            }
        }
        
        return allFunctions;
    }
    
    /// <summary>
    /// Gets all registered plugin registrations
    /// </summary>
    public IReadOnlyList<PluginRegistration> GetPluginRegistrations() => _registrations.AsReadOnly();
    
    /// <summary>
    /// Gets all registered plugin types.
    /// </summary>
    public IReadOnlyList<Type> GetRegisteredPluginTypes()
    {
        return _registrations.Select(r => r.PluginType).ToList().AsReadOnly();
    }
    
    /// <summary>
    /// Clears all plugin registrations.
    /// </summary>
    public void Clear()
    {
        _registrations.Clear();
    }
}
