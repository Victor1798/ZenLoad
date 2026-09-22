using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ZenLoad.ViewModels;

public sealed class RuleEntry : INotifyPropertyChanged
{
    private string _extension;
    private string _destination;

    public RuleEntry(string extension, string destination)
    {
        _extension = extension;
        _destination = destination;
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

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
