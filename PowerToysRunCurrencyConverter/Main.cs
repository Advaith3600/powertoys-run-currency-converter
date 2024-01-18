﻿using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using ManagedCommon;
using Wox.Plugin;
using Microsoft.PowerToys.Settings.UI.Library;


namespace PowerToysRunCurrencyConverter
{
    public class Main : IPlugin, ISettingProvider
    {
        public static string PluginID => "EF1F634F20484459A3679B4DE7B07999";

        private string IconPath { get; set; }
        private PluginInitContext Context { get; set; }
        public string Name => "Currency Converter";

        public string Description => "Currency Converter Plugin";

        private Dictionary<string, (double, DateTime)> ConversionCache = new Dictionary<string, (double, DateTime)>();
        private readonly HttpClient Client = new HttpClient();
        private readonly RegionInfo regionInfo = new RegionInfo(CultureInfo.CurrentCulture.LCID);

        private int ConversionDirection;
        private string LocalCurrency, GlobalCurrency;

        public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>()
        {
            new PluginAdditionalOption()
            {
                Key = "QuickConversionDirection",
                DisplayLabel = "Quick Convertion Direction",
                DisplayDescription = "Set in which direction you want to convert.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Combobox,
                ComboBoxItems = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("From local to global", "0"),
                    new KeyValuePair<string, string>("From global to local", "1"),
                },
                ComboBoxValue = ConversionDirection,
            },
            new PluginAdditionalOption()
            {
                Key = "QuickConversionLocalCurrency",
                DisplayLabel = "Quick Convertion Local Currency",
                DisplayDescription = "Set your local currency.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = regionInfo.ISOCurrencySymbol,
            },
            new PluginAdditionalOption()
            {
                Key = "QuickConversionGlobalCurrency",
                DisplayLabel = "Quick Convertion Global Currency",
                DisplayDescription = "Set your global currency.",
                PluginOptionType = PluginAdditionalOption.AdditionalOptionType.Textbox,
                TextValue = "USD",
            },
        };

        public void UpdateSettings(PowerLauncherPluginSettings settings)
        {
            if (settings != null && settings.AdditionalOptions != null)
            {
                ConversionDirection = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionDirection")?.ComboBoxValue ?? 0;
                string _LocalCurrency = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionLocalCurrency").TextValue;
                LocalCurrency = _LocalCurrency == "" ? regionInfo.ISOCurrencySymbol : _LocalCurrency;

                string _GlobalCurrency = settings.AdditionalOptions.FirstOrDefault(x => x.Key == "QuickConversionGlobalCurrency").TextValue;
                GlobalCurrency = _GlobalCurrency == "" ? "USD" : _GlobalCurrency;
            }
        }

        private double? GetConversionRate(string fromCurrency, string toCurrency)
        {
            string key = $"{fromCurrency}-{toCurrency}";
            if (ConversionCache.ContainsKey(key) && ConversionCache[key].Item2 > DateTime.Now.AddHours(-1)) // cache for 1 hour
            {
                return ConversionCache[key].Item1;
            }
            else
            {
                string url = $"https://cdn.jsdelivr.net/gh/fawazahmed0/currency-api@1/latest/currencies/{fromCurrency}/{toCurrency}.json";
                try
                {
                    var response = Client.GetStringAsync(url).Result;
                    JsonDocument document = JsonDocument.Parse(response);
                    JsonElement root = document.RootElement;
                    double conversionRate = root.GetProperty(toCurrency).GetDouble();
                    ConversionCache[key] = (conversionRate, DateTime.Now);
                    return conversionRate;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        public List<Result> Query(Query query)
        {
            double amountToConvert = 0;
            var parts = query.Search.Trim().Split(" ");
            if (! ((parts.Length == 1 || parts.Length == 4) && double.TryParse(parts[0], out amountToConvert)))
            {
                return new List<Result>();
            }

            string fromCurrency, toCurrency;

            if (parts.Length == 1)
            {
                fromCurrency = (ConversionDirection == 0 ? LocalCurrency : GlobalCurrency).ToLower();
                toCurrency = (ConversionDirection == 0 ? GlobalCurrency : LocalCurrency).ToLower();
            }
            else
            {
                fromCurrency = parts[1].ToLower();
                toCurrency = parts[3].ToLower();
            }

            double? conversionRate = GetConversionRate(fromCurrency, toCurrency);

            if (conversionRate == null)
            {
                return new List<Result>
                {
                    new Result
                    {
                        Title = "Something went wrong.",
                        SubTitle = "Please try again.",
                        IcoPath = IconPath,
                    }
                };
            }

            double convertedAmount = Math.Round(amountToConvert * (double) conversionRate, 2);

            return new List<Result>
            {
                new Result
                {
                    Title = $"{convertedAmount} {toCurrency.ToUpper()}",
                    SubTitle = $"Currency conversion from {fromCurrency.ToUpper()} to {toCurrency.ToUpper()}",
                    IcoPath = IconPath,
                    Action = e =>
                    {
                        Clipboard.SetText(convertedAmount.ToString());
                        return true;
                    }
                }
            };
        }

        public void Init(PluginInitContext context)
        {
            Context = context;
            Context.API.ThemeChanged += OnThemeChanged;
            UpdateIconPath(Context.API.GetCurrentTheme());
        }

        private void UpdateIconPath(Theme theme)
        {
            if (theme == Theme.Light || theme == Theme.HighContrastWhite)
            {
                IconPath = "images/icon-black.png";
            }
            else
            {
                IconPath = "images/icon-white.png";
            }
        }

        private void OnThemeChanged(Theme currentTheme, Theme newTheme)
        {
            UpdateIconPath(newTheme);
        }

        public System.Windows.Controls.Control CreateSettingPanel()
        {
            throw new NotImplementedException();
        }
    }
}