using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace XpemFinancial.Utils;

public class RangeObservableCollection<T> : ObservableCollection<T>
{
    public RangeObservableCollection() { }

    public RangeObservableCollection(IEnumerable<T> items) : base(items) { }
    public void AddRange(IEnumerable<T> items)
    {
        var list = items.ToList();
        if (list.Count == 0) return;

        int startIndex = Count;
        foreach (var item in list)
            Items.Add(item);

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add, list, startIndex));
    }

    public void ReplaceAll(IEnumerable<T> items)
    {
        Items.Clear();
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Reset));

        var list = items.ToList();
        if (list.Count == 0) return;

        foreach (var item in list)
            Items.Add(item);

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Add, list, 0));
    }
}
