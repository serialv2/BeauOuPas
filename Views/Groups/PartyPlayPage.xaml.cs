using BeauOuPas.ViewModels.Groups;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;

namespace BeauOuPas.Views.Groups;

/// <summary>
/// Code-behind de l'écran de vote joueur "Partie Rapide".
///
/// Calqué sur QuizPlayPage : le ViewModel est injecté via DI, InitAsync()
/// est déclenché dans OnAppearing(), CleanupAsync() dans OnDisappearing().
/// Le ViewModel reçoit SeriesId / SeriesTitle via [QueryProperty] (paramètres
/// de navigation Shell), donc OnAppearing arrive après que les query params
/// soient appliqués.
///
/// Caméra cachée pour selfie auto : voir le bloc CaptureSelfie ci-dessous.
/// </summary>
public partial class PartyPlayPage : ContentPage
{
    private readonly PartyPlayViewModel _vm;
    private readonly ICameraProvider _cameraProvider;
    private bool _cameraReady = false;
    private bool _permissionDenied = false;
    private bool _cameraInitInProgress = false;

    public PartyPlayPage(PartyPlayViewModel vm, ICameraProvider cameraProvider)
    {
        InitializeComponent();
        _vm = vm;
        _cameraProvider = cameraProvider;
        BindingContext = _vm;

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
            System.Diagnostics.Debug.WriteLine("[PartyPlayPage] Camera permission denied → selfies désactivés");
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
                            $"[PartyPlayPage] Caméra avant: {frontCamera.Name}");
                    }

                    // ⚠️ Sur 2ème navigation (nouvelle partie après être revenu en arrière),
                    // la SurfaceView n'a pas encore réattaché son Handler quand on appelle
                    // StartCameraPreview → "Unable to retrieve Handler". On attend le
                    // Handler max 3s puis on retry une fois si encore échec.
                    var handlerTimeout = DateTime.UtcNow.AddSeconds(3);
                    while (HiddenCamera.Handler == null && DateTime.UtcNow < handlerTimeout)
                        await Task.Delay(100);

                    try
                    {
                        await HiddenCamera.StartCameraPreview(CancellationToken.None);
                        _cameraReady = true;
                        System.Diagnostics.Debug.WriteLine("[PartyPlayPage] Caméra prête");
                    }
                    catch (Exception exFirst) when (exFirst.Message.Contains("Handler"))
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[PartyPlayPage] Camera 1st try failed, retry: {exFirst.Message}");
                        await Task.Delay(500);
                        await HiddenCamera.StartCameraPreview(CancellationToken.None);
                        _cameraReady = true;
                        System.Diagnostics.Debug.WriteLine("[PartyPlayPage] Caméra prête (after retry)");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[PartyPlayPage] Camera init: {ex.Message}");
                    _cameraReady = false;
                }
                finally
                {
                    _cameraInitInProgress = false;
                }
            }
        }

        // 2) Lancer le flow de la partie
        await _vm.InitAsync();
    }

    /// <summary>
    /// Handler appelé par le ViewModel quand il faut prendre le selfie.
    /// </summary>
    private async Task<Stream?> CaptureSelfieFromCameraAsync()
    {
        if (_permissionDenied || !_cameraReady)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[PartyPlayPage] Selfie skipped (denied={_permissionDenied}, ready={_cameraReady})");
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

            var completed = await Task.WhenAny(tcs.Task, Task.Delay(8000));
            if (completed != tcs.Task)
            {
                HiddenCamera.MediaCaptured -= handler;
                System.Diagnostics.Debug.WriteLine("[PartyPlayPage] Selfie capture TIMEOUT (8s)");
                return null;
            }
            return await tcs.Task;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PartyPlayPage] CaptureSelfie: {ex.Message}");
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

        await _vm.CleanupAsync();
        _cameraReady = false;
    }
}
