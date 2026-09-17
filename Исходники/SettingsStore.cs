using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace RkdEstimator
{
    public static class SettingsStore
    {
        private static string SettingsDirectory
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RKD Estimator"); }
        }

        private static string SettingsPath
        {
            get { return Path.Combine(SettingsDirectory, "settings.dat"); }
        }

        public static AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return AppSettings.CreateDefault();
                using (FileStream stream = File.OpenRead(SettingsPath))
                {
                    AppSettings settings = new BinaryFormatter().Deserialize(stream) as AppSettings;
                    return MergeWithDefaults(settings);
                }
            }
            catch
            {
                return AppSettings.CreateDefault();
            }
        }

        public static void Save(AppSettings settings)
        {
            Directory.CreateDirectory(SettingsDirectory);
            using (FileStream stream = File.Create(SettingsPath))
            {
                new BinaryFormatter().Serialize(stream, settings);
            }
        }

        public static void Reset()
        {
            if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
        }

        private static AppSettings MergeWithDefaults(AppSettings saved)
        {
            AppSettings defaults = AppSettings.CreateDefault();
            if (saved == null) return defaults;
            if (saved.Options == null) saved.Options = new List<ComplexityOption>();

            Dictionary<string, ComplexityOption> existing = saved.Options
                .Where(o => o != null && !string.IsNullOrEmpty(o.Id))
                .GroupBy(o => o.Id)
                .ToDictionary(g => g.Key, g => g.First());
            List<ComplexityOption> merged = new List<ComplexityOption>();
            foreach (ComplexityOption item in defaults.Options)
            {
                ComplexityOption old;
                decimal percent = existing.TryGetValue(item.Id, out old) && item.Id != "site_measure" ? old.Percent : item.Percent;
                merged.Add(new ComplexityOption(item.Id, item.Group, item.Name, percent));
            }
            saved.Options = merged;
            if (saved.RoundTo <= 0m) saved.RoundTo = defaults.RoundTo;
            if (saved.MeasurementFee <= 0m) saved.MeasurementFee = defaults.MeasurementFee;
            return saved;
        }
    }
}
