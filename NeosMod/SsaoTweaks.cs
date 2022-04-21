using HarmonyLib;
using NeosModLoader;
using System;
using System.Reflection;
using UnityEngine;
using BaseX;
using AmplifyOcclusion;


// Adapted from https://github.com/zkxs/SsaoDisable

namespace SsaoTweaks
{
    public class SsaoTweaks : NeosMod
    {
        // Mod registration
        public override string Name => "SsaoTweaks";
        public override string Author => "DoubleStyx";
        public override string Version => "1.0.1";
        public override string Link => "https://github.com/DoubleStyx/SsaoTweaks";

        private static ModConfiguration config;

        // Mod config registration
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<AmplifyOcclusionBase.ApplicationMethod> KEY_APPLYMETHOD = 
            new ModConfigurationKey<AmplifyOcclusionBase.ApplicationMethod>("Apply Method", "", () => AmplifyOcclusionBase.ApplicationMethod.PostEffect);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<SampleCountLevel> KEY_SAMPLECOUNT = 
            new ModConfigurationKey<SampleCountLevel>("Sample Count", "", () => SampleCountLevel.Low);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<AmplifyOcclusionBase.PerPixelNormalSource> KEY_PERPIXELNORMALS =
            new ModConfigurationKey<AmplifyOcclusionBase.PerPixelNormalSource>("Per Pixel Normals", "", () => AmplifyOcclusionBase.PerPixelNormalSource.None);

        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> KEY_INTENSITY = new ModConfigurationKey<float>("Intensity", "", () => 1f);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<color> KEY_TINT = new ModConfigurationKey<color>("Tint", "", () => new color(0, 0, 0));
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> KEY_RADIUS = new ModConfigurationKey<float>("Radius", "", () => 4f);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> KEY_POWEREXPONENT = new ModConfigurationKey<float>("Power Exponent", "", () => 0.6f);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> KEY_BIAS = new ModConfigurationKey<float>("Bias", "", () => 0.05f);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> KEY_THICKNESS = new ModConfigurationKey<float>("Thickness", "", () => 1f);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_DOWNSAMPLE = new ModConfigurationKey<bool>("Downsample", "", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_CACHEAWARE = new ModConfigurationKey<bool>("Cache Aware", "", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_BLURENABLED = new ModConfigurationKey<bool>("Blur Enabled", "", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<int> KEY_BLURRADIUS = new ModConfigurationKey<int>("Blur Radius", "", () => 4); 
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<int> KEY_BLURPASSES = new ModConfigurationKey<int>("Blur Passes", "", () => 2);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> KEY_BLURSHARPNESS = new ModConfigurationKey<float>("Blur Sharpness", "", () => 3f);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<bool> KEY_FILTERENABLED = new ModConfigurationKey<bool>("Filter Enabled", "", () => true);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> KEY_FILTERBLENDING = new ModConfigurationKey<float>("Filter Blending", "", () => 0.25f);
        [AutoRegisterConfigKey]
        private static readonly ModConfigurationKey<float> KEY_FILTERRESPONSE = new ModConfigurationKey<float>("Filter Response", "", () => 0.9f);

        // This override lets us change optional settings in our configuration definition
        public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
        {
            builder
                .Version(new Version(1, 0, 0)) // manually set config version (default is 1.0.0)
                .AutoSave(false); // don't autosave on Neos shutdown (default is true)
        }

        // Main hook
        public override void OnEngineInit()
        {
            // Patch method
            Harmony harmony = new Harmony("net.DoubleStyx.SsaoTweaks");
            MethodInfo originalMethod = AccessTools.DeclaredMethod(typeof(CameraInitializer), nameof(CameraInitializer.SetPostProcessing), new Type[] { typeof(Camera), typeof(bool), typeof(bool), typeof(bool) });
            if (originalMethod == null)
            {
                Error("Could not find CameraInitializer.SetPostProcessing(Camera, bool, bool, bool)");
                return;
            }
            MethodInfo replacementMethod = AccessTools.DeclaredMethod(typeof(SsaoTweaks), nameof(SetPostProcessingPostfix));
            harmony.Patch(originalMethod, postfix: new HarmonyMethod(replacementMethod));
            Msg("Hooks installed successfully");

            // Set up config
            config = GetConfiguration();
            ModConfiguration.OnAnyConfigurationChanged += OnConfigurationChanged;
            Msg("Config loaded successfully");
        }

        private void OnConfigurationChanged(ConfigurationChangedEvent @event)
        {
            ModifyExistingSsaos();
            Msg("Updated ssao values due to configuration change");
        }

        private static void SetPostProcessingPostfix(Camera c, bool enabled, bool motionBlur, bool screenspaceReflections)
        {
            ModifyExistingSsaos();
        }

        private static void ModifyExistingSsaos()
        {
            // Find ssaos
            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            int count = 0;
            foreach (Camera camera in cameras)
            {
                try
                {
                    var ssao = camera.GetComponent<AmplifyOcclusionEffect>();
                    if (ssao != null && ssao.enabled)
                    {
                        ApplyPostFixValues(camera);
                        count += 1;
                    }
                }
                catch (Exception e)
                {
                    Warn($"failed to modify a prexisting SSAO: {e}");
                }
            }
            Msg($"modified {count} prexisting SSAOs");
        }

        private static void ApplyPostFixValues(Camera c)
        {
            AmplifyOcclusionEffect ssao = c.GetComponent<AmplifyOcclusionEffect>();

            ssao.Radius = config.GetValue(KEY_RADIUS);
            ssao.BlurSharpness = config.GetValue(KEY_BLURSHARPNESS);
            ssao.FilterBlending = config.GetValue(KEY_FILTERBLENDING);
            ssao.FilterResponse = config.GetValue(KEY_FILTERRESPONSE);
            ssao.Bias = config.GetValue(KEY_BIAS);
            ssao.Thickness = config.GetValue(KEY_THICKNESS);
            ssao.SampleCount = config.GetValue(KEY_SAMPLECOUNT);
            ssao.Downsample = config.GetValue(KEY_DOWNSAMPLE);
            ssao.BlurPasses = config.GetValue(KEY_BLURPASSES);
            ssao.BlurRadius = config.GetValue(KEY_BLURRADIUS);
            ssao.CacheAware = config.GetValue(KEY_CACHEAWARE);
            ssao.PerPixelNormals = config.GetValue(KEY_PERPIXELNORMALS);
            ssao.BlurEnabled = config.GetValue(KEY_BLURENABLED);
            ssao.FilterEnabled = config.GetValue(KEY_FILTERENABLED);
            ssao.ApplyMethod = config.GetValue(KEY_APPLYMETHOD);
            ssao.PowerExponent = config.GetValue(KEY_POWEREXPONENT);
            ssao.Intensity = config.GetValue(KEY_INTENSITY);
            ssao.Tint = new Color(config.GetValue(KEY_TINT).r, config.GetValue(KEY_TINT).g, config.GetValue(KEY_TINT).b);
        }
    }
}
