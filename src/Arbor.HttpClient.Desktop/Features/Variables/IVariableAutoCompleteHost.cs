using System.Collections.Generic;
using Arbor.HttpClient.Desktop.Features.Environments;

namespace Arbor.HttpClient.Desktop.Features.Variables;

/// <summary>
/// The minimal surface <see cref="VariableTextBox"/> needs to offer <c>{{variable}}</c>
/// auto-completion: the active environment's variables. Implemented by <c>MainWindowViewModel</c>
/// so the shared control does not depend on the whole main view model.
/// </summary>
public interface IVariableAutoCompleteHost
{
    IReadOnlyList<EnvironmentVariableViewModel> ActiveEnvironmentVariables { get; }
}
