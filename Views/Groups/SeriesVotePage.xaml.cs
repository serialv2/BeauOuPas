using BeauOuPas.ViewModels.Groups;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace BeauOuPas.Views.Groups;

public partial class SeriesVotePage : ContentPage
{
    private readonly SeriesVoteViewModel _vm;
    private readonly ICameraProvider _cameraProvider;
    private bool _cameraReady = false;
    private bool _permissionDenied = false;
    private bool _cameraInitInProgress = false;

    // ⚡ NOUVEAU : pour gérer la confirmation de sortie pendant la série
    private bool _confirmingExit = false;
    private bool _exitConfirmed = false;

    public SeriesVotePage(SeriesVoteViewModel vm, ICameraProvider cameraProvider)
    {
        InitializeComponent();
        _vm = vm;
        _cameraProvider = cameraProvider;
        BindingContext = vm;

        _vm.CaptureSelfieRequested = CaptureSelfieFromCameraAsync;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Si on revient sur la page (ex: relance de série), on réinitialise le ViewModel
        _vm.Reset();

        // 1) Permission caméra
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            _permissionDenied = true;
            await DisplayAlert(
                "Caméra requise",
                "BeauOuPas a besoin de la caméra pour le selfie automatique. " +
                "Tu peux activer la permission dans les réglages.",
                "OK");
        }
        else
        {
            // On n'init la caméra qu'UNE SEULE FOIS
            if (!_cameraReady && !_cameraInitInProgress)
            {
                _cameraInitInProgress = true;
                try
                {
                    await _cameraProvider.RefreshAvailableCameras(CancellationToken.None);
                    var frontCamera = _cameraProvider.AvailableCameras
                        .FirstOrDefault(c => c.Position == CameraPosition.Front);

                    if (frontCamera != null)
                    {
                        HiddenCamera.SelectedCamera = frontCamera;
                        System.Diagnostics.Debug.WriteLine(
                            $"[SeriesVotePage] Caméra avant sélectionnée: {frontCamera.Name}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[SeriesVotePage] Pas de caméra avant trouvée, fallback sur la défaut");
                    }

                    await HiddenCamera.StartCameraPreview(CancellationToken.None);
                    _cameraReady = true;
                    System.Diagnostics.Debug.WriteLine("[SeriesVotePage] Caméra prête");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SeriesVotePage] Camera init: {ex.Message}");
                    _cameraReady = false;
                }
                finally
                {
                    _cameraInitInProgress = false;
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SeriesVotePage] Caméra déjà prête (ready={_cameraReady}, inProgress={_cameraInitInProgress}), skip");
            }
        }

        // 2) Lancer le flow de vote
        if (!string.IsNullOrEmpty(_vm.SeriesId))
            await _vm.InitAsync();
    }

    // ─────────────────────────────────────────────────────────────────
    // ⚡ NOUVEAU : Interception du bouton Retour Android
    // ─────────────────────────────────────────────────────────────────
    /// <summary>
    /// Appelée quand l'utilisateur appuie sur le bouton Retour Android
    /// (physique ou geste de retour). On affiche une confirmation pour
    /// éviter de quitter la série par erreur en plein vote.
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        // Si la série est déjà terminée ou si l'utilisateur a déjà confirmé
        // qu'il veut sortir, on laisse partir normalement.
        if (_vm.IsSeriesFinished || _exitConfirmed)
        {
            return base.OnBackButtonPressed();
        }

        // On évite de spammer la popup si l'utilisateur appuie plusieurs fois
        if (_confirmingExit)
        {
            return true;  // true = on intercepte, on ne laisse pas remonter
        }

        // On affiche la confirmation
        _ = HandleExitConfirmationAsync();

        // true = on intercepte le bouton retour, le système ne remonte pas
        return true;
    }

    private async Task HandleExitConfirmationAsync()
    {
        _confirmingExit = true;
        try
        {
            bool confirm = await DisplayAlert(
                "Quitter la série ?",
                "Si tu quittes maintenant, tu sortiras de la session de vote en cours. " +
                "Tu pourras la rejoindre depuis la page de la série.",
                "Quitter", "Continuer à voter");

            if (confirm)
            {
                _exitConfirmed = true;

                // ⚡ NOUVEAU : retirer l'utilisateur de la liste des participants
                // pour que le compteur de la TV se mette à jour.
                await _vm.LeaveSeriesAsync();

                // Cleanup et retour à la page précédente
                await _vm.CleanupAsync();
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesVotePage] HandleExit: {ex.Message}");
        }
        finally
        {
            _confirmingExit = false;
        }
    }
    /// <summary>
    /// Handler appelé par le ViewModel quand il faut prendre le selfie.
    /// </summary>
    private async Task<Stream?> CaptureSelfieFromCameraAsync()
    {
        if (_permissionDenied || !_cameraReady)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[SeriesVotePage] Selfie skipped (denied={_permissionDenied}, ready={_cameraReady})");
            return null;
        }

        try
        {
            var tcs = new TaskCompletionSource<Stream?>();
            EventHandler<MediaCapturedEventArgs>? handler = null;

            handler = (s, e) =>
            {
                HiddenCamera.MediaCaptured -= handler;
                tcs.TrySetResult(e.Media);
            };

            HiddenCamera.MediaCaptured += handler;

            await HiddenCamera.CaptureImage(CancellationToken.None);

            // Timeout 8s pour les premiers shots qui peuvent être lents
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(8000));
            if (completed != tcs.Task)
            {
                HiddenCamera.MediaCaptured -= handler;
                System.Diagnostics.Debug.WriteLine(
                    "[SeriesVotePage] Selfie capture TIMEOUT (8s)");
                return null;
            }
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SeriesVotePage] CaptureSelfie: {ex.Message}");
            return null;
        }
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        try
        {
            HiddenCamera.Handler?.DisconnectHandler();
        }
        catch { }

        // Fermer les subscriptions Realtime
        await _vm.CleanupAsync();

        _vm.Dispose();
        _cameraReady = false;
    }
}