using System.Threading;
using System.Threading.Tasks;

namespace SSH_Helper.Services.Scripting.Commands
{
    public interface ILocalCmdConfirmation
    {
        Task<LocalCmdConfirmResult> ConfirmAsync(string resolvedCommand, string shell, string workingDir, CancellationToken cancellationToken);
    }

    public enum LocalCmdConfirmResult
    {
        Run,
        RunAll,
        Cancel
    }
}
