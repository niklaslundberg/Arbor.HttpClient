using System;
using System.Collections.Generic;
using System.Linq;
using Arbor.HttpClient.Desktop.Features.Environments;

namespace Arbor.HttpClient.Desktop.Features.Variables;

internal static class VariableNameHelper
{
    public static IReadOnlyList<string> ExtractDistinctNames(IEnumerable<EnvironmentVariableViewModel>? variables) =>
        variables?
            .Where(variable => !string.IsNullOrWhiteSpace(variable.Name))
            .Select(variable => variable.Name.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];
}
