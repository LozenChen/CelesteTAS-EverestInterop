using Celeste;
using Monocle;
using MonoMod.Cil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using TAS.ModInterop;
using TAS.Module;
using TAS.Tools;
using TAS.Utils;

namespace TAS.Gameplay.Optimization;

/// Applies optimization to the gameplay by disabling visual effects which aren't seen anyway, while fast forwarding at high speeds
internal static class FastForwardOptimization {

    private static bool Active => Manager.FastForwarding || SyncChecker.Active;
    private static bool IgnoreGarbageCollect => Active && TasSettings.IgnoreGcCollect;

    private static Type? sjCreditsType;

    [Initialize]
    private static void Initialize() {
        // Particles
        SkipMethods(
            typeof(ParticleSystem).GetMethodInfo(nameof(ParticleSystem.Update))!,
            typeof(ParticleSystem).GetMethodInfo(nameof(ParticleSystem.Clear))!,
            typeof(ParticleSystem).GetMethodInfo(nameof(ParticleSystem.ClearRect))!,
            typeof(ParticleEmitter).GetMethodInfo(nameof(ParticleEmitter.Update))!,
            typeof(ParticleEmitter).GetMethodInfo(nameof(ParticleEmitter.Emit))!
        );
        SkipMethods(
            typeof(ParticleSystem).GetAllMethodInfos().Where(m => m.Name == nameof(ParticleSystem.Emit))
        );

        // Renderers
        SkipMethod(typeof(BackdropRenderer).GetMethodInfo(nameof(BackdropRenderer.Update))!);
        SkipMethod(typeof(SeekerBarrierRenderer).GetMethodInfo(nameof(SeekerBarrierRenderer.Update)));

        // Sound
        On.Celeste.SoundEmitter.Update += On_SoundEmitter_Update;

        // Visual Entities
        SkipMethods(
            typeof(ReflectionTentacles).GetMethodInfo(nameof(ReflectionTentacles.Update)),
            typeof(Decal).GetMethodInfo(nameof(Decal.Update)),
            typeof(FloatingDebris).GetMethodInfo(nameof(FloatingDebris.Update)),
            typeof(AnimatedTiles).GetMethodInfo(nameof(AnimatedTiles.Update)),
            typeof(Water.Surface).GetMethodInfo(nameof(Water.Surface.Update)),
            typeof(DustGraphic).GetMethodInfo(nameof(DustGraphic.Update)),
            typeof(LavaRect).GetMethodInfo(nameof(LavaRect.Update)),
            typeof(CliffsideWindFlag).GetMethodInfo(nameof(CliffsideWindFlag.Update)),
            typeof(CrystalStaticSpinner).GetMethodInfo(nameof(CrystalStaticSpinner.UpdateHue)),
            typeof(HiresSnow).GetMethodInfo(nameof(HiresSnow.Update)),
            typeof(Snow3D).GetMethodInfo(nameof(Snow3D.Update)),
            typeof(AutoSplitterInfo).GetMethodInfo(nameof(AutoSplitterInfo.Update)),
            typeof(SeekerBarrier).GetMethodInfo(nameof(SeekerBarrier.Update)),

            ModUtils.GetMethod("IsaGrabBag", "Celeste.Mod.IsaGrabBag.DreamSpinnerBorder", nameof(Entity.Update))
        );

        // Garbage Collection
        IL.Monocle.Engine.OnSceneTransition += SkipGC;
        IL.Celeste.Level.Reload += SkipGC;
        typeof(Level).GetMethodInfo(nameof(Level._GCCollect))
            ?.SkipMethod(static () => IgnoreGarbageCollect);

        // Special
        ModUtils.GetMethod("StrawberryJam2021", "Celeste.Mod.StrawberryJam2021.Cutscenes.CS_Credits", "Level_OnLoadEntity")
            ?.IlHook((cursor, _) => {
                // Reduce LINQ usage of 'CS_Credits credits = level.Entities.ToAdd.OfType<CS_Credits>().FirstOrDefault();'
                if (!cursor.TryGotoNext(
                    instr => instr.MatchCallvirt<Scene>($"get_{nameof(Scene.Entities)}"),
                    instr => instr.MatchCallvirt<EntityList>($"get_{nameof(EntityList.ToAdd)}"),
                    instr => instr.MatchCall(typeof(Enumerable), nameof(Enumerable.OfType)),
                    instr => instr.MatchCall(typeof(Enumerable), nameof(Enumerable.FirstOrDefault))
                )) {
                    return;
                }

                sjCreditsType = ModUtils.GetType("StrawberryJam2021", "Celeste.Mod.StrawberryJam2021.Cutscenes.CS_Credits")!;

                // Nothing else should be hooking the SJ credits. This is just for performance
#pragma warning disable CL0005
                cursor.RemoveRange(4);
                cursor.EmitStaticDelegate(static Entity? (Level level) => {
                    foreach (var entity in level.Entities.ToAdd) {
                        if (entity.GetType() == sjCreditsType) {
                            return entity;
                        }
                    }

                    return null;
                });
#pragma warning restore CL0005
            });
    }
    [Unload]
    private static void Unload() {
        // On.Celeste.BackdropRenderer.Render -= On_BackdropRenderer_Render;
        On.Celeste.SoundEmitter.Update -= On_SoundEmitter_Update;

        IL.Monocle.Engine.OnSceneTransition -= SkipGC;
        IL.Celeste.Level.Reload -= SkipGC;
    }

    /// Skips calling the original method while fast forwarding
    public static void SkipMethod(MethodInfo? method) {
        if (method == null) {
            return;
        }

#if DEBUG
        Debug.Assert(method.ReturnType == typeof(void));
#endif
        method.IlHook((cursor, _) => {
            var start = cursor.MarkLabel();
            cursor.MoveBeforeLabels();

            cursor.EmitCall(typeof(FastForwardOptimization).GetGetMethod(nameof(Active))!);
            cursor.EmitBrfalse(start);
            cursor.EmitRet();
        });
    }
    /// Skips calling the original methods while fast forwarding
    public static void SkipMethods(params ReadOnlySpan<MethodInfo?> methods) {
        foreach (var method in methods) {
            SkipMethod(method);
        }
    }
    /// Skips calling the original methods while fast forwarding
    public static void SkipMethods(params IEnumerable<MethodInfo?> methods) {
        foreach (var method in methods) {
            SkipMethod(method);
        }
    }

    private static void SkipGC(ILContext il) {
        var cursor = new ILCursor(il);
        if (!cursor.TryGotoNext(
                instr => instr.MatchCall(typeof(GC), nameof(GC.Collect)),
                instr => instr.MatchCall(typeof(GC), nameof(GC.WaitForPendingFinalizers))
            )) {
            return;
        }

        var afterGC = cursor.DefineLabel();
        cursor.EmitCall(typeof(FastForwardOptimization).GetGetMethod(nameof(IgnoreGarbageCollect))!);
        cursor.EmitBrtrue(afterGC);

        cursor.Index += 2; // Go past both calls
        cursor.MarkLabel(afterGC);
    }

    private static void On_SoundEmitter_Update(On.Celeste.SoundEmitter.orig_Update orig, SoundEmitter self) {
        // Disable sound sources while fast-forwarding
        if (Manager.FastForwarding) {
            self.RemoveSelf();
        } else {
            orig(self);
        }
    }

}
