namespace BeauOuPas.Platforms.Android
{
    /// <summary>
    /// Service de messagerie push Android — NEUTRALISÉ.
    ///
    /// Le package Plugin.Firebase a été retiré (mur de compilation
    /// iOS + Android). Cette classe est conservée vide pour ne casser
    /// aucune référence éventuelle, mais ne reçoit plus de messages FCM.
    ///
    /// Les notifications push (Android + iOS) seront réintégrées plus
    /// tard via une stratégie dédiée. Non bloquant pour la publication.
    /// </summary>
    public class BeauOuPasFirebaseMessagingService
    {
        // Aucune dépendance Firebase. Réintégration future :
        //  - réactiver Plugin.Firebase.* dans le csproj
        //  - restaurer l'héritage FirebaseMessagingService
        //  - restaurer [Service]/[IntentFilter] + OnMessageReceived/OnNewToken
    }
}