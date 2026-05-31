#if ANDROID
using Android.App;
using Android.Content;
using Android.Views;
using Microsoft.Maui.Handlers;

namespace XpemFinancial.Platforms.Android;

/// <summary>
/// Custom handler that overrides the DatePicker dialog creation to apply
/// the app's XpemDatePickerDialog theme, fixing invisible OK/Cancel buttons
/// on dark backgrounds.
/// </summary>
public class ThemedDatePickerHandler : DatePickerHandler
{
    protected override DatePickerDialog CreateDatePickerDialog(int year, int month, int day)
    {
        var context = MauiContext!.Context!;
        var themedContext = new ContextThemeWrapper(context, Resource.Style.XpemDatePickerDialog);

        var dialog = new DatePickerDialog(themedContext, OnDateSet, year, month, day);
        return dialog;
    }

    private void OnDateSet(object? sender, DatePickerDialog.DateSetEventArgs e)
    {
        if (VirtualView != null)
        {
            // e.Year, e.Month (0-based no Android), e.DayOfMonth vêm direto do EventArgs
            VirtualView.Date = new DateTime(e.Year, e.Month + 1, e.DayOfMonth);
        }
    }
}
#endif
