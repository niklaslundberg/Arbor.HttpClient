using System;
using System.Collections.Generic;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;

namespace Arbor.HttpClient.Desktop.Features.Variables;

internal sealed class VariableAutoCompleteController : IDisposable
{
    private readonly TextEditor _editor;
    private readonly Func<IReadOnlyList<string>> _getVariableNames;
    private readonly Func<IReadOnlyList<string>> _getEnvVariableNames;
    private CompletionWindow? _completionWindow;

    public VariableAutoCompleteController(
        TextEditor editor,
        Func<IReadOnlyList<string>> getVariableNames,
        Func<IReadOnlyList<string>> getEnvVariableNames)
    {
        _editor = editor;
        _getVariableNames = getVariableNames;
        _getEnvVariableNames = getEnvVariableNames;
        _editor.TextArea.TextEntered += OnTextEntered;
        _editor.TextArea.TextEntering += OnTextEntering;
    }

    public void Dispose()
    {
        _editor.TextArea.TextEntered -= OnTextEntered;
        _editor.TextArea.TextEntering -= OnTextEntering;
        CloseCompletionWindow();
    }

    internal CompletionWindow? CurrentCompletionWindow => _completionWindow;

    private void OnTextEntered(object? sender, TextInputEventArgs e)
    {
        UpdateCompletionWindow();
    }

    private void OnTextEntering(object? sender, TextInputEventArgs e)
    {
        if (_completionWindow is null || string.IsNullOrEmpty(e.Text))
        {
            return;
        }

        var typedCharacter = e.Text[0];
        if (char.IsWhiteSpace(typedCharacter) || typedCharacter == '}')
        {
            _completionWindow.CompletionList.RequestInsertion(e);
        }
    }

    private void UpdateCompletionWindow()
    {
        if (!VariableCompletionEngine.TryGetContext(_editor.Text, _editor.CaretOffset, out var context))
        {
            CloseCompletionWindow();
            return;
        }

        var sourceNames = context.IsEnvVariable ? _getEnvVariableNames() : _getVariableNames();
        var suggestions = VariableCompletionEngine.GetSuggestions(sourceNames, context.Prefix);
        if (suggestions.Count == 0)
        {
            CloseCompletionWindow();
            return;
        }

        _completionWindow ??= CreateCompletionWindow();
        _completionWindow.StartOffset = context.ReplaceStartOffset;
        _completionWindow.EndOffset = _editor.CaretOffset;

        var completionData = _completionWindow.CompletionList.CompletionData;
        completionData.Clear();
        foreach (var variableName in suggestions)
        {
            completionData.Add(new VariableCompletionData(variableName));
        }

        _completionWindow.CompletionList.SelectItem(context.Prefix);
        if (!_completionWindow.IsOpen)
        {
            _completionWindow.Show();
        }
    }

    private CompletionWindow CreateCompletionWindow()
    {
        var window = new CompletionWindow(_editor.TextArea)
        {
            CloseAutomatically = true,
            CloseWhenCaretAtBeginning = true
        };

        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_completionWindow, window))
            {
                _completionWindow = null;
            }
        };

        return window;
    }

    private void CloseCompletionWindow()
    {
        if (_completionWindow is null)
        {
            return;
        }

        _completionWindow.Close();
        _completionWindow = null;
    }
}
