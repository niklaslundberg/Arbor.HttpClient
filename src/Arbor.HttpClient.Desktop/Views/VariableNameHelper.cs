using System;
using System.Collections.Generic;
using System.Linq;
using Arbor.HttpClient.Desktop.ViewModels;

namespace Arbor.HttpClient.Desktop.Views;

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
