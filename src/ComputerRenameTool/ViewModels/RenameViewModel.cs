using ComputerRenameTool.Helpers;
using ComputerRenameTool.Models;
using ComputerRenameTool.MVVM;
using ComputerRenameTool.Services;

namespace ComputerRenameTool.ViewModels;

/// <summary>
/// Real-time rename state machine (DESIGN.md §5.2). Every keystroke updates
/// <see cref="State"/> and <see cref="CanSubmit"/>; the bound UI reflects the
/// new state via the same property change notifications.
/// </summary>
public sealed class RenameViewModel : ObservableObject
{
    private readonly IComputerRenameService _renameService;
    private readonly string _currentName;
    private readonly string? _suggestedName;

    private string _inputName = string.Empty;
    private ValidationState _state = ValidationState.Empty;
    private string _validationMessage = "请输入新的机器名";
    private bool _canSubmit;
    private bool _isSubmitting;
    private string _submitResult = string.Empty;
    private bool _isSubmitSuccess;

    public RenameViewModel(IComputerRenameService renameService, string currentName, string? suggestedName = null)
    {
        _renameService = renameService;
        _currentName = currentName;
        _suggestedName = suggestedName;

        SubmitCommand = new RelayCommand(async _ => await SubmitAsync(), _ => CanSubmit && !IsSubmitting);
        UseSuggestedCommand = new RelayCommand(() => InputName = _suggestedName ?? string.Empty,
                                               () => !string.IsNullOrEmpty(_suggestedName));
        CopyCurrentNameCommand = new RelayCommand(() => ClipboardHelper.CopyText(_currentName));
    }

    public string CurrentName => _currentName;
    public string? SuggestedName => _suggestedName;
    public bool HasSuggestion => !string.IsNullOrEmpty(_suggestedName);

    public string InputName
    {
        get => _inputName;
        set
        {
            if (SetProperty(ref _inputName, value))
            {
                OnInputNameChanged(value);
            }
        }
    }

    public ValidationState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(ValidationIcon));
                OnPropertyChanged(nameof(ValidationIconBrush));
            }
        }
    }

    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public bool CanSubmit
    {
        get => _canSubmit;
        private set => SetProperty(ref _canSubmit, value);
    }

    public bool IsSubmitting
    {
        get => _isSubmitting;
        private set
        {
            if (SetProperty(ref _isSubmitting, value))
            {
                OnPropertyChanged(nameof(SubmitButtonText));
            }
        }
    }

    public string SubmitResult
    {
        get => _submitResult;
        set
        {
            if (SetProperty(ref _submitResult, value))
            {
                OnPropertyChanged(nameof(HasSubmitResult));
                OnPropertyChanged(nameof(IsSubmitSuccess));
            }
        }
    }

    public bool HasSubmitResult => !string.IsNullOrEmpty(_submitResult);

    public bool IsSubmitSuccess
    {
        get => _isSubmitSuccess;
        private set
        {
            if (SetProperty(ref _isSubmitSuccess, value))
            {
                OnPropertyChanged(nameof(SubmitResultColor));
            }
        }
    }

    public string SubmitButtonText => _isSubmitting ? "处理中..." : "修改机器名";

    /// <summary>Visual color of the submit-result message (success vs error).</summary>
    public string SubmitResultColor => _isSubmitSuccess ? "#1B8E3B" : "#C42B1C";

    /// <summary>Status icon glyph bound to <see cref="State"/>.</summary>
    public string ValidationIcon => _state switch
    {
        ValidationState.Valid => "✅",
        ValidationState.SameAsCurrent => "⚠️",
        _ => "❌"
    };

    /// <summary>
    /// Resolved foreground brush for the validation icon. WPF can't bind a
    /// <see cref="System.Windows.Media.Brush"/> resource key directly, so the
    /// VM resolves the brush from <see cref="App.Current"/> at runtime.
    /// </summary>
    public System.Windows.Media.Brush ValidationIconBrush
    {
        get
        {
            var key = _state switch
            {
                ValidationState.Valid => "StatusValidBrush",
                ValidationState.SameAsCurrent => "StatusNeutralBrush",
                _ => "StatusInvalidBrush",
            };
            return (System.Windows.Media.Brush)System.Windows.Application.Current.TryFindResource(key)
                ?? System.Windows.Media.Brushes.Gray;
        }
    }

    public RelayCommand SubmitCommand { get; }
    public RelayCommand UseSuggestedCommand { get; }
    public RelayCommand CopyCurrentNameCommand { get; }

    private void OnInputNameChanged(string? value)
    {
        // Empty — separate state for nicer UX
        if (string.IsNullOrWhiteSpace(value))
        {
            State = ValidationState.Empty;
            ValidationMessage = "请输入新的机器名";
            CanSubmit = false;
            return;
        }

        // Same as current
        if (string.Equals(value, _currentName, StringComparison.Ordinal))
        {
            State = ValidationState.SameAsCurrent;
            ValidationMessage = "机器名未发生变化";
            CanSubmit = false;
            return;
        }

        // Length
        if (value.Length > ComputerNameValidator.MaxLength)
        {
            State = ValidationState.TooLong;
            ValidationMessage = $"机器名长度不能超过{ComputerNameValidator.MaxLength}个字符";
            CanSubmit = false;
            return;
        }

        // Format
        if (!ComputerNameValidator.IsValid(value, out var error))
        {
            State = ValidationState.Invalid;
            ValidationMessage = error;
            CanSubmit = false;
            return;
        }

        State = ValidationState.Valid;
        ValidationMessage = string.Empty;
        CanSubmit = true;
    }

    private async Task SubmitAsync()
    {
        if (!CanSubmit || IsSubmitting) return;

        IsSubmitting = true;
        try
        {
            var result = await Task.Run(() => _renameService.Rename(_inputName));
            IsSubmitSuccess = result.IsSuccess;
            SubmitResult = result.IsSuccess
                ? "✅ 机器名修改成功。请重启电脑以使新名称生效。"
                : $"❌ {result.Message}";

            if (!result.IsSuccess)
            {
                App.Logger?.Warn($"Rename failed. HRESULT=0x{result.HResult:X8}");
            }
        }
        finally
        {
            IsSubmitting = false;
        }
    }
}

/// <summary>Real-time validation states bound to the rename input (DESIGN.md §5.2).</summary>
public enum ValidationState
{
    Empty,
    Invalid,
    TooLong,
    SameAsCurrent,
    Valid
}
