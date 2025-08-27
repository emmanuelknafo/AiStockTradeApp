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
                ["Industry"] = "Industry"
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
                ["Industry"] = "Industrie"
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