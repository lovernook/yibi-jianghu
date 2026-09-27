using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Yibi.Rules;
using Yibi.UI;

public class GestureLabPlayTests
{
    private System.IDisposable profile;
    [SetUp] public void IsolateProfile(){profile=Yibi.Battle.ProfileStore.UseTransientProfile(new Yibi.Battle.PlayerProfile());}
    [UnityTearDown] public IEnumerator RestoreProfile(){yield return SceneManager.LoadSceneAsync("Bootstrap");profile.Dispose();}
    private GestureLabPresenter lab;
    [UnitySetUp]
    public IEnumerator Load()
    {
        yield return SceneManager.LoadSceneAsync("GestureLab");yield return null;
        lab=Object.FindObjectOfType<GestureLabPresenter>();Assert.That(lab,Is.Not.Null);Canvas.ForceUpdateCanvases();
    }
    private static Vector2 Screen(GestureBoard board,Point2 p)
    {
        var r=board.Rect.rect;
        return RectTransformUtility.WorldToScreenPoint(null,board.Rect.TransformPoint(new Vector3(r.xMin+(float)p.X*r.width,r.yMin+(float)p.Y*r.height,0)));
    }
    private void Draw()
    {
        var b=lab.board;var points=FixedGestureSamples.Dense(b.template.ToRules(),20);
        var e=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=Screen(b,points[0].ToPoint())};
        ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerDownHandler);
        for(int i=1;i<points.Length-1;i++)b.Move(b.ScreenToNormalized(Screen(b,points[i].ToPoint()),null),Time.unscaledTimeAsDouble+i*.034);
        e.position=Screen(b,points[points.Length-1].ToPoint());ExecuteEvents.Execute(b.gameObject,e,ExecuteEvents.pointerUpHandler);
    }
    [UnityTest]
    public IEnumerator AllSixRoutesScoreThroughPointerEvents()
    {
        for(int i=0;i<6;i++)
        {
            lab.SelectRoute(i);Draw();Assert.That(lab.board.LastResult.Valid,Is.True,"route="+i+" error="+lab.board.LastResult.ErrorCode+" node="+lab.board.LastResult.FirstWrongNode);Assert.That(lab.board.LastResult.Score,Is.GreaterThanOrEqualTo(99));
            Assert.That(lab.scoreText.text,Is.EqualTo(lab.board.LastResult.Score.ToString()));Assert.That(lab.metricBars[0].rectTransform.localScale.x,Is.GreaterThan(.99f));
        }
        yield return null;
    }
    [UnityTest]
    public IEnumerator ClearSwitchAndNewStrokeNeverAppend()
    {
        Draw();lab.Clear();Assert.That(lab.board.Sampler.Points.Count,Is.Zero);Assert.That(lab.board.LastResult,Is.Null);
        Draw();lab.SelectRoute(1);Assert.That(lab.board.Sampler.Points.Count,Is.Zero);Assert.That(lab.board.LastResult,Is.Null);
        Draw();lab.board.Begin(new Point2(.2,.2),0);Assert.That(lab.board.Sampler.Points.Count,Is.EqualTo(1));
        yield return null;
    }
    [UnityTest]
    public IEnumerator LockAndOutOfBoundsAreEnforced()
    {
        lab.board.inputEnabled=false;lab.board.Begin(new Point2(.2,.2),0);Assert.That(lab.board.Sampler.IsDrawing,Is.False);
        lab.board.inputEnabled=true;var n=lab.board.template.ToRules().Nodes;
        lab.board.Begin(n[0],0);lab.board.Move(new Point2(1.1,.2),1);lab.board.Finish(n[n.Length-1],2);
        Assert.That(lab.board.LastResult.ErrorCode,Is.EqualTo(GestureError.OutOfBounds));Assert.That(lab.detailsText.text,Does.Contain("越出画板"));yield return null;
    }
    [UnityTest]
    public IEnumerator CanvasScalingPreservesCoordinatesAndScore()
    {
        var scaler=lab.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
        foreach(float scale in new[]{.8f,1f,1.2f})
        {
            scaler.scaleFactor=scale;yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(lab.board.Rect.rect.width,Is.EqualTo(lab.board.Rect.rect.height).Within(.01));
            var p=lab.board.ScreenToNormalized(Screen(lab.board,new Point2(.25,.75)),null);
            Assert.That(p.X,Is.EqualTo(.25).Within(.0001));Assert.That(p.Y,Is.EqualTo(.75).Within(.0001));
            Draw();Assert.That(lab.board.LastResult.Score,Is.GreaterThanOrEqualTo(99));
        }
    }
    [UnityTest]
    public IEnumerator ProgramSampleCannotBeSavedAsHuman()
    {
        lab.NextSample();Assert.That(lab.board.IsProgramSample,Is.True);
        lab.SaveHumanSample();Assert.That(lab.statusText.text,Does.Contain("程序样本不会标成真人记录"));yield return null;
    }
    [UnityTest]
    public IEnumerator AllFixedButtonsHaveSavedListenersAndGraphicsHaveRenderers()
    {
        foreach(var button in lab.GetComponentsInChildren<Button>())Assert.That(button.onClick.GetPersistentEventCount(),Is.EqualTo(1),button.name);
        foreach(var graphic in lab.GetComponentsInChildren<Graphic>(true))Assert.That(graphic.GetComponent<CanvasRenderer>(),Is.Not.Null,graphic.name);
        Assert.That(lab.board.nodeMarkers.Length,Is.EqualTo(7));
        yield return null;
    }
}
