using BeauOuPas.ViewModels.Groups;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace BeauOuPas.Views.Groups;

public partial class QuizPlayPage : ContentPage
{
    private readonly QuizPlayViewModel _vm;
    private readonly ICameraProvider _cameraProvider;
    private bool _cameraReady = false;
    private bool _permissionDenied = false;
    private bool _cameraInitInProgress = false;

    // Pour gérer la confirmation de sortie en plein quiz
    private bool _confirmingExit = false;
    private bool _exitConfirmed = false;

    public QuizPlayPage(QuizPlayViewModel vm, ICameraProvider cameraProvider)
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

        // 1) Permission caméra (pour le selfie auto)
        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            _permissionDenied = true;
            // Pas bloquant : le quiz peut tourner sans selfie. On informe juste.
            System.Diagnostics.Debug.WriteLine("[QuizPlayPage] Camera permission denied → selfies désactivés");
        }
        else
        {
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
                            $"[QuizPlayPage] Caméra avant: {frontCamera.Name}");
                    }

                    // ⚠️ Android : la SurfaceView peut ne pas avoir réattaché son Handler
                    // lors d'une 2ème navigation → on attend max 3s.
                    // iOS : Handler est toujours connecté → boucle instantanée.
                    var handlerTimeout = DateTime.UtcNow.AddSeconds(3);
                    while (HiddenCamera.Handler == null && DateTime.UtcNow < handlerTimeout)
                        await Task.Delay(100);

                    try
                    {
                        await HiddenCamera.StartCameraPreview(CancellationToken.None);

                        // ⚠️ iOS : AVCaptureSession.StartRunning() est asynchrone.
                        // Sans ce délai, CaptureImage peut être appelé avant que la
                        // session soit vraiment active → MediaCaptured ne se déclenche
                        // jamais → timeout 8s → selfie null.
                        if (DeviceInfo.Platform == DevicePlatform.iOS)
                            await Task.Delay(1500);

                        _cameraReady = true;
                        System.Diagnostics.Debug.WriteLine("[QuizPlayPage] Caméra prête");
                    }
                    catch (Exception exFirst)
                    {
                        // Filtre retiré : sur Android le message contient "Handler",
                        // sur iOS il peut être différent → on retry dans tous les cas.
                        System.Diagnostics.Debug.WriteLine(
                            $"[QuizPlayPage] Camera 1st try failed, retry in 500ms: {exFirst.Message}");
                        await Task.Delay(500);
                        await HiddenCamera.StartCameraPreview(CancellationToken.None);
                        if (DeviceInfo.Platform == DevicePlatform.iOS)
                            await Task.Delay(1500);
                        _cameraReady = true;
                        System.Diagnostics.Debug.WriteLine("[QuizPlayPage] Caméra prête (after retry)");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[QuizPlayPage] Camera init: {ex.Message}");
                    _cameraReady = false;
                }
                finally
                {
                    _cameraInitInProgress = false;
                }
            }
        }

        // 2) Lancer le flow du quiz
        if (string.IsNullOrEmpty(_vm.SeriesId)) return;
        await _vm.InitAsync();
    }

    /// <summary>
    /// Interception du bouton Retour Android : on demande confirmation
    /// avant de quitter (sauf si la série est terminée ou en erreur).
    /// </summary>
    protected override bool OnBackButtonPressed()
    {
        if (_vm.IsFinish || _vm.IsError || _exitConfirmed)
        {
            return base.OnBackButtonPressed();
        }
        if (_confirmingExit) return true;
        _ = HandleExitConfirmationAsync();
        return true;
    }

    private async Task HandleExitConfirmationAsync()
    {
        _confirmingExit = true;
        try
        {
            bool confirm = await DisplayAlert(
                "Quitter le quiz ?",
                "Si tu quittes maintenant, tu sortiras de la session en cours. " +
                "Tu pourras le rejoindre depuis la page de la série.",
                "Quitter", "Continuer à jouer");

            if (confirm)
            {
                _exitConfirmed = true;
                await _vm.CleanupAsync();
                await Shell.Current.GoToAsync("..");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlayPage] HandleExit: {ex.Message}");
        }
        finally
        {
            _confirmingExit = false;
        }
    }

    /// <summary>
    /// Handler appelé par le ViewModel quand il faut prendre le selfie.
    /// Retourne null si pas de permission, cam pas prête, ou timeout.
    /// </summary>
    private async Task<Stream?> CaptureSelfieFromCameraAsync()
    {
        if (_permissionDenied || !_cameraReady)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[QuizPlayPage] Selfie skipped (denied={_permissionDenied}, ready={_cameraReady})");
            return null;
        }

        try
        {
            var result = await TryCaptureOnceAsync("[QuizPlayPage]");

            // iOS : premier frame parfois null (AE/AF pas encore stabilisé).
            // Un retry immédiat suffit en général.
            if (result == null && DeviceInfo.Platform == DevicePlatform.iOS)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[QuizPlayPage] MediaCaptured null (iOS first frame), retry");
                await Task.Delay(300);
                result = await TryCaptureOnceAsync("[QuizPlayPage] retry");
            }

            return result;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[QuizPlayPage] CaptureSelfie: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Tente une seule capture et attend MediaCaptured (timeout 8s).
    /// Retourne null en cas d'échec ou de timeout.
    /// </summary>
    private async Task<Stream?> TryCaptureOnceAsync(string tag)
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

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(8000));
        if (completed != tcs.Task)
        {
            HiddenCamera.MediaCaptured -= handler;
            System.Diagnostics.Debug.WriteLine($"{tag} Selfie capture TIMEOUT (8s)");
            return null;
        }
        return await tcs.Task;
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        try
        {
            HiddenCamera.Handler?.DisconnectHandler();
        }
        catch { }

        await _vm.CleanupAsync();
        _vm.Dispose();
        _cameraReady = false;
    }
}
