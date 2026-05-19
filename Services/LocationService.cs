namespace BeauOuPas.Services;

public class LocationService
{
    // ─── Récupérer la localisation ───────────────────────────────────
    public async Task<(double? Lat, double? Lng, string City, string Country)>
        GetLocationAsync()
    {
        try
        {
            // Vérifier les permissions
            var status = await Permissions
                .RequestAsync<Permissions.LocationWhenInUse>();

            if (status != PermissionStatus.Granted)
                return (null, null, string.Empty, string.Empty);

            // Récupérer la position
            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest
                {
                    DesiredAccuracy = GeolocationAccuracy.Medium,
                    Timeout = TimeSpan.FromSeconds(10)
                });

            if (location == null)
                return (null, null, string.Empty, string.Empty);

            // Reverse geocoding → ville et pays
            var placemarks = await Geocoding.Default
                .GetPlacemarksAsync(
                    location.Latitude,
                    location.Longitude);

            var placemark = placemarks?.FirstOrDefault();

            return (
                location.Latitude,
                location.Longitude,
                placemark?.Locality ?? string.Empty,
                placemark?.CountryName ?? string.Empty
            );
        }
        catch
        {
            return (null, null, string.Empty, string.Empty);
        }
    }
}