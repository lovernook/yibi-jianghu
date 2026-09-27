using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using Yibi.Battle;
using Yibi.UI;
using Yibi.Rules;

namespace Yibi.Diagnostics
{
    // Explicit command-line harness only; absent from normal gameplay and never writes player progress.
    [UnityEngine.Scripting.Preserve]
    public sealed class PerformanceRun:MonoBehaviour
    {
        [Serializable] public sealed class Sample {public string scene;public int run,frames;public double seconds,frameP50,frameP95,frameP99,gcBytesPerFrame;public long gcMax,totalUsed,monoUsed,textureBytes,meshBytes;public bool gcCounter;}
        [Serializable] public sealed class Visit {public int cycle,objects,sessions;public long used,managed;}
        [Serializable] public sealed class Report {public string utc,cpu,gpu,unity;public int memoryMB,width,height;public Sample[] samples;public Visit[] visits;public string note="Development player; 60 FPS cap; synthetic input; frame duration includes pacing. Memory snapshots are post-warmup. Not a zero-GC or whole-game performance claim.";}
        static string output;
        [UnityEngine.Scripting.Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Boot(){var args=Environment.GetCommandLineArgs();int i=Array.IndexOf(args,"--perf-report");if(i<0||i+1>=args.Length)return;output=args[i+1];Debug.Log("YIBI_PERF_BOOT");Application.runInBackground=true;var g=new GameObject("PerformanceEvidence");DontDestroyOnLoad(g);g.AddComponent<PerformanceRun>();}
        IEnumerator Start()
        {
            Debug.Log("YIBI_PERF_START");
            Application.runInBackground=true;QualitySettings.vSyncCount=0;Application.targetFrameRate=60;
            using(var profile=ProfileStore.UseTransientProfile(new PlayerProfile())){
                var report=new Report{utc=DateTime.UtcNow.ToString("O"),cpu=SystemInfo.processorType,gpu=SystemInfo.graphicsDeviceName,unity=Application.unityVersion,memoryMB=SystemInfo.systemMemorySize,width=Screen.width,height=Screen.height};var samples=new List<Sample>();var visits=new List<Visit>();
                foreach(var scene in new[]{"Valley","GestureLab","Arena_Stone"})for(int run=1;run<=3;run++){
                    Debug.Log("YIBI_PERF_LOAD "+scene+" "+run);yield return SceneManager.LoadSceneAsync(scene);Debug.Log("YIBI_PERF_LOADED");yield return Resources.UnloadUnusedAssets();GC.Collect();yield return new WaitForSecondsRealtime(5);
                    var lab=FindObjectOfType<GestureLabPresenter>();var battle=FindObjectOfType<BattlePresenter>();var board=lab==null?null:lab.board;var nodes=board==null?null:board.template.ToRules().Nodes;
                    var frames=new double[6000];int count=0,strokeStep=-1;double start=Time.realtimeSinceStartupAsDouble,lastStroke=start,nextAction=start;long gcTotal=0,gcMax=0;
                    using(var gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1)){
                        while(Time.realtimeSinceStartupAsDouble-start<60){
                            double now=Time.realtimeSinceStartupAsDouble;
                            if(board!=null){double elapsed=now-lastStroke;if(strokeStep<0){board.Begin(nodes[0],now);strokeStep=0;}
                                double along=Math.Min(1,elapsed/4)*(nodes.Length-1);int segment=Math.Min(nodes.Length-2,(int)along);double t=along-segment;var point=new Point2(nodes[segment].X+(nodes[segment+1].X-nodes[segment].X)*t,nodes[segment].Y+(nodes[segment+1].Y-nodes[segment].Y)*t);
                                if(elapsed>=4){board.Finish(nodes[nodes.Length-1],now);lastStroke=now;strokeStep=-1;}else board.Move(point,now);
                            }
                            if(battle!=null&&now>=nextAction){nextAction=now+.4;if(battle.State.winner!=-2)battle.Restart();else if(battle.basicButton.interactable)battle.Basic();}
                            yield return null;
                            if(count<frames.Length)frames[count++]=Time.unscaledDeltaTime*1000;
                            if(gc.Valid){long value=gc.LastValue;gcTotal+=value;gcMax=Math.Max(gcMax,value);}
                        }
                        Array.Sort(frames,0,count);samples.Add(new Sample{scene=scene,run=run,frames=count,seconds=Time.realtimeSinceStartupAsDouble-start,frameP50=frames[(int)((count-1)*.5)],frameP95=frames[(int)((count-1)*.95)],frameP99=frames[(int)((count-1)*.99)],gcCounter=gc.Valid,gcBytesPerFrame=count==0?0:(double)gcTotal/count,gcMax=gcMax,totalUsed=Profiler.GetTotalAllocatedMemoryLong(),monoUsed=Profiler.GetMonoUsedSizeLong(),textureBytes=MemoryOf<Texture>(),meshBytes=MemoryOf<Mesh>()});
                    }
                    report.samples=samples.ToArray();Write(report);Debug.Log("YIBI_PERF_SAMPLE "+scene+" run="+run);
                }
                for(int cycle=0;cycle<=20;cycle++){
                    if(cycle>0){yield return SceneManager.LoadSceneAsync("GestureLab");yield return null;yield return SceneManager.LoadSceneAsync("Arena_Stone");yield return null;}
                    yield return SceneManager.LoadSceneAsync("Valley");yield return Resources.UnloadUnusedAssets();GC.Collect();yield return new WaitForSecondsRealtime(.5f);
                    visits.Add(new Visit{cycle=cycle,objects=Resources.FindObjectsOfTypeAll<UnityEngine.Object>().Length,sessions=FindObjectsOfType<Yibi.Networking.NetSession>().Length,used=Profiler.GetTotalAllocatedMemoryLong(),managed=Profiler.GetMonoUsedSizeLong()});
                }
                report.visits=visits.ToArray();Write(report);Debug.Log("YIBI_PERF_PASS");
            }Application.Quit(0);
        }
        static long MemoryOf<T>() where T:UnityEngine.Object {long bytes=0;foreach(var item in Resources.FindObjectsOfTypeAll<T>())bytes+=Profiler.GetRuntimeMemorySizeLong(item);return bytes;}
        static void Write(Report report){Directory.CreateDirectory(Path.GetDirectoryName(output));File.WriteAllText(output,JsonUtility.ToJson(report,true));}
    }
}
