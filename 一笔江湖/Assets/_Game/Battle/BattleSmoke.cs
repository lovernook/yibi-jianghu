using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Yibi.Rules;

namespace Yibi.Battle
{
    public sealed class BattleSmoke:MonoBehaviour
    {
        private IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"--m2-smoke")<0)yield break;
            Application.runInBackground=true;var p=GetComponent<BattlePresenter>();
            yield return new WaitForSecondsRealtime(2);p.Restart();float until=Time.realtimeSinceStartup+85;int actions=0;
            while(p.State.winner==-2&&Time.realtimeSinceStartup<until){
                if(p.State.activeSeat==0&&p.basicButton.interactable){
                    var skill=BattleReducer.CanUse(p.State,0,"lieshi")==null?"lieshi":BattleReducer.CanUse(p.State,0,"dianxue")==null?"dianxue":"basic";
                    p.Resolve(skill,skill=="basic"?null:BattleContent.Perfect(skill));actions++;
                }
                yield return null;
            }
            Debug.Log("YIBI_M2_SMOKE winner="+p.State.winner+" revision="+p.State.revision+" playerActions="+actions);
            int at=Array.IndexOf(args,"--evidence-dir");if(at>=0&&at+1<args.Length){Directory.CreateDirectory(args[at+1]);ScreenCapture.CaptureScreenshot(Path.Combine(args[at+1],"M2-result.png"));}
            yield return new WaitForSecondsRealtime(1);Application.Quit(p.State.winner==-2?1:0);
        }
    }
}
