using BeauOuPas.ViewModels.Groups;

namespace BeauOuPas.Views.Groups;

public partial class CreateSeriesPage : ContentPage
{
    private readonly CreateSeriesViewModel _vm;

    public CreateSeriesPage(CreateSeriesViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;

        // Écouter les changements de sélection
        vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CreateSeriesViewModel.MaxProjects))
            UpdateMaxButtons(_vm.MaxProjects);
        else if (e.PropertyName == nameof(CreateSeriesViewModel.MaxPerMember))
            UpdatePerButtons(_vm.MaxPerMember);
    }

    private void UpdateMaxButtons(int selected)
    {
        SetActive(BtnMax5, selected == 5);
        SetActive(BtnMax10, selected == 10);
        SetActive(BtnMax15, selected == 15);
        SetActive(BtnMax20, selected == 20);
    }

    private void UpdatePerButtons(int selected)
    {
        SetActive(BtnPer1, selected == 1);
        SetActive(BtnPer2, selected == 2);
        SetActive(BtnPer3, selected == 3);
        SetActive(BtnPerAll, selected == 999);
    }

    private static void SetActive(Border border, bool active)
    {
        border.BackgroundColor = active ? Color.FromArgb("#E5DCC9") : Colors.White;
        border.Stroke = active ? Color.FromArgb("#C2754C") : Color.FromArgb("#E5DCC9");

        if (border.Content is Label lbl)
            lbl.TextColor = active ? Color.FromArgb("#C2754C") : Color.FromArgb("#3D2817");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.PropertyChanged -= OnViewModelPropertyChanged;
    }
}