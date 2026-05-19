using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class GroupDetailPage : ContentPage
{
    private readonly GroupDetailViewModel _vm;

    public GroupDetailPage(GroupDetailViewModel vm)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        InitializeComponent();
        sw.Stop();
        System.Diagnostics.Debug.WriteLine(
            $"[GroupDetail-DIAG] InitializeComponent = {sw.ElapsedMilliseconds}ms");
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        base.OnAppearing();
        System.Diagnostics.Debug.WriteLine(
            $"[GroupDetail-DIAG] OnAppearing T+0ms (GroupId={_vm.GroupId})");
        if (!string.IsNullOrEmpty(_vm.GroupId))
        {
            await _vm.LoadAllAsync();
        }
        sw.Stop();
        System.Diagnostics.Debug.WriteLine(
            $"[GroupDetail-DIAG] OnAppearing END T+{sw.ElapsedMilliseconds}ms");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.StopRealtime();
    }

    private void OnTabSeriesTapped(object sender, TappedEventArgs e) => ShowTab(0);
    private void OnTabChatTapped(object sender, TappedEventArgs e) => ShowTab(1);
    private void OnTabMembersTapped(object sender, TappedEventArgs e) => ShowTab(2);

    // ─────────────────────────────────────────────────────────────────
    // ⚡ QW2 : Retour Android sort du mode multi-sélection si on y est,
    // sinon comportement normal (retour à la liste des groupes).
    // ─────────────────────────────────────────────────────────────────
    protected override bool OnBackButtonPressed()
    {
        if (_vm != null && _vm.IsSeriesSelectionMode)
        {
            _vm.ExitSeriesSelectionCommand.Execute(null);
            return true; // bloque la sortie de page
        }
        return base.OnBackButtonPressed();
    }

    private void ShowTab(int index)
    {
        ContentSeries.IsVisible  = index == 0;
        ContentChat.IsVisible    = index == 1;
        ContentMembers.IsVisible = index == 2;

        TabSeries.BackgroundColor  = index == 0 ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
        TabChat.BackgroundColor    = index == 1 ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");
        TabMembers.BackgroundColor = index == 2 ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");

        foreach (var child in new[] { TabSeries, TabChat, TabMembers })
        {
            if (child.Content is Label lbl)
                lbl.TextColor = child.BackgroundColor == Color.FromArgb("#C2754C")
                    ? Colors.White : Color.FromArgb("#8A6F4A");
        }
    }
}
