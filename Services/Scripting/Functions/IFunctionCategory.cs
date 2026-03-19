namespace SSH_Helper.Services.Scripting.Functions
{
    /// <summary>
    /// Delegate signature for a registered scripting function.
    /// </summary>
    /// <param name="argsString">Raw comma-separated argument string (not yet split or resolved).</param>
    /// <param name="context">The current script execution context.</param>
    /// <returns>The function result, or null.</returns>
    public delegate object? ScriptFunction(string argsString, ScriptContext context);

    /// <summary>
    /// Implemented by category classes that bulk-register functions into the <see cref="FunctionRegistry"/>.
    /// </summary>
    public interface IFunctionCategory
    {
        void Register(FunctionRegistry registry);
    }
}
