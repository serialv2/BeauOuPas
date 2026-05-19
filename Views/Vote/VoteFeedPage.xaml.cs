using BeauOuPas.ViewModels.Vote;

namespace BeauOuPas.Views.Vote;

public partial class VoteFeedPage : ContentPage
{
    private VoteFeedViewModel? _vm;

    public VoteFeedPage(VoteFeedViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _vm = viewModel;
        _vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender,
        System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(VoteFeedViewModel.CurrentProject))
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                var project = _vm?.CurrentProject;
                if (project == null)
                {
                    PhotoVoteCard.IsVisible = false;
                    DuelCard.IsVisible = false;
                    PollCard.IsVisible = false;
                    VoteButtonsBar.IsVisible = false;
                    return;
                }
                PhotoVoteCard.IsVisible = project.IsPhotoVote;
                DuelCard.IsVisible = project.IsDuel;
                PollCard.IsVisible = project.IsPoll;
                VoteButtonsBar.IsVisible = project.IsPhotoVote;
            });
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_vm != null)
            await _vm.LoadFeedAsync();
    }
}