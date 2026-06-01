using System;
using System.Collections.Generic;
using SSH_Helper.Services.Scripting.Functions;

namespace SSH_Helper.Services.Scripting
{
    /// <summary>
    /// Central registry for all scripting built-in functions.
    /// Functions are registered by category classes and dispatched by name.
    /// </summary>
    public class FunctionRegistry
    {
        private static readonly Lazy<FunctionRegistry> _instance = new(() =>
        {
            var registry = new FunctionRegistry();
            registry.RegisterBuiltInCategories();
            return registry;
        });

        /// <summary>
        /// Singleton instance with all built-in functions pre-registered.
        /// </summary>
        public static FunctionRegistry Instance => _instance.Value;

        private readonly Dictionary<string, ScriptFunction> _functions = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates an empty registry. Use <see cref="Instance"/> for the pre-populated singleton.
        /// </summary>
        public FunctionRegistry()
        {
        }

        /// <summary>
        /// Registers a function by name. Overwrites any existing registration for the same name.
        /// </summary>
        public void Register(string name, ScriptFunction handler)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Function name cannot be null or empty.", nameof(name));

            _functions[name] = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        /// <summary>
        /// Registers all functions from a category.
        /// </summary>
        public void RegisterCategory(IFunctionCategory category)
        {
            if (category == null)
                throw new ArgumentNullException(nameof(category));

            category.Register(this);
        }

        /// <summary>
        /// Attempts to evaluate a function by name with the given argument string.
        /// </summary>
        /// <returns>True if the function was found and executed; false if the name is not registered.</returns>
        public bool TryEvaluate(string name, string argsString, ScriptContext context, out object? value)
        {
            value = null;

            if (string.IsNullOrWhiteSpace(name))
                return false;

            if (!_functions.TryGetValue(name, out var handler))
                return false;

            value = handler(argsString, context);
            return true;
        }

        /// <summary>
        /// Returns true if a function with the given name is registered.
        /// </summary>
        public bool IsRegistered(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && _functions.ContainsKey(name);
        }

        /// <summary>
        /// Returns the number of registered functions.
        /// </summary>
        public int Count => _functions.Count;

        /// <summary>
        /// Returns all registered function names.
        /// </summary>
        public IEnumerable<string> RegisteredNames => _functions.Keys;

        private void RegisterBuiltInCategories()
        {
            RegisterCategory(new StringFunctions());
            RegisterCategory(new MathFunctions());
            RegisterCategory(new CollectionFunctions());
            RegisterCategory(new TypeFunctions());
            RegisterCategory(new DateTimeFunctions());
            RegisterCategory(new EncodingFunctions());
            RegisterCategory(new NetworkFunctions());
            RegisterCategory(new VaultFunctions());
        }
    }
}
