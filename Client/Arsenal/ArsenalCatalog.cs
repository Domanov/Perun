using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;

namespace Perun.Client.Arsenal;

internal sealed class ArsenalEntry
{
    public string TemplateId;
    public ItemTemplate Template;
    public string Name;
    public string Search;
    public int Category;
}

internal static class ArsenalCatalog
{
    public static readonly string[] CategoryNames =
    {
        "Weapons", "Melee", "Grenades", "Magazines", "Ammo", "Armor", "Headgear", "Rigs", "Backpacks", "Medical", "Food", "Keys", "Mods", "Other"
    };

    private static ItemFactory _builtFor;
    private static readonly List<ArsenalEntry> All = new();

    public static IReadOnlyList<ArsenalEntry> Entries
    {
        get
        {
            Ensure();
            return All;
        }
    }

    private static void Ensure()
    {
        ItemFactory factory = Singleton<ItemFactory>.Instance;
        if (factory == null || ReferenceEquals(factory, _builtFor))
        {
            return;
        }

        _builtFor = factory;
        All.Clear();
        foreach (var pair in factory.ItemTemplates)
        {
            ItemTemplate template = pair.Value;
            if (template == null || template._type != NodeType.Item || template.QuestItem || string.IsNullOrEmpty(template.Prefab.path))
            {
                continue;
            }

            int category = CategoryOf(template);
            if (category < 0)
            {
                continue;
            }

            string id = template._id.ToString();
            string name = template.NameLocalizationKey.Localized();
            if (string.IsNullOrEmpty(name) || name == template.NameLocalizationKey)
            {
                continue;
            }

            All.Add(new ArsenalEntry
            {
                TemplateId = id,
                Template = template,
                Name = name,
                Search = name + " " + template.ShortNameLocalizationKey.Localized() + " " + id,
                Category = category
            });
        }

        All.Sort((a, b) => a.Category != b.Category ? a.Category.CompareTo(b.Category) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        Plugin.Log.LogInfo($"Arsenal: catalogue has {All.Count} items");
    }

    private static int CategoryOf(ItemTemplate template)
    {
        switch (template)
        {
            case StashTemplate or LootContainerTemplate or MobContainerTemplate or InventoryEquipmentTemplate or HideoutAreaContainerTemplate or SortingTableTemplate or EmptyHandsTemplate or DialogItemTemplate:
                return -1;
            case KnifeTemplate:
                return 1;
            case ThrowWeapTemplate:
                return 2;
            case WeaponTemplate:
                return 0;
            case MagazineTemplate:
                return 3;
            case AmmoTemplate or AmmoBoxTemplate:
                return 4;
            case ArmorTemplate or ArmorPlateTemplate:
                return 5;
            case HeadwearTemplate or FaceCoverTemplate or VisorsTemplate or NightVisionTemplate:
                return 6;
            case VestTemplate:
                return 7;
            case BackpackTemplate:
                return 8;
            case MedsTemplate or MedicalTemplate or MedKitTemplate or DrugsTemplate or MedicalSuppliesTemplate:
                return 9;
            case FoodDrinkTemplate:
                return 10;
            case KeyTemplate or KeycardTemplate:
                return 11;
            case ModTemplate:
                return 12;
            default:
                return 13;
        }
    }

    public static bool Fits(ItemFilter[] filters, ItemTemplate template)
    {
        if (filters == null || filters.Length == 0)
        {
            return true;
        }

        foreach (ItemFilter filter in filters)
        {
            if (filter.Filter != null && filter.Filter.Length > 0 && !Matches(filter.Filter, template))
            {
                return false;
            }
            if (filter.ExcludedFilter != null && filter.ExcludedFilter.Length > 0 && Matches(filter.ExcludedFilter, template))
            {
                return false;
            }
        }
        return true;
    }

    private static bool Matches(MongoID[] ids, ItemTemplate template)
    {
        for (ItemTemplate node = template; node != null; node = node.Parent)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                if (node._id == ids[i])
                {
                    return true;
                }
            }
        }
        return false;
    }
}
