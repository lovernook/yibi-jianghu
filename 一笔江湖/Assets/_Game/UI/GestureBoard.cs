using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Yibi.Rules;

namespace Yibi.UI
{
    public sealed class GestureBoard : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public GestureTemplateSO template;
        public PolylineGraphic targetLine,playerLine;
        public RectTransform[] nodeMarkers;
        public Text[] nodeLabels;
        public bool inputEnabled=true;
        public event Action<ScoreResult> Scored;
        public event Action Changed;
        public readonly GestureSampler Sampler=new GestureSampler();
        public ScoreResult LastResult {get;private set;}
        public bool IsProgramSample {get;private set;}
        private QuantizedPoint[] programTrace=Array.Empty<QuantizedPoint>();
        public System.Collections.Generic.IReadOnlyList<QuantizedPoint> DisplayTrace => IsProgramSample?programTrace:Sampler.Points;
        public int StrokeVersion {get;private set;}
        private Camera eventCamera;
        private bool pointerActive;
        public RectTransform Rect => (RectTransform)transform;

        private void Start(){PreviewTemplate();}
        private void Update()
        {
            if(Input.GetKeyDown(KeyCode.Escape)){Clear();return;}
            if(!pointerActive)return;
            var p=ScreenToNormalized(Input.mousePosition,eventCamera);
            if(Input.GetMouseButtonUp(0)){Finish(p,Time.unscaledTimeAsDouble);pointerActive=false;}
            else if(Input.GetMouseButton(0))Move(p,Time.unscaledTimeAsDouble);
        }
        private void OnApplicationFocus(bool focus){if(!focus && pointerActive)Clear();}
        private void OnDisable(){Clear();}
        public Point2 ScreenToNormalized(Vector2 screen,Camera camera)
        {
            Vector2 local;
            if(!RectTransformUtility.ScreenPointToLocalPointInRectangle(Rect,screen,camera,out local))return new Point2(double.NaN,double.NaN);
            var r=Rect.rect;
            return new Point2((local.x-r.xMin)/r.width,(local.y-r.yMin)/r.height);
        }
        public void OnPointerDown(PointerEventData e)
        {
            if(!inputEnabled || e.button!=PointerEventData.InputButton.Left)return;
            eventCamera=e.pressEventCamera;
            var point=ScreenToNormalized(e.position,eventCamera);
            if(!point.IsFinite || point.X<0 || point.X>1 || point.Y<0 || point.Y>1)return;
            Begin(point,Time.unscaledTimeAsDouble);pointerActive=true;
        }
        public void OnPointerUp(PointerEventData e)
        {
            if(!pointerActive || e.button!=PointerEventData.InputButton.Left)return;
            Finish(ScreenToNormalized(e.position,eventCamera),Time.unscaledTimeAsDouble);pointerActive=false;
        }
        public void Begin(Point2 point,double time)
        {
            if(!inputEnabled)return;
            IsProgramSample=false;LastResult=null;StrokeVersion++;Sampler.Begin(point,time);Redraw();
        }
        public void Move(Point2 point,double time){if(!inputEnabled)return;int count=Sampler.Points.Count;bool bounds=Sampler.OutOfBounds,overflow=Sampler.Overflow;Sampler.Move(point,time);if(count!=Sampler.Points.Count||bounds!=Sampler.OutOfBounds||overflow!=Sampler.Overflow)Redraw();}
        public void Finish(Point2 point,double time)
        {
            if(!Sampler.IsDrawing)return;
            Sampler.End(point,time);Redraw();
            LastResult=GestureScorer.Score(template.ToRules(),Sampler.Snapshot(),Sampler.OutOfBounds,Sampler.Overflow);
            Scored?.Invoke(LastResult);
        }
        public void Clear(){pointerActive=false;Sampler.Clear();LastResult=null;IsProgramSample=false;programTrace=Array.Empty<QuantizedPoint>();StrokeVersion++;if(playerLine!=null)playerLine.Clear();Changed?.Invoke();}
        private void Redraw(){playerLine.SetPoints(Sampler.Points);Changed?.Invoke();}
        public void SetTemplate(GestureTemplateSO value){Clear();template=value;PreviewTemplate();Changed?.Invoke();}
        public void PreviewTemplate()
        {
            if(template==null || targetLine==null)return;
            targetLine.SetTemplate(template.nodes);
            for(int i=0;i<nodeMarkers.Length;i++)
            {
                bool active=template.nodes!=null && i<template.nodes.Length;nodeMarkers[i].gameObject.SetActive(active);if(!active)continue;
                var marker=nodeMarkers[i];marker.anchorMin=marker.anchorMax=template.nodes[i];marker.anchoredPosition=Vector2.zero;
                marker.sizeDelta=Vector2.one*(template.radius*2*Rect.rect.width);
                nodeLabels[i].text=(i+1).ToString();
            }
        }
        private void OnRectTransformDimensionsChange(){if(nodeMarkers!=null)PreviewTemplate();}
        public void ShowSample(QuantizedPoint[] points)
        {
            Clear();IsProgramSample=true;programTrace=(QuantizedPoint[])points.Clone();playerLine.SetPoints(points);
            LastResult=GestureScorer.Score(template.ToRules(),points);Scored?.Invoke(LastResult);
        }
    }
}
