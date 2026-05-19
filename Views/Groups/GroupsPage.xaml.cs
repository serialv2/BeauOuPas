using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class GroupsPage : ContentPage
{
    private readonly GroupsViewModel _vm;

    public GroupsPage(GroupsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.LoadAsync();
    }

    private void OnTabGroupsTapped(object sender, TappedEventArgs e) => ShowTab(0);
    private void OnTabSeriesTapped(object sender, TappedEventArgs e) => ShowTab(1);

    private void ShowTab(int index)
    {
        ContentGroups.IsVisible   = index == 0;
        ContentMySeries.IsVisible = index == 1;

        TabGroups.BackgroundColor = index == 0 ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
        TabSeries.BackgroundColor = index == 1 ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");

        if (TabGroups.Content is Label lbl0)
            lbl0.TextColor = index == 0 ? Colors.White : Color.FromArgb("#8A6F4A");
        if (TabSeries.Content is Label lbl1)
            lbl1.TextColor = index == 1 ? Colors.White : Color.FromArgb("#8A6F4A");

        if (index == 1)
            _ = _vm.LoadStandaloneSeriesAsync();
    }
}
