using Xunit;

namespace SSH_Helper.Tests.UI;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CallbackUiSerialCollection
{
    public const string Name = "CallbackUiSerial";
}
