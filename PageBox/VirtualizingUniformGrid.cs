namespace PageBox;

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

public class VirtualizingUniformGrid : VirtualizingPanel, IScrollInfo
{
    public static readonly DependencyProperty RowsProperty = UniformGrid.RowsProperty.AddOwner(typeof(VirtualizingUniformGrid));
    public static readonly DependencyProperty ColumnsProperty = UniformGrid.ColumnsProperty.AddOwner(typeof(VirtualizingUniformGrid));

    private int offset;
    private Size childSize;

    public double ViewportWidth { get; private set; }

    public double ViewportHeight { get; private set; }

    public double ExtentWidth { get; private set; }

    public double ExtentHeight { get; private set; }

    public double HorizontalOffset { get; private set; }

    public double VerticalOffset { get; private set; }

    public bool CanHorizontallyScroll { get; set; }

    public bool CanVerticallyScroll { get; set; }

    public ScrollViewer? ScrollOwner { get; set; }

    public int Rows
    {
        get => (int)this.GetValue(RowsProperty);
        set => this.SetValue(RowsProperty, value);
    }

    public int Columns
    {
        get => (int)this.GetValue(ColumnsProperty);
        set => this.SetValue(ColumnsProperty, value);
    }

    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        foreach (UIElement child in this.InternalChildren)
        {
            if (child == visual)
            {
                return VisualTreeHelper.GetContentBounds(child);
            }
        }

        return default;
    }

    public void LineUp() => this.ShiftOffset(-this.Columns);

    public void LineDown() => this.ShiftOffset(this.Columns);

    public void LineLeft() => this.ShiftOffset(-1);

    public void LineRight() => this.ShiftOffset(1);

    public void MouseWheelUp() => this.LineUp();

    public void MouseWheelDown() => this.LineDown();

    public void MouseWheelLeft() => this.LineLeft();

    public void MouseWheelRight() => this.LineRight();

    public void PageUp() => this.ShiftOffset(-(this.Rows * this.Columns));

    public void PageDown() => this.ShiftOffset(this.Rows * this.Columns);

    public void PageLeft() => this.ShiftOffset(-(this.Rows * this.Columns));

    public void PageRight() => this.ShiftOffset(this.Rows * this.Columns);

    public void SetHorizontalOffset(double offset)
    {
        this.HorizontalOffset = offset;
        var clamp = Clamp();
        if (clamp == this.offset)
        {
            return;
        }

        this.offset = clamp;
        this.VerticalOffset = this.VerticallOffsetFromIndex();
        this.InvalidateMeasure();

        int Clamp()
        {
            if (ItemsControl.GetItemsOwner(this) is { Items: { Count: > 0 } items })
            {
                return (int)Math.Clamp(offset / this.childSize.Width, 0, items.Count - 1);
            }

            return this.offset;
        }
    }

    public void SetVerticalOffset(double offset)
    {
        this.VerticalOffset = offset;
        var clamp = Clamp();
        if (clamp == this.offset)
        {
            return;
        }

        this.offset = clamp;
        this.HorizontalOffset = this.HorizontalOffsetFromIndex();

        this.InvalidateMeasure();

        int Clamp()
        {
            if (ItemsControl.GetItemsOwner(this) is { Items: { Count: > 0 } items })
            {
                return (int)Math.Clamp(this.Columns * offset / this.childSize.Height, 0, items.Count - 1);
            }

            return this.offset;
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var itemsControl = ItemsControl.GetItemsOwner(this);
        if (itemsControl is null)
        {
            this.childSize = new Size(0, 0);
            return new Size(0, 0);
        }

        var rows = this.Rows;
        var columns = this.Columns;
        //// Access InternalChildren before ItemContainerGenerator to work around WPF weirdness
        var children = this.InternalChildren;
        this.RemoveInternalChildRange(0, children.Count);
        var generator = this.ItemContainerGenerator;
        this.offset = Math.Min(this.offset, Math.Max(0, itemsControl.Items.Count - 1));
        var count = Math.Min(rows * columns, itemsControl.Items.Count - this.offset);

        if (count == 0)
        {
            generator.RemoveAll();
            return availableSize;
        }

        var childConstraint = new Size(availableSize.Width / columns, availableSize.Height / rows);
        this.childSize = childConstraint;
        using (generator.StartAt(generator.GeneratorPositionFromIndex(this.offset), GeneratorDirection.Forward, allowStartAtRealizedItem: true))
        {
            for (var i = 0; i < count; i++)
            {
                var child = (UIElement)generator.GenerateNext(out _);
                this.AddInternalChild(child);
                generator.PrepareItemContainer(child);
                child.Measure(childConstraint);
                var childDesiredSize = child.DesiredSize;
                this.childSize = new Size(Math.Max(this.childSize.Width, childDesiredSize.Width), Math.Max(this.childSize.Height, childDesiredSize.Height));
            }
        }

        if (children.Count > count)
        {
            Cleanup(0, this.offset + 1);
            Cleanup(this.offset, children.Count - this.offset);

            void Cleanup(int index, int range)
            {
                if (range > 0)
                {
                    generator.Remove(new GeneratorPosition(index, 0), range);
                }
            }
        }

        return new Size(this.childSize.Width * columns, this.childSize.Height * rows);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateScrollInfo();
        var childBounds = new Rect(this.childSize);
        var xStep = childBounds.Width;
        foreach (UIElement child in this.InternalChildren)
        {
            child.Arrange(childBounds);
            if (childBounds.X < finalSize.Width - xStep)
            {
                childBounds.X += xStep;
            }
            else
            {
                childBounds.X = 0;
                childBounds.Y += childBounds.Height;
            }
        }

        return finalSize;

        void UpdateScrollInfo()
        {
            var rows = this.Rows;
            var columns = this.Columns;
            this.ViewportWidth = this.childSize.Width * columns;
            this.ViewportHeight = this.childSize.Height * rows;
            if (ItemsControl.GetItemsOwner(this) is { Items: { Count: > 0 } items })
            {
                this.ExtentWidth = this.childSize.Width * items.Count;
                this.ExtentHeight = this.childSize.Height * Math.Ceiling((double)items.Count / columns);
            }
            else
            {
                this.ExtentWidth = 0;
                this.ExtentHeight = 0;
                this.HorizontalOffset = 0;
                this.VerticalOffset = 0;
            }

            this.ScrollOwner?.InvalidateScrollInfo();
        }
    }

    protected override void BringIndexIntoView(int index)
    {
        this.offset = index < this.offset
            ? index
            : Math.Max(0, index - (this.Rows * this.Columns));

        this.InvalidateMeasure();
    }

    private void ShiftOffset(int value)
    {
        var clamp = Clamp();
        if (clamp == this.offset)
        {
            return;
        }

        this.offset = clamp;
        this.HorizontalOffset = this.HorizontalOffsetFromIndex();
        this.VerticalOffset = this.VerticallOffsetFromIndex();
        this.InvalidateMeasure();

        int Clamp()
        {
            if (ItemsControl.GetItemsOwner(this) is { Items: { Count: > 0 } items })
            {
                if (value < 0)
                {
                    return Math.Max(0, this.offset + value);
                }

                var columns = this.Columns;
                if (value == columns &&
                    this.offset + value >= items.Count)
                {
                    return this.offset;
                }

                if (value == this.Rows * columns &&
                    this.offset + value >= items.Count)
                {
                    return this.offset;
                }

                return Math.Min(items.Count - 1, this.offset + value);
            }

            return 0;
        }
    }

    private double HorizontalOffsetFromIndex() => this.childSize.Width * this.offset;

    private double VerticallOffsetFromIndex() => this.childSize.Height * Math.Ceiling((double)this.offset / this.Columns);
}
