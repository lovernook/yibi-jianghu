using System;
using UnityEditor;
using UnityEngine;
using Yibi.Battle;

namespace Yibi.Editor
{
    // Editor-only visual inspection scope. Never shipped; no writes to the player's persistent profile.
    [InitializeOnLoad]
    public static class StageCVisualReview
    {
        static IDisposable profile;
        static StageCVisualReview(){EditorApplication.playModeStateChanged+=Changed;AssemblyReloadEvents.beforeAssemblyReload+=End;}
        static void Changed(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredEditMode)End();}
        public static BattlePresenter Begin()
        {
            if(!EditorApplication.isPlaying)throw new InvalidOperationException("Visual review requires Play mode.");
            if(profile==null)profile=ProfileStore.UseTransientProfile(new PlayerProfile());
            var p=UnityEngine.Object.FindObjectOfType<BattlePresenter>();
            if(p!=null){p.Restart();p.enabled=false;} // Hold the view for inspection; this is not a timing/performance test.
            return p;
        }
        public static void End(){profile?.Dispose();profile=null;}
    }
}
