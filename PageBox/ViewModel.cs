namespace PageBox;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

public class ViewModel : INotifyPropertyChanged
{
    private int count;

    public ViewModel()
    {
        this.Count = 24;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<Item> Items { get; } = new();

    public int Count
    {
        get => this.count;
        set
        {
            if (value == this.count)
            {
                return;
            }

            this.count = value;
            this.OnPropertyChanged();
            this.Items.Clear();
            for (var i = 0; i < value; i++)
            {
                this.Items.Add(new Item(i));
            }
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
