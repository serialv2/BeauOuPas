using BeauOuPas.ViewModels;
using BeauOuPas.ViewModels.Projects;

namespace BeauOuPas.Views.Projects;

public partial class MyProjectsPage : ContentPage
{
    private readonly ProjectDetailViewModel _detailVm;
    private MyProjectsViewModel? _vm;

    public MyProjectsPage(
        MyProjectsViewModel viewModel,
        ProjectDetailViewModel detailViewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _vm = viewModel;
        _detailVm = detailViewModel;

        // Lier la popup à son propre ViewModel
        DetailPopup.BindingContext = _detailVm;

        // Surveiller les demandes d'ouverture popup depuis le ViewModel
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private async void OnViewModelPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MyProjectsViewModel.DetailRequest))
        {
            var item = _vm?.DetailRequest;
            if (item == null) return;

            // ⚡ NOUVEAU : on est sur la page "Mes projets",
            //    donc l'utilisateur regarde SES propres projets.
            //    On passe isOwnerView: true pour :
            //    - cacher les boutons de vote (il ne vote pas sur son projet)
            //    - charger les stats directement (sans attendre un vote ni
            //      tenir compte du paramètre "afficher les résultats")
            await _detailVm.ShowAsync(
                projectId: item.Id,
                type: item.Type,
                title: item.Title,
                photoUrl: item.PhotoUrl,
                photoLeftUrl: item.PhotoLeftUrl,
                photoRightUrl: item.PhotoRightUrl,
                ownerId: item.OwnerId,
                isOwnerView: true);   // ⚡ NOUVEAU
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm != null)
            await _vm.LoadProjectsAsync();
    }
}