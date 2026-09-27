using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yibi.Rules;

namespace Yibi.UI
{
    // Opt-in built-player check. No runtime HUD creation; uses the saved scene and listeners.
    public sealed class GestureLabSmoke : MonoBehaviour
    {
        private IEnumerator Start()
        {
            string[] args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"--m1-smoke")<0)yield break;
            Application.runInBackground=true;
            float deadline=Time.realtimeSinceStartup+20;
            while(!UnityEngine.Rendering.SplashScreen.isFinished && Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            for(int frame=0;frame<10;frame++)yield return null;
            var lab=GetComponent<GestureLabPresenter>();
            int rootIndex=Array.IndexOf(args,"--evidence-dir");
            string root=rootIndex>=0 && rootIndex+1<args.Length?args[rootIndex+1]:Application.persistentDataPath;
            Directory.CreateDirectory(root);
            bool passed=true;
            for(int route=0;route<3;route++)
            {
                lab.SelectRoute(route);Canvas.ForceUpdateCanvases();
                var board=lab.board;var points=FixedGestureSamples.Dense(board.template.ToRules(),20);
                var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=ScreenPoint(board,points[0].ToPoint())};
                ExecuteEvents.Execute(board.gameObject,e,ExecuteEvents.pointerDownHandler);
                for(int i=1;i<points.Length-1;i++)board.Move(board.ScreenToNormalized(ScreenPoint(board,points[i].ToPoint()),null),Time.unscaledTimeAsDouble+i*.034);
                e.position=ScreenPoint(board,points[points.Length-1].ToPoint());ExecuteEvents.Execute(board.gameObject,e,ExecuteEvents.pointerUpHandler);
                bool valid=board.LastResult!=null && board.LastResult.Valid && board.LastResult.Score>=99;
                passed &= valid;
                Debug.Log("YIBI_M1_ROUTE "+board.template.stableId+" valid="+valid+" score="+(board.LastResult==null?-1:board.LastResult.Score)+" resolution="+Screen.width+"x"+Screen.height);
                lab.sampleText.text="自动化指针测试  ·  非真人样本";
                yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(root,"M1-"+Screen.width+"x"+Screen.height+"-"+board.template.stableId+".png"));
                yield return null;yield return null;
            }
            lab.SelectRoute(0);var n=lab.board.template.ToRules().Nodes;
            lab.board.Begin(n[0],0);lab.board.Move(new Point2(1.2,.2),1);lab.board.Finish(n[n.Length-1],2);
            passed &= lab.board.LastResult.ErrorCode==GestureError.OutOfBounds;
            Debug.Log("YIBI_M1_SMOKE "+(passed?"PASS":"FAIL")+" synthetic_pointer=true");
            for(int i=0;i<4;i++)yield return null;
            Application.Quit(passed?0:1);
        }
        private static Vector2 ScreenPoint(GestureBoard b,Point2 p)
        {
            var r=b.Rect.rect;
            return RectTransformUtility.WorldToScreenPoint(null,b.Rect.TransformPoint(new Vector3(r.xMin+(float)p.X*r.width,r.yMin+(float)p.Y*r.height,0)));
        }
    }
}
