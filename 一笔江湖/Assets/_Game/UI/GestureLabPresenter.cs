using System;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Yibi.Rules;

namespace Yibi.UI
{
    public sealed class GestureLabPresenter : MonoBehaviour
    {
        public GestureBoard board;
        public GestureTemplateSO[] routes;
        public Text routeTitle,routeDescription,scoreText,gradeText,detailsText,statusText,sampleText;
        public Image[] metricBars;
        public Image[] routeHighlights;
        [Tooltip("路线选中态的视觉颜色；制作工具可从界面皮肤配置同步。")]
        public Color selectedRouteColor = new Color(1, .84f, .5f);
        public Color normalRouteColor = new Color(.68f, .77f, .84f);
        public Text[] metricValues;
        private int sampleIndex;
        private GestureBoardFeedback feedback;
        private bool showingDrawing;
        public bool showDiagnostics;
        [Serializable] private sealed class SavedSample
        {
            public string templateId,source,utc;
            public int gestureVersion;
            public Vector2[] templateNodes;
            public float templateRadius;
            public QuantizedPoint[] points;
            public bool outOfBounds,overflow;
            public string error;
            public int score;
        }
        private void OnEnable(){if(board==null)return;board.Scored+=ShowScore;board.Changed+=ShowDrawingState;}
        private void OnDisable(){if(board==null)return;board.Scored-=ShowScore;board.Changed-=ShowDrawingState;}
        private void Start(){feedback=board.GetComponent<GestureBoardFeedback>();SelectRoute(0);}
        public void SelectRoute(int index)
        {
            board.SetTemplate(routes[index]);sampleIndex=0;
            routeTitle.text=routes[index].displayName;routeDescription.text=routes[index].description;
            for(int i=0;i<routeHighlights.Length;i++)routeHighlights[i].color=i==index?selectedRouteColor:normalRouteColor;
            ResetScore();sampleText.text="固定样本  ·  未载入";
        }
        public void ToggleDiagnostics(){showDiagnostics=!showDiagnostics;if(board.LastResult!=null)ShowScore(board.LastResult);}
        public void Clear(){board.Clear();ResetScore();sampleText.text="真人手绘  ·  等待输入";}
        private void ResetScore()
        {
            scoreText.text="—";gradeText.text="静心 · 起笔";
            detailsText.text="从 1 号穴位起笔，依次经过全部穴位。\n松开左键后自动评分；再次起笔会清空上一笔。";
            for(int i=0;i<3;i++){metricValues[i].text="—";SetMetric(i,0);}
        }
        private void ShowDrawingState()
        {
            if(feedback==null)feedback=board.GetComponent<GestureBoardFeedback>();
            if(feedback!=null){if(board.Sampler.IsDrawing&&!showingDrawing){scoreText.text="…";gradeText.text="凝神 · 运笔";sampleText.text="真人手绘 · 当前输入";}showingDrawing=board.Sampler.IsDrawing;return;}
            string hint=board.Sampler.Overflow?"采样超过 512 点，请清空重画":board.Sampler.OutOfBounds?"已离开画板，请清空重画":board.Sampler.IsDrawing?"正在运笔 · 松开评分":"左键按住绘制 · Esc 取消";
            statusText.text=hint+"    |    采样 "+board.Sampler.Points.Count+" / 512";
            if(board.Sampler.IsDrawing){scoreText.text="…";gradeText.text="凝神 · 运笔";sampleText.text="真人手绘  ·  当前输入";}
        }
        public void ShowScore(ScoreResult result)
        {
            detailsText.fontSize=showDiagnostics?15:17;
            var values=new[]{result.ShapeScore,result.OrderScore,result.LengthScore};
            for(int i=0;i<3;i++){SetMetric(i,(float)values[i]/100);metricValues[i].text=result.Valid?values[i].ToString("F1"):"—";}
            if(!result.Valid)
            {
                scoreText.text="未成形";gradeText.text="运功失误";
                detailsText.text=Explain(result)+"\n\n下一步：先看闪动穴位，按编号缓慢连接。\n可点“运笔示范”观察正确行进方向。"+(showDiagnostics?"\n错误码："+result.ErrorCode:"");
            }
            else
            {
                scoreText.text=result.Score.ToString();gradeText.text=result.Score>=90?"圆融":result.Score>=70?"熟练":"初成";
                detailsText.text=showDiagnostics?"质量倍率  "+result.Multiplier.ToString("F2")+"×\n\n形状占 60%  ·  顺序占 25%  ·  长度占 15%\n玩家 → 目标距离  "+result.PlayerToTemplate.ToString("F4")+
                    "\n目标 → 玩家距离  "+result.TemplateToPlayer.ToString("F4")+"\n弧长配对距离        "+result.PairedDistance.ToString("F4")+
                    "\n实际 / 目标线长    "+result.PlayerLength.ToString("F3")+" / "+result.TemplateLength.ToString("F3")+"\n\n速度不参与评分。":"本次效果  "+result.Multiplier.ToString("F2")+"×\n\n"+(result.ShapeScore<80?"贴近金线，尤其留意两穴之间的转折。":result.LengthScore<90?"减少绕行和重复描线，让这一笔更干净。":result.OrderScore<85?"按金线逐段前进，不要在转折处抄近路。":"这一笔很稳，可以尝试其他秘籍或六脉考核。")+"\n\n金色：已连通  ·  青色：下一穴\n红色：顺序错误  ·  橙色：偏离\n\n不比手速，准确比快更重要。";
            }
        }
        public static string Explain(ScoreResult r)
        {
            string node=(r.FirstWrongNode+1).ToString();
            switch(r.ErrorCode)
            {
                case GestureError.TooShort:return "轨迹太短，至少需要两个不同的点。";
                case GestureError.OutOfBounds:return "轨迹越出画板，本笔无效。";
                case GestureError.WrongStart:return "请从 1 号穴位圆内开始。";
                case GestureError.WrongEnd:return "请在最后一个穴位圆内收笔。";
                case GestureError.MissingNode:return "漏过了第 "+node+" 号穴位，按编号依次连接。";
                case GestureError.WrongOrder:return "返回了已访问的第 "+node+" 号穴位，避免逆序运笔。";
                case GestureError.TooManyPoints:return "超过 512 点上限，请重新绘制。";
                default:return "路线配置无效，请检查 Inspector 中的节点。";
            }
        }
        private void SetMetric(int index,float value)
        {
            var bar=metricBars[index];bar.fillAmount=value;
            // A sprite-less UGUI Image ignores Filled; scale a left-anchored rectangle instead.
            var rect=bar.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(0,.5f);rect.pivot=new Vector2(0,.5f);rect.anchoredPosition=Vector2.zero;
            rect.localScale=new Vector3(value,1,1);
        }
        public void NextSample()
        {
            var samples=FixedGestureSamples.Create(board.template.ToRules());var sample=samples[sampleIndex%samples.Length];
            board.ShowSample(sample.Points);
            sampleText.text="程序固定样本 "+(sampleIndex%samples.Length+1)+" / "+samples.Length+"  ·  "+sample.Name;
            statusText.text="程序样本用于回归测试，不代表真人练习。";sampleIndex++;
        }
        public void SaveHumanSample()
        {
            if(board.IsProgramSample || board.LastResult==null || board.Sampler.Points.Count==0)
            {statusText.text="请先完成一笔真人手绘；程序样本不会标成真人记录。";return;}
            var saved=new SavedSample {templateId=board.template.stableId,source="mouse",utc=DateTime.UtcNow.ToString("O"),gestureVersion=1,
                templateNodes=board.template.nodes,templateRadius=board.template.radius,points=board.Sampler.Snapshot(),outOfBounds=board.Sampler.OutOfBounds,
                overflow=board.Sampler.Overflow,error=board.LastResult.ErrorCode.ToString(),score=board.LastResult.Score};
            try
            {
                string dir=Path.Combine(Application.persistentDataPath,"GestureSamples");Directory.CreateDirectory(dir);
                string path=Path.Combine(dir,DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")+".json");
                File.WriteAllText(path,JsonUtility.ToJson(saved,true));statusText.text="手绘样本已保存至本机 GestureSamples 文件夹。";Debug.Log("YIBI_SAMPLE_SAVED "+path);
            }
            catch(Exception ex){statusText.text="保存失败："+ex.Message;}
        }
    }
}
