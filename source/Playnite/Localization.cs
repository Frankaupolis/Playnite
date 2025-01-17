﻿using Playnite.Common;
using Playnite.SDK;
using Playnite.Settings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Markup;

namespace Playnite
{
    public class PlayniteLanguage
    {
        public int TranslatedPercentage
        {
            get; set;
        } = -1;

        public string LocaleString
        {
            get; set;
        }

        public string Id
        {
            get; set;
        }

        public string DisplayString
        {
            get => ToString();
        }

        public override string ToString()
        {
            if (TranslatedPercentage > -1)
            {
                return $"{LocaleString}   ({Id}, {TranslatedPercentage}%)";
            }
            else
            {
                return LocaleString;
            }
        }
    }

    public static class Localization
    {
        private static ILogger logger = LogManager.GetLogger();
        public const string SourceLanguageId = "english";

        public static List<PlayniteLanguage> AvailableLanguages
        {
            get
            {
                return GetLanguagesFromFolder(PlaynitePaths.LocalizationsPath);
            }
        }

        public static string CurrentLanguage
        {
            get; private set;
        } = SourceLanguageId;

        public static CultureInfo ApplicationLanguageCultureInfo
        {
            get; private set;
        } = CultureInfo.CurrentCulture;

        public static bool IsRightToLeft
        {
            get
            {
                return ApplicationLanguageCultureInfo.TextInfo.IsRightToLeft;
            }
        }

        public static List<PlayniteLanguage> GetLanguagesFromFolder(string path)
        {
            var coverage = Serialization.FromJsonFile<Dictionary<string, int>>(PlaynitePaths.LocalizationsStatusPath);
            var langs = new List<PlayniteLanguage>() {
                new PlayniteLanguage()
                {
                    Id = SourceLanguageId,
                    LocaleString = "English"
                }
            };

            if (!Directory.Exists(path))
            {
                return langs;
            }

            foreach (var file in Directory.GetFiles(path, "*.xaml"))
            {
                if (!Regex.IsMatch(file, "[a-zA-Z]+_[a-zA-Z]+"))
                {
                    continue;
                }

                var fileCode = Path.GetFileNameWithoutExtension(file);
                if (!coverage.TryGetValue(fileCode.Replace('_', '-'), out var lngCov))
                {
                    coverage.TryGetValue(fileCode.Split('_')[0], out lngCov);
                }

                var langPath = Path.Combine(path, file);
                ResourceDictionary res = null;
                try
                {
                    res = Xaml.FromFile<ResourceDictionary>(langPath);
                }
                catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    logger.Error(e, $"Failed to parse localization file {file}");
                    continue;
                }

                langs.Add(new PlayniteLanguage()
                {
                    Id = Path.GetFileNameWithoutExtension(langPath),
                    LocaleString = res["LanguageName"].ToString(),
                    TranslatedPercentage = lngCov
                });
            }

            return langs.OrderBy(a => a.LocaleString).ToList();
        }

        public static void SetLanguage(string language)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            if (CurrentLanguage != SourceLanguageId)
            {
                var currentLang = dictionaries.FirstOrDefault(a => a["LanguageName"] != null && a.Source == null);
                if (currentLang != null)
                {
                    dictionaries.Remove(currentLang);
                }
            }

            var langFile = Path.Combine(PlaynitePaths.LocalizationsPath, language + ".xaml");
            if (File.Exists(langFile) && language != SourceLanguageId)
            {
                ResourceDictionary res = null;
                try
                {
                    res = Xaml.FromFile<ResourceDictionary>(langFile);
                    res.Source = new Uri(langFile, UriKind.Absolute);
                    // Unstranslated strings are imported as empty entries by Crowdin.
                    // We need to remove them to make sure that origina English text will be displayed instead.
                    foreach (var key in res.Keys)
                    {
                        if (res[key] is string locString && locString.IsNullOrEmpty())
                        {
                            res.Remove(key);
                        }
                    }
                }
                catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    logger.Error(e, $"Failed to parse localization file {langFile}");
                    return;
                }

                dictionaries.Add(res);

                ApplicationLanguageCultureInfo = new CultureInfo(language.Replace("_", "-"), false);
            }
            else
            {
                ApplicationLanguageCultureInfo = new CultureInfo("en-US", false); // english is the default language
            }

            CurrentLanguage = language;
        }

        public static void LoadExtensionsLocalization(string extensionDir)
        {
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            void loadString(string xamlPath)
            {
                ResourceDictionary res = null;
                try
                {
                    res = Xaml.FromFile<ResourceDictionary>(xamlPath);
                    res.Source = new Uri(xamlPath, UriKind.Absolute);
                    foreach (var key in res.Keys)
                    {
                        if (res[key] is string locString)
                        {
                            if (locString.IsNullOrEmpty())
                            {
                                res.Remove(key);
                            }
                        }
                        else
                        {
                            res.Remove(key);
                        }
                    }
                }
                catch (Exception e) when (!PlayniteEnvironment.ThrowAllErrors)
                {
                    logger.Error(e, $"Failed to parse localization file {xamlPath}");
                    return;
                }

                dictionaries.Add(res);
            }

            var localDir = Path.Combine(extensionDir, PlaynitePaths.LocalizationsDirName);
            if (!Directory.Exists(localDir))
            {
                return;
            }

            var enXaml = Path.Combine(localDir, "en_US.xaml");
            if (!File.Exists(enXaml))
            {
                return;
            }

            loadString(enXaml);
            if (CurrentLanguage != "english" && CurrentLanguage != "en_US")
            {
                var langXaml = Path.Combine(localDir, $"{CurrentLanguage}.xaml");
                if (File.Exists(langXaml))
                {
                    loadString(langXaml);
                }
            }
        }
    }
}
