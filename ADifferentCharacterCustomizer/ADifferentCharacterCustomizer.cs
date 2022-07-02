using BepInEx;
using BepInEx.Configuration;
using R2API;
using R2API.Utils;
using RoR2;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace ADifferentCharacterCustomizer
{
    [BepInDependency(R2API.R2API.PluginGUID)]
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class ADifferentCharacterCustomizer : BaseUnityPlugin
    {
        public const string PluginGUID = PluginAuthor + "." + PluginName;
        public const string PluginAuthor = "GiGaGon";
        public const string PluginName = "ADifferentCharacterCustomizer";
        public const string PluginVersion = "1.1.0";

        readonly List<string> svModifyableValues = new() { "baseMaxHealth", "levelMaxHealth", "baseRegen", "levelRegen", "baseDamage", "levelDamage", "baseArmor", "levelArmor", "baseMoveSpeed", "levelMoveSpeed" };

        readonly List<List<string>> skillIntModifyableValues = new() { 
            new List<string> () { "baseMaxStock", "Maximum number of charges this skill can carry"},
            new List<string> () { "rechargeStock", "How much stock to restore on a recharge." },
            new List<string> () { "requiredStock", "How much stock is required to activate this skill." },
            new List<string> () { "stockToConsume", "How much stock to deduct when the skill is activated." }
        };
        readonly List<List<string>> skillBoolModifyableValues = new()
        {
            new List<string> () { "resetCooldownTimerOnUse", "Whether or not it resets any progress on cooldowns." },
            new List<string> () {"fullRestockOnAssign", "Whether or not to fully restock this skill when it's assigned."},
            new List<string> () { "dontAllowPastMaxStocks", "Whether or not this skill can hold past it's maximum stock." },
            new List<string> () { "beginSkillCooldownOnSkillEnd", "Whether or not the cooldown waits until it leaves the set state" },
            new List<string> () { "cancelSprintingOnActivation", "Whether or not activating the skill forces off sprinting." },
            new List<string> () { "forceSprintDuringState", "Whether or not this skill is considered 'mobility'. Currently just forces sprint." },
            new List<string> () { "canceledFromSprinting", "Whether or not sprinting sets the skill's state to be reset." },
            new List<string> () { "mustKeyPress", "The skill can't be activated if the key is held." }
        };

        internal class ModConfig
        {
            public static ConfigEntry<KeyboardShortcut> reloadKeyBind;
            public static ConfigEntry<bool> generateConfigs;

            public static void InitConfig(ConfigFile config)
            {
                reloadKeyBind = config.Bind("_General", "Reload Keybind", new KeyboardShortcut(KeyCode.F8), "Keybind to press to reload the mod's configs.");
                generateConfigs = config.Bind("_General", "Generate Configs", true, "If disabled, new configs will not be generated. Existing configs will still function normally. Can be used to speed up load times durring testing/playing.");
            }
        }
        public void Awake()
        {
            Debug.Log("ADifferentCharacterCustomizer - Loading standard config");
            ModConfig.InitConfig(Config);
            On.RoR2.RoR2Application.OnLoad += AfterLoad;
        }

        private void Update()
        {
            if (ModConfig.reloadKeyBind.Value.IsDown())
            {
                Debug.Log("ADifferentCharacterCustomizer - Reloading Config");
                ModConfig.InitConfig(Config);
                MakeChanges();
                Debug.Log("ADifferentCharacterCustomizer - Reloading Finished");
            }
        }


        private IEnumerator AfterLoad(On.RoR2.RoR2Application.orig_OnLoad orig, RoR2Application self)
        {
            yield return orig(self);
            MakeChanges();
            Debug.Log("ADifferentCharacterCustomizer - Loading Finished");
        }


        private void MakeChanges()
        {
            if (ModConfig.generateConfigs.Value) GenerateCharacterConfigs();
            ChangeCharacterStats();
            if (ModConfig.generateConfigs.Value) GenerateSkillConfigs();
            ChangeSkillStats();
        }


        private void GenerateCharacterConfigs()
        {
            Debug.Log("ADifferentCharacterCustomizer - Generating character configs");
            foreach (SurvivorDef sv in SurvivorCatalog.allSurvivorDefs)
            {
                CharacterBody svCB = sv.bodyPrefab.GetComponent<CharacterBody>();
                string svNameToken = svCB.name;

                if (svNameToken == null || svNameToken == "") continue;

                
                ConfigEntry<bool> isEnabled = Config.Bind(svNameToken, svNameToken + "_Enable", false, "If true, " + svNameToken + "'s values will be changed. A reload is needed if the related config settings have not been generated yet.");
                if (isEnabled.Value == true)
                {
                    foreach (string val in svModifyableValues)
                    {
                        float vDefault = svCB.GetFieldValue<float>(val);
                        Config.Bind(svNameToken, val, float.NaN, "Value of " + val + " for survivor " + svNameToken + " of type float wtih default value " + vDefault.ToString());
                    }
                }
            }
            Debug.Log("ADifferentCharacterCustomizer - Character config generation finished");
        }


        private void ChangeCharacterStats()
        {
            Debug.Log("ADifferentCharacterCustomizer - Changing character stats");
            foreach (SurvivorDef sv in SurvivorCatalog.allSurvivorDefs)
            {
                CharacterBody svCB = sv.bodyPrefab.GetComponent<CharacterBody>();
                string svNameToken = svCB.name;

                Config.TryGetEntry(svNameToken, svNameToken + "_Enable", out ConfigEntry<bool> entry);
                if (entry.Value)
                {
                    foreach (string val in svModifyableValues)
                    {
                        float vDefault = svCB.GetFieldValue<float>(val);
                        Config.TryGetEntry(svNameToken, val, out ConfigEntry<float> cfg);
                        if (cfg != null && cfg.Value.ToString() != "NaN" && !cfg.Value.Equals(float.NaN))
                        {
                            Debug.Log("Overriding " + svNameToken + " " + val + " from " + vDefault.ToString() + " to " + cfg.Value.ToString());
                            svCB.SetFieldValue<float>(val, cfg.Value);
                        }
                    }
                }
            }
            Debug.Log("ADifferentCharacterCustomizer - Character stats changed");
        }


        private void GenerateSkillConfigs()
        {
            Debug.Log("ADifferentCharacterCustomizer - Generating skill configs");
            foreach (var skill in RoR2.Skills.SkillCatalog.allSkillDefs)
            {
                string skillNameToken = skill.skillName;

                if (skillNameToken == null || skillNameToken == "") continue;

                ConfigEntry<bool> isEnabled = Config.Bind(skillNameToken, skillNameToken + "_Enable", false, "If true, " + skillNameToken + "'s values will be changed. A reload is needed if the related config settings have not been generated yet.");
                if (isEnabled.Value == true)
                {
                    float rechargeDefault = skill.GetFieldValue<float>("baseRechargeInterval");
                    Config.Bind<float>(skillNameToken, "baseRechargeInterval", float.NaN, "Value of baseRechargeInterval for skill " + skillNameToken + " of type float with default value " + rechargeDefault.ToString());


                    foreach (List<string> info in skillIntModifyableValues)
                    {
                        int skIntDefault = skill.GetFieldValue<int>(info[0]);
                        Config.Bind<int>(skillNameToken, info[0], int.MinValue, info[1] + " Default value: " + skIntDefault.ToString());
                    }


                    foreach (List<string> info in skillBoolModifyableValues)
                    {
                        bool skBoolDefault = skill.GetFieldValue<bool>(info[0]);
                        Config.Bind<string>(skillNameToken, info[0], "N/A", info[1] + " Default value: " + skBoolDefault.ToString());
                    }
                }
            }
            Debug.Log("ADifferentCharacterCustomizer - Skill config generation finished");
        }


        private void ChangeSkillStats()
        {
            Debug.Log("ADifferentCharacterCustomizer - Changing skill stats");
            foreach (var skill in RoR2.Skills.SkillCatalog.allSkillDefs)
            {
                string skillNameToken = skill.skillName;

                if (skillNameToken == null || skillNameToken == "") continue;

                Config.TryGetEntry(skillNameToken, skillNameToken + "_Enable", out ConfigEntry<bool> entry);
                if (entry.Value)
                {
                    float rechargeDefault = skill.GetFieldValue<float>("baseRechargeInterval");
                    Config.TryGetEntry(skillNameToken, "baseRechargeInterval", out ConfigEntry<float> cfgRecharge);
                    if (cfgRecharge != null && cfgRecharge.Value.ToString() != "NaN" && !cfgRecharge.Value.Equals(float.NaN))
                    {
                        Debug.Log("ADifferentCharacterCustomizer - Overriding " + skillNameToken + " " + "baseRechargeInterval" + " from " + rechargeDefault.ToString() + " to " + cfgRecharge.Value.ToString());
                        skill.SetFieldValue("baseRechargeInterval", cfgRecharge.Value);
                    }


                    foreach (List<string> info in skillIntModifyableValues)
                    {
                        int skIntDefault = skill.GetFieldValue<int>(info[0]);
                        Config.TryGetEntry(skillNameToken, info[0], out ConfigEntry<int> cfg);
                        if (cfg != null && cfg.Value.ToString() != "-2147483648")
                        {
                            Debug.Log("ADifferentCharacterCustomizer - Overriding " + skillNameToken + " " + info[0] + " from " + skIntDefault.ToString() + " to " + cfg.Value.ToString());
                            skill.SetFieldValue<int>(info[0], cfg.Value);
                        }
                    }


                    foreach (List<string> info in skillBoolModifyableValues)
                    {
                        bool skBoolDefault = skill.GetFieldValue<bool>(info[0]);
                        Config.TryGetEntry(skillNameToken, info[0], out ConfigEntry<string> cfg);
                        if (cfg != null && cfg.Value != "N/A")
                        {
                            Debug.Log("ADifferentCharacterCustomizer - Overriding " + skillNameToken + " " + info[0] + " from " + skBoolDefault.ToString() + " to " + cfg.Value.ToString());
                            skill.SetFieldValue<bool>(info[0], cfg.Value == "True");
                        }
                    }
                }
            }
            Debug.Log("ADifferentCharacterCustomizer - Skill stats changed");
        }
    }
}
