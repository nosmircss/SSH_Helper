using System;
using System.Threading;
using System.Threading.Tasks;
using SSH_Helper.Services.Scripting.Models;
using SSH_Helper.Services.Scripting.Parsers;

namespace SSH_Helper.Services.Scripting.Commands
{
    /// <summary>
    /// Parses device configuration text into structured JSON data.
    /// Supports FortiGate and other network device configuration formats.
    /// </summary>
    public class ParseCommand : IScriptCommand
    {
        public Task<CommandResult> ExecuteAsync(ScriptStep step, ScriptContext context, CancellationToken cancellationToken)
        {
            if (step.Parse == null)
                return Task.FromResult(CommandResult.Fail("Parse command has no options"));

            var options = step.Parse;

            // Validate required parameters
            if (string.IsNullOrEmpty(options.Format))
                return Task.FromResult(CommandResult.Fail("Parse requires 'format' parameter (e.g., 'fortigate')"));

            if (string.IsNullOrEmpty(options.From))
                return Task.FromResult(CommandResult.Fail("Parse requires 'from' variable"));

            if (string.IsNullOrEmpty(options.Into))
                return Task.FromResult(CommandResult.Fail("Parse requires 'into' variable"));

            // Get the source text
            var sourceText = context.GetVariableString(options.From);
            if (string.IsNullOrEmpty(sourceText))
            {
                context.EmitOutput($"Parse: source variable '{options.From}' is empty", ScriptOutputType.Warning);
                // Store empty dictionary
                context.SetVariable(options.Into, new System.Collections.Generic.Dictionary<string, object>());
                return Task.FromResult(CommandResult.Ok());
            }

            try
            {
                // Get the appropriate parser
                IConfigParser parser;
                try
                {
                    parser = ParserFactory.GetParser(options.Format);
                }
                catch (ArgumentException ex)
                {
                    return Task.FromResult(CommandResult.Fail(ex.Message));
                }

                // Parse the configuration
                var result = parser.Parse(sourceText, options.Sections);

                // Store the result
                context.SetVariable(options.Into, result);

                // Report success
                var sectionInfo = options.Sections != null && options.Sections.Count > 0
                    ? $" (sections: {string.Join(", ", options.Sections)})"
                    : "";
                context.EmitOutput($"Parse: parsed {options.Format} config into '{options.Into}'{sectionInfo}", ScriptOutputType.Debug);

                return Task.FromResult(CommandResult.Ok());
            }
            catch (Exception ex)
            {
                return Task.FromResult(CommandResult.Fail($"Parse failed: {ex.Message}"));
            }
        }
    }
}
