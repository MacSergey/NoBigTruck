using ColossalFramework;
using ColossalFramework.IO;
using ColossalFramework.UI;
using ICities;
using ModsCommon;
using ModsCommon.UI;
using ModsCommon.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UnityEngine;

namespace NoBigTruck
{
    public class Settings : BaseSettings<Mod>
    {
        public static string ConfigFile => Path.Combine(DataLocation.localApplicationData, $"{nameof(NoBigTruck)}.xml");
        private bool IsLoaded { get; set; } = false;
        public static List<Rule> Rules { get; } = new List<Rule>();
        private AddRuleButton AddButton { get; set; }

        private UIAdvancedHelper RulesTab => GetTab(nameof(RulesTab));

        protected override IEnumerable<KeyValuePair<string, string>> AdditionalTabs
        {
            get
            {
                yield return new KeyValuePair<string, string>(nameof(RulesTab), Localize.RulesTab);
            }
        }
        protected override void FillSettings()
        {
            base.FillSettings();

            AddLanguage(GeneralTab);
            AddNotifications(GeneralTab);
            RulesTab.Content.autoLayoutPadding = new RectOffset(50, 50, 0, 25);

            Load();

            AddAddRuleButton();
            AddRulePanels();
        }

        private void AddAddRuleButton()
        {
            AddButton = RulesTab.Content.AddUIComponent<AddRuleButton>();
            AddButton.Text = Localize.AddNewRule;
            AddButton.Init();
            AddButton.OnButtonClick += AddRule;

            void AddRule()
            {
                var rule = new Rule();
                this.AddRule(rule);
                AddRulePanel(rule);
            }
        }
        private void AddRulePanels()
        {
            foreach (var rule in Rules)
                AddRulePanel(rule);
        }
        private void AddRulePanel(Rule rule)
        {
            var rulePanel = RulesTab.Content.AddUIComponent<PropertyGroupPanel>();
            rulePanel.Init();
            AddButton.zOrder = rulePanel.zOrder + 1;

            rulePanel.StopLayout();
            var header = rulePanel.AddUIComponent<RuleHeaderPanel>();
            header.Init(30f);
            header.OnDelete += OnDeleteRule;

            rule.GetUIComponents(rulePanel);
            rulePanel.StartLayout();

            void OnDeleteRule()
            {
                RulesTab.Content.RemoveUIComponent(rulePanel);
                UnityEngine.Object.Destroy(rulePanel);
                DeleteRule(rule);
            }
        }

        private void Load()
        {
            Rules.Clear();

            if (!File.Exists(ConfigFile))
            {
                SingletonMod<Mod>.Logger.Debug($"Config file not exist, create default");
                AddRule(new Rule());
                Save();
            }
            else
            {
                var file = XmlExtension.Load(ConfigFile);

                foreach (var config in file.Root.Elements(nameof(Rule)))
                {
                    var rule = new Rule();
                    rule.FromXml(config);
                    AddRule(rule);
                }
            }

            IsLoaded = true;

            SingletonMod<Mod>.Logger.Debug($"Config loaded: {Rules.Count} rules");
        }

        private void Save()
        {
            var config = new XElement("Config");

            foreach (var rule in Rules)
                config.Add(rule.ToXml());

            new XDocument(config).Save(ConfigFile);
        }
        private void AddRule(Rule rule)
        {
            rule.OnRuleChanged = RuleOnChanged;
            Rules.Add(rule);

            if (IsLoaded)
                Save();
        }
        private void DeleteRule(Rule rule)
        {
            Rules.Remove(rule);
            Save();
        }
        private void RuleOnChanged() => Save();
    }

    public class Rule
    {
        public Action OnRuleChanged { private get; set; }

        public PropertyEnumValue<SourceBuildingTypes> SourceBuildings { get; }
        public PropertyEnumValue<TargetBuildingTypes> TargetBuildings { get; }
        public PropertyBoolValue UseSize { get; }
        public PropertyStructValue<int> MaxLength { get; }
        public PropertyStructValue<int> MaxWidth { get; }

        public Rule(Action onRuleChanged = null)
        {
            OnRuleChanged = onRuleChanged;

            SourceBuildings = new PropertyEnumValue<SourceBuildingTypes>("Source", RuleChanged, SourceBuildingTypes.All);
            TargetBuildings = new PropertyEnumValue<TargetBuildingTypes>("Target", RuleChanged, TargetBuildingTypes.All);
            UseSize = new PropertyBoolValue("UseSize", RuleChanged, false);
            MaxLength = new PropertyStructValue<int>("MaxLength", RuleChanged, 4);
            MaxWidth = new PropertyStructValue<int>("MaxWidth", RuleChanged, 4);
        }

        private void RuleChanged() => OnRuleChanged?.Invoke();

        public virtual List<EditorItem> GetUIComponents(UIComponent parent)
        {
            var components = new List<EditorItem>();

            components.Add(AddSourceBuildingProperty(parent));
            components.Add(AddTargetBuildingProperty(parent));

            var useSize = AddUseSizeProperty(parent);
            var maxLength = AddSizeProperty(parent, MaxLength, Localize.MaxLength);
            var maxWidth = AddSizeProperty(parent, MaxWidth, Localize.MaxWidth);

            components.Add(useSize);
            components.Add(maxLength);
            components.Add(maxWidth);

            useSize.OnSelectObjectChanged += ChangeSizeVisible;
            ChangeSizeVisible(useSize.SelectedObject);

            void ChangeSizeVisible(bool useSize)
            {
                maxLength.isVisible = useSize;
                maxWidth.isVisible = useSize;
            }

            return components;
        }

        private SourceBuildingPropertyPanel AddSourceBuildingProperty(UIComponent parent)
        {
            var sourceBuildingProperty = parent.AddUIComponent<SourceBuildingPropertyPanel>();
            sourceBuildingProperty.Text = Localize.Source;
            sourceBuildingProperty.Init();
            sourceBuildingProperty.SelectedObject = SourceBuildings;
            sourceBuildingProperty.OnSelectObjectChanged += (value) => SourceBuildings.Value = value;

            return sourceBuildingProperty;
        }
        private TargetBuildingPropertyPanel AddTargetBuildingProperty(UIComponent parent)
        {
            var targetBuildingProperty = parent.AddUIComponent<TargetBuildingPropertyPanel>();
            targetBuildingProperty.Text = Localize.Target;
            targetBuildingProperty.Init();
            targetBuildingProperty.SelectedObject = TargetBuildings;
            targetBuildingProperty.OnSelectObjectChanged += (value) => TargetBuildings.Value = value;

            return targetBuildingProperty;
        }
        protected BoolListPropertyPanel AddUseSizeProperty(UIComponent parent)
        {
            var useSizeProperty = parent.AddUIComponent<BoolListPropertyPanel>();
            useSizeProperty.Text = Localize.CheckTargetSize;
            useSizeProperty.Init(CommonLocalize.MessageBox_No, CommonLocalize.MessageBox_Yes);
            useSizeProperty.SelectedObject = UseSize;
            useSizeProperty.OnSelectObjectChanged += (bool value) => UseSize.Value = value;
            return useSizeProperty;
        }
        protected IntPropertyPanel AddSizeProperty(UIComponent parent, PropertyValue<int> property, string text)
        {
            var lineCountProperty = parent.AddUIComponent<IntPropertyPanel>();
            lineCountProperty.Text = text;
            lineCountProperty.UseWheel = true;
            lineCountProperty.WheelStep = 1;
            lineCountProperty.CheckMin = true;
            lineCountProperty.MinValue = 1;
            lineCountProperty.Init();
            lineCountProperty.Value = property;
            lineCountProperty.OnValueChanged += (int value) => property.Value = value;

            return lineCountProperty;
        }

        public void FromXml(XElement config)
        {
            SourceBuildings.FromXml(config, SourceBuildings);
            TargetBuildings.FromXml(config, TargetBuildings);
            UseSize.FromXml(config, UseSize);
            MaxLength.FromXml(config, MaxLength);
            MaxWidth.FromXml(config, MaxWidth);
        }
        public XElement ToXml()
        {
            var config = new XElement("Rule");

            SourceBuildings.ToXml(config);
            TargetBuildings.ToXml(config);
            UseSize.ToXml(config);
            MaxLength.ToXml(config);
            MaxWidth.ToXml(config);

            return config;
        }
    }

    [Flags]
    public enum SourceBuildingTypes
    {
        [NotVisible]
        None = 0,

        [Description(nameof(Localize.SourceIndustry))]
        Industry = 1,

        [Description(nameof(Localize.SourceOutside))]
        Outside = 2,

        [Description(nameof(Localize.SourceWarehouse))]
        Warehouse = 4,

        [NotVisible]
        All = Industry | Outside | Warehouse,
    }

    [Flags]
    public enum TargetBuildingTypes
    {
        [NotVisible]
        None = 0,

        [Description(nameof(Localize.TargetLow))]
        Low = 1,

        [Description(nameof(Localize.TargetHigh))]
        High = 2,

        [Description(nameof(Localize.TargetEco))]
        Eco = 4,

        [Description(nameof(Localize.TargetLeisure))]
        Leisure = 8,

        [Description(nameof(Localize.TargetTourist))]
        Tourist = 16,

        [NotVisible]
        Industry = 32,

        [NotVisible]
        Outside = 64,

        [NotVisible]
        Warehouse = 128,

        [NotVisible]
        Disaster = 256,

        [NotVisible]
        All = Low | High | Eco | Leisure | Tourist,
    }

    public class SourceBuildingPropertyPanel : EnumMultyPropertyPanel<SourceBuildingTypes, SourceBuildingPropertyPanel.SourceBuildingSegmented>
    {
        protected override string GetDescription(SourceBuildingTypes value) => value.Description<SourceBuildingTypes, Mod>();
        protected override bool IsEqual(SourceBuildingTypes first, SourceBuildingTypes second) => first == second;

        public class SourceBuildingSegmented : UIMultySegmented<SourceBuildingTypes> { }
    }
    public class TargetBuildingPropertyPanel : EnumMultyPropertyPanel<TargetBuildingTypes, TargetBuildingPropertyPanel.TargetBuildingSegmented>
    {
        protected override string GetDescription(TargetBuildingTypes value) => value.Description<TargetBuildingTypes, Mod>();
        protected override bool IsEqual(TargetBuildingTypes first, TargetBuildingTypes second) => first == second;

        public class TargetBuildingSegmented : UIMultySegmented<TargetBuildingTypes> { }
    }
    public class AddRuleButton : ButtonPanel
    {
        public AddRuleButton()
        {
            Button.textScale = 1f;
        }
        protected override void SetSize() => Button.size = size;
    }
    public class RuleHeaderPanel : BaseDeletableHeaderPanel<HeaderContent> { }
}
