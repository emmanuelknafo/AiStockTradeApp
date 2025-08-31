using Microsoft.Extensions.Localization;
using System.Globalization;
using System.Collections.Concurrent;

namespace AiStockTradeApp.Services
{
    public class SimpleStringLocalizer : IStringLocalizer<SharedResource>
    {
        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _translations;

        public SimpleStringLocalizer()
        {
            _translations = new ConcurrentDictionary<string, Dictionary<string, string>>();
            LoadTranslations();
        }

        private void LoadTranslations()
        {
            // English translations
            _translations["en"] = new Dictionary<string, string>
            {
                ["App_Title"] = "AI Stock Tracker",
                ["Nav_Dashboard"] = "Dashboard",
                ["Nav_ListedStocks"] = "Listed Stocks",
                ["Header_Title"] = "AI-Powered Stock Tracker",
                ["Api_Status"] = "Status: Using multiple APIs for real-time data",
                ["Search_Placeholder"] = "Enter ticker symbol (e.g., AAPL)",
                ["Btn_AddStock"] = "Add Stock",
                ["Btn_ClearAll"] = "Clear All",
                ["Settings_Title"] = "Settings",
                ["Settings_AutoRefresh"] = "Auto-refresh every",
                ["Settings_Sound"] = "Sound notifications",
                ["Settings_ShowCharts"] = "Show mini charts",
                ["Stock_Price"] = "Price",
                ["Stock_Change"] = "Change",
                ["Stock_Percent"] = "Percent",
                ["Stock_Analysis"] = "Analysis",
                ["Stock_Recommendation"] = "Recommendation",
                ["Loading"] = "Loading...",
                ["Alerts_Title"] = "Price Alerts",
                ["Portfolio_Summary"] = "Portfolio Summary",
                ["Portfolio_TotalValue"] = "Total Value",
                ["Portfolio_TodaysChange"] = "Today's Change",
                ["Portfolio_Stocks"] = "Stocks",
                ["Export_CSV"] = "Export CSV",
                ["Export_JSON"] = "Export JSON",
                ["Import_Data"] = "Import Data",
                ["Lang_English"] = "English",
                ["Lang_French"] = "Français",
                ["Interval_10s"] = "10 seconds",
                ["Interval_30s"] = "30 seconds",
                ["Interval_1m"] = "1 minute",
                ["Interval_5m"] = "5 minutes",
                ["Listed_Title"] = "Listed Stocks",
                ["Back_To_Dashboard"] = "Back to Dashboard",
                ["Listed_TotalHistPrices"] = "Total historical prices:",
                ["Listed_SearchPlaceholder"] = "Search symbol or company name",
                ["Listed_AllSectors"] = "All Sectors",
                ["Listed_AllIndustries"] = "All Industries",
                ["Apply"] = "Apply",
                ["Results"] = "results",
                ["Historical_Prices"] = "Historical Prices",
                ["Date"] = "Date",
                ["Open"] = "Open",
                ["High"] = "High",
                ["Low"] = "Low",
                ["Close"] = "Close",
                ["Volume"] = "Volume",
                ["Symbol"] = "Symbol",
                ["Name"] = "Name",
                ["Last"] = "Last",
                ["Percent_Change"] = "% Change",
                ["Market_Cap"] = "Market Cap",
                ["Prev"] = "Prev",
                ["Next"] = "Next",
                ["Page"] = "Page",
                ["Of"] = "of",
                ["No_Historical_Data"] = "No historical data",
                ["Error_Loading_History"] = "Error loading history",
                ["Version_Label"] = "Version:",
                ["Sector"] = "Sector",
                ["Industry"] = "Industry",

                // Authentication translations
                ["Nav_Welcome"] = "Welcome",
                ["Nav_Profile"] = "Profile",
                ["Nav_Login"] = "Login",
                ["Nav_Logout"] = "Logout",
                ["Nav_Register"] = "Register",
                ["Nav_ManageWatchlist"] = "Manage Watchlist",

                ["Account_Login_Title"] = "Login",
                ["Account_Register_Title"] = "Create Account",
                ["Account_Profile_Title"] = "User Profile",
                ["Account_AccessDenied_Title"] = "Access Denied",

                ["Account_Email_Label"] = "Email Address",
                ["Account_Email_Placeholder"] = "Enter your email",
                ["Account_Email_ReadOnly"] = "Email cannot be changed",
                ["Account_Password_Label"] = "Password",
                ["Account_Password_Placeholder"] = "Enter your password",
                ["Account_Password_Requirements"] = "Minimum 6 characters, including uppercase, lowercase, and digit",
                ["Account_FirstName_Label"] = "First Name",
                ["Account_FirstName_Placeholder"] = "Enter your first name",
                ["Account_LastName_Label"] = "Last Name",
                ["Account_LastName_Placeholder"] = "Enter your last name",
                ["Account_ConfirmPassword_Label"] = "Confirm Password",
                ["Account_ConfirmPassword_Placeholder"] = "Confirm your password",
                ["Account_RememberMe_Label"] = "Remember me",
                ["Account_Language_Label"] = "Preferred Language",
                ["Account_EnableAlerts_Label"] = "Enable price alerts",
                ["Account_MemberSince_Label"] = "Member Since",
                ["Account_LastLogin_Label"] = "Last Login",
                ["Account_LastLogin_Never"] = "Never",

                ["Account_Login_Button"] = "Login",
                ["Account_Register_Button"] = "Create Account",
                ["Account_Profile_SaveButton"] = "Save Changes",
                ["Account_Login_Link"] = "Login",
                ["Account_Register_Link"] = "Create account",

                ["Account_Login_NoAccount"] = "Don't have an account?",
                ["Account_Register_HaveAccount"] = "Already have an account?",
                ["Account_Login_InvalidCredentials"] = "Invalid email or password",
                ["Account_Login_LockedOut"] = "Account temporarily locked due to multiple failed attempts",
                ["Account_Login_NotAllowed"] = "Account not verified. Please check your email for verification instructions.",
                ["Account_Login_Error"] = "An error occurred during login. Please try again.",
                ["Account_Register_Error"] = "An error occurred during registration. Please try again.",
                ["Account_Profile_UpdateSuccess"] = "Profile updated successfully",
                ["Account_Profile_UpdateError"] = "Error updating profile. Please try again.",

                ["Account_AccessDenied_Message"] = "You don't have permission to access this resource",
                ["Account_AccessDenied_Description"] = "Please login with an account that has the required permissions.",
                ["Account_AccessDenied_BackHome"] = "Back to Home",

                // User watchlist error and success messages
                ["Error_InvalidSymbol"] = "Invalid stock symbol",
                ["Error_StockNotFound"] = "Stock not found",
                ["Error_AddingStock"] = "Error adding stock to watchlist",
                ["Error_RemovingStock"] = "Error removing stock from watchlist",
                ["Error_ClearingWatchlist"] = "Error clearing watchlist",
                ["Error_LoadingWatchlist"] = "Error loading watchlist",
                ["Error_LoadingDashboard"] = "Error loading dashboard",
                ["Error_InvalidAlert"] = "Invalid alert request",
                ["Error_StockNotInWatchlist"] = "Stock not found in watchlist",
                ["Error_SettingAlert"] = "Error setting price alert",
                ["Error_LoadingWatchlistManagement"] = "Error loading watchlist management",

                ["Success_StockAdded"] = "Added {0} to watchlist",
                ["Success_StockRemoved"] = "Removed {0} from watchlist", 
                ["Success_WatchlistCleared"] = "Watchlist cleared successfully",
                ["Success_AlertSet"] = "Alert set: {0} {1} ${2}"
            };

            // French translations
            _translations["fr"] = new Dictionary<string, string>
            {
                ["App_Title"] = "Suivi d'actions IA",
                ["Nav_Dashboard"] = "Tableau de bord",
                ["Nav_ListedStocks"] = "Actions cotées",
                ["Header_Title"] = "Suivi d'actions piloté par l'IA",
                ["Api_Status"] = "Statut : Utilisation de plusieurs API pour des données en temps réel",
                ["Search_Placeholder"] = "Entrez le symbole (ex. : AAPL)",
                ["Btn_AddStock"] = "Ajouter",
                ["Btn_ClearAll"] = "Tout effacer",
                ["Settings_Title"] = "Paramètres",
                ["Settings_AutoRefresh"] = "Actualisation auto toutes les",
                ["Settings_Sound"] = "Notifications sonores",
                ["Settings_ShowCharts"] = "Afficher les mini-graphiques",
                ["Stock_Price"] = "Prix",
                ["Stock_Change"] = "Variation",
                ["Stock_Percent"] = "Pourcentage",
                ["Stock_Analysis"] = "Analyse",
                ["Stock_Recommendation"] = "Recommandation",
                ["Loading"] = "Chargement…",
                ["Alerts_Title"] = "Alertes de prix",
                ["Portfolio_Summary"] = "Récapitulatif du portefeuille",
                ["Portfolio_TotalValue"] = "Valeur totale",
                ["Portfolio_TodaysChange"] = "Variation du jour",
                ["Portfolio_Stocks"] = "Actions",
                ["Export_CSV"] = "Exporter CSV",
                ["Export_JSON"] = "Exporter JSON",
                ["Import_Data"] = "Importer des données",
                ["Lang_English"] = "English",
                ["Lang_French"] = "Français",
                ["Interval_10s"] = "10 secondes",
                ["Interval_30s"] = "30 secondes",
                ["Interval_1m"] = "1 minute",
                ["Interval_5m"] = "5 minutes",
                ["Listed_Title"] = "Actions cotées",
                ["Back_To_Dashboard"] = "Retour au tableau de bord",
                ["Listed_TotalHistPrices"] = "Total des prix historiques :",
                ["Listed_SearchPlaceholder"] = "Rechercher un symbole ou le nom de l'entreprise",
                ["Listed_AllSectors"] = "Tous les secteurs",
                ["Listed_AllIndustries"] = "Toutes les industries",
                ["Apply"] = "Appliquer",
                ["Results"] = "résultats",
                ["Historical_Prices"] = "Prix historiques",
                ["Date"] = "Date",
                ["Open"] = "Ouverture",
                ["High"] = "Plus haut",
                ["Low"] = "Plus bas",
                ["Close"] = "Clôture",
                ["Volume"] = "Volume",
                ["Symbol"] = "Symbole",
                ["Name"] = "Nom",
                ["Last"] = "Dernier",
                ["Percent_Change"] = "% Variation",
                ["Market_Cap"] = "Capitalisation",
                ["Prev"] = "Préc.",
                ["Next"] = "Suiv.",
                ["Page"] = "Page",
                ["Of"] = "sur",
                ["No_Historical_Data"] = "Aucune donnée historique",
                ["Error_Loading_History"] = "Erreur de chargement de l'historique",
                ["Version_Label"] = "Version :",
                ["Sector"] = "Secteur",
                ["Industry"] = "Industrie",

                // Authentication translations
                ["Nav_Welcome"] = "Bienvenue",
                ["Nav_Profile"] = "Profil",
                ["Nav_Login"] = "Connexion",
                ["Nav_Logout"] = "Déconnexion",
                ["Nav_Register"] = "S'inscrire",
                ["Nav_ManageWatchlist"] = "Gérer la liste de surveillance",

                ["Account_Login_Title"] = "Connexion",
                ["Account_Register_Title"] = "Créer un compte",
                ["Account_Profile_Title"] = "Profil utilisateur",
                ["Account_AccessDenied_Title"] = "Accès refusé",

                ["Account_Email_Label"] = "Adresse email",
                ["Account_Email_Placeholder"] = "Entrez votre email",
                ["Account_Email_ReadOnly"] = "L'email ne peut pas être modifié",
                ["Account_Password_Label"] = "Mot de passe",
                ["Account_Password_Placeholder"] = "Entrez votre mot de passe",
                ["Account_Password_Requirements"] = "Minimum 6 caractères, incluant majuscules, minuscules et chiffres",
                ["Account_FirstName_Label"] = "Prénom",
                ["Account_FirstName_Placeholder"] = "Entrez votre prénom",
                ["Account_LastName_Label"] = "Nom",
                ["Account_LastName_Placeholder"] = "Entrez votre nom",
                ["Account_ConfirmPassword_Label"] = "Confirmer le mot de passe",
                ["Account_ConfirmPassword_Placeholder"] = "Confirmez votre mot de passe",
                ["Account_RememberMe_Label"] = "Se souvenir de moi",
                ["Account_Language_Label"] = "Langue préférée",
                ["Account_EnableAlerts_Label"] = "Activer les alertes de prix",
                ["Account_MemberSince_Label"] = "Membre depuis",
                ["Account_LastLogin_Label"] = "Dernière connexion",
                ["Account_LastLogin_Never"] = "Jamais",

                ["Account_Login_Button"] = "Se connecter",
                ["Account_Register_Button"] = "Créer le compte",
                ["Account_Profile_SaveButton"] = "Enregistrer",
                ["Account_Login_Link"] = "Se connecter",
                ["Account_Register_Link"] = "Créer un compte",

                ["Account_Login_NoAccount"] = "Pas de compte ?",
                ["Account_Register_HaveAccount"] = "Déjà un compte ?",
                ["Account_Login_InvalidCredentials"] = "Email ou mot de passe invalide",
                ["Account_Login_LockedOut"] = "Compte temporairement verrouillé suite à plusieurs tentatives",
                ["Account_Login_NotAllowed"] = "Compte non vérifié. Vérifiez votre email pour les instructions de vérification.",
                ["Account_Login_Error"] = "Erreur lors de la connexion. Veuillez réessayer.",
                ["Account_Register_Error"] = "Erreur lors de l'inscription. Veuillez réessayer.",
                ["Account_Profile_UpdateSuccess"] = "Profil mis à jour avec succès",
                ["Account_Profile_UpdateError"] = "Erreur lors de la mise à jour. Veuillez réessayer.",

                ["Account_AccessDenied_Message"] = "Vous n'avez pas l'autorisation d'accéder à cette ressource",
                ["Account_AccessDenied_Description"] = "Veuillez vous connecter avec un compte ayant les permissions requises.",
                ["Account_AccessDenied_BackHome"] = "Retour à l'accueil",

                // User watchlist error and success messages (French)
                ["Error_InvalidSymbol"] = "Symbole d'action invalide",
                ["Error_StockNotFound"] = "Action non trouvée",
                ["Error_AddingStock"] = "Erreur lors de l'ajout de l'action à la liste de surveillance",
                ["Error_RemovingStock"] = "Erreur lors de la suppression de l'action de la liste de surveillance",
                ["Error_ClearingWatchlist"] = "Erreur lors de l'effacement de la liste de surveillance",
                ["Error_LoadingWatchlist"] = "Erreur lors du chargement de la liste de surveillance",
                ["Error_LoadingDashboard"] = "Erreur lors du chargement du tableau de bord",
                ["Error_InvalidAlert"] = "Demande d'alerte invalide",
                ["Error_StockNotInWatchlist"] = "Action non trouvée dans la liste de surveillance",
                ["Error_SettingAlert"] = "Erreur lors de la configuration de l'alerte de prix",
                ["Error_LoadingWatchlistManagement"] = "Erreur lors du chargement de la gestion de la liste de surveillance",

                ["Success_StockAdded"] = "{0} ajouté à la liste de surveillance",
                ["Success_StockRemoved"] = "{0} supprimé de la liste de surveillance",
                ["Success_WatchlistCleared"] = "Liste de surveillance effacée avec succès",
                ["Success_AlertSet"] = "Alerte configurée : {0} {1} ${2}"
            };
        }

        public LocalizedString this[string name]
        {
            get
            {
                var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
                
                if (_translations.TryGetValue(culture, out var translations) && 
                    translations.TryGetValue(name, out var value))
                {
                    return new LocalizedString(name, value, false);
                }
                
                // Fallback to English
                if (_translations.TryGetValue("en", out var englishTranslations) && 
                    englishTranslations.TryGetValue(name, out var englishValue))
                {
                    return new LocalizedString(name, englishValue, false);
                }
                
                // Return the key itself if not found
                return new LocalizedString(name, name, true);
            }
        }

        public LocalizedString this[string name, params object[] arguments]
        {
            get
            {
                var localizedString = this[name];
                return new LocalizedString(name, string.Format(localizedString.Value, arguments), localizedString.ResourceNotFound);
            }
        }

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            var culture = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
            
            if (_translations.TryGetValue(culture, out var translations))
            {
                return translations.Select(kvp => new LocalizedString(kvp.Key, kvp.Value, false));
            }
            
            // Fallback to English
            if (_translations.TryGetValue("en", out var englishTranslations))
            {
                return englishTranslations.Select(kvp => new LocalizedString(kvp.Key, kvp.Value, false));
            }
            
            return Enumerable.Empty<LocalizedString>();
        }
    }
}