﻿using System;
using System.Collections.Generic;
using Monocle;
using MonoMod.ModInterop;
using TAS.Module;

namespace TAS.ModInterop;
internal static class TasHelperInterop {

    private static bool loaded;

    public static HashSet<Entity> GetUnimportantTriggers() {
        return loaded ? TasHelperImport.GetUnimportantTriggers!() : [];
    }

    public static bool InPrediction => loaded && TasHelperImport.InPrediciton!();

    [Initialize]
    private static void Initialize() {
        typeof(TasHelperImport).ModInterop();
        loaded = TasHelperImport.GetUnimportantTriggers is not null;
    }

    [ModImportName("TASHelper")]
    private static class TasHelperImport {

        public static Func<HashSet<Entity>>? GetUnimportantTriggers = null;

        public static Func<bool>? InPrediciton = null;
    }
}

