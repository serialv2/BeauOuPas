using BeauOuPas.ViewModels.Groups;
using System.Diagnostics;

namespace BeauOuPas.Views.Groups;

public partial class SeriesDetailPage : ContentPage
{
    private readonly SeriesDetailViewModel _vm;

    // ⚡ NOUVEAU : gère le Retour Android pour la popup "Quitter la série"
    // _confirmingExit : la popup est en cours d'affichage (évite les double-taps)
    // _exitConfirmed  : l'utilisateur a confirmé "Quitter" (autorise la sortie)
    private bool _confirmingExit = false;
    private bool _exitConfirmed = false;

    public SeriesDetailPage(SeriesDetailViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // ⚡ Reset des flags Retour Android à chaque entrée sur la page
        _confirmingExit = false;
        _exitConfirmed = false;

        if (!string.IsNullOrEmpty(_vm.SeriesId))
            await _vm.LoadAsync();
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        await _vm.CleanupAsync();
    }

    // ─────────────────────────────────────────────────────────────────
    // Tap sur la carte projet → modifier le titre
    // ─────────────────────────────────────────────────────────────────
    private void OnEditProjectTapped(object sender, TappedEventArgs e)
    {
        if (sender is BindableObject bindable
            && bindable.BindingContext is SeriesProjectItem item)
        {
            if (_vm.EditProjectTitleCommand.CanExecute(item))
                _vm.EditProjectTitleCommand.Execute(item);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Tap sur le bouton 🗑️ → supprimer le projet
    // ─────────────────────────────────────────────────────────────────
    private void OnDeleteProjectTapped(object sender, TappedEventArgs e)
    {
        if (sender is BindableObject bindable
            && bindable.BindingContext is SeriesProjectItem item)
        {
            if (_vm.DeleteMyProjectCommand.CanExecute(item))
                _vm.DeleteMyProjectCommand.Execute(item);
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // ⚡ NOUVEAU : Sécurisation du Retour Android sur la salle d'attente
    //
    // Si l'utilisateur courant est inscrit comme participant ET que le mode TV
    // est actif ET qu'il N'EST PAS le créateur, on lui demande confirmation
    // avant de sortir, et on le décompte de series_participants.
    //
    // Sinon (créateur, pas en mode TV, pas participant) : sortie normale.
    // ─────────────────────────────────────────────────────────────────
    protected override bool OnBackButtonPressed()
    {
        // Si l'exit a déjà été confirmé, on laisse partir normalement
        if (_exitConfirmed) return base.OnBackButtonPressed();

        // Si on est déjà en train de demander confirmation, on bloque tout autre appui
        if (_confirmingExit) return true;

        // Vérifier les conditions pour afficher la popup :
        // → ViewModel chargé, mode TV actif, pas le créateur, est participant
        if (_vm == null) return base.OnBackButtonPressed();
        if (!_vm.TvActive) return base.OnBackButtonPressed();

        // 🎉 PARTIE RAPIDE — KILL GAME : si l'ANIMATEUR (créateur)
        // d'une partie rapide quitte alors qu'une session TV est active,
        // on lui propose de terminer la partie pour ne pas laisser de
        // parties zombies actives côté serveur.
        if (_vm.IsCreator && _vm.IsParty)
        {
            _ = HandleHostExitPartyAsync();
            return true; // bloque la sortie immédiate, la popup décide
        }

        if (_vm.IsCreator) return base.OnBackButtonPressed();
        if (!_vm.IsCurrentUserParticipant) return base.OnBackButtonPressed();

        // Toutes les conditions sont remplies → afficher la popup
        _ = HandleExitConfirmationAsync();
        return true; // bloque la sortie immédiate, la popup décide
    }

    // 🎉 PARTIE RAPIDE — l'animateur quitte une partie en cours.
    // 3 choix : Terminer la partie (kill game), Quitter sans terminer,
    // ou Rester. "Terminer" passe la série en 'finished' (via le
    // ViewModel.KillGameAsync -> SeriesService.StopSeriesAsync).
    private async Task HandleHostExitPartyAsync()
    {
        _confirmingExit = true;
        try
        {
            string action = await DisplayActionSheet(
                "Quitter la partie ?",
                "Rester",
                null,
                "Terminer la partie pour tout le monde",
                "Quitter sans terminer");

            if (action == "Terminer la partie pour tout le monde")
            {
                _exitConfirmed = true;
                await _vm.KillGameAsync();      // status -> 'finished'
                await _vm.CleanupAsync();
                await Shell.Current.GoToAsync("..");
            }
            else if (action == "Quitter sans terminer")
            {
                _exitConfirmed = true;
                await _vm.CleanupAsync();
                await Shell.Current.GoToAsync("..");
            }
            // "Rester" / annulation : on ne fait rien, on reste sur la page.
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SeriesDetailPage] HostExitParty: {ex.Message}");
        }
        finally
        {
            _confirmingExit = false;
        }
    }

    private async Task HandleExitConfirmationAsync()
    {
        _confirmingExit = true;
        try
        {
            bool confirm = await DisplayAlert(
                "Quitter la série ?",
                "Une session est en cours. Si tu quittes maintenant, tu seras retiré " +
                "de la liste des participants. Tu pourras rejoindre à nouveau plus tard " +
                "avec le code d'accès.",
                "Quitter", "Rester");

            if (confirm)
            {
                _exitConfirmed = true;

                // Retirer l'utilisateur de la liste des participants
                // → la TV reçoit le DELETE Realtime et décrémente son compteur
                await _vm.LeaveSeriesAsync();

                // Cleanup et retour à la page précédente
                await _vm.CleanupAsync();
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[SeriesDetailPage] HandleExit: {ex.Message}");
        }
        finally
        {
            _confirmingExit = false;
        }
    }
}