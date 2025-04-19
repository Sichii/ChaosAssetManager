using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using Point = System.Windows.Point;

namespace ChaosAssetManager.Extensions;

internal static class ControlExtensions
{
    internal static T? FindVisualChild<T>(this DependencyObject parent, bool ignoreSelf = true) where T: DependencyObject
    {
        if (!ignoreSelf && parent is T result)
            return result;

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T dependencyObject)
                return dependencyObject;

            var childOfChild = FindVisualChild<T>(child);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (childOfChild is not null)
                return childOfChild;
        }

        return null;
    }

    internal static T? FindVisualElementAtPoint<T>(this Visual parent, Point point) where T: DependencyObject
    {
        var hitTestResult = VisualTreeHelper.HitTest(parent, point);

        if (hitTestResult == null)
            return null;

        if (hitTestResult.VisualHit is T result)
            return result;

        return FindVisualParent<T>(hitTestResult.VisualHit) ?? FindVisualChild<T>(hitTestResult.VisualHit);
    }

    internal static T? FindVisualParent<T>(this DependencyObject child, bool ignoreSelf = true) where T: DependencyObject
    {
        if (!ignoreSelf && child is T result)
            return result;

        while (true)
        {
            var parentObject = VisualTreeHelper.GetParent(child);

            switch (parentObject)
            {
                case null:
                    return null;
                case T parent:
                    return parent;
                default:
                    child = parentObject;

                    break;
            }
        }
    }

    internal static DataGridCell? GetCell(this DataGrid dataGrid, DataGridRow? rowContainer, int column)
    {
        if (rowContainer != null)
        {
            var presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);

            if (presenter == null)
            {
                /* if the row has been virtualized away, call its ApplyTemplate() method
                 * to build its visual tree in order for the DataGridCellsPresenter
                 * and the DataGridCells to be created */
                rowContainer.ApplyTemplate();
                presenter = FindVisualChild<DataGridCellsPresenter>(rowContainer);
            }

            if (presenter != null)
            {
                if (presenter.ItemContainerGenerator.ContainerFromIndex(column) is not DataGridCell cell)
                {
                    /* bring the column into view
                     * in case it has been virtualized away */
                    dataGrid.ScrollIntoView(rowContainer, dataGrid.Columns[column]);
                    cell = (presenter.ItemContainerGenerator.ContainerFromIndex(column) as DataGridCell)!;
                }

                return cell;
            }
        }

        return null;
    }

    private static IEnumerable<DependencyProperty> GetDependencyProperties(DependencyObject obj)
    {
        var localValueEnumerator = obj.GetLocalValueEnumerator();

        while (localValueEnumerator.MoveNext())
            yield return localValueEnumerator.Current.Property;
    }

    internal static List<T> GetVisibleItems<T>(this DataGrid dataGrid)
    {
        var visibleItems = new List<T>();

        // Try to get the ScrollViewer inside the DataGrid
        var scrollViewer = FindVisualChild<ScrollViewer>(dataGrid);

        if (scrollViewer == null)
            return visibleItems;

        var generator = dataGrid.ItemContainerGenerator;

        for (var i = 0; i < dataGrid.Items.Count; i++)
            if (generator.ContainerFromIndex(i) is DataGridRow)
                visibleItems.Add((T)dataGrid.Items[i]);

        return visibleItems;
    }

    internal static void RefreshBindings(this DependencyObject obj)
    {
        foreach (var dp in GetDependencyProperties(obj))
        {
            var bindingExpression = BindingOperations.GetBindingExpression(obj, dp);
            bindingExpression?.UpdateTarget();
        }
    }

    internal static void SelectCellByIndex(
        this DataGrid dataGrid,
        int rowIndex,
        int columnIndex,
        bool focus = true)
    {
        if (!dataGrid.SelectionUnit.Equals(DataGridSelectionUnit.Cell))
            throw new ArgumentException("The SelectionUnit of the DataGrid must be set to Cell.");

        if ((rowIndex < 0) || (rowIndex > (dataGrid.Items.Count - 1)))
            throw new ArgumentException($"{rowIndex} is an invalid row index.");

        if ((columnIndex < 0) || (columnIndex > (dataGrid.Columns.Count - 1)))
            throw new ArgumentException($"{columnIndex} is an invalid column index.");

        dataGrid.SelectedCells.Clear();

        var item = dataGrid.Items[rowIndex]!;

        // ReSharper disable once UseNegatedPatternMatching
        var row = dataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;

        if (row == null)
        {
            dataGrid.ScrollIntoView(item);
            row = dataGrid.ItemContainerGenerator.ContainerFromIndex(rowIndex) as DataGridRow;
        }

        if (row != null)
        {
            var cell = GetCell(dataGrid, row, columnIndex);

            if (cell != null)
            {
                var dataGridCellInfo = new DataGridCellInfo(cell);
                dataGrid.SelectedCells.Add(dataGridCellInfo);

                if (focus)
                    cell.Focus();
            }
        }
    }
}