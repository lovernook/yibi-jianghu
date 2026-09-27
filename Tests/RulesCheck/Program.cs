using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Yibi.Rules;

internal static class Program
{
    private static int Main(string[] args)
    {
        int passed=0;var rows=new List<object>();
        foreach(var template in StarterRoutes.Create())
            foreach(var sample in FixedGestureSamples.Create(template))
            {
                var result=GestureScorer.Score(template,sample.Points);
                bool ok=result.Valid==sample.ExpectedValid && result.ErrorCode==sample.ExpectedError &&
                    (!result.Valid || (result.Score>=sample.MinimumScore && result.Score<=sample.MaximumScore));
                rows.Add(new {route=template.Id,sample=sample.Name,passed=ok,points=sample.Points,result=result});
                if(!ok)throw new Exception(template.Id+"/"+sample.Name+": "+result.ErrorCode+" "+result.Score);
                passed++;
            }
        var options=new JsonSerializerOptions{IncludeFields=true,WriteIndented=true};
        if(args.Length==1)File.WriteAllText(args[0],JsonSerializer.Serialize(rows,options));
        Console.WriteLine("YIBI_SHARED_RULES_PASS "+passed+"/"+rows.Count+" (linked Unity source; C# 9)");
        return 0;
    }
}
