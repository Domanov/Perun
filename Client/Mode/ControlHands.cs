using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Comfort.Common;
using Diz.Jobs;
using Diz.Resources;
using EFT;
using EFT.Customization;
using EFT.Visual;
using UnityEngine.Rendering;

namespace Perun.Client.Mode;

internal static class ControlHands
{
    public static bool Ensure(Player bot, Player main)
    {
        PlayerBody body = bot.PlayerBody;
        if (body == null || body.SkeletonHands == null)
        {
            return false;
        }

        if (body.BodySkins.TryGetValue(EBodyModelPart.Hands, out LoddedSkin existing) && existing != null)
        {
            return true;
        }

        CustomizationSolver solver = Singleton<CustomizationSolver>.Instance;
        IEasyAssets assets = Singleton<IEasyAssets>.Instance;
        if (solver == null || assets == null)
        {
            return false;
        }

        ResourceKey bundle = LoadedHands(solver, assets, bot.Profile?.Customization) ?? LoadedHands(solver, assets, main?.PlayerBody?.BodyCustomization);
        if (bundle == null)
        {
            return false;
        }

        body.SetSkin(new KeyValuePair<EBodyModelPart, ResourceKey>(EBodyModelPart.Hands, bundle), body.SkeletonHands);
        if (body.BodySkins.TryGetValue(EBodyModelPart.Hands, out LoddedSkin hands) && hands != null)
        {
            hands.SetShadowCastingMode(ShadowCastingMode.Off);
            return true;
        }
        return false;
    }

    private static ResourceKey LoadedHands(CustomizationSolver solver, IEasyAssets assets, BodyCustomization customization)
    {
        ResourceKey bundle = HandsOf(solver, customization);
        return bundle != null && IsLoaded(assets, bundle) ? bundle : null;
    }

    private static ResourceKey HandsOf(CustomizationSolver solver, BodyCustomization customization)
    {
        if (solver == null || customization == null || !customization.TryGetValue(EBodyModelPart.Hands, out MongoID id))
        {
            return null;
        }
        return solver.GetBundle(id);
    }

    private static bool IsLoaded(IEasyAssets assets, ResourceKey bundle)
    {
        try
        {
            return assets.GetAsset<LoddedSkin>(bundle) != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Task Prepare(Player bot)
    {
        if (bot?.PlayerBody == null || bot.PlayerBody.BodySkins.TryGetValue(EBodyModelPart.Hands, out LoddedSkin existing) && existing != null)
        {
            return null;
        }

        CustomizationSolver solver = Singleton<CustomizationSolver>.Instance;
        IEasyAssets assets = Singleton<IEasyAssets>.Instance;
        ResourceKey bundle = HandsOf(solver, bot.Profile?.Customization);
        if (bundle == null || assets == null || IsLoaded(assets, bundle) || !Singleton<ObjectsFactory>.Instantiated)
        {
            return null;
        }

        return Singleton<ObjectsFactory>.Instance.LoadBundlesAndCreatePools(ObjectsFactory.PoolsCategory.Raid, ObjectsFactory.AssemblyType.Local, new[] { bundle }, JobYieldPriority.Immediate, null, ObjectsFactory.DefaultCancellationToken);
    }
}
