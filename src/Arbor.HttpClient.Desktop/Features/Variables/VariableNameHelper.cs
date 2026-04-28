using System;
using System.Collections.Generic;
using System.Linq;
using Arbor.HttpClient.Core.Variables;
using Arbor.HttpClient.Desktop.Features.Environments;

namespace Arbor.HttpClient.Desktop.Features.Variables;

internal static class VariableNameHelper
{
    private static readonly ISystemEnvironmentVariableProvider SystemEnvProvider = new SystemEnvironmentVariableProvider();

    public static IReadOnlyList<string> ExtractDistinctNames(IEnumerable<EnvironmentVariableViewModel>? variables) =>
        variables?
            .Where(variable => !string.IsNullOrWhiteSpace(variable.Name))
            .Select(variable => variable.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];

    /// <summary>Returns the names of all system (process) environment variables, sorted alphabetically.</summary>
    public static IReadOnlyList<string> GetSystemEnvironmentVariableNames()
    {
        var names = SystemEnvProvider.GetAll().Keys.ToList();
        names.Sort(StringComparer.OrdinalIgnoreCase);
        return names;
    }
}
