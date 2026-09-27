using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yibi.Rules;

namespace Yibi.UI
{
    [RequireComponent(typeof(GestureBoard))]
    [DisallowMultipleComponent]
    public sealed class GestureBoardFeedback : MonoBehaviour
    {
        public GestureBoard board;
        public Text hint;
        public RectTransform brush;
        public PolylineGraphic guideLine;
        public Graphic[] rings;
        public Color pending=new Color(.7f,.77f,.73f),completed=new Color(.95f,.78f,.36f),next=new Color(.36f,1,.81f),failure=new Color(1,.4f,.32f);
        private readonly GestureGuide guide=new GestureGuide();
        private readonly List<QuantizedPoint> playback=new List<QuantizedPoint>(512);
        private Coroutine routine;
        private int processed,version=-1;
        private GestureTemplateSO route;
        private bool previousInput;
        private int hintKey=int.MinValue;
        public bool IsPlayingBack {get;private set;}
        public int Reached=>guide.Reached;
        void OnEnable(){if(board==null)board=GetComponent<GestureBoard>();board.Changed+=Changed;board.Scored+=Scored;}
        void OnDisable(){StopPlayback();board.Changed-=Changed;board.Scored-=Scored;}
        void Start(){Changed();}
        void Update()
        {
            if(!IsPlayingBack&&board.LastResult==null&&guide.Error==GestureError.None){int current=Mathf.Min(guide.Reached,board.nodeMarkers.Length-1);if(current>=0&&current<rings.Length&&board.nodeMarkers[current].gameObject.activeSelf)rings[current].color=Color.Lerp(pending,next,.65f+.3f*Mathf.Sin(Time.unscaledTime*4));}
        }
        void Changed()
        {
            if(board.template==null)return;
            if(IsPlayingBack){if(board.template!=route||version!=board.StrokeVersion)StopPlayback();else return;}
            if(route!=board.template||version!=board.StrokeVersion){hintKey=int.MinValue;route=board.template;version=board.StrokeVersion;processed=0;guide.Reset(route.ToRules());foreach(var r in rings)r.color=pending;}
            var points=board.Sampler.Points;while(processed<points.Count)guide.Push(points[processed++]);
            for(int i=0;i<rings.Length;i++)rings[i].color=i<guide.Reached?completed:pending;
            bool bad=board.Sampler.OutOfBounds||board.Sampler.Overflow||guide.Error!=GestureError.None;
            if(guide.WrongNode>=0&&guide.WrongNode<rings.Length)rings[guide.WrongNode].color=failure;
            board.playerLine.color=bad?failure:guide.Distance>.055?new Color(1,.72f,.25f):next;
            if(brush!=null){brush.gameObject.SetActive(points.Count>0);if(points.Count>0)Place(points[points.Count-1]);}
            int key=points.Count==0?0:board.Sampler.OutOfBounds?-1:board.Sampler.Overflow?-2:guide.Error!=GestureError.None?-3:1+guide.Reached*2+(guide.Distance>.055?1:0);
            if(hint!=null&&key!=hintKey){hintKey=key;hint.text=board.Sampler.OutOfBounds?"已越出画板 · 松开后重画":board.Sampler.Overflow?"笔迹过长 · 请重新起笔":guide.Error!=GestureError.None?"顺序有误 · 检查红色穴位后重新起笔":guide.Reached>=route.nodes.Length?"经脉已贯通 · 在末穴松开鼠标":points.Count==0?"从闪动的 1 号穴位起笔，沿金线依次连接":"已连通 "+guide.Reached+" / "+route.nodes.Length+" · 下一穴 "+(guide.Reached+1)+(guide.Distance>.055?" · 笔迹偏离金线":"");}
        }
        void Scored(ScoreResult result)
        {
            if(IsPlayingBack)return;
            if(result.Valid){for(int i=0;i<rings.Length;i++)rings[i].color=completed;if(hint!=null)hint.text=(board.IsProgramSample?"程序样本":"本笔完成")+" · "+result.Score+" 分 · "+(result.Score>=85?"经脉通畅": "可回看笔迹，留意转折处");}
            else {if(result.FirstWrongNode>=0&&result.FirstWrongNode<rings.Length)rings[result.FirstWrongNode].color=failure;if(hint!=null)hint.text=GestureLabPresenter.Explain(result);}
        }
        void Place(QuantizedPoint p){brush.anchorMin=brush.anchorMax=new Vector2(p.x/10000f,p.y/10000f);brush.anchoredPosition=Vector2.zero;}
        public void Demonstrate()
        {
            var nodes=board.template.nodes;var trace=new QuantizedPoint[nodes.Length];for(int i=0;i<nodes.Length;i++)trace[i]=QuantizedPoint.From(new Point2(nodes[i].x,nodes[i].y));StartPlayback(trace,true);
        }
        public void Replay()
        {
            if(IsPlayingBack){StopPlayback();Changed();return;}
            if(board.DisplayTrace.Count<2){if(hint!=null)hint.text="先完成一笔，再回看自己的轨迹。";return;}
            var trace=new QuantizedPoint[board.DisplayTrace.Count];for(int i=0;i<trace.Length;i++)trace[i]=board.DisplayTrace[i];StartPlayback(trace,false);
        }
        void StartPlayback(QuantizedPoint[] trace,bool demonstration)
        {
            StopPlayback();previousInput=board.inputEnabled;if(demonstration)board.Clear();route=board.template;board.inputEnabled=false;board.playerLine.enabled=false;IsPlayingBack=true;routine=StartCoroutine(Animate(trace,demonstration));
        }
        IEnumerator Animate(QuantizedPoint[] trace,bool demonstration)
        {
            // Arc-length playback has constant brush speed; it never calls Finish or Scored.
            var lengths=new float[trace.Length];float total=0;for(int i=1;i<trace.Length;i++){total+=(float)Point2.Distance(trace[i-1].ToPoint(),trace[i].ToPoint());lengths[i]=total;}
            float elapsed=0,duration=demonstration?4.5f:3.2f;
            while(elapsed<duration&&board.template==route){elapsed+=Time.unscaledDeltaTime;float distance=Mathf.Clamp01(elapsed/duration)*total;int end=1;while(end<trace.Length-1&&lengths[end]<distance)end++;
                float t=Mathf.InverseLerp(lengths[end-1],lengths[end],distance);var a=trace[end-1];var b=trace[end];var p=new QuantizedPoint((int)Mathf.Lerp(a.x,b.x,t),(int)Mathf.Lerp(a.y,b.y,t));
                playback.Clear();for(int i=0;i<end;i++)playback.Add(trace[i]);playback.Add(p);guideLine.SetPoints(playback);brush.gameObject.SetActive(true);Place(p);
                if(hint!=null)hint.text=(demonstration?"示范运笔":"笔迹回放")+" · 观察行进方向 · 不计成绩与奖励";yield return null;
            }
            routine=null;StopPlayback();hintKey=int.MinValue;Changed();if(!demonstration&&board.LastResult!=null)Scored(board.LastResult);else if(hint!=null)hint.text="轮到你了 · 从 1 号穴位起笔";
        }
        public void StopPlayback()
        {
            if(routine!=null){StopCoroutine(routine);routine=null;}if(!IsPlayingBack)return;
            IsPlayingBack=false;board.inputEnabled=previousInput;board.playerLine.enabled=true;guideLine.Clear();brush.gameObject.SetActive(false);
        }
    }
}
