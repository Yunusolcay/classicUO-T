// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using ClassicUO.Configuration;
using ClassicUO.Game.Lighting;
using Xunit;

namespace ClassicUO.UnitTests.Configuration
{
    public class LightingProfileTests : IDisposable
    {
        private readonly string _directory = Directory.CreateTempSubdirectory("ClassicUO-Lighting-").FullName;

        [Fact]
        public void NewProfile_UsesLightingPlanDefaults()
        {
            AssertLightingDefaults(new Profile());
        }

        [Fact]
        public void LegacyProfile_LoadAndSave_PreservesExistingSettingsAndAddsDefaults()
        {
            string legacyPath = Path.Combine(_directory, "legacy.json");
            File.WriteAllText(legacyPath, """
                {
                  "use_alternative_lights": true,
                  "use_custom_light_level": true,
                  "light_level": 17,
                  "light_level_type": 1,
                  "use_colored_lights": false,
                  "use_dark_nights": true,
                  "sound_volume": 37
                }
                """);

            Profile profile = ConfigurationResolver.Load(legacyPath, ProfileJsonContext.DefaultToUse.Profile);
            AssertLightingDefaults(profile);
            AssertLegacySettings(profile);

            string savedPath = Path.Combine(_directory, "saved", "profile.json");
            ConfigurationResolver.Save(profile, savedPath, ProfileJsonContext.DefaultToUse.Profile);

            using (JsonDocument json = JsonDocument.Parse(File.ReadAllText(savedPath)))
            {
                JsonElement root = json.RootElement;
                Assert.True(root.GetProperty("enhanced_lighting").GetBoolean());
                Assert.Equal(1, root.GetProperty("lighting_quality").GetInt32());
                Assert.True(root.GetProperty("light_flicker").GetBoolean());
                Assert.True(root.GetProperty("day_night_tint").GetBoolean());
                Assert.Equal(2.0f, root.GetProperty("light_transition_seconds").GetSingle());
                Assert.Equal(1.0f, root.GetProperty("light_intensity").GetSingle());
            }

            Profile reloaded = ConfigurationResolver.Load(savedPath, ProfileJsonContext.DefaultToUse.Profile);
            AssertLightingDefaults(reloaded);
            AssertLegacySettings(reloaded);
        }

        [Theory]
        [InlineData(0, 0.0f, 0.5f)]
        [InlineData(1, 2.75f, 1.25f)]
        [InlineData(2, 4.5f, 2.0f)]
        public void LightingSettings_SaveAndLoad_PreservesExplicitValues(
            int quality, float transitionSeconds, float intensity)
        {
            var profile = new Profile
            {
                EnhancedLighting = false,
                LightingQuality = (LightingQuality)quality,
                LightFlicker = false,
                DayNightTint = false,
                LightTransitionSeconds = transitionSeconds,
                LightIntensity = intensity
            };
            string path = Path.Combine(_directory, "profile.json");

            ConfigurationResolver.Save(profile, path, ProfileJsonContext.DefaultToUse.Profile);
            Profile reloaded = ConfigurationResolver.Load(path, ProfileJsonContext.DefaultToUse.Profile);

            Assert.NotNull(reloaded);
            Assert.False(reloaded.EnhancedLighting);
            Assert.Equal((LightingQuality)quality, reloaded.LightingQuality);
            Assert.False(reloaded.LightFlicker);
            Assert.False(reloaded.DayNightTint);
            Assert.Equal(transitionSeconds, reloaded.LightTransitionSeconds);
            Assert.Equal(intensity, reloaded.LightIntensity);
        }

        [Theory]
        [InlineData("en-US")]
        [InlineData("tr-TR")]
        public void LightIntensity_UsesStableJsonKeyAcrossCultures(string cultureName)
        {
            CultureInfo originalCulture = CultureInfo.CurrentCulture;

            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
                var context = new ProfileJsonContext(new JsonSerializerOptions(ProfileJsonContext.DefaultToUse.Options));
                Profile profile = JsonSerializer.Deserialize("""{"light_intensity": 1.75}""", context.Profile);

                Assert.Equal(1.75f, profile.LightIntensity);

                string json = JsonSerializer.Serialize(profile, context.Profile);
                using JsonDocument document = JsonDocument.Parse(json);
                Assert.Equal(1.75f, document.RootElement.GetProperty("light_intensity").GetSingle());

                Profile reloaded = JsonSerializer.Deserialize(json, context.Profile);
                Assert.Equal(1.75f, reloaded.LightIntensity);
            }
            finally
            {
                CultureInfo.CurrentCulture = originalCulture;
            }
        }

        private static void AssertLightingDefaults(Profile profile)
        {
            Assert.NotNull(profile);
            Assert.True(profile.EnhancedLighting);
            Assert.Equal(LightingQuality.Medium, profile.LightingQuality);
            Assert.True(profile.LightFlicker);
            Assert.True(profile.DayNightTint);
            Assert.Equal(2.0f, profile.LightTransitionSeconds);
            Assert.Equal(1.0f, profile.LightIntensity);
        }

        private static void AssertLegacySettings(Profile profile)
        {
            Assert.True(profile.UseAlternativeLights);
            Assert.True(profile.UseCustomLightLevel);
            Assert.Equal((byte)17, profile.LightLevel);
            Assert.Equal(1, profile.LightLevelType);
            Assert.False(profile.UseColoredLights);
            Assert.True(profile.UseDarkNights);
            Assert.Equal(37, profile.SoundVolume);
        }

        public void Dispose()
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
