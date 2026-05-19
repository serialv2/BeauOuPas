$projectRoot = Get-Location
$stringsPath = Join-Path $projectRoot "Resources\Strings"

if (!(Test-Path $stringsPath)) {
    Write-Host "Dossier Resources\Strings introuvable." -ForegroundColor Red
    exit
}

$translations = @{
    "AppResources.resx" = @{
        "Common_Female" = "Femme"
        "Common_Male" = "Homme"
        "Common_Other" = "Autre"
        "Common_Yes" = "Oui"
        "CompleteProfile_AgeError" = "Tu dois avoir au moins 18 ans pour utiliser l'application."
        "CompleteProfile_BirthDate" = "Date de naissance"
        "CompleteProfile_Continue" = "Continuer"
        "CompleteProfile_Gender" = "Genre"
        "CompleteProfile_Location" = "Localisation"
        "CompleteProfile_LocationHint" = "Autorise la localisation pour améliorer ton expérience."
        "CompleteProfile_Notifs" = "Notifications"
        "CompleteProfile_NotifsHint" = "Reçois les alertes importantes de l'application."
        "CompleteProfile_ProjectPref" = "Préférences de projets"
        "CompleteProfile_Subtitle" = "Complète ton profil pour commencer à utiliser BeauOuPas."
        "CompleteProfile_Title" = "Compléter mon profil"
        "CompleteProfile_Username" = "Pseudo"
        "CompleteProfile_UsernameRequired" = "Le pseudo est obligatoire."
        "Create_CheckingCredits" = "Vérification des crédits..."
        "Create_Compressing" = "Compression de la photo..."
        "Create_CompressingAll" = "Compression des photos..."
        "Create_ErrorPhoto" = "Veuillez ajouter une photo."
        "Create_ErrorTitle" = "Veuillez saisir un titre."
        "Create_ErrorTwoPhotos" = "Veuillez ajouter deux photos pour un duel."
        "Create_PhotoInvalid" = "La photo n'est pas valide."
        "Create_PrivateProjectHint" = "Seules les personnes invitées pourront voir ce projet."
        "Create_PrivateProjectWarning" = "Projet privé"
        "Create_Refund" = "Tes crédits ont été remboursés."
        "Feedback_ErrorEmpty" = "Merci de saisir un message."
        "Feedback_Message" = "Message"
        "Feedback_Placeholder" = "Écris ton retour ici..."
        "Feedback_Rating" = "Note"
        "Feedback_Send" = "Envoyer"
        "Feedback_Subtitle" = "Ton avis nous aide à améliorer l'application."
        "Feedback_SuccessMessage" = "Merci pour ton retour !"
        "Feedback_SuccessTitle" = "Envoyé"
        "Feedback_Title" = "Donner mon avis"
        "FriendProfile_Message" = "Message"
        "FriendProfile_NoProjects" = "Aucun projet pour le moment."
        "FriendProfile_Projects" = "Projets"
        "FriendProfile_Remove" = "Supprimer"
        "Friends_Accept" = "Accepter"
        "Friends_AcceptError" = "Impossible d'accepter cette invitation."
        "Friends_CancelConfirm" = "Annuler l'invitation"
        "Friends_CreditsEarned" = "Crédits gagnés"
        "Friends_DeleteConfirm" = "Supprimer cet ami"
        "Friends_DeleteError" = "Impossible de supprimer cette relation."
        "Friends_DeleteTitle" = "Confirmation"
        "Friends_Empty" = "Aucun ami pour le moment."
        "Friends_EmptyHint" = "Invite tes amis pour voter sur leurs projets."
        "Friends_Invite" = "Inviter"
        "Friends_InviteAccepted" = "Invitation acceptée"
        "Friends_InviteButton" = "Inviter un ami"
        "Friends_NewFriend" = "Nouvel ami"
        "Friends_PendingReceived" = "Invitations reçues"
        "Friends_PendingSent" = "Invitations envoyées"
        "Friends_Title" = "Amis"
        "Maintenance_Message" = "L'application est temporairement indisponible."
        "Maintenance_Retry" = "Réessayer"
        "Profile_Avatar" = "Avatar"
        "Profile_ChangeAvatar" = "Changer l'avatar"
        "Profile_Feedback" = "Donner mon avis"
        "Profile_Logout" = "Déconnexion"
        "Profile_Online" = "En ligne"
        "Profile_Projects" = "Projets"
        "Profile_RecentlyOnline" = "Récemment en ligne"
        "Profile_UploadingAvatar" = "Envoi de l'avatar..."
        "Profile_Votes" = "Votes"
        "Report_AlreadyDone" = "Tu as déjà signalé ce contenu."
        "Report_Fake" = "Faux profil"
        "Report_Nudity" = "Nudité"
        "Report_Other" = "Autre"
        "Report_Spam" = "Spam"
        "Report_Success" = "Signalement envoyé."
        "Report_Title" = "Signaler"
        "Stats_AverageScore" = "Score moyen"
        "Stats_Dislike" = "Pas fan"
        "Stats_DuelResult" = "Résultat du duel"
        "Stats_Gender" = "Genre"
        "Stats_Like" = "J'aime"
        "Stats_Neutral" = "Moyen"
        "Stats_NoVotes" = "Aucun vote pour le moment."
        "Stats_Title" = "Statistiques"
        "Stats_Votes" = "Votes"
        "Update_Button" = "Mettre à jour"
        "Update_Message" = "Une nouvelle version est disponible."
        "Update_Title" = "Mise à jour disponible"
        "ProjectDetail_GiveOpinion" = "Donne ton avis"
        "ProjectDetail_Like" = "J’aime"
        "ProjectDetail_Neutral" = "Moyen"
        "ProjectDetail_Dislike" = "Pas fan"
        "ProjectDetail_TapVoteA" = "Appuie pour voter A"
        "ProjectDetail_TapVoteB" = "Appuie pour voter B"
        "ProjectDetail_ThanksVote" = "Merci pour ton vote !"
        "ProjectDetail_StatsHiddenAfterVote" = "Les statistiques sont masquées pour ce projet."
    }

    "AppResources.fr.resx" = @{
        "Common_Female" = "Femme"
        "Common_Male" = "Homme"
        "Common_Other" = "Autre"
        "Create_PrivateProjectHint" = "Seules les personnes invitées pourront voir ce projet."
        "Create_PrivateProjectWarning" = "Projet privé"
        "Friends_Accept" = "Accepter"
        "Friends_AcceptError" = "Impossible d'accepter cette invitation."
        "Friends_CancelConfirm" = "Annuler l'invitation"
        "Friends_CreditsEarned" = "Crédits gagnés"
        "Friends_Empty" = "Aucun ami pour le moment."
        "Friends_EmptyHint" = "Invite tes amis pour voter sur leurs projets."
        "Friends_Invite" = "Inviter"
        "Friends_InviteAccepted" = "Invitation acceptée"
        "Friends_InviteButton" = "Inviter un ami"
        "Friends_NewFriend" = "Nouvel ami"
        "Friends_PendingReceived" = "Invitations reçues"
        "Friends_PendingSent" = "Invitations envoyées"
        "Friends_Title" = "Amis"
        "Profile_Logout" = "Déconnexion"
        "Stats_AverageScore" = "Score moyen"
        "Stats_Dislike" = "Pas fan"
        "Stats_DuelResult" = "Résultat du duel"
        "Stats_Gender" = "Genre"
        "Stats_Like" = "J'aime"
        "Stats_Neutral" = "Moyen"
        "Stats_NoVotes" = "Aucun vote pour le moment."
        "Stats_Title" = "Statistiques"
        "Stats_Votes" = "Votes"
        "ProjectDetail_GiveOpinion" = "Donne ton avis"
        "ProjectDetail_Like" = "J’aime"
        "ProjectDetail_Neutral" = "Moyen"
        "ProjectDetail_Dislike" = "Pas fan"
        "ProjectDetail_TapVoteA" = "Appuie pour voter A"
        "ProjectDetail_TapVoteB" = "Appuie pour voter B"
        "ProjectDetail_ThanksVote" = "Merci pour ton vote !"
        "ProjectDetail_StatsHiddenAfterVote" = "Les statistiques sont masquées pour ce projet."
    }

    "AppResources.en.resx" = @{
        "Common_Female" = "Female"
        "Common_Male" = "Male"
        "Common_Other" = "Other"
        "Create_PrivateProjectHint" = "Only invited people will be able to see this project."
        "Create_PrivateProjectWarning" = "Private project"
        "Friends_Accept" = "Accept"
        "Friends_AcceptError" = "Unable to accept this invitation."
        "Friends_CancelConfirm" = "Cancel the invitation"
        "Friends_CreditsEarned" = "Credits earned"
        "Friends_Empty" = "No friends yet."
        "Friends_EmptyHint" = "Invite your friends to vote on their projects."
        "Friends_Invite" = "Invite"
        "Friends_InviteAccepted" = "Invitation accepted"
        "Friends_InviteButton" = "Invite a friend"
        "Friends_NewFriend" = "New friend"
        "Friends_PendingReceived" = "Received invitations"
        "Friends_PendingSent" = "Sent invitations"
        "Friends_Title" = "Friends"
        "Profile_Logout" = "Log out"
        "Stats_AverageScore" = "Average score"
        "Stats_Dislike" = "Dislike"
        "Stats_DuelResult" = "Duel result"
        "Stats_Gender" = "Gender"
        "Stats_Like" = "Like"
        "Stats_Neutral" = "Neutral"
        "Stats_NoVotes" = "No votes yet."
        "Stats_Title" = "Statistics"
        "Stats_Votes" = "Votes"
        "ProjectDetail_GiveOpinion" = "Give your opinion"
        "ProjectDetail_Like" = "Like"
        "ProjectDetail_Neutral" = "Neutral"
        "ProjectDetail_Dislike" = "Dislike"
        "ProjectDetail_TapVoteA" = "Tap to vote A"
        "ProjectDetail_TapVoteB" = "Tap to vote B"
        "ProjectDetail_ThanksVote" = "Thanks for your vote!"
        "ProjectDetail_StatsHiddenAfterVote" = "Statistics are hidden for this project."
    }

    "AppResources.de.resx" = @{
        "Common_Female" = "Frau"
        "Common_Male" = "Mann"
        "Common_Other" = "Andere"
        "Create_PrivateProjectHint" = "Nur eingeladene Personen können dieses Projekt sehen."
        "Create_PrivateProjectWarning" = "Privates Projekt"
        "Friends_Accept" = "Annehmen"
        "Friends_AcceptError" = "Diese Einladung kann nicht angenommen werden."
        "Friends_CancelConfirm" = "Einladung abbrechen"
        "Friends_CreditsEarned" = "Verdiente Credits"
        "Friends_Empty" = "Noch keine Freunde."
        "Friends_EmptyHint" = "Lade deine Freunde ein, um über ihre Projekte abzustimmen."
        "Friends_Invite" = "Einladen"
        "Friends_InviteAccepted" = "Einladung angenommen"
        "Friends_InviteButton" = "Freund einladen"
        "Friends_NewFriend" = "Neuer Freund"
        "Friends_PendingReceived" = "Erhaltene Einladungen"
        "Friends_PendingSent" = "Gesendete Einladungen"
        "Friends_Title" = "Freunde"
        "Profile_Logout" = "Abmelden"
        "Stats_AverageScore" = "Durchschnittliche Bewertung"
        "Stats_Dislike" = "Gefällt mir nicht"
        "Stats_DuelResult" = "Duell-Ergebnis"
        "Stats_Gender" = "Geschlecht"
        "Stats_Like" = "Gefällt mir"
        "Stats_Neutral" = "Neutral"
        "Stats_NoVotes" = "Noch keine Stimmen."
        "Stats_Title" = "Statistiken"
        "Stats_Votes" = "Stimmen"
        "ProjectDetail_GiveOpinion" = "Gib deine Meinung ab"
        "ProjectDetail_Like" = "Gefällt mir"
        "ProjectDetail_Neutral" = "Neutral"
        "ProjectDetail_Dislike" = "Gefällt mir nicht"
        "ProjectDetail_TapVoteA" = "Tippe, um für A zu stimmen"
        "ProjectDetail_TapVoteB" = "Tippe, um für B zu stimmen"
        "ProjectDetail_ThanksVote" = "Danke für deine Stimme!"
        "ProjectDetail_StatsHiddenAfterVote" = "Die Statistiken sind für dieses Projekt ausgeblendet."
    }

    "AppResources.es.resx" = @{
        "Common_Female" = "Mujer"
        "Common_Male" = "Hombre"
        "Common_Other" = "Otro"
        "Create_PrivateProjectHint" = "Solo las personas invitadas podrán ver este proyecto."
        "Create_PrivateProjectWarning" = "Proyecto privado"
        "Friends_Accept" = "Aceptar"
        "Friends_AcceptError" = "No se puede aceptar esta invitación."
        "Friends_CancelConfirm" = "Cancelar la invitación"
        "Friends_CreditsEarned" = "Créditos ganados"
        "Friends_Empty" = "Aún no tienes amigos."
        "Friends_EmptyHint" = "Invita a tus amigos para votar en sus proyectos."
        "Friends_Invite" = "Invitar"
        "Friends_InviteAccepted" = "Invitación aceptada"
        "Friends_InviteButton" = "Invitar a un amigo"
        "Friends_NewFriend" = "Nuevo amigo"
        "Friends_PendingReceived" = "Invitaciones recibidas"
        "Friends_PendingSent" = "Invitaciones enviadas"
        "Friends_Title" = "Amigos"
        "Profile_Logout" = "Cerrar sesión"
        "Stats_AverageScore" = "Puntuación media"
        "Stats_Dislike" = "No me gusta"
        "Stats_DuelResult" = "Resultado del duelo"
        "Stats_Gender" = "Género"
        "Stats_Like" = "Me gusta"
        "Stats_Neutral" = "Neutral"
        "Stats_NoVotes" = "Aún no hay votos."
        "Stats_Title" = "Estadísticas"
        "Stats_Votes" = "Votos"
        "ProjectDetail_GiveOpinion" = "Da tu opinión"
        "ProjectDetail_Like" = "Me gusta"
        "ProjectDetail_Neutral" = "Neutral"
        "ProjectDetail_Dislike" = "No me gusta"
        "ProjectDetail_TapVoteA" = "Toca para votar A"
        "ProjectDetail_TapVoteB" = "Toca para votar B"
        "ProjectDetail_ThanksVote" = "¡Gracias por tu voto!"
        "ProjectDetail_StatsHiddenAfterVote" = "Las estadísticas están ocultas para este proyecto."
    }

    "AppResources.it.resx" = @{
        "Common_Female" = "Donna"
        "Common_Male" = "Uomo"
        "Common_Other" = "Altro"
        "Create_PrivateProjectHint" = "Solo le persone invitate potranno vedere questo progetto."
        "Create_PrivateProjectWarning" = "Progetto privato"
        "Friends_Accept" = "Accetta"
        "Friends_AcceptError" = "Impossibile accettare questo invito."
        "Friends_CancelConfirm" = "Annulla l'invito"
        "Friends_CreditsEarned" = "Crediti guadagnati"
        "Friends_Empty" = "Nessun amico per il momento."
        "Friends_EmptyHint" = "Invita i tuoi amici a votare i loro progetti."
        "Friends_Invite" = "Invita"
        "Friends_InviteAccepted" = "Invito accettato"
        "Friends_InviteButton" = "Invita un amico"
        "Friends_NewFriend" = "Nuovo amico"
        "Friends_PendingReceived" = "Inviti ricevuti"
        "Friends_PendingSent" = "Inviti inviati"
        "Friends_Title" = "Amici"
        "Profile_Logout" = "Disconnetti"
        "Stats_AverageScore" = "Punteggio medio"
        "Stats_Dislike" = "Non mi piace"
        "Stats_DuelResult" = "Risultato del duello"
        "Stats_Gender" = "Genere"
        "Stats_Like" = "Mi piace"
        "Stats_Neutral" = "Neutrale"
        "Stats_NoVotes" = "Ancora nessun voto."
        "Stats_Title" = "Statistiche"
        "Stats_Votes" = "Voti"
        "ProjectDetail_GiveOpinion" = "Dai la tua opinione"
        "ProjectDetail_Like" = "Mi piace"
        "ProjectDetail_Neutral" = "Neutrale"
        "ProjectDetail_Dislike" = "Non mi piace"
        "ProjectDetail_TapVoteA" = "Tocca per votare A"
        "ProjectDetail_TapVoteB" = "Tocca per votare B"
        "ProjectDetail_ThanksVote" = "Grazie per il tuo voto!"
        "ProjectDetail_StatsHiddenAfterVote" = "Le statistiche sono nascoste per questo progetto."
    }

    "AppResources.pt.resx" = @{
        "Common_Female" = "Mulher"
        "Common_Male" = "Homem"
        "Common_Other" = "Outro"
        "Create_PrivateProjectHint" = "Somente pessoas convidadas poderão ver este projeto."
        "Create_PrivateProjectWarning" = "Projeto privado"
        "Friends_Accept" = "Aceitar"
        "Friends_AcceptError" = "Não foi possível aceitar este convite."
        "Friends_CancelConfirm" = "Cancelar o convite"
        "Friends_CreditsEarned" = "Créditos ganhos"
        "Friends_Empty" = "Ainda não há amigos."
        "Friends_EmptyHint" = "Convide seus amigos para votar nos projetos deles."
        "Friends_Invite" = "Convidar"
        "Friends_InviteAccepted" = "Convite aceito"
        "Friends_InviteButton" = "Convidar um amigo"
        "Friends_NewFriend" = "Novo amigo"
        "Friends_PendingReceived" = "Convites recebidos"
        "Friends_PendingSent" = "Convites enviados"
        "Friends_Title" = "Amigos"
        "Profile_Logout" = "Sair"
        "Stats_AverageScore" = "Pontuação média"
        "Stats_Dislike" = "Não gostei"
        "Stats_DuelResult" = "Resultado do duelo"
        "Stats_Gender" = "Gênero"
        "Stats_Like" = "Gostei"
        "Stats_Neutral" = "Neutro"
        "Stats_NoVotes" = "Ainda não há votos."
        "Stats_Title" = "Estatísticas"
        "Stats_Votes" = "Votos"
        "ProjectDetail_GiveOpinion" = "Dê sua opinião"
        "ProjectDetail_Like" = "Gostei"
        "ProjectDetail_Neutral" = "Neutro"
        "ProjectDetail_Dislike" = "Não gostei"
        "ProjectDetail_TapVoteA" = "Toque para votar A"
        "ProjectDetail_TapVoteB" = "Toque para votar B"
        "ProjectDetail_ThanksVote" = "Obrigado pelo seu voto!"
        "ProjectDetail_StatsHiddenAfterVote" = "As estatísticas estão ocultas para este projeto."
    }
}

function Add-Or-Update-ResxValue {
    param(
        [string]$filePath,
        [hashtable]$values
    )

    if (!(Test-Path $filePath)) {
        Write-Host "Fichier introuvable : $filePath" -ForegroundColor Red
        return
    }

    [xml]$xml = Get-Content $filePath -Raw

    foreach ($key in $values.Keys) {
        $existing = $xml.root.data | Where-Object { $_.name -eq $key }

        if ($existing) {
            continue
        }

        $data = $xml.CreateElement("data")
        $nameAttr = $xml.CreateAttribute("name")
        $nameAttr.Value = $key
        $data.Attributes.Append($nameAttr) | Out-Null

        $spaceAttr = $xml.CreateAttribute("xml", "space", "http://www.w3.org/XML/1998/namespace")
        $spaceAttr.Value = "preserve"
        $data.Attributes.Append($spaceAttr) | Out-Null

        $value = $xml.CreateElement("value")
        $value.InnerText = $values[$key]
        $data.AppendChild($value) | Out-Null

        $xml.root.AppendChild($data) | Out-Null
    }

    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Indent = $true
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)

    $writer = [System.Xml.XmlWriter]::Create($filePath, $settings)
    $xml.Save($writer)
    $writer.Close()

    Write-Host "Mis à jour : $filePath" -ForegroundColor Green
}

foreach ($fileName in $translations.Keys) {
    $filePath = Join-Path $stringsPath $fileName
    Add-Or-Update-ResxValue -filePath $filePath -values $translations[$fileName]
}

Write-Host ""
Write-Host "Ajout des traductions terminé." -ForegroundColor Cyan
Write-Host "Relance ensuite check-translations.ps1 pour vérifier." -ForegroundColor Yellow