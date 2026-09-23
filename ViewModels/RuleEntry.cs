using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZenLoad.ViewModels;

public sealed class RuleEntry : INotifyPropertyChanged
{
    private string _extension;
    private string _destination;
    private bool _isEnabled;

    public RuleEntry(string extension, string destination, bool isEnabled = true)
    {
        _extension = extension;
        _destination = destination;
        _isEnabled = isEnabled;
    }

    public string Extension
    {
        get => _extension;
        set
        {
            if (_extension == value)
            {
                return;
            }

            _extension = value;
            OnPropertyChanged();
        }
    }

    public string Destination
    {
        get => _destination;
        set
        {
            if (_destination == value)
            {
                return;
            }

            _destination = value;
            OnPropertyChanged();
        }
    }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
            {
                return;
            }

            _isEnabled = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
