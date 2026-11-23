using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using JavaMarket;
using JavaMarket.Java;
using JavaMarket.Scripting;
using JavaMarket.UI;
using JavaMarket.World;
using CodeMarket.ModAPI.Core.Contracts;
using FieldInfo = System.Reflection.FieldInfo;

namespace CodeMarket.ModAPI.Unity.Patches;

internal static class ScriptingPatches
{
    private static readonly HashSet<string> InjectedRobotMethods = new HashSet<string>(StringComparer.Ordinal);
    private static readonly HashSet<string> InjectedStoreMethods = new HashSet<string>(StringComparer.Ordinal);

    public static void Apply(Harmony harmony, IModLogger logger)
    {
        if (harmony is null) throw new ArgumentNullException(nameof(harmony));
        if (logger is null) throw new ArgumentNullException(nameof(logger));

        var awakeTarget = AccessTools.Method(typeof(CodeInputField), "Awake");
        var awakePostfix = AccessTools.Method(typeof(ScriptingPatches), nameof(CodeInputFieldAwakePostfix));
        if (awakeTarget is not null && awakePostfix is not null)
        {
            harmony.Patch(awakeTarget, postfix: new HarmonyMethod(awakePostfix));
        }
        else
        {
            logger.Warning("[ModAPI] Could not locate CodeInputField.Awake for script character limit hook.");
        }

        ModApiRuntime.SetScriptSyncCallback(() => SyncRegisteredMethods(logger));
        SyncRegisteredMethods(logger);
    }

    public static void SyncRegisteredMethods(IModLogger logger)
    {
        SyncTargetMethods<Robot, RobotMethod>(
            ScriptTarget.Robot,
            typeof(RobotCommands),
            typeof(RobotApi),
            InjectedRobotMethods,
            (desc, parameters) => new RobotMethod(
                desc.Name,
                desc.ReturnType,
                Tr.Same(desc.Description),
                RobotMethodKind.Query,
                parameters,
                (robot, args) => InvokeSafely(desc, robot, args)),
            logger);

        SyncTargetMethods<Store, StoreMethod>(
            ScriptTarget.Store,
            typeof(StoreCommands),
            typeof(StoreApi),
            InjectedStoreMethods,
            (desc, parameters) => new StoreMethod(
                desc.Name,
                desc.ReturnType,
                Tr.Same(desc.Description),
                RobotMethodKind.Query,
                parameters,
                (store, args) => InvokeSafely(desc, store, args)),
            logger);
    }

    private static void SyncTargetMethods<TEntity, TMethod>(
        ScriptTarget target,
        Type commandsType,
        Type apiType,
        HashSet<string> injectedNames,
        Func<ScriptMethodDescriptor, RobotParameter[], TMethod> factory,
        IModLogger logger)
        where TMethod : ApiMethod
    {
        IReadOnlyList<ScriptMethodDescriptor> descriptors = ModApiRuntime.Scripting.GetMethods(target);
        if (descriptors.Count == 0) return;

        FieldInfo? methodsField = AccessTools.Field(commandsType, "Methods");
        FieldInfo? byNameField = AccessTools.Field(commandsType, "ByName");
        FieldInfo? apiNamesField = AccessTools.Field(apiType, "Names");
        if (methodsField is null || byNameField is null || apiNamesField is null)
        {
            logger.Warning($"[ModAPI] Scripting registry fields missing on {commandsType.Name}/{apiType.Name}.");
            return;
        }

        var existingMethods = methodsField.GetValue(null) as IEnumerable<TMethod>;
        object? byNameIndex = byNameField.GetValue(null);
        FieldInfo? dictField = byNameIndex is null ? null : AccessTools.Field(byNameIndex.GetType(), "byName");
        var byNameDict = dictField?.GetValue(byNameIndex) as Dictionary<string, TMethod>;
        var existingNames = apiNamesField.GetValue(null) as IEnumerable<string>;

        if (existingMethods is null || byNameDict is null || existingNames is null)
        {
            logger.Warning($"[ModAPI] Could not read live scripting tables on {commandsType.Name}.");
            return;
        }

        var updatedMethods = new List<TMethod>(existingMethods);
        var updatedNames = new HashSet<string>(existingNames, StringComparer.Ordinal);
        int added = 0;

        foreach (ScriptMethodDescriptor desc in descriptors)
        {
            if (injectedNames.Contains(desc.Name) || byNameDict.ContainsKey(desc.Name))
            {
                continue;
            }

            var parameters = new RobotParameter[desc.Parameters.Count];
            for (int i = 0; i < desc.Parameters.Count; i++)
            {
                parameters[i] = new RobotParameter(desc.Parameters[i].Name, desc.Parameters[i].Type);
            }

            TMethod method = factory(desc, parameters);
            updatedMethods.Add(method);
            byNameDict[desc.Name] = method;
            updatedNames.Add(desc.Name);
            injectedNames.Add(desc.Name);
            added++;
        }

        if (added > 0)
        {
            // Replacing static readonly backing fields exposes new methods to both Java (Api) and C# (CsGame.TypeOf).
            methodsField.SetValue(null, updatedMethods.AsReadOnly());
            apiNamesField.SetValue(null, updatedNames);
            logger.Info($"[ModAPI] Registered {added} custom {target} scripting method(s) for Java & C#.");
        }
    }

    private static Value InvokeSafely(ScriptMethodDescriptor desc, object entity, Value[] args)
    {
        try
        {
            object?[] unboxed = new object?[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                unboxed[i] = UnboxValue(args[i]);
            }

            object? result = desc.Handler(entity, unboxed);
            return BoxValue(result);
        }
        catch (Exception ex)
        {
            ModApiRuntime.Lifecycle.Quarantine(
                desc.ModId,
                $"ScriptCall({desc.Target}.{desc.Name})",
                $"{ex.GetType().Name}: {ex.Message}",
                rollback: () => ModApiRuntime.RollbackMod(desc.ModId));
            return Value.Null;
        }
    }

    private static object? UnboxValue(Value val)
    {
        if (val.IsNull) return null;
        if (val.Kind == ValueKind.String) return val.AsString;
        if (val.Kind == ValueKind.Boolean) return val.AsBool;
        if (val.IsIntegral) return val.AsLong;
        if (val.IsFloating) return val.AsDouble;
        return val.Ref;
    }

    private static Value BoxValue(object? obj)
    {
        return obj switch
        {
            null => Value.Null,
            Value v => v,
            int i => Value.Of(i),
            long l => Value.OfLong(l),
            float f => Value.OfFloat(f),
            double d => Value.Of(d),
            bool b => Value.Of(b),
            char c => Value.Of(c),
            string s => Value.Of(s),
            _ => Value.Of(obj.ToString() ?? string.Empty)
        };
    }

    private static void CodeInputFieldAwakePostfix(CodeInputField __instance)
    {
        if (__instance is null) return;

        int? limit = ModApiRuntime.Balance.EffectiveScriptCharLimit;
        if (limit.HasValue)
        {
            __instance.characterLimit = limit.Value;
            if (ModApiRuntime.VerboseHookLogging)
            {
                ModApiRuntime.Logger.Info($"[ModAPI] Applied CodeInputField.characterLimit = {limit.Value}.");
            }
        }
    }
}
