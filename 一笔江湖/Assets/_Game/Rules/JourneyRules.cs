using System.Collections.Generic;
namespace Yibi.Rules
{
    public static class JourneyRules
    {
        public static readonly string[] Milestones={"practice-pass","first-win-0","valley-trial","first-win-1"};
        public static readonly string[] Titles={"初识经脉","竹林问剑","遗迹疾行","问心决战","归云篇 · 完成"};
        public static readonly string[] Objectives={"在练功台手绘一条路线，达到 70 分","击败竹林守卫，领会破绽联动","在 45 秒内依次通过五个疾行标记","击败遗迹守卫，完成问心试炼","归云谷已通关 · 收集秘籍或前往双人论武"};
        public static int Stage(ICollection<string> completed){for(int i=0;i<Milestones.Length;i++)if(!completed.Contains(Milestones[i]))return i;return 4;}
        public static bool CanChallengeFinal(ICollection<string> completed)=>completed.Contains("first-win-0")&&completed.Contains("valley-trial");
    }
}
