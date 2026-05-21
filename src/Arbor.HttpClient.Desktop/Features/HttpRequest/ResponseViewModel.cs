using Dock.Model.Mvvm.Controls;

namespace Arbor.HttpClient.Desktop.Features.HttpRequest;

/// <summary>
/// Legacy response document view model retained for backward compatibility with old layouts.
/// Response is now integrated into the request document.
/// </summary>
public sealed class ResponseViewModel : Document
{
    public ResponseViewModel()
    {
        Id = "response";
        Title = "Response";
    }
}
