namespace PageBox;

public class Item
{
    public Item(int value)
    {
        this.Value = value;
    }

    public int Value { get; }

    public override string ToString() => $"Item {this.Value}";
}
