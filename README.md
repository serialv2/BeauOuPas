# HomePage v2 — Refonte avec flow de création + crédits + onglet Amis

## Ce qui change

1. **Icône Home** : ajout de `icon_home.svg` (manquait → l'icône s'affiche enfin)
2. **TabBar** : 👥 mène désormais aux **Amis** (avant : Groupes)
   - Les Groupes deviennent accessibles via la mini-card "Mes groupes" sur l'accueil
3. **Carte violette "Jouer un quiz"** → devient **"✨ Créer un quiz ou une partie"**
   - N'ouvre plus la saisie de code (c'était un doublon avec le champ rose au-dessus)
   - Ouvre maintenant un nouvel écran avec 3 options : Sans groupe / Nouveau groupe / Groupe existant
4. **Pastille crédits** en haut à droite de la barre de titre, cliquable → CreditsPage

## Fichiers à AJOUTER (4 nouveaux)

```
Views/Home/CreateContextPage.xaml
Views/Home/CreateContextPage.xaml.cs
Views/Home/PickGroupForCreationPage.xaml
Views/Home/PickGroupForCreationPage.xaml.cs

ViewModels/Home/CreateContextViewModel.cs
ViewModels/Home/PickGroupForCreationViewModel.cs

Resources/Images/icon_home.svg     ← important : icône qui manquait
```

## Fichiers à REMPLACER

```
Views/Home/HomePage.xaml                  ← carte violette modifiée + pastille crédits
ViewModels/Home/HomeViewModel.cs          ← GoToCreate au lieu de GoToQuiz
ViewModels/Groups/CreateGroupViewModel.cs ← support du chainCreate (flow nouveau groupe)

AppShell.xaml                             ← icon_home.svg + onglet Friends
AppShell.xaml.cs                          ← routes CreateContextPage + PickGroupForCreationPage

MauiProgram.cs                            ← DI pour CreateContextViewModel/Page +
                                            PickGroupForCreationViewModel/Page

Resources/Strings/AppResources*.resx (×7) ← +17 nouvelles clés (en plus des 25 v1)
```

## Flow de création (le nouveau)

L'utilisateur clique sur la carte **"✨ Créer un quiz ou une partie"** → ouvre `CreateContextPage`

### Choix 1 — Sans groupe
- Direct sur `CreateSeriesTypePage` sans `GroupId`
- Crée une série standalone (rattachée à l'utilisateur)

### Choix 2 — Nouveau groupe
- Va sur `CreateGroupPage` avec `?ChainCreate=true`
- Une fois le groupe créé, le ViewModel enchaîne automatiquement sur `CreateSeriesTypePage` avec le `GroupId` tout neuf
- Sinon (`ChainCreate=false`) : comportement classique (retour en arrière)

### Choix 3 — Dans un groupe existant
- **Caché si l'utilisateur a 0 groupe**
- Va sur `PickGroupForCreationPage` (liste des groupes)
- Une fois un groupe choisi : `CreateSeriesTypePage` avec le `GroupId` choisi

## Pastille crédits

- Affichée dans la `Shell.TitleView` (zone à droite du titre de page)
- Format : 🪙 `{Credits}` sur fond rose pâle, texte rose vif
- Tap → ouvre `CreditsPage`
- Mise à jour automatique à chaque retour sur HomePage (`OnAppearing` rappelle `LoadAsync`)

## Onglet Friends à la place de Groups

J'ai changé l'icône 👥 dans la TabBar pour qu'elle pointe vers `FriendsPage`.
- Les Groupes restent accessibles via la mini-card "Mes groupes" sur l'accueil
- La route `GroupsPage` est toujours enregistrée dans `AppShell.xaml.cs` donc la
  navigation programmatique vers `//GroupsPage` continue de fonctionner.

## Petit détail technique sur CreateGroupViewModel

J'ai ajouté un nouveau `[QueryProperty(nameof(ChainCreate), "ChainCreate")]` au ViewModel.
Quand l'utilisateur arrive depuis le flow "Nouveau groupe" (Choix 2), ce flag est mis à true.
Après création du groupe, on fait :

```csharp
if (ChainCreate)
{
    await Shell.Current.GoToAsync("..");           // retire CreateGroupPage du back-stack
    await Task.Delay(50);                          // laisse la nav se stabiliser
    await Shell.Current.GoToAsync("CreateSeriesTypePage", { GroupId, GroupName });
}
else
{
    await Shell.Current.GoToAsync("..");           // comportement classique
}
```

Comme ça le bouton retour depuis `CreateSeriesTypePage` ne ramène pas l'utilisateur sur le formulaire de création de groupe (qui n'a plus de sens).

## Tests à faire

1. **Démarrage** : login → tu arrives sur HomePage avec :
   - Icône maison qui s'affiche dans la TabBar (1er onglet)
   - Pastille 🪙 en haut à droite avec ton nb de crédits
2. **Pastille crédits** : tap → ouvre CreditsPage
3. **Onglet 👥** : tu arrives sur la page Amis (et plus Groupes)
4. **Carte violette "Créer un quiz ou une partie"** : tap → tu arrives sur CreateContextPage
5. **3 cartes "Où créer ?"** :
   - Si tu as 0 groupe : seules les 2 premières cartes s'affichent
   - Si tu as ≥1 groupe : les 3 s'affichent
6. **Flow 1 (Sans groupe)** : tap → CreateSeriesTypePage → choix Vote/Quiz → série créée standalone
7. **Flow 2 (Nouveau groupe)** : tap → CreateGroupPage → tu remplis le nom → "Créer le groupe" → enchaîne automatiquement sur CreateSeriesTypePage → la nouvelle série/quiz se crée DANS le nouveau groupe
8. **Flow 3 (Groupe existant)** : tap → PickGroupForCreationPage → tu choisis un groupe → CreateSeriesTypePage → la série se crée DANS le groupe choisi
9. **Multi-langue** : change la langue de l'app → tous les nouveaux textes sont traduits dans 7 langues

## Si ça plante au build

Cause probable : oubli d'inclure `icon_home.svg` dans le projet `.csproj` (Build Action = MauiImage).
- Soit tu fais clic-droit sur le fichier dans VS → propriétés → Build Action = MauiImage
- Soit tu vérifies que le fichier est bien dans `<MauiImage Include="Resources\Images\*" />` dans ton `.csproj`

## Backlog post-livraison

Quand l'utilisateur est dans le flow "Nouveau groupe" (flow 2), s'il appuie sur "Annuler" ou retour, on revient à `CreateContextPage`. Comportement OK.

Quand il est dans le flow 3 et clique retour depuis la liste de groupes, il revient à `CreateContextPage`. OK.

Si une série est créée avec succès, l'utilisateur arrive sur `SeriesDetailPage`. S'il clique retour, il revient... à `CreateContextPage` ou la page de création — c'est à voir si ça pose problème. Si oui, on pourra faire un `Shell.Current.GoToAsync("//HomePage")` après création réussie.
